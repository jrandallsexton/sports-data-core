using AutoFixture;

using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.FranchiseContestFacts.Commands.ProjectContestFacts;
using SportsData.Producer.Application.FranchiseContestFacts.Commands.RebuildSeasonFacts;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.FranchiseContestFacts;

public class RebuildSeasonFactsCommandHandlerTests : ProducerTestBase<RebuildSeasonFactsCommandHandler>
{
    private const int SeasonYear = 2025;

    public RebuildSeasonFactsCommandHandlerTests()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow())
            .Returns(new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Execute_EnqueuesFinalizedContests_AndContestsWithExistingRows()
    {
        var finalized = SeedContest(SeasonYear, finalized: true);
        var unfinalizedWithRows = SeedContest(SeasonYear, finalized: false);
        SeedContest(SeasonYear, finalized: false);          // no rows, not finalized: skipped
        SeedContest(SeasonYear - 1, finalized: true);       // other season: skipped
        FootballDataContext.FranchiseContestFacts.Add(new FranchiseContestFact
        {
            ContestId = unfinalizedWithRows.Id,
            FranchiseSeasonId = Guid.NewGuid(),
            SeasonYear = SeasonYear,
        });
        // The finalized contest also has a row: enqueued once, not twice.
        FootballDataContext.FranchiseContestFacts.Add(new FranchiseContestFact
        {
            ContestId = finalized.Id,
            FranchiseSeasonId = Guid.NewGuid(),
            SeasonYear = SeasonYear,
        });
        await FootballDataContext.SaveChangesAsync();

        var enqueued = new List<Guid>();
        Mocker.GetMock<IProvideBackgroundJobs>()
            .Setup(x => x.Enqueue(It.IsAny<Expression<Func<IProjectContestFactsCommandHandler, Task>>>()))
            .Callback<Expression<Func<IProjectContestFactsCommandHandler, Task>>>(expr =>
                enqueued.Add(ContestIdOf(expr)))
            .Returns("job");

        var result = await Mocker.CreateInstance<RebuildSeasonFactsCommandHandler>()
            .ExecuteAsync(new RebuildSeasonFactsCommand(SeasonYear));

        result.Value.Should().Be(2);
        enqueued.Should().BeEquivalentTo([finalized.Id, unfinalizedWithRows.Id]);
    }

    [Theory]
    [InlineData(1999)]
    [InlineData(2028)]
    public async Task Execute_WhenSeasonOutOfRange_ReturnsValidationFailure(int seasonYear)
    {
        var result = await Mocker.CreateInstance<RebuildSeasonFactsCommandHandler>()
            .ExecuteAsync(new RebuildSeasonFactsCommand(seasonYear));

        result.Status.Should().Be(ResultStatus.Validation);
    }

    /// <summary>Reads the command's ContestId out of the enqueued call.</summary>
    private static Guid ContestIdOf(Expression<Func<IProjectContestFactsCommandHandler, Task>> expr)
    {
        var call = (MethodCallExpression)expr.Body;
        var command = (ProjectContestFactsCommand)Expression.Lambda(call.Arguments[0]).Compile().DynamicInvoke()!;
        return command.ContestId;
    }

    private FootballContest SeedContest(int seasonYear, bool finalized)
    {
        var contest = Fixture.Build<FootballContest>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.HomeTeamFranchiseSeasonId, Guid.NewGuid())
            .With(x => x.AwayTeamFranchiseSeasonId, Guid.NewGuid())
            .With(x => x.SeasonYear, seasonYear)
            .With(x => x.StartDateUtc, new DateTime(seasonYear, 10, 1, 0, 0, 0, DateTimeKind.Utc))
            .With(x => x.FinalizedUtc, finalized ? new DateTime(seasonYear, 10, 1, 4, 0, 0, DateTimeKind.Utc) : null)
            .With(x => x.Name, "Game")
            .With(x => x.ShortName, "G")
            .With(x => x.Sport, Sport.FootballNcaa)
            .Create();
        FootballDataContext.Contests.Add(contest);
        return contest;
    }
}
