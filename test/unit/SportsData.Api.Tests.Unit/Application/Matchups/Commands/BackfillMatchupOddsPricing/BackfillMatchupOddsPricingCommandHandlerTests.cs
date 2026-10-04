using System.Linq.Expressions;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.Matchups.Commands.BackfillMatchupOddsPricing;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Processing;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Matchups.Commands.BackfillMatchupOddsPricing;

public class BackfillMatchupOddsPricingCommandHandlerTests : ApiTestBase<BackfillMatchupOddsPricingCommandHandler>
{
    private static readonly DateTime Kickoff = new(2026, 9, 26, 19, 30, 0, DateTimeKind.Utc);

    private readonly List<ApplyMatchupOddsPricingCommand> _enqueued = [];

    public BackfillMatchupOddsPricingCommandHandlerTests()
    {
        Mocker.GetMock<IProvideBackgroundJobs>()
            .Setup(x => x.Enqueue(It.IsAny<Expression<Func<IApplyMatchupOddsPricing, Task>>>()))
            .Callback<Expression<Func<IApplyMatchupOddsPricing, Task>>>(e => _enqueued.Add(FirstArgument(e)));
    }

    /// <summary>The command inside the enqueued p => p.Process(cmd) lambda, evaluated.</summary>
    private static ApplyMatchupOddsPricingCommand FirstArgument(LambdaExpression captured)
    {
        var call = (MethodCallExpression)captured.Body;
        return (ApplyMatchupOddsPricingCommand)Expression.Lambda(call.Arguments[0]).Compile().DynamicInvoke()!;
    }

    private async Task<Guid> SeedGroupAsync(Sport sport, League league)
    {
        var group = new PickemGroup
        {
            Id = Guid.NewGuid(), Name = $"{sport} league", Sport = sport, League = league, CommissionerUserId = Guid.NewGuid()
        };
        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.SaveChangesAsync();
        return group.Id;
    }

    private async Task SeedMatchupAsync(Guid groupId, Guid contestId)
    {
        await DataContext.PickemGroupMatchups.AddAsync(new PickemGroupMatchup
        {
            Id = Guid.NewGuid(), GroupId = groupId, ContestId = contestId, SeasonWeekId = Guid.NewGuid(),
            SeasonYear = 2026, SeasonWeek = 5, StartDateUtc = Kickoff
        });
        await DataContext.SaveChangesAsync();
    }

    private Task<Result<BackfillMatchupOddsPricingResult>> RunAsync() =>
        Mocker.CreateInstance<BackfillMatchupOddsPricingCommandHandler>()
            .ExecuteAsync(new BackfillMatchupOddsPricingCommand(), CancellationToken.None);

    [Fact]
    public async Task EnqueuesOneJobPerDistinctContestAndSport_UnderOneCorrelationId()
    {
        var sharedNcaaContest = Guid.NewGuid();
        var otherNcaaContest = Guid.NewGuid();
        var nflContest = Guid.NewGuid();
        var ncaaA = await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF);
        var ncaaB = await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF);
        var nfl = await SeedGroupAsync(Sport.FootballNfl, League.NFL);
        // Two leagues carry the same NCAA contest: priced ONCE; the job updates both rows.
        await SeedMatchupAsync(ncaaA, sharedNcaaContest);
        await SeedMatchupAsync(ncaaB, sharedNcaaContest);
        await SeedMatchupAsync(ncaaA, otherNcaaContest);
        await SeedMatchupAsync(nfl, nflContest);

        var result = await RunAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(ResultStatus.Accepted, result.Status);
        Assert.Equal(3, result.Value.ContestsEnqueued);
        Assert.Equal(2, Assert.Single(result.Value.Sports, s => s.Sport == Sport.FootballNcaa).ContestsEnqueued);
        Assert.Equal(1, Assert.Single(result.Value.Sports, s => s.Sport == Sport.FootballNfl).ContestsEnqueued);

        Assert.Equal(3, _enqueued.Count);
        Assert.Contains(_enqueued, c => c.ContestId == sharedNcaaContest && c.Sport == Sport.FootballNcaa);
        Assert.Contains(_enqueued, c => c.ContestId == otherNcaaContest && c.Sport == Sport.FootballNcaa);
        Assert.Contains(_enqueued, c => c.ContestId == nflContest && c.Sport == Sport.FootballNfl);
        Assert.All(_enqueued, c => Assert.Equal(result.Value.CorrelationId, c.CorrelationId));
    }

    [Fact]
    public async Task NoMatchups_EnqueuesNothing()
    {
        var result = await RunAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.ContestsEnqueued);
        Assert.Empty(_enqueued);
    }
}
