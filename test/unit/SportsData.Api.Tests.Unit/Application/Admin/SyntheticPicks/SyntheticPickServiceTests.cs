using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Api.Application;
using SportsData.Api.Application.Admin.SyntheticPicks;
using SportsData.Api.Application.UI.Leagues;
using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Config;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Admin.SyntheticPicks;

public class SyntheticPickServiceTests : ApiTestBase<SyntheticPickService>
{
    private readonly Mock<ISyntheticPickStyleProvider> _mockPickStyleProvider;
    private readonly Mock<ILeagueService> _mockLeagueService;
    private readonly SyntheticPickService _sut;

    public SyntheticPickServiceTests()
    {
        _mockPickStyleProvider = Mocker.GetMock<ISyntheticPickStyleProvider>();
        _mockLeagueService = Mocker.GetMock<ILeagueService>();
        _sut = Mocker.CreateInstance<SyntheticPickService>();
    }

    [Fact]
    public async Task GenerateMetricBasedPicksForSynthetic_NoMatchups_ReturnsEarly()
    {
        // Arrange
        var pickemGroupId = Guid.NewGuid();
        var syntheticId = Guid.NewGuid();
        
        _mockLeagueService
            .Setup(x => x.GetMatchupsForLeagueWeekAsync(syntheticId, pickemGroupId, 14, It.IsAny<CancellationToken>()))
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

        _mockLeagueService
            .Setup(x => x.GetMatchupsForLeagueWeekAsync(syntheticId, pickemGroupId, 14, It.IsAny<CancellationToken>()))
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

        _mockLeagueService
            .Setup(x => x.GetMatchupsForLeagueWeekAsync(syntheticId, pickemGroupId, 14, It.IsAny<CancellationToken>()))
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

        _mockLeagueService
            .Setup(x => x.GetMatchupsForLeagueWeekAsync(syntheticId, pickemGroupId, 14, It.IsAny<CancellationToken>()))
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
    public async Task GenerateMetricBasedPicksForSynthetic_ATSWithLowConfidence_FlipsToUnderdog()
    {
        // Arrange
        var pickemGroupId = Guid.NewGuid();
        var syntheticId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();

        var prediction = Fixture.Build<ContestPrediction>()
            .With(p => p.ContestId, contestId)
            .With(p => p.PredictionType, PickType.AgainstTheSpread)
            .With(p => p.WinnerFranchiseSeasonId, homeTeamId) // Model picks home team
            .With(p => p.WinProbability, 0.60m) // 60% confidence
            .Create();
        await DataContext.ContestPredictions.AddAsync(prediction);
        await DataContext.SaveChangesAsync();

        var matchup = Fixture.Build<LeagueWeekMatchupsDto.MatchupForPickDto>()
            .With(m => m.ContestId, contestId)
            .With(m => m.HomeFranchiseSeasonId, homeTeamId)
            .With(m => m.AwayFranchiseSeasonId, awayTeamId)
            .With(m => m.SpreadCurrent, -21.0m) // Large spread
            .Create();

        var matchupsDto = Fixture.Build<LeagueWeekMatchupsDto>()
            .With(m => m.Matchups, new List<LeagueWeekMatchupsDto.MatchupForPickDto> { matchup })
            .Create();

        _mockLeagueService
            .Setup(x => x.GetMatchupsForLeagueWeekAsync(syntheticId, pickemGroupId, 14, It.IsAny<CancellationToken>()))
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
        picks[0].FranchiseId.Should().Be(awayTeamId); // Flipped to underdog (60% < 80%)
    }
}
