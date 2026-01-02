using AutoFixture;
using SportsData.Api.Application.Common.Enums;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Api.Application;
using SportsData.Api.Application.Admin.SyntheticPicks;
using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Config;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Admin.SyntheticPicks;

public class SyntheticPickServiceTests : ApiTestBase<SyntheticPickService>
{
    private readonly Mock<ISyntheticPickStyleProvider> _mockPickStyleProvider;
    private readonly Mock<IGetLeagueWeekMatchupsQueryHandler> _mockGetLeagueWeekMatchupsHandler;
    private readonly SyntheticPickService _sut;

    public SyntheticPickServiceTests()
    {
        _mockPickStyleProvider = Mocker.GetMock<ISyntheticPickStyleProvider>();
        _mockGetLeagueWeekMatchupsHandler = Mocker.GetMock<IGetLeagueWeekMatchupsQueryHandler>();
        _sut = Mocker.CreateInstance<SyntheticPickService>();
    }

    [Fact]
    public async Task GenerateMetricBasedPicksForSynthetic_NoMatchups_ReturnsEarly()
    {
        // Arrange
        var pickemGroupId = Guid.NewGuid();
        var syntheticId = Guid.NewGuid();
        
        _mockGetLeagueWeekMatchupsHandler
            .Setup(x => x.ExecuteAsync(It.Is<GetLeagueWeekMatchupsQuery>(q =>
                q.UserId == syntheticId && q.LeagueId == pickemGroupId && q.Week == 14), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<LeagueWeekMatchupsDto>(
                default!,
                ResultStatus.NotFound,
                []));

        // Act
        await _sut.GenerateMetricBasedPicksForSynthetic(
            pickemGroupId,
            PickType.AgainstTheSpread,
            syntheticId,
            "moderate",
            14);

        // Assert
        var picks = await DataContext.UserPicks.ToListAsync();
        picks.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateMetricBasedPicksForSynthetic_PickAlreadyExists_SkipsMatchup()
    {
        // Arrange
        var pickemGroupId = Guid.NewGuid();
        var syntheticId = Guid.NewGuid();
        var contestId = Guid.NewGuid();

        // Add existing pick
        var existingPick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.ContestId, contestId)
            .With(p => p.PickemGroupId, pickemGroupId)
            .With(p => p.UserId, syntheticId)
            .Create();
        await DataContext.UserPicks.AddAsync(existingPick);
        await DataContext.SaveChangesAsync();

        var matchup = Fixture.Build<LeagueWeekMatchupsDto.MatchupForPickDto>()
            .With(m => m.ContestId, contestId)
            .Create();

        var matchupsDto = Fixture.Build<LeagueWeekMatchupsDto>()
            .With(m => m.Matchups, new List<LeagueWeekMatchupsDto.MatchupForPickDto> { matchup })
            .Create();

        _mockGetLeagueWeekMatchupsHandler
            .Setup(x => x.ExecuteAsync(It.Is<GetLeagueWeekMatchupsQuery>(q =>
                q.UserId == syntheticId && q.LeagueId == pickemGroupId && q.Week == 14), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<LeagueWeekMatchupsDto>(matchupsDto));

        // Act
        await _sut.GenerateMetricBasedPicksForSynthetic(
            pickemGroupId,
            PickType.AgainstTheSpread,
            syntheticId,
            "moderate",
            14);

        // Assert
        var picks = await DataContext.UserPicks.ToListAsync();
        picks.Should().HaveCount(1); // Only the existing pick, no new ones
    }

    [Fact]
    public async Task GenerateMetricBasedPicksForSynthetic_NoPrediction_SkipsMatchup()
    {
        // Arrange
        var pickemGroupId = Guid.NewGuid();
        var syntheticId = Guid.NewGuid();
        var contestId = Guid.NewGuid();

        var matchup = Fixture.Build<LeagueWeekMatchupsDto.MatchupForPickDto>()
            .With(m => m.ContestId, contestId)
            .Create();

        var matchupsDto = Fixture.Build<LeagueWeekMatchupsDto>()
            .With(m => m.Matchups, new List<LeagueWeekMatchupsDto.MatchupForPickDto> { matchup })
            .Create();

        _mockGetLeagueWeekMatchupsHandler
            .Setup(x => x.ExecuteAsync(It.Is<GetLeagueWeekMatchupsQuery>(q =>
                q.UserId == syntheticId && q.LeagueId == pickemGroupId && q.Week == 14), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<LeagueWeekMatchupsDto>(matchupsDto));

        // No prediction in database

        // Act
        await _sut.GenerateMetricBasedPicksForSynthetic(
            pickemGroupId,
            PickType.AgainstTheSpread,
            syntheticId,
            "moderate",
            14);

        // Assert
        var picks = await DataContext.UserPicks.ToListAsync();
        picks.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateMetricBasedPicksForSynthetic_WithValidPrediction_CreatesPickUsingModelPrediction()
    {
        // Arrange
        var pickemGroupId = Guid.NewGuid();
        var syntheticId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();

        var prediction = Fixture.Build<ContestPrediction>()
            .With(p => p.ContestId, contestId)
            .With(p => p.PredictionType, PickType.StraightUp)
            .With(p => p.WinnerFranchiseSeasonId, homeTeamId)
            .With(p => p.WinProbability, 0.75m)
            .Create();
        await DataContext.ContestPredictions.AddAsync(prediction);
        await DataContext.SaveChangesAsync();

        var matchup = Fixture.Build<LeagueWeekMatchupsDto.MatchupForPickDto>()
            .With(m => m.ContestId, contestId)
            .With(m => m.HomeFranchiseSeasonId, homeTeamId)
            .With(m => m.AwayFranchiseSeasonId, awayTeamId)
            .Create();

        var matchupsDto = Fixture.Build<LeagueWeekMatchupsDto>()
            .With(m => m.Matchups, new List<LeagueWeekMatchupsDto.MatchupForPickDto> { matchup })
            .Create();

        _mockGetLeagueWeekMatchupsHandler
            .Setup(x => x.ExecuteAsync(It.Is<GetLeagueWeekMatchupsQuery>(q =>
                q.UserId == syntheticId && q.LeagueId == pickemGroupId && q.Week == 14), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<LeagueWeekMatchupsDto>(matchupsDto));

        // Act
        await _sut.GenerateMetricBasedPicksForSynthetic(
            pickemGroupId,
            PickType.StraightUp,
            syntheticId,
            "moderate",
            14);

        // Assert
        var picks = await DataContext.UserPicks.ToListAsync();
        picks.Should().HaveCount(1);
        picks[0].FranchiseId.Should().Be(homeTeamId);
        picks[0].ContestId.Should().Be(contestId);
        picks[0].UserId.Should().Be(syntheticId);
        picks[0].Week.Should().Be(14);
    }

    [Fact]
    public async Task GenerateMetricBasedPicksForSynthetic_ATS_HomeFavored_LowConfidence_PicksUnderdog()
    {
        // Arrange
        // Scenario: Home team favored by 21 points, but only 60% confident
        // Threshold requires 80% for large spreads
        // Should pick away team (underdog)
        var pickemGroupId = Guid.NewGuid();
        var syntheticId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();

        var prediction = Fixture.Build<ContestPrediction>()
            .With(p => p.ContestId, contestId)
            .With(p => p.PredictionType, PickType.AgainstTheSpread)
            .With(p => p.WinnerFranchiseSeasonId, homeTeamId) // Always home team
            .With(p => p.WinProbability, 0.60m) // 60% home team covers
            .Create();
        await DataContext.ContestPredictions.AddAsync(prediction);
        await DataContext.SaveChangesAsync();

        var matchup = Fixture.Build<LeagueWeekMatchupsDto.MatchupForPickDto>()
            .With(m => m.ContestId, contestId)
            .With(m => m.HomeFranchiseSeasonId, homeTeamId)
            .With(m => m.AwayFranchiseSeasonId, awayTeamId)
            .With(m => m.SpreadCurrent, -21.0m) // Home favored by 21
            .Create();

        var matchupsDto = Fixture.Build<LeagueWeekMatchupsDto>()
            .With(m => m.Matchups, new List<LeagueWeekMatchupsDto.MatchupForPickDto> { matchup })
            .Create();

        _mockGetLeagueWeekMatchupsHandler
            .Setup(x => x.ExecuteAsync(It.Is<GetLeagueWeekMatchupsQuery>(q =>
                q.UserId == syntheticId && q.LeagueId == pickemGroupId && q.Week == 14), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<LeagueWeekMatchupsDto>(matchupsDto));

        _mockPickStyleProvider
            .Setup(p => p.GetRequiredConfidence("moderate", 21.0))
            .Returns(0.80); // Requires 80% confidence

        // Act
        await _sut.GenerateMetricBasedPicksForSynthetic(
            pickemGroupId,
            PickType.AgainstTheSpread,
            syntheticId,
            "moderate",
            14);

        // Assert
        var picks = await DataContext.UserPicks.ToListAsync();
        picks.Should().HaveCount(1);
        picks[0].FranchiseId.Should().Be(awayTeamId); // Picks underdog (60% < 80%)
    }

    [Fact]
    public async Task GenerateMetricBasedPicksForSynthetic_ATS_HomeFavored_HighConfidence_PicksFavorite()
    {
        // Arrange
        // Scenario: Home team favored by 21 points with 85% confidence
        // Threshold requires 80% for large spreads
        // Should pick home team (favorite)
        var pickemGroupId = Guid.NewGuid();
        var syntheticId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();

        var prediction = Fixture.Build<ContestPrediction>()
            .With(p => p.ContestId, contestId)
            .With(p => p.PredictionType, PickType.AgainstTheSpread)
            .With(p => p.WinnerFranchiseSeasonId, homeTeamId) // Always home team
            .With(p => p.WinProbability, 0.85m) // 85% home team covers
            .Create();
        await DataContext.ContestPredictions.AddAsync(prediction);
        await DataContext.SaveChangesAsync();

        var matchup = Fixture.Build<LeagueWeekMatchupsDto.MatchupForPickDto>()
            .With(m => m.ContestId, contestId)
            .With(m => m.HomeFranchiseSeasonId, homeTeamId)
            .With(m => m.AwayFranchiseSeasonId, awayTeamId)
            .With(m => m.SpreadCurrent, -21.0m) // Home favored by 21
            .Create();

        var matchupsDto = Fixture.Build<LeagueWeekMatchupsDto>()
            .With(m => m.Matchups, new List<LeagueWeekMatchupsDto.MatchupForPickDto> { matchup })
            .Create();

        _mockGetLeagueWeekMatchupsHandler
            .Setup(x => x.ExecuteAsync(It.Is<GetLeagueWeekMatchupsQuery>(q =>
                q.UserId == syntheticId && q.LeagueId == pickemGroupId && q.Week == 14), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<LeagueWeekMatchupsDto>(matchupsDto));

        _mockPickStyleProvider
            .Setup(p => p.GetRequiredConfidence("moderate", 21.0))
            .Returns(0.80); // Requires 80% confidence

        // Act
        await _sut.GenerateMetricBasedPicksForSynthetic(
            pickemGroupId,
            PickType.AgainstTheSpread,
            syntheticId,
            "moderate",
            14);

        // Assert
        var picks = await DataContext.UserPicks.ToListAsync();
        picks.Should().HaveCount(1);
        picks[0].FranchiseId.Should().Be(homeTeamId); // Picks favorite (85% >= 80%)
    }

    [Fact]
    public async Task GenerateMetricBasedPicksForSynthetic_ATS_HomeFavored_VeryLowConfidence_PicksUnderdog()
    {
        // Arrange
        // Real scenario: Troy @ JMU (-22.5) from conference championship
        // Home team JMU favored by 22.5, but only 29.3% confidence to cover
        // This means Troy (underdog) has 70.7% chance to cover
        // Threshold requires 80% for large spreads
        // Since favorite (JMU) has only 29.3% < 80%, pick underdog (Troy)
        var pickemGroupId = Guid.NewGuid();
        var syntheticId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var homeTeamId = Guid.NewGuid(); // JMU
        var awayTeamId = Guid.NewGuid(); // Troy

        var prediction = Fixture.Build<ContestPrediction>()
            .With(p => p.ContestId, contestId)
            .With(p => p.PredictionType, PickType.AgainstTheSpread)
            .With(p => p.WinnerFranchiseSeasonId, homeTeamId) // Always home team
            .With(p => p.WinProbability, 0.293m) // 29.3% JMU (home/favorite) covers -22.5
            .Create();
        await DataContext.ContestPredictions.AddAsync(prediction);
        await DataContext.SaveChangesAsync();

        var matchup = Fixture.Build<LeagueWeekMatchupsDto.MatchupForPickDto>()
            .With(m => m.ContestId, contestId)
            .With(m => m.HomeFranchiseSeasonId, homeTeamId)
            .With(m => m.AwayFranchiseSeasonId, awayTeamId)
            .With(m => m.SpreadCurrent, -22.5m) // JMU (home) favored by 22.5
            .Create();

        var matchupsDto = Fixture.Build<LeagueWeekMatchupsDto>()
            .With(m => m.Matchups, new List<LeagueWeekMatchupsDto.MatchupForPickDto> { matchup })
            .Create();

        _mockGetLeagueWeekMatchupsHandler
            .Setup(x => x.ExecuteAsync(It.Is<GetLeagueWeekMatchupsQuery>(q =>
                q.UserId == syntheticId && q.LeagueId == pickemGroupId && q.Week == 14), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<LeagueWeekMatchupsDto>(matchupsDto));

        _mockPickStyleProvider
            .Setup(p => p.GetRequiredConfidence("aggressive", 22.5))
            .Returns(0.80); // Requires 80% confidence

        // Act
        await _sut.GenerateMetricBasedPicksForSynthetic(
            pickemGroupId,
            PickType.AgainstTheSpread,
            syntheticId,
            "aggressive",
            14);

        // Assert
        var picks = await DataContext.UserPicks.ToListAsync();
        picks.Should().HaveCount(1);
        // Favorite (JMU/home) has only 29.3% < 80% threshold
        // So pick the underdog (Troy/away) who has 70.7% chance to cover
        picks[0].FranchiseId.Should().Be(awayTeamId); // Picks Troy (underdog)
    }

    [Fact]
    public async Task GenerateMetricBasedPicksForSynthetic_ATS_SmallSpread_UsesModelPrediction()
    {
        // Arrange
        // Scenario: Small spread (-3.5), lower threshold required
        var pickemGroupId = Guid.NewGuid();
        var syntheticId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();

        var prediction = Fixture.Build<ContestPrediction>()
            .With(p => p.ContestId, contestId)
            .With(p => p.PredictionType, PickType.AgainstTheSpread)
            .With(p => p.WinnerFranchiseSeasonId, homeTeamId) // Always home team
            .With(p => p.WinProbability, 0.55m) // 55% home team covers
            .Create();
        await DataContext.ContestPredictions.AddAsync(prediction);
        await DataContext.SaveChangesAsync();

        var matchup = Fixture.Build<LeagueWeekMatchupsDto.MatchupForPickDto>()
            .With(m => m.ContestId, contestId)
            .With(m => m.HomeFranchiseSeasonId, homeTeamId)
            .With(m => m.AwayFranchiseSeasonId, awayTeamId)
            .With(m => m.SpreadCurrent, -3.5m) // Small spread
            .Create();

        var matchupsDto = Fixture.Build<LeagueWeekMatchupsDto>()
            .With(m => m.Matchups, new List<LeagueWeekMatchupsDto.MatchupForPickDto> { matchup })
            .Create();

        _mockGetLeagueWeekMatchupsHandler
            .Setup(x => x.ExecuteAsync(It.Is<GetLeagueWeekMatchupsQuery>(q =>
                q.UserId == syntheticId && q.LeagueId == pickemGroupId && q.Week == 14), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<LeagueWeekMatchupsDto>(matchupsDto));

        _mockPickStyleProvider
            .Setup(p => p.GetRequiredConfidence("moderate", 3.5))
            .Returns(0.50); // Requires only 50% for small spreads

        // Act
        await _sut.GenerateMetricBasedPicksForSynthetic(
            pickemGroupId,
            PickType.AgainstTheSpread,
            syntheticId,
            "moderate",
            14);

        // Assert
        var picks = await DataContext.UserPicks.ToListAsync();
        picks.Should().HaveCount(1);
        picks[0].FranchiseId.Should().Be(homeTeamId); // Picks favorite (55% >= 50%)
    }
}
