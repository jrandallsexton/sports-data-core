using Moq;

using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Scoring;

public class LeagueWeekScoringProcessorTests : ApiTestBase<LeagueWeekScoringProcessor>
{
    private static readonly DateTime FixedUtcNow = new(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc);

    private const int SeasonYear = 2026;
    private const int SeasonWeek = 10;

    [Fact]
    public async Task Process_WhenAllPicksScoredBeforeOldestCalc_SkipsScoring()
    {
        // Arrange — every UserPick.ScoredAt is older than the OLDEST
        // PickemGroupWeekResult.CalculatedUtc for this (league, year, week).
        // This is the coalesce path: a later-enqueued duplicate job hits the
        // short-circuit because the first run already brought the leaderboard
        // current.
        var leagueId = Guid.NewGuid();
        var contestId = Guid.NewGuid();

        await SeedMatchupAndPick(leagueId, contestId, pickScoredAt: FixedUtcNow.AddMinutes(-30));
        await SeedWeekResult(leagueId, calculatedUtc: FixedUtcNow.AddMinutes(-5));

        var sut = Mocker.CreateInstance<LeagueWeekScoringProcessor>();

        // Act
        await sut.Process(leagueId, SeasonYear, SeasonWeek, Guid.NewGuid());

        // Assert — staleness short-circuit fired; no rescore.
        Mocker.GetMock<ILeagueWeekScoringService>()
            .Verify(s => s.ScoreLeagueWeekAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
    }

    [Fact]
    public async Task Process_WhenPickScoredAfterOldestCalc_InvokesScoring()
    {
        // Arrange — UserPick.ScoredAt is newer than the OLDEST
        // PickemGroupWeekResult.CalculatedUtc. Real work to do.
        var leagueId = Guid.NewGuid();
        var contestId = Guid.NewGuid();

        await SeedMatchupAndPick(leagueId, contestId, pickScoredAt: FixedUtcNow);
        await SeedWeekResult(leagueId, calculatedUtc: FixedUtcNow.AddMinutes(-10));

        var sut = Mocker.CreateInstance<LeagueWeekScoringProcessor>();

        // Act
        await sut.Process(leagueId, SeasonYear, SeasonWeek, Guid.NewGuid());

        // Assert
        Mocker.GetMock<ILeagueWeekScoringService>()
            .Verify(s => s.ScoreLeagueWeekAsync(
                leagueId,
                SeasonYear,
                SeasonWeek,
                It.IsAny<CancellationToken>()),
                Times.Once);
    }

    [Fact]
    public async Task Process_WhenNoResultRowsYet_InvokesScoring()
    {
        // Arrange — no PickemGroupWeekResult rows for this tuple. First-ever
        // scoring of a (league, year, week) must always run; the staleness
        // predicate has nothing to compare against.
        var leagueId = Guid.NewGuid();
        var contestId = Guid.NewGuid();

        await SeedMatchupAndPick(leagueId, contestId, pickScoredAt: FixedUtcNow);

        var sut = Mocker.CreateInstance<LeagueWeekScoringProcessor>();

        // Act
        await sut.Process(leagueId, SeasonYear, SeasonWeek, Guid.NewGuid());

        // Assert
        Mocker.GetMock<ILeagueWeekScoringService>()
            .Verify(s => s.ScoreLeagueWeekAsync(
                leagueId,
                SeasonYear,
                SeasonWeek,
                It.IsAny<CancellationToken>()),
                Times.Once);
    }

    private async Task SeedMatchupAndPick(Guid leagueId, Guid contestId, DateTime? pickScoredAt)
    {
        var matchup = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = leagueId,
            ContestId = contestId,
            SeasonYear = SeasonYear,
            SeasonWeek = SeasonWeek,
            StartDateUtc = FixedUtcNow.AddDays(-1),
            CreatedUtc = FixedUtcNow,
            CreatedBy = Guid.Empty
        };

        var pick = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = Guid.NewGuid(),
            ContestId = contestId,
            ScoredAt = pickScoredAt,
            CreatedUtc = FixedUtcNow,
            CreatedBy = Guid.Empty
        };

        await DataContext.PickemGroupMatchups.AddAsync(matchup);
        await DataContext.UserPicks.AddAsync(pick);
        await DataContext.SaveChangesAsync();
    }

    private async Task SeedWeekResult(Guid leagueId, DateTime calculatedUtc)
    {
        var result = new PickemGroupWeekResult
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = Guid.NewGuid(),
            SeasonYear = SeasonYear,
            SeasonWeek = SeasonWeek,
            CalculatedUtc = calculatedUtc,
            CreatedUtc = FixedUtcNow,
            CreatedBy = Guid.Empty
        };

        await DataContext.PickemGroupWeekResults.AddAsync(result);
        await DataContext.SaveChangesAsync();
    }
}
