using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Contests;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data.Entities;

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

    #region Process — competitor guard

    [Fact]
    public async Task Process_WhenCompetitionNotFound_ReturnsEarly()
    {
        // No competition seeded in the in-memory DB
        var command = new EnrichContestCommand(Guid.NewGuid(), Guid.NewGuid());

        await _sut.Process(command);

        // Should not attempt to publish anything
        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestEnrichmentCompleted>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenNoCompetitors_ReturnsEarly()
    {
        var contestId = Guid.NewGuid();
        var contest = new Contest
        {
            Id = contestId,
            Name = "Test Game",
            ShortName = "TST",
            StartDateUtc = DateTime.UtcNow,
            Sport = Sport.FootballNcaa,
            SeasonYear = 2024
        };
        var competition = new Competition
        {
            Id = Guid.NewGuid(),
            ContestId = contestId,
            Contest = contest,
            Competitors = new List<CompetitionCompetitor>() // empty
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
        var contest = new Contest
        {
            Id = contestId,
            Name = "Test Game",
            ShortName = "TST",
            StartDateUtc = DateTime.UtcNow,
            Sport = Sport.FootballNcaa,
            SeasonYear = 2024
        };
        var competition = new Competition
        {
            Id = competitionId,
            ContestId = contestId,
            Contest = contest,
            Competitors = new List<CompetitionCompetitor>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = competitionId,
                    FranchiseSeasonId = HomeFranchiseSeasonId,
                    HomeAway = "home",
                    Order = 0
                }
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
        var contest = new Contest
        {
            Id = contestId,
            Name = "Test Game",
            ShortName = "TST",
            StartDateUtc = DateTime.UtcNow,
            Sport = Sport.FootballNcaa,
            SeasonYear = 2024
        };
        var competition = new Competition
        {
            Id = competitionId,
            ContestId = contestId,
            Contest = contest,
            Competitors = new List<CompetitionCompetitor>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = competitionId,
                    FranchiseSeasonId = AwayFranchiseSeasonId,
                    HomeAway = "away",
                    Order = 0
                }
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
    public async Task Process_WhenCompetitorsHaveEmptyExternalIds_ReturnsEarlyOnD2Fallback()
    {
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var contest = new Contest
        {
            Id = contestId,
            Name = "D2 Test Game",
            ShortName = "D2T",
            StartDateUtc = DateTime.UtcNow,
            Sport = Sport.FootballNcaa,
            SeasonYear = 2024
        };
        var competition = new Competition
        {
            Id = competitionId,
            ContestId = contestId,
            Contest = contest,
            Status = new CompetitionStatus
            {
                Id = Guid.NewGuid(),
                CompetitionId = competitionId,
                StatusTypeName = "STATUS_FINAL"
            },
            ExternalIds = new List<CompetitionExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = competitionId,
                    Provider = SourceDataProvider.Espn,
                    Value = "12345",
                    SourceUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/12345/competitions/12345",
                    SourceUrlHash = "abc123"
                }
            },
            Competitors = new List<CompetitionCompetitor>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = competitionId,
                    FranchiseSeasonId = AwayFranchiseSeasonId,
                    HomeAway = "away",
                    Order = 0,
                    ExternalIds = new List<CompetitionCompetitorExternalId>() // empty
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = competitionId,
                    FranchiseSeasonId = HomeFranchiseSeasonId,
                    HomeAway = "home",
                    Order = 1,
                    ExternalIds = new List<CompetitionCompetitorExternalId>() // empty
                }
            }
        };

        FootballDataContext.Contests.Add(contest);
        FootballDataContext.Competitions.Add(competition);
        await FootballDataContext.SaveChangesAsync();

        // Mock plays to return empty (triggers D2 fallback path)
        Mock.Get(Mocker.Get<IProvideEspnApiData>())
            .Setup(x => x.GetCompetitionPlaysAsync(It.IsAny<Uri>()))
            .ReturnsAsync(new EspnEventCompetitionPlaysDto { Count = 0, Items = new() });

        var command = new EnrichContestCommand(contestId, Guid.NewGuid());

        await _sut.Process(command);

        // Should return early due to missing ExternalIds — no enrichment event published
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
        // Home favored by 7 (spread = -7), home wins 28-14 (covers)
        var result = _sut.GetSpreadWinnerFranchiseSeasonId(
            AwayFranchiseSeasonId, HomeFranchiseSeasonId,
            awayScore: 14, homeScore: 28, spread: -7m);

        result.Should().Be(HomeFranchiseSeasonId);
    }

    [Fact]
    public void GetSpreadWinner_WhenAwayCoversSpread_ReturnsAwayFranchise()
    {
        // Home favored by 7 (spread = -7), home wins 21-17 (doesn't cover)
        var result = _sut.GetSpreadWinnerFranchiseSeasonId(
            AwayFranchiseSeasonId, HomeFranchiseSeasonId,
            awayScore: 17, homeScore: 21, spread: -7m);

        result.Should().Be(AwayFranchiseSeasonId);
    }

    [Fact]
    public void GetSpreadWinner_WhenExactSpread_ReturnsPush()
    {
        // Home favored by 7 (spread = -7), home wins 24-17 (push)
        var result = _sut.GetSpreadWinnerFranchiseSeasonId(
            AwayFranchiseSeasonId, HomeFranchiseSeasonId,
            awayScore: 17, homeScore: 24, spread: -7m);

        result.Should().Be(Guid.Empty);
    }

    [Fact]
    public void GetSpreadWinner_WhenAwayFavored_CalculatesCorrectly()
    {
        // Away favored (spread = +3.5 for home), away wins 31-21
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

        // Home wins 28-14
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

        // Home wins 28-14 (margin = 14)
        // ESPN Bet spread -7: home covers (28 + (-7) = 21 > 14) → home wins ATS
        // DraftKings spread -14.5: home doesn't cover (28 + (-14.5) = 13.5 < 14) → away wins ATS
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

        // Total = 28 + 14 = 42
        // ESPN Bet O/U 40.0: Over
        // DraftKings O/U 45.0: Under
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
        odds[0].OverUnderResult.Should().Be(OverUnderResult.Push); // 42 == 42.0 → Push
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

    private static CompetitionOdds CreateOdds(
        string providerId,
        string providerName,
        decimal? spread,
        decimal? overUnder)
    {
        return new CompetitionOdds
        {
            Id = Guid.NewGuid(),
            CompetitionId = Guid.NewGuid(),
            ProviderRef = new Uri("https://example.com/odds"),
            ProviderId = providerId,
            ProviderName = providerName,
            Spread = spread,
            OverUnder = overUnder
        };
    }

    #endregion
}
