using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Producer.Application.Contests;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Contests;

public class ContestEnrichmentProcessorTests : ProducerTestBase<ContestEnrichmentProcessor>
{
    private readonly ContestEnrichmentProcessor _sut;

    private static readonly Guid AwayFranchiseSeasonId = Guid.NewGuid();
    private static readonly Guid HomeFranchiseSeasonId = Guid.NewGuid();

    public ContestEnrichmentProcessorTests()
    {
        Mocker.Use(typeof(IDateTimeProvider), new Mock<IDateTimeProvider>().Object);
        Mock.Get(Mocker.Get<IDateTimeProvider>())
            .Setup(x => x.UtcNow())
            .Returns(new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc));

        _sut = Mocker.CreateInstance<ContestEnrichmentProcessor>();
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
        var competition = new FootballCompetition
        {
            Id = Guid.NewGuid(),
            ContestId = contestId,
            Contest = contest,
            Competitors = new List<CompetitionCompetitor>()
        };

        FootballDataContext.Contests.Add(contest);
        FootballDataContext.Competitions.Add(competition);
        await FootballDataContext.SaveChangesAsync();

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());

        await _sut.Process(command);

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestEnrichmentCompleted>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenOnlyHomeCompetitor_ReturnsEarly()
    {
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var contest = CreateContest(contestId);
        var competition = new FootballCompetition
        {
            Id = competitionId,
            ContestId = contestId,
            Contest = contest,
            Competitors = new List<CompetitionCompetitor>
            {
                CreateCompetitor(competitionId, HomeFranchiseSeasonId, "home")
            }
        };

        FootballDataContext.Contests.Add(contest);
        FootballDataContext.Competitions.Add(competition);
        await FootballDataContext.SaveChangesAsync();

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());

        await _sut.Process(command);

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestEnrichmentCompleted>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenOnlyAwayCompetitor_ReturnsEarly()
    {
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var contest = CreateContest(contestId);
        var competition = new FootballCompetition
        {
            Id = competitionId,
            ContestId = contestId,
            Contest = contest,
            Competitors = new List<CompetitionCompetitor>
            {
                CreateCompetitor(competitionId, AwayFranchiseSeasonId, "away")
            }
        };

        FootballDataContext.Contests.Add(contest);
        FootballDataContext.Competitions.Add(competition);
        await FootballDataContext.SaveChangesAsync();

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());

        await _sut.Process(command);

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestEnrichmentCompleted>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenStatusNotFinal_ReturnsEarly()
    {
        var (contestId, competitionId) = await SeedCompetitionWithStatus("STATUS_SCHEDULED");

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
        var competition = new FootballCompetition
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

        FootballDataContext.Contests.Add(contest);
        FootballDataContext.Competitions.Add(competition);
        await FootballDataContext.SaveChangesAsync();

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());

        await _sut.Process(command);

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestEnrichmentCompleted>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Process — scoring from plays

    [Fact]
    public async Task Process_WhenFinalWithScoringPlays_SetsScoresFromLastScoringPlay()
    {
        var (contestId, competitionId) = await SeedCompetitionWithStatus("STATUS_FINAL");

        // Seed scoring plays
        FootballDataContext.CompetitionPlays.AddRange(
            CreatePlay(competitionId, scoringPlay: true, awayScore: 7, homeScore: 0, period: 1, clock: 600),
            CreatePlay(competitionId, scoringPlay: false, awayScore: 7, homeScore: 0, period: 2, clock: 800),
            CreatePlay(competitionId, scoringPlay: true, awayScore: 14, homeScore: 7, period: 3, clock: 500),
            CreatePlay(competitionId, scoringPlay: true, awayScore: 14, homeScore: 14, period: 4, clock: 300)
        );
        await FootballDataContext.SaveChangesAsync();

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());
        await _sut.Process(command);

        var contest = await FootballDataContext.Contests.FindAsync(contestId);
        contest!.AwayScore.Should().Be(14);
        contest.HomeScore.Should().Be(14);
        contest.FinalizedUtc.Should().NotBeNull();

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestEnrichmentCompleted>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Process_WhenFinalWithScoringPlays_SetsWinner()
    {
        var (contestId, competitionId) = await SeedCompetitionWithStatus("STATUS_FINAL");

        FootballDataContext.CompetitionPlays.Add(
            CreatePlay(competitionId, scoringPlay: true, awayScore: 21, homeScore: 28, period: 4, clock: 100));
        await FootballDataContext.SaveChangesAsync();

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());
        await _sut.Process(command);

        var contest = await FootballDataContext.Contests.FindAsync(contestId);
        contest!.WinnerFranchiseId.Should().Be(HomeFranchiseSeasonId);
    }

    #endregion

    #region Process — D2 fallback (competitor scores)

    [Fact]
    public async Task Process_WhenNoPlaysButHasCompetitorScores_UsesScoreFallback()
    {
        var (contestId, competitionId) = await SeedCompetitionWithStatus("STATUS_FINAL");

        // Get the competitors to seed scores against
        var competition = await FootballDataContext.Competitions
            .Include(c => c.Competitors)
            .FirstAsync(c => c.Id == competitionId);

        var away = competition.Competitors.First(c => c.HomeAway == "away");
        var home = competition.Competitors.First(c => c.HomeAway == "home");

        FootballDataContext.CompetitionCompetitorScores.AddRange(
            new CompetitionCompetitorScore
            {
                Id = Guid.NewGuid(),
                CompetitionCompetitorId = away.Id,
                Value = 17,
                DisplayValue = "17",
                Winner = false,
                SourceId = "1",
                SourceDescription = "Final"
            },
            new CompetitionCompetitorScore
            {
                Id = Guid.NewGuid(),
                CompetitionCompetitorId = home.Id,
                Value = 24,
                DisplayValue = "24",
                Winner = true,
                SourceId = "1",
                SourceDescription = "Final"
            }
        );
        await FootballDataContext.SaveChangesAsync();

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());
        await _sut.Process(command);

        var contest = await FootballDataContext.Contests.FindAsync(contestId);
        contest!.AwayScore.Should().Be(17);
        contest.HomeScore.Should().Be(24);
        contest.WinnerFranchiseId.Should().Be(HomeFranchiseSeasonId);
        contest.FinalizedUtc.Should().NotBeNull();

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestEnrichmentCompleted>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Process_WhenNoPlaysAndOnlyOneTeamHasScore_ReturnsEarly()
    {
        var (contestId, competitionId) = await SeedCompetitionWithStatus("STATUS_FINAL");

        var competition = await FootballDataContext.Competitions
            .Include(c => c.Competitors)
            .FirstAsync(c => c.Id == competitionId);

        var away = competition.Competitors.First(c => c.HomeAway == "away");

        // Only seed away score — home has none
        FootballDataContext.CompetitionCompetitorScores.Add(
            new CompetitionCompetitorScore
            {
                Id = Guid.NewGuid(),
                CompetitionCompetitorId = away.Id,
                Value = 17,
                DisplayValue = "17",
                Winner = false,
                SourceId = "1",
                SourceDescription = "Final"
            });
        await FootballDataContext.SaveChangesAsync();

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());
        await _sut.Process(command);

        var contest = await FootballDataContext.Contests.FindAsync(contestId);
        contest!.FinalizedUtc.Should().BeNull();

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestEnrichmentCompleted>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenNoPlaysAndNoCompetitorScores_ReturnsEarly()
    {
        var (contestId, _) = await SeedCompetitionWithStatus("STATUS_FINAL");

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());
        await _sut.Process(command);

        var contest = await FootballDataContext.Contests.FindAsync(contestId);
        contest!.FinalizedUtc.Should().BeNull();

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestEnrichmentCompleted>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region GetOverUnderResult

    [Fact]
    public void GetOverUnderResult_WhenTotalExceedsLine_ReturnsOver()
    {
        var result = _sut.GetOverUnderResult(awayScore: 28, homeScore: 31, overUnder: 45.5m);

        result.Should().Be(OverUnderResult.Over);
    }

    [Fact]
    public void GetOverUnderResult_WhenTotalBelowLine_ReturnsUnder()
    {
        var result = _sut.GetOverUnderResult(awayScore: 10, homeScore: 14, overUnder: 45.5m);

        result.Should().Be(OverUnderResult.Under);
    }

    [Fact]
    public void GetOverUnderResult_WhenTotalEqualsLine_ReturnsPush()
    {
        var result = _sut.GetOverUnderResult(awayScore: 21, homeScore: 24, overUnder: 45.0m);

        result.Should().Be(OverUnderResult.Push);
    }

    #endregion

    #region GetSpreadWinnerFranchiseSeasonId

    [Fact]
    public void GetSpreadWinner_WhenHomeCoversSpread_ReturnsHomeFranchise()
    {
        var result = _sut.GetSpreadWinnerFranchiseSeasonId(
            AwayFranchiseSeasonId, HomeFranchiseSeasonId,
            awayScore: 14, homeScore: 28, spread: -7m);

        result.Should().Be(HomeFranchiseSeasonId);
    }

    [Fact]
    public void GetSpreadWinner_WhenAwayCoversSpread_ReturnsAwayFranchise()
    {
        var result = _sut.GetSpreadWinnerFranchiseSeasonId(
            AwayFranchiseSeasonId, HomeFranchiseSeasonId,
            awayScore: 17, homeScore: 21, spread: -7m);

        result.Should().Be(AwayFranchiseSeasonId);
    }

    [Fact]
    public void GetSpreadWinner_WhenExactSpread_ReturnsPush()
    {
        var result = _sut.GetSpreadWinnerFranchiseSeasonId(
            AwayFranchiseSeasonId, HomeFranchiseSeasonId,
            awayScore: 17, homeScore: 24, spread: -7m);

        result.Should().Be(Guid.Empty);
    }

    [Fact]
    public void GetSpreadWinner_WhenAwayFavored_CalculatesCorrectly()
    {
        var result = _sut.GetSpreadWinnerFranchiseSeasonId(
            AwayFranchiseSeasonId, HomeFranchiseSeasonId,
            awayScore: 31, homeScore: 21, spread: 3.5m);

        result.Should().Be(AwayFranchiseSeasonId);
    }

    #endregion

    #region EnrichOddsResults

    [Fact]
    public void EnrichOddsResults_SetsWinnerForAllProviders()
    {
        var odds = new List<CompetitionOdds>
        {
            CreateOdds("provider-1", "ESPN Bet", spread: -7m, overUnder: 45.5m),
            CreateOdds("provider-2", "DraftKings", spread: -6.5m, overUnder: 44.5m),
            CreateOdds("provider-3", "FanDuel", spread: -7.5m, overUnder: 46.0m)
        };

        _sut.EnrichOddsResults(odds, AwayFranchiseSeasonId, HomeFranchiseSeasonId, awayScore: 14, homeScore: 28);

        foreach (var o in odds)
        {
            o.WinnerFranchiseSeasonId.Should().Be(HomeFranchiseSeasonId);
            o.EnrichedUtc.Should().NotBeNull();
        }
    }

    [Fact]
    public void EnrichOddsResults_ComputesDifferentAtsResults_PerProvider()
    {
        var odds = new List<CompetitionOdds>
        {
            CreateOdds("provider-1", "ESPN Bet", spread: -7m, overUnder: 45.5m),
            CreateOdds("provider-2", "DraftKings", spread: -14.5m, overUnder: 45.5m)
        };

        _sut.EnrichOddsResults(odds, AwayFranchiseSeasonId, HomeFranchiseSeasonId, awayScore: 14, homeScore: 28);

        odds[0].AtsWinnerFranchiseSeasonId.Should().Be(HomeFranchiseSeasonId);
        odds[1].AtsWinnerFranchiseSeasonId.Should().Be(AwayFranchiseSeasonId);
    }

    [Fact]
    public void EnrichOddsResults_ComputesDifferentOverUnderResults_PerProvider()
    {
        var odds = new List<CompetitionOdds>
        {
            CreateOdds("provider-1", "ESPN Bet", spread: -7m, overUnder: 40.0m),
            CreateOdds("provider-2", "DraftKings", spread: -7m, overUnder: 45.0m)
        };

        _sut.EnrichOddsResults(odds, AwayFranchiseSeasonId, HomeFranchiseSeasonId, awayScore: 14, homeScore: 28);

        odds[0].OverUnderResult.Should().Be(OverUnderResult.Over);
        odds[1].OverUnderResult.Should().Be(OverUnderResult.Under);
    }

    [Fact]
    public void EnrichOddsResults_WhenTie_DoesNotSetWinner()
    {
        var odds = new List<CompetitionOdds>
        {
            CreateOdds("provider-1", "ESPN Bet", spread: -3m, overUnder: 42.0m)
        };

        _sut.EnrichOddsResults(odds, AwayFranchiseSeasonId, HomeFranchiseSeasonId, awayScore: 21, homeScore: 21);

        odds[0].WinnerFranchiseSeasonId.Should().BeNull();
        odds[0].OverUnderResult.Should().Be(OverUnderResult.Push);
    }

    [Fact]
    public void EnrichOddsResults_WhenNoSpreadOrOverUnder_OnlySetsWinnerAndTimestamp()
    {
        var odds = new List<CompetitionOdds>
        {
            CreateOdds("provider-1", "ESPN Bet", spread: null, overUnder: null)
        };

        _sut.EnrichOddsResults(odds, AwayFranchiseSeasonId, HomeFranchiseSeasonId, awayScore: 14, homeScore: 28);

        odds[0].WinnerFranchiseSeasonId.Should().Be(HomeFranchiseSeasonId);
        odds[0].AtsWinnerFranchiseSeasonId.Should().BeNull();
        odds[0].OverUnderResult.Should().Be(OverUnderResult.None);
        odds[0].EnrichedUtc.Should().NotBeNull();
    }

    #endregion

    #region Helpers

    private static FootballContest CreateContest(Guid contestId) => new()
    {
        Id = contestId,
        Name = "Test Game",
        ShortName = "TST",
        StartDateUtc = DateTime.UtcNow,
        Sport = Sport.FootballNcaa,
        SeasonYear = 2024
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
        var competition = new FootballCompetition
        {
            Id = competitionId,
            ContestId = contestId,
            Contest = contest,
            Status = new FootballCompetitionStatus
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

        FootballDataContext.Contests.Add(contest);
        FootballDataContext.Competitions.Add(competition);
        await FootballDataContext.SaveChangesAsync();

        return (contestId, competitionId);
    }

    private static FootballCompetitionPlay CreatePlay(
        Guid competitionId, bool scoringPlay, int awayScore, int homeScore, int period, double clock) => new()
    {
        Id = Guid.NewGuid(),
        CompetitionId = competitionId,
        ScoringPlay = scoringPlay,
        AwayScore = awayScore,
        HomeScore = homeScore,
        PeriodNumber = period,
        ClockValue = clock,
        EspnId = Guid.NewGuid().ToString()[..8],
        SequenceNumber = "1",
        Text = "Test play",
        TypeId = "1",
        Type = PlayType.Unknown,
        Modified = DateTime.UtcNow
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
