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
            .Verify(x => x.Publish(It.IsAny<ContestEnrichmentCompleted>(), It.IsAny<CancellationToken>()), Times.Never);
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
            Competitors = new List<CompetitionCompetitor>()
        };

        _baseballDataContext.Contests.Add(contest);
        _baseballDataContext.Competitions.Add(competition);
        await _baseballDataContext.SaveChangesAsync();

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());

        await _sut.Process(command);

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestEnrichmentCompleted>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenStatusNotFinal_ReturnsEarly()
    {
        var (contestId, _) = await SeedCompetitionWithStatus("STATUS_IN_PROGRESS");

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());

        await _sut.Process(command);

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestEnrichmentCompleted>(), It.IsAny<CancellationToken>()), Times.Never);
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
            Competitors = new List<CompetitionCompetitor>
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
            .Verify(x => x.Publish(It.IsAny<ContestEnrichmentCompleted>(), It.IsAny<CancellationToken>()), Times.Never);
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
        contest.WinnerFranchiseId.Should().Be(HomeFranchiseSeasonId);
        contest.FinalizedUtc.Should().NotBeNull();

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestEnrichmentCompleted>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Process_PrefersFinalScoreOverNonFinalRecords()
    {
        var (contestId, competitionId) = await SeedCompetitionWithStatus("STATUS_FINAL");

        var competition = await _baseballDataContext.Competitions
            .Include(c => c.Competitors)
            .FirstAsync(c => c.Id == competitionId);

        var away = competition.Competitors.First(c => c.HomeAway == "away");
        var home = competition.Competitors.First(c => c.HomeAway == "home");

        // Mid-game ticks should be ignored in favor of the "Final" record.
        _baseballDataContext.CompetitionCompetitorScores.AddRange(
            CreateScore(away.Id, value: 2, sourceDescription: "Live"),
            CreateScore(away.Id, value: 5, sourceDescription: "Final"),
            CreateScore(home.Id, value: 3, sourceDescription: "Live"),
            CreateScore(home.Id, value: 6, sourceDescription: "Final"));
        await _baseballDataContext.SaveChangesAsync();

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());
        await _sut.Process(command);

        var contest = await _baseballDataContext.Contests.FindAsync(contestId);
        contest!.AwayScore.Should().Be(5);
        contest.HomeScore.Should().Be(6);
    }

    [Fact]
    public async Task Process_WhenNoFinalRecord_FallsBackToMostRecent()
    {
        var (contestId, competitionId) = await SeedCompetitionWithStatus("STATUS_FINAL");

        var competition = await _baseballDataContext.Competitions
            .Include(c => c.Competitors)
            .FirstAsync(c => c.Id == competitionId);

        var away = competition.Competitors.First(c => c.HomeAway == "away");
        var home = competition.Competitors.First(c => c.HomeAway == "home");

        var earlier = new DateTime(2026, 4, 27, 10, 0, 0, DateTimeKind.Utc);
        var later = new DateTime(2026, 4, 27, 13, 0, 0, DateTimeKind.Utc);

        _baseballDataContext.CompetitionCompetitorScores.AddRange(
            CreateScore(away.Id, value: 1, sourceDescription: "Live", createdUtc: earlier),
            CreateScore(away.Id, value: 8, sourceDescription: "Live", createdUtc: later),
            CreateScore(home.Id, value: 2, sourceDescription: "Live", createdUtc: earlier),
            CreateScore(home.Id, value: 3, sourceDescription: "Live", createdUtc: later));
        await _baseballDataContext.SaveChangesAsync();

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());
        await _sut.Process(command);

        var contest = await _baseballDataContext.Contests.FindAsync(contestId);
        contest!.AwayScore.Should().Be(8);
        contest.HomeScore.Should().Be(3);
        contest.WinnerFranchiseId.Should().Be(AwayFranchiseSeasonId);
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
            .Verify(x => x.Publish(It.IsAny<ContestEnrichmentCompleted>(), It.IsAny<CancellationToken>()), Times.Never);
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
            .Verify(x => x.Publish(It.IsAny<ContestEnrichmentCompleted>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenTie_DoesNotSetWinner()
    {
        var (contestId, competitionId) = await SeedCompetitionWithStatus("STATUS_FINAL");

        var competition = await _baseballDataContext.Competitions
            .Include(c => c.Competitors)
            .FirstAsync(c => c.Id == competitionId);

        var away = competition.Competitors.First(c => c.HomeAway == "away");
        var home = competition.Competitors.First(c => c.HomeAway == "home");

        _baseballDataContext.CompetitionCompetitorScores.AddRange(
            CreateScore(away.Id, value: 5, sourceDescription: "Final"),
            CreateScore(home.Id, value: 5, sourceDescription: "Final"));
        await _baseballDataContext.SaveChangesAsync();

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());
        await _sut.Process(command);

        var contest = await _baseballDataContext.Contests.FindAsync(contestId);
        contest!.AwayScore.Should().Be(5);
        contest.HomeScore.Should().Be(5);
        contest.WinnerFranchiseId.Should().BeNull();
        contest.FinalizedUtc.Should().NotBeNull();
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
            o.EnrichedUtc.Should().NotBeNull();
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

    private static CompetitionCompetitor CreateCompetitor(
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
            Competitors = new List<CompetitionCompetitor>
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
        SourceId = "1",
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
