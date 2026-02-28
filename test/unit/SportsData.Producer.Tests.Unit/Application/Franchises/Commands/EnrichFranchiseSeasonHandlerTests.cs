using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Franchise;
using SportsData.Producer.Application.Franchises.Commands;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Franchises.Commands;

/// <summary>
/// Unit tests for EnrichFranchiseSeasonHandler to verify win/loss calculations and scoring margins
/// </summary>
[Collection("Sequential")]
public class EnrichFranchiseSeasonHandlerTests :
    ProducerTestBase<EnrichFranchiseSeasonHandler<FootballDataContext>>
{
    [Fact]
    public async Task Process_CalculatesWinsAndLosses_Correctly()
    {
        // Arrange
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonHandler<FootballDataContext>>();

        var seasonYear = 2024;
        var franchise = CreateFranchise();
        var franchiseSeason = CreateFranchiseSeason(franchise.Id, seasonYear);

        await FootballDataContext.Franchises.AddAsync(franchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);

        // Create 3 wins, 2 losses
        var opponentFranchise = CreateFranchise();
        var opponentSeason = CreateFranchiseSeason(opponentFranchise.Id, seasonYear);
        await FootballDataContext.Franchises.AddAsync(opponentFranchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(opponentSeason);

        // Win 1
        await FootballDataContext.Contests.AddAsync(CreateContest(
            franchiseSeason.Id, opponentSeason.Id, seasonYear,
            homeScore: 35, awayScore: 14, winnerId: franchiseSeason.Id));

        // Win 2
        await FootballDataContext.Contests.AddAsync(CreateContest(
            opponentSeason.Id, franchiseSeason.Id, seasonYear,
            homeScore: 21, awayScore: 28, winnerId: franchiseSeason.Id));

        // Win 3
        await FootballDataContext.Contests.AddAsync(CreateContest(
            franchiseSeason.Id, opponentSeason.Id, seasonYear,
            homeScore: 42, awayScore: 7, winnerId: franchiseSeason.Id));

        // Loss 1
        await FootballDataContext.Contests.AddAsync(CreateContest(
            franchiseSeason.Id, opponentSeason.Id, seasonYear,
            homeScore: 10, awayScore: 24, winnerId: opponentSeason.Id));

        // Loss 2
        await FootballDataContext.Contests.AddAsync(CreateContest(
            opponentSeason.Id, franchiseSeason.Id, seasonYear,
            homeScore: 31, awayScore: 17, winnerId: opponentSeason.Id));

        await FootballDataContext.SaveChangesAsync();

        var command = new EnrichFranchiseSeasonCommand(
            franchiseSeason.Id,
            seasonYear,
            Guid.NewGuid());

        // Act
        await sut.Process(command);

        // Assert
        var enriched = await FootballDataContext.FranchiseSeasons
            .FirstAsync(fs => fs.Id == franchiseSeason.Id);

        enriched.Wins.Should().Be(3, "there were 3 wins");
        enriched.Losses.Should().Be(2, "there were 2 losses");
        enriched.Ties.Should().Be(0, "there were no ties");
    }

    [Fact]
    public async Task Process_CalculatesTies_Correctly()
    {
        // Arrange
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonHandler<FootballDataContext>>();

        var seasonYear = 2024;
        var franchise = CreateFranchise();
        var franchiseSeason = CreateFranchiseSeason(franchise.Id, seasonYear);

        await FootballDataContext.Franchises.AddAsync(franchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);

        var opponentFranchise = CreateFranchise();
        var opponentSeason = CreateFranchiseSeason(opponentFranchise.Id, seasonYear);
        await FootballDataContext.Franchises.AddAsync(opponentFranchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(opponentSeason);

        // Win
        await FootballDataContext.Contests.AddAsync(CreateContest(
            franchiseSeason.Id, opponentSeason.Id, seasonYear,
            homeScore: 28, awayScore: 14, winnerId: franchiseSeason.Id));

        // Tie (WinnerFranchiseId = null)
        await FootballDataContext.Contests.AddAsync(CreateContest(
            franchiseSeason.Id, opponentSeason.Id, seasonYear,
            homeScore: 21, awayScore: 21, winnerId: null));

        // Loss
        await FootballDataContext.Contests.AddAsync(CreateContest(
            opponentSeason.Id, franchiseSeason.Id, seasonYear,
            homeScore: 35, awayScore: 14, winnerId: opponentSeason.Id));

        await FootballDataContext.SaveChangesAsync();

        var command = new EnrichFranchiseSeasonCommand(
            franchiseSeason.Id,
            seasonYear,
            Guid.NewGuid());

        // Act
        await sut.Process(command);

        // Assert
        var enriched = await FootballDataContext.FranchiseSeasons
            .FirstAsync(fs => fs.Id == franchiseSeason.Id);

        enriched.Wins.Should().Be(1, "there was 1 win");
        enriched.Losses.Should().Be(1, "there was 1 loss");
        enriched.Ties.Should().Be(1, "there was 1 tie");
    }

    [Fact]
    public async Task Process_CalculatesConferenceWinsAndLosses_Correctly()
    {
        // Arrange
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonHandler<FootballDataContext>>();

        var seasonYear = 2024;
        var conferenceId = Guid.NewGuid();

        var franchise = CreateFranchise();
        var franchiseSeason = CreateFranchiseSeason(franchise.Id, seasonYear, conferenceId);

        await FootballDataContext.Franchises.AddAsync(franchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);

        // Same conference opponent
        var confOpponentFranchise = CreateFranchise();
        var confOpponentSeason = CreateFranchiseSeason(confOpponentFranchise.Id, seasonYear, conferenceId);
        await FootballDataContext.Franchises.AddAsync(confOpponentFranchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(confOpponentSeason);

        // Different conference opponent
        var nonConfOpponentFranchise = CreateFranchise();
        var nonConfOpponentSeason = CreateFranchiseSeason(nonConfOpponentFranchise.Id, seasonYear, Guid.NewGuid());
        await FootballDataContext.Franchises.AddAsync(nonConfOpponentFranchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(nonConfOpponentSeason);

        // Conference Win
        await FootballDataContext.Contests.AddAsync(CreateContest(
            franchiseSeason.Id, confOpponentSeason.Id, seasonYear,
            homeScore: 35, awayScore: 14, winnerId: franchiseSeason.Id));

        // Conference Loss
        await FootballDataContext.Contests.AddAsync(CreateContest(
            confOpponentSeason.Id, franchiseSeason.Id, seasonYear,
            homeScore: 28, awayScore: 21, winnerId: confOpponentSeason.Id));

        // Non-conference Win (should not count toward conference record)
        await FootballDataContext.Contests.AddAsync(CreateContest(
            franchiseSeason.Id, nonConfOpponentSeason.Id, seasonYear,
            homeScore: 42, awayScore: 7, winnerId: franchiseSeason.Id));

        // Non-conference Loss (should not count toward conference record)
        await FootballDataContext.Contests.AddAsync(CreateContest(
            nonConfOpponentSeason.Id, franchiseSeason.Id, seasonYear,
            homeScore: 31, awayScore: 10, winnerId: nonConfOpponentSeason.Id));

        await FootballDataContext.SaveChangesAsync();

        var command = new EnrichFranchiseSeasonCommand(
            franchiseSeason.Id,
            seasonYear,
            Guid.NewGuid());

        // Act
        await sut.Process(command);

        // Assert
        var enriched = await FootballDataContext.FranchiseSeasons
            .FirstAsync(fs => fs.Id == franchiseSeason.Id);

        enriched.Wins.Should().Be(2, "there were 2 total wins");
        enriched.Losses.Should().Be(2, "there were 2 total losses");
        enriched.ConferenceWins.Should().Be(1, "there was 1 conference win");
        enriched.ConferenceLosses.Should().Be(1, "there was 1 conference loss");
    }

    [Fact]
    public async Task Process_CalculatesConferenceTies_Correctly()
    {
        // Arrange
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonHandler<FootballDataContext>>();

        var seasonYear = 2024;
        var conferenceId = Guid.NewGuid();

        var franchise = CreateFranchise();
        var franchiseSeason = CreateFranchiseSeason(franchise.Id, seasonYear, conferenceId);

        await FootballDataContext.Franchises.AddAsync(franchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);

        var confOpponentFranchise = CreateFranchise();
        var confOpponentSeason = CreateFranchiseSeason(confOpponentFranchise.Id, seasonYear, conferenceId);
        await FootballDataContext.Franchises.AddAsync(confOpponentFranchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(confOpponentSeason);

        // Conference Win
        await FootballDataContext.Contests.AddAsync(CreateContest(
            franchiseSeason.Id, confOpponentSeason.Id, seasonYear,
            homeScore: 35, awayScore: 14, winnerId: franchiseSeason.Id));

        // Conference Tie (WinnerFranchiseId = null)
        await FootballDataContext.Contests.AddAsync(CreateContest(
            franchiseSeason.Id, confOpponentSeason.Id, seasonYear,
            homeScore: 21, awayScore: 21, winnerId: null));

        await FootballDataContext.SaveChangesAsync();

        var command = new EnrichFranchiseSeasonCommand(
            franchiseSeason.Id,
            seasonYear,
            Guid.NewGuid());

        // Act
        await sut.Process(command);

        // Assert
        var enriched = await FootballDataContext.FranchiseSeasons
            .FirstAsync(fs => fs.Id == franchiseSeason.Id);

        enriched.Wins.Should().Be(1, "there was 1 win");
        enriched.Losses.Should().Be(0, "there were no losses");
        enriched.Ties.Should().Be(1, "there was 1 tie");
        enriched.ConferenceWins.Should().Be(1, "there was 1 conference win");
        enriched.ConferenceLosses.Should().Be(0, "there were no conference losses");
        enriched.ConferenceTies.Should().Be(1, "there was 1 conference tie");

        // Ties must not contribute to loss-margin statistics
        enriched.MarginLossMin.Should().BeNull("ties should not populate loss margin fields");
        enriched.MarginLossMax.Should().BeNull("ties should not populate loss margin fields");
        enriched.MarginLossAvg.Should().BeNull("ties should not populate loss margin fields");
    }

    [Fact]
    public async Task Process_CalculatesScoringMargins_Correctly()
    {
        // Arrange
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonHandler<FootballDataContext>>();

        var seasonYear = 2024;
        var franchise = CreateFranchise();
        var franchiseSeason = CreateFranchiseSeason(franchise.Id, seasonYear);

        await FootballDataContext.Franchises.AddAsync(franchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);

        var opponentFranchise = CreateFranchise();
        var opponentSeason = CreateFranchiseSeason(opponentFranchise.Id, seasonYear);
        await FootballDataContext.Franchises.AddAsync(opponentFranchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(opponentSeason);

        // Win by 21: 35-14 (scored 35, allowed 14)
        await FootballDataContext.Contests.AddAsync(CreateContest(
            franchiseSeason.Id, opponentSeason.Id, seasonYear,
            homeScore: 35, awayScore: 14, winnerId: franchiseSeason.Id));

        // Win by 7: 28-21 (scored 28, allowed 21)
        await FootballDataContext.Contests.AddAsync(CreateContest(
            opponentSeason.Id, franchiseSeason.Id, seasonYear,
            homeScore: 21, awayScore: 28, winnerId: franchiseSeason.Id));

        // Loss by 14: 10-24 (scored 10, allowed 24)
        await FootballDataContext.Contests.AddAsync(CreateContest(
            franchiseSeason.Id, opponentSeason.Id, seasonYear,
            homeScore: 10, awayScore: 24, winnerId: opponentSeason.Id));

        await FootballDataContext.SaveChangesAsync();

        var command = new EnrichFranchiseSeasonCommand(
            franchiseSeason.Id,
            seasonYear,
            Guid.NewGuid());

        // Act
        await sut.Process(command);

        // Assert
        var enriched = await FootballDataContext.FranchiseSeasons
            .FirstAsync(fs => fs.Id == franchiseSeason.Id);

        // Points Scored: 35, 28, 10
        enriched.PtsScoredMin.Should().Be(10);
        enriched.PtsScoredMax.Should().Be(35);
        enriched.PtsScoredAvg.Should().Be(24.33m); // (35+28+10)/3 = 24.33

        // Points Allowed: 14, 21, 24
        enriched.PtsAllowedMin.Should().Be(14);
        enriched.PtsAllowedMax.Should().Be(24);
        enriched.PtsAllowedAvg.Should().Be(19.67m); // (14+21+24)/3 = 19.67

        // Win Margins: 21, 7
        enriched.MarginWinMin.Should().Be(7);
        enriched.MarginWinMax.Should().Be(21);
        enriched.MarginWinAvg.Should().Be(14m); // (21+7)/2 = 14

        // Loss Margins: 14 (absolute value)
        enriched.MarginLossMin.Should().Be(14);
        enriched.MarginLossMax.Should().Be(14);
        enriched.MarginLossAvg.Should().Be(14m);
    }

    [Fact]
    public async Task Process_PublishesFranchiseSeasonEnrichmentCompleted_Event()
    {
        // Arrange
        var eventBus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonHandler<FootballDataContext>>();

        var seasonYear = 2024;
        var correlationId = Guid.NewGuid();
        var franchise = CreateFranchise();
        var franchiseSeason = CreateFranchiseSeason(franchise.Id, seasonYear);

        await FootballDataContext.Franchises.AddAsync(franchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await FootballDataContext.SaveChangesAsync();

        var command = new EnrichFranchiseSeasonCommand(
            franchiseSeason.Id,
            seasonYear,
            correlationId);

        // Act
        await sut.Process(command);

        // Assert
        eventBus.Verify(
            x => x.Publish(
                It.Is<FranchiseSeasonEnrichmentCompleted>(e =>
                    e.FranchiseSeasonId == franchiseSeason.Id &&
                    e.SeasonYear == seasonYear &&
                    e.CorrelationId == correlationId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Process_WhenNoContests_SetsRecordToZero()
    {
        // Arrange
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonHandler<FootballDataContext>>();

        var seasonYear = 2024;
        var franchise = CreateFranchise();
        var franchiseSeason = CreateFranchiseSeason(franchise.Id, seasonYear);

        await FootballDataContext.Franchises.AddAsync(franchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await FootballDataContext.SaveChangesAsync();

        var command = new EnrichFranchiseSeasonCommand(
            franchiseSeason.Id,
            seasonYear,
            Guid.NewGuid());

        // Act
        await sut.Process(command);

        // Assert
        var enriched = await FootballDataContext.FranchiseSeasons
            .FirstAsync(fs => fs.Id == franchiseSeason.Id);

        enriched.Wins.Should().Be(0);
        enriched.Losses.Should().Be(0);
        enriched.Ties.Should().Be(0);
        enriched.PtsScoredMin.Should().BeNull();
        enriched.PtsScoredMax.Should().BeNull();
        enriched.PtsScoredAvg.Should().BeNull();
    }

    private Franchise CreateFranchise()
    {
        return Fixture.Build<Franchise>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.Name, "Test Franchise")
            .With(x => x.DisplayName, "Test Franchise")
            .With(x => x.DisplayNameShort, "Test")
            .With(x => x.Location, "Test City")
            .With(x => x.Slug, Guid.NewGuid().ToString())
            .With(x => x.ColorCodeHex, "#000000")
            .With(x => x.Sport, Sport.FootballNcaa)
            .Create();
    }

    private FranchiseSeason CreateFranchiseSeason(Guid franchiseId, int seasonYear, Guid? groupSeasonId = null)
    {
        return Fixture.Build<FranchiseSeason>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.FranchiseId, franchiseId)
            .With(x => x.SeasonYear, seasonYear)
            .With(x => x.GroupSeasonId, groupSeasonId)
            .With(x => x.Name, "Test Season")
            .With(x => x.DisplayName, "Test Season")
            .With(x => x.DisplayNameShort, "Test")
            .With(x => x.Abbreviation, "TEST")
            .With(x => x.Location, "Test City")
            .With(x => x.Slug, Guid.NewGuid().ToString())
            .With(x => x.ColorCodeHex, "#000000")
            .With(x => x.ExternalIds, new List<FranchiseSeasonExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    Value = "test-value",
                    SourceUrl = "http://example.com/test",
                    SourceUrlHash = "test-hash"
                }
            })
            .Create();
    }

    private Contest CreateContest(
        Guid homeTeamId,
        Guid awayTeamId,
        int seasonYear,
        int homeScore,
        int awayScore,
        Guid? winnerId)
    {
        return Fixture.Build<Contest>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.HomeTeamFranchiseSeasonId, homeTeamId)
            .With(x => x.AwayTeamFranchiseSeasonId, awayTeamId)
            .With(x => x.SeasonYear, seasonYear)
            .With(x => x.HomeScore, homeScore)
            .With(x => x.AwayScore, awayScore)
            .With(x => x.WinnerFranchiseId, winnerId)
            .With(x => x.FinalizedUtc, DateTime.UtcNow)
            .With(x => x.Name, "Test Contest")
            .With(x => x.ShortName, "Test")
            .With(x => x.Sport, Sport.FootballNcaa)
            .Create();
    }
}
