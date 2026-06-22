using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Producer.Application.Contests;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data.Baseball;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Contests;

// Parallels ContestEnrichmentProcessorTests but exercises the baseball
// path: no CompetitionPlays primary lookup — final scores come straight
// from CompetitionCompetitorScores. Class-local BaseballDataContext is
// overlaid so the SUT's _dataContext sees the baseball model.
public class BaseballContestEnrichmentProcessorTests
    : ProducerTestBase<BaseballContestEnrichmentProcessor>
{
    private readonly BaseballDataContext _baseballDataContext;
    private readonly BaseballContestEnrichmentProcessor _sut;

    private static readonly Guid AwayFranchiseSeasonId = Guid.NewGuid();
    private static readonly Guid HomeFranchiseSeasonId = Guid.NewGuid();

    public BaseballContestEnrichmentProcessorTests()
    {
        _baseballDataContext = new BaseballDataContext(
            new DbContextOptionsBuilder<BaseballDataContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()[..8])
                .Options);
        Mocker.Use(_baseballDataContext);

        Mocker.Use(typeof(IDateTimeProvider), new Mock<IDateTimeProvider>().Object);
        Mock.Get(Mocker.Get<IDateTimeProvider>())
            .Setup(x => x.UtcNow())
            .Returns(new DateTime(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc));

        _sut = Mocker.CreateInstance<BaseballContestEnrichmentProcessor>();
    }

    #region Process — guard clauses

    [Fact]
    public async Task Process_WhenCompetitionNotFound_ReturnsEarly()
    {
        var command = new EnrichContestCommand(Guid.NewGuid(), Guid.NewGuid());

        await _sut.Process(command);

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestFinalized>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenContestAlreadyFinalized_SkipsWithoutPublishing()
    {
        // D4 short-circuit (docs/contest-finalization-event-restructure.md):
        // a Contest already carrying FinalizedUtc means the work is done.
        // Re-runs (admin replay, at-least-once redelivery, event+cron overlap)
        // must no-op rather than re-doing the work and re-publishing.
        var (contestId, competitionId) = await SeedCompetitionWithStatus("STATUS_FINAL");

        // Stamp FinalizedUtc on the seeded Contest after seeding.
        var contestToFinalize = await _baseballDataContext.Contests.FindAsync(contestId);
        contestToFinalize!.FinalizedUtc = new DateTime(2026, 4, 26, 23, 30, 0, DateTimeKind.Utc);
        contestToFinalize.AwayScore = 4;
        contestToFinalize.HomeScore = 7;
        await _baseballDataContext.SaveChangesAsync();

        // Seed competitor scores that would otherwise drive the enrichment write —
        // ensures the assertion below catches accidental re-writes, not just a
        // graceful "no data" return.
        var competition = await _baseballDataContext.Competitions
            .Include(c => c.Competitors)
            .FirstAsync(c => c.Id == competitionId);

        var away = competition.Competitors.First(c => c.HomeAway == "away");
        var home = competition.Competitors.First(c => c.HomeAway == "home");

        _baseballDataContext.CompetitionCompetitorScores.AddRange(
            CreateScore(away.Id, value: 99, sourceDescription: "Final"),
            CreateScore(home.Id, value: 99, sourceDescription: "Final"));
        await _baseballDataContext.SaveChangesAsync();

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());

        await _sut.Process(command);

        // No publish — the short-circuit fired before the publish site is reached.
        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestFinalized>(), It.IsAny<CancellationToken>()), Times.Never);

        // Scores untouched (still 4-7, not overwritten to 99-99).
        var contest = await _baseballDataContext.Contests.FindAsync(contestId);
        contest!.AwayScore.Should().Be(4);
        contest.HomeScore.Should().Be(7);
    }

    [Fact]
    public async Task Process_WhenNoCompetitors_ReturnsEarly()
    {
        var contestId = Guid.NewGuid();
        var contest = CreateContest(contestId);
        var competition = new BaseballCompetition
        {
            Id = Guid.NewGuid(),
            ContestId = contestId,
            Contest = contest,
            Competitors = new List<CompetitionCompetitorBase>()
        };

        _baseballDataContext.Contests.Add(contest);
        _baseballDataContext.Competitions.Add(competition);
        await _baseballDataContext.SaveChangesAsync();

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());

        await _sut.Process(command);

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestFinalized>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenStatusNotFinal_ReturnsEarly()
    {
        var (contestId, _) = await SeedCompetitionWithStatus("STATUS_IN_PROGRESS");

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());

        await _sut.Process(command);

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestFinalized>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenStatusNull_ReturnsEarly()
    {
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var contest = CreateContest(contestId);
        var competition = new BaseballCompetition
        {
            Id = competitionId,
            ContestId = contestId,
            Contest = contest,
            Status = null,
            Competitors = new List<CompetitionCompetitorBase>
            {
                CreateCompetitor(competitionId, AwayFranchiseSeasonId, "away"),
                CreateCompetitor(competitionId, HomeFranchiseSeasonId, "home")
            }
        };

        _baseballDataContext.Contests.Add(contest);
        _baseballDataContext.Competitions.Add(competition);
        await _baseballDataContext.SaveChangesAsync();

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());

        await _sut.Process(command);

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestFinalized>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Process — scoring from competitor scores

    [Fact]
    public async Task Process_WhenFinalWithCompetitorScores_SetsScoresAndFinalizes()
    {
        var (contestId, competitionId) = await SeedCompetitionWithStatus("STATUS_FINAL");

        var competition = await _baseballDataContext.Competitions
            .Include(c => c.Competitors)
            .FirstAsync(c => c.Id == competitionId);

        var away = competition.Competitors.First(c => c.HomeAway == "away");
        var home = competition.Competitors.First(c => c.HomeAway == "home");

        _baseballDataContext.CompetitionCompetitorScores.AddRange(
            CreateScore(away.Id, value: 4, sourceDescription: "Final"),
            CreateScore(home.Id, value: 7, sourceDescription: "Final"));
        await _baseballDataContext.SaveChangesAsync();

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());
        await _sut.Process(command);

        var contest = await _baseballDataContext.Contests.FindAsync(contestId);
        contest!.AwayScore.Should().Be(4);
        contest.HomeScore.Should().Be(7);
        contest.WinnerFranchiseSeasonId.Should().Be(HomeFranchiseSeasonId);
        contest.FinalizedUtc.Should().NotBeNull();

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestFinalized>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Process_PicksMaxValuePerCompetitor()
    {
        // Earlier ticks (lower values) are ignored — MAX(Value) per
        // competitor returns the highest recorded score, which is always
        // the latest cumulative state because game scores only ever climb.
        var (contestId, competitionId) = await SeedCompetitionWithStatus("STATUS_FINAL");

        var competition = await _baseballDataContext.Competitions
            .Include(c => c.Competitors)
            .FirstAsync(c => c.Id == competitionId);

        var away = competition.Competitors.First(c => c.HomeAway == "away");
        var home = competition.Competitors.First(c => c.HomeAway == "home");

        _baseballDataContext.CompetitionCompetitorScores.AddRange(
            CreateScore(away.Id, value: 2, sourceDescription: "feed"),
            CreateScore(away.Id, value: 5, sourceDescription: "feed"),
            CreateScore(home.Id, value: 3, sourceDescription: "feed"),
            CreateScore(home.Id, value: 6, sourceDescription: "feed"));
        await _baseballDataContext.SaveChangesAsync();

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());
        await _sut.Process(command);

        var contest = await _baseballDataContext.Contests.FindAsync(contestId);
        contest!.AwayScore.Should().Be(5);
        contest.HomeScore.Should().Be(6);
    }

    [Fact]
    public async Task Process_WhenOnlyBootstrapRecords_DefersForCronRetry()
    {
        // Every competitor gets a SourceDescription="basic/manual" placeholder
        // row at season-start with Value=0. The ESPN feed later inserts (MLB)
        // or updates in place (NCAAFB) the real score. Before that lands,
        // MAX(Value) per competitor returns 0/0 — the MLB 0-0 sanity guard
        // catches this and defers (MLB cannot end 0-0 in regulation).
        // ContestEnrichmentJob cron sweep retries once feed lands.
        var (contestId, competitionId) = await SeedCompetitionWithStatus("STATUS_FINAL");

        var competition = await _baseballDataContext.Competitions
            .Include(c => c.Competitors)
            .FirstAsync(c => c.Id == competitionId);

        var away = competition.Competitors.First(c => c.HomeAway == "away");
        var home = competition.Competitors.First(c => c.HomeAway == "home");

        _baseballDataContext.CompetitionCompetitorScores.AddRange(
            CreateScore(away.Id, value: 0, sourceDescription: "basic/manual"),
            CreateScore(home.Id, value: 0, sourceDescription: "basic/manual"));
        await _baseballDataContext.SaveChangesAsync();

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());
        await _sut.Process(command);

        var contest = await _baseballDataContext.Contests.FindAsync(contestId);
        contest!.AwayScore.Should().BeNull();
        contest.HomeScore.Should().BeNull();
        contest.WinnerFranchiseSeasonId.Should().BeNull();
        contest.FinalizedUtc.Should().BeNull();

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestFinalized>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenMlbFinalScoresAreZeroZero_DefersForCronRetry()
    {
        // MLB games cannot end 0-0 in regulation — would proceed to extras.
        // A 0-0 "Final" is corrupt ESPN data or a sourcing-window race;
        // defer rather than lock in a finalized 0-0 contest with null Winner.
        var (contestId, competitionId) = await SeedCompetitionWithStatus("STATUS_FINAL");

        var competition = await _baseballDataContext.Competitions
            .Include(c => c.Competitors)
            .FirstAsync(c => c.Id == competitionId);

        var away = competition.Competitors.First(c => c.HomeAway == "away");
        var home = competition.Competitors.First(c => c.HomeAway == "home");

        _baseballDataContext.CompetitionCompetitorScores.AddRange(
            CreateScore(away.Id, value: 0, sourceDescription: "feed"),
            CreateScore(home.Id, value: 0, sourceDescription: "feed"));
        await _baseballDataContext.SaveChangesAsync();

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());
        await _sut.Process(command);

        var contest = await _baseballDataContext.Contests.FindAsync(contestId);
        contest!.AwayScore.Should().BeNull();
        contest.HomeScore.Should().BeNull();
        contest.WinnerFranchiseSeasonId.Should().BeNull();
        contest.FinalizedUtc.Should().BeNull();

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestFinalized>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenNoCompetitorScores_ReturnsEarly()
    {
        var (contestId, _) = await SeedCompetitionWithStatus("STATUS_FINAL");

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());
        await _sut.Process(command);

        var contest = await _baseballDataContext.Contests.FindAsync(contestId);
        contest!.FinalizedUtc.Should().BeNull();

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestFinalized>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenOnlyOneTeamHasScore_ReturnsEarly()
    {
        var (contestId, competitionId) = await SeedCompetitionWithStatus("STATUS_FINAL");

        var competition = await _baseballDataContext.Competitions
            .Include(c => c.Competitors)
            .FirstAsync(c => c.Id == competitionId);

        var away = competition.Competitors.First(c => c.HomeAway == "away");

        _baseballDataContext.CompetitionCompetitorScores.Add(
            CreateScore(away.Id, value: 4, sourceDescription: "Final"));
        await _baseballDataContext.SaveChangesAsync();

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());
        await _sut.Process(command);

        var contest = await _baseballDataContext.Contests.FindAsync(contestId);
        contest!.FinalizedUtc.Should().BeNull();

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestFinalized>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenMlbScoresAreNonZeroTie_DefersForCronRetry()
    {
        // MLB cannot end tied — extras run until a side leads at the end of
        // an inning. A non-zero tied "Final" (e.g. the 2026-06-20 White Sox
        // @ Tigers incident: enrichment ran with MAX(CCS)=2-2 because the
        // feed had only sourced through the early innings) means stale data,
        // same root cause as the 0-0 case but with a non-bootstrap value.
        // Defer rather than lock in a finalized tied contest with null
        // Winner. Prior behavior here was "finalize tied with null winner",
        // which was the corruption shape we're explicitly preventing.
        var (contestId, competitionId) = await SeedCompetitionWithStatus("STATUS_FINAL");

        var competition = await _baseballDataContext.Competitions
            .Include(c => c.Competitors)
            .FirstAsync(c => c.Id == competitionId);

        var away = competition.Competitors.First(c => c.HomeAway == "away");
        var home = competition.Competitors.First(c => c.HomeAway == "home");

        _baseballDataContext.CompetitionCompetitorScores.AddRange(
            CreateScore(away.Id, value: 2, sourceDescription: "feed"),
            CreateScore(home.Id, value: 2, sourceDescription: "feed"));
        await _baseballDataContext.SaveChangesAsync();

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());
        await _sut.Process(command);

        var contest = await _baseballDataContext.Contests.FindAsync(contestId);
        contest!.AwayScore.Should().BeNull();
        contest.HomeScore.Should().BeNull();
        contest.WinnerFranchiseSeasonId.Should().BeNull();
        contest.FinalizedUtc.Should().BeNull();
    }

    #endregion

    #region GetOverUnderResult

    [Fact]
    public void GetOverUnderResult_WhenTotalExceedsLine_ReturnsOver()
    {
        _sut.GetOverUnderResult(awayScore: 5, homeScore: 7, overUnder: 8.5m)
            .Should().Be(OverUnderResult.Over);
    }

    [Fact]
    public void GetOverUnderResult_WhenTotalBelowLine_ReturnsUnder()
    {
        _sut.GetOverUnderResult(awayScore: 1, homeScore: 2, overUnder: 8.5m)
            .Should().Be(OverUnderResult.Under);
    }

    [Fact]
    public void GetOverUnderResult_WhenTotalEqualsLine_ReturnsPush()
    {
        _sut.GetOverUnderResult(awayScore: 4, homeScore: 5, overUnder: 9.0m)
            .Should().Be(OverUnderResult.Push);
    }

    #endregion

    #region GetSpreadWinnerFranchiseSeasonId

    [Fact]
    public void GetSpreadWinner_WhenHomeCoversSpread_ReturnsHomeFranchise()
    {
        var result = _sut.GetSpreadWinnerFranchiseSeasonId(
            AwayFranchiseSeasonId, HomeFranchiseSeasonId,
            awayScore: 3, homeScore: 7, spread: -1.5m);

        result.Should().Be(HomeFranchiseSeasonId);
    }

    [Fact]
    public void GetSpreadWinner_WhenAwayCoversSpread_ReturnsAwayFranchise()
    {
        var result = _sut.GetSpreadWinnerFranchiseSeasonId(
            AwayFranchiseSeasonId, HomeFranchiseSeasonId,
            awayScore: 6, homeScore: 5, spread: -1.5m);

        result.Should().Be(AwayFranchiseSeasonId);
    }

    [Fact]
    public void GetSpreadWinner_WhenAwayFavored_CalculatesCorrectly()
    {
        var result = _sut.GetSpreadWinnerFranchiseSeasonId(
            AwayFranchiseSeasonId, HomeFranchiseSeasonId,
            awayScore: 8, homeScore: 4, spread: 1.5m);

        result.Should().Be(AwayFranchiseSeasonId);
    }

    #endregion

    #region EnrichOddsResults

    [Fact]
    public void EnrichOddsResults_SetsWinnerForAllProviders()
    {
        var odds = new List<CompetitionOdds>
        {
            CreateOdds("provider-1", "ESPN Bet", spread: -1.5m, overUnder: 8.5m),
            CreateOdds("provider-2", "DraftKings", spread: -1.5m, overUnder: 9.0m)
        };

        _sut.EnrichOddsResults(odds, AwayFranchiseSeasonId, HomeFranchiseSeasonId, awayScore: 3, homeScore: 7);

        foreach (var o in odds)
        {
            o.WinnerFranchiseSeasonId.Should().Be(HomeFranchiseSeasonId);
            o.FinalizedUtc.Should().NotBeNull();
        }
    }

    [Fact]
    public void EnrichOddsResults_ComputesDifferentOverUnderResults_PerProvider()
    {
        var odds = new List<CompetitionOdds>
        {
            CreateOdds("provider-1", "ESPN Bet", spread: -1.5m, overUnder: 8.0m),
            CreateOdds("provider-2", "DraftKings", spread: -1.5m, overUnder: 11.0m)
        };

        _sut.EnrichOddsResults(odds, AwayFranchiseSeasonId, HomeFranchiseSeasonId, awayScore: 4, homeScore: 6);

        odds[0].OverUnderResult.Should().Be(OverUnderResult.Over);
        odds[1].OverUnderResult.Should().Be(OverUnderResult.Under);
    }

    [Fact]
    public void EnrichOddsResults_WhenTie_DoesNotSetWinner()
    {
        var odds = new List<CompetitionOdds>
        {
            CreateOdds("provider-1", "ESPN Bet", spread: -1.5m, overUnder: 9.0m)
        };

        _sut.EnrichOddsResults(odds, AwayFranchiseSeasonId, HomeFranchiseSeasonId, awayScore: 4, homeScore: 4);

        odds[0].WinnerFranchiseSeasonId.Should().BeNull();
        odds[0].OverUnderResult.Should().Be(OverUnderResult.Under);
    }

    #endregion

    #region Helpers

    private static BaseballContest CreateContest(Guid contestId) => new()
    {
        Id = contestId,
        Name = "Test Game",
        ShortName = "TST",
        StartDateUtc = DateTime.UtcNow,
        Sport = Sport.BaseballMlb,
        SeasonYear = 2026
    };

    private static BaseballCompetitionCompetitor CreateCompetitor(
        Guid competitionId, Guid franchiseSeasonId, string homeAway) => new()
    {
        Id = Guid.NewGuid(),
        CompetitionId = competitionId,
        FranchiseSeasonId = franchiseSeasonId,
        HomeAway = homeAway,
        Order = homeAway == "home" ? 1 : 0
    };

    private async Task<(Guid ContestId, Guid CompetitionId)> SeedCompetitionWithStatus(string statusTypeName)
    {
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var contest = CreateContest(contestId);
        var competition = new BaseballCompetition
        {
            Id = competitionId,
            ContestId = contestId,
            Contest = contest,
            Status = new BaseballCompetitionStatus
            {
                Id = Guid.NewGuid(),
                CompetitionId = competitionId,
                StatusTypeName = statusTypeName
            },
            Competitors = new List<CompetitionCompetitorBase>
            {
                CreateCompetitor(competitionId, AwayFranchiseSeasonId, "away"),
                CreateCompetitor(competitionId, HomeFranchiseSeasonId, "home")
            }
        };

        _baseballDataContext.Contests.Add(contest);
        _baseballDataContext.Competitions.Add(competition);
        await _baseballDataContext.SaveChangesAsync();

        return (contestId, competitionId);
    }

    private static CompetitionCompetitorScore CreateScore(
        Guid competitionCompetitorId,
        double value,
        string sourceDescription,
        DateTime? createdUtc = null) => new()
    {
        Id = Guid.NewGuid(),
        CompetitionCompetitorId = competitionCompetitorId,
        Value = value,
        DisplayValue = value.ToString(),
        Winner = false,
        // Derive SourceId from sourceDescription to match production semantics
        // (SourceId="1" + "basic/manual" is the bootstrap; SourceId="2" + "feed"
        // is the canonical ESPN feed value). Case-insensitive to cover the
        // NCAAFB "Basic/Manual" variant.
        SourceId = sourceDescription.Equals("basic/manual", StringComparison.OrdinalIgnoreCase)
            ? "1"
            : "2",
        SourceDescription = sourceDescription,
        CreatedUtc = createdUtc ?? DateTime.UtcNow
    };

    private static CompetitionOdds CreateOdds(
        string providerId,
        string providerName,
        decimal? spread,
        decimal? overUnder) => new()
    {
        Id = Guid.NewGuid(),
        CompetitionId = Guid.NewGuid(),
        ProviderRef = new Uri("https://example.com/odds"),
        ProviderId = providerId,
        ProviderName = providerName,
        Spread = spread,
        OverUnder = overUnder
    };

    #endregion
}
