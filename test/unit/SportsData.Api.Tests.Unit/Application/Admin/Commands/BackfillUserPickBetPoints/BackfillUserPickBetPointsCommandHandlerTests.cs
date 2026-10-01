using System.Linq.Expressions;

using Moq;

using SportsData.Api.Application.Admin.Commands.BackfillUserPickBetPoints;
using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Processing;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Admin.Commands.BackfillUserPickBetPoints;

public class BackfillUserPickBetPointsCommandHandlerTests : ApiTestBase<BackfillUserPickBetPointsCommandHandler>
{
    private static readonly DateTime ScoredAt = new(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc);

    private readonly List<ApplyUserPickBetPointsCommand> _enqueued = [];

    public BackfillUserPickBetPointsCommandHandlerTests()
    {
        Mocker.GetMock<IProvideBackgroundJobs>()
            .Setup(x => x.Enqueue(It.IsAny<Expression<Func<IApplyUserPickBetPoints, Task>>>()))
            .Callback<Expression<Func<IApplyUserPickBetPoints, Task>>>(e => _enqueued.Add(FirstArgument(e)));
    }

    /// <summary>The command inside the enqueued p => p.Process(cmd) lambda, evaluated.</summary>
    private static ApplyUserPickBetPointsCommand FirstArgument(LambdaExpression captured)
    {
        var call = (MethodCallExpression)captured.Body;
        return (ApplyUserPickBetPointsCommand)Expression.Lambda(call.Arguments[0]).Compile().DynamicInvoke()!;
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

    private async Task SeedPickAsync(Guid groupId, Guid contestId, DateTime? scoredAt)
    {
        await DataContext.UserPicks.AddAsync(new PickemGroupUserPick
        {
            Id = Guid.NewGuid(), PickemGroupId = groupId, UserId = Guid.NewGuid(), ContestId = contestId,
            FranchiseSeasonId = Guid.NewGuid(), ScoredAt = scoredAt
        });
        await DataContext.SaveChangesAsync();
    }

    [Fact]
    public async Task EnqueuesOneJobPerDistinctScoredContestAndSport_UnderOneCorrelationId()
    {
        var sharedNcaaContest = Guid.NewGuid();
        var unscoredContest = Guid.NewGuid();
        var nflContest = Guid.NewGuid();
        var ncaaA = await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF);
        var ncaaB = await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF);
        var nfl = await SeedGroupAsync(Sport.FootballNfl, League.NFL);
        // Several picks on one contest across two leagues: one job.
        await SeedPickAsync(ncaaA, sharedNcaaContest, ScoredAt);
        await SeedPickAsync(ncaaA, sharedNcaaContest, ScoredAt);
        await SeedPickAsync(ncaaB, sharedNcaaContest, ScoredAt);
        // Nothing scored yet: nothing to price.
        await SeedPickAsync(ncaaA, unscoredContest, null);
        await SeedPickAsync(nfl, nflContest, ScoredAt);

        var result = await Mocker.CreateInstance<BackfillUserPickBetPointsCommandHandler>()
            .ExecuteAsync(new BackfillUserPickBetPointsCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ResultStatus.Accepted, result.Status);
        Assert.Equal(2, result.Value.ContestsEnqueued);
        Assert.Equal(2, _enqueued.Count);
        Assert.Contains(_enqueued, c => c.ContestId == sharedNcaaContest && c.Sport == Sport.FootballNcaa);
        Assert.Contains(_enqueued, c => c.ContestId == nflContest && c.Sport == Sport.FootballNfl);
        Assert.Single(_enqueued.Select(c => c.CorrelationId).Distinct());
        Assert.Equal(result.Value.CorrelationId, _enqueued[0].CorrelationId);
    }
}
