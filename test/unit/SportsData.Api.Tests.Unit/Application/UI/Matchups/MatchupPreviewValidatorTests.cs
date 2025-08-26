using FluentAssertions;

using SportsData.Api.Application.UI.Matchups;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Matchups;

public class MatchupPreviewValidatorTests
{
    private readonly Guid _home = Guid.NewGuid();
    private readonly Guid _away = Guid.NewGuid();

    [Theory]
    [InlineData(30, 20, -5, true, true)] // Home wins and covers
    [InlineData(24, 31, -3.5, false, false)] // Away wins and covers
    [InlineData(28, 24, -4, true, null)] // Push
    [InlineData(27, 24, 0, true, true)] // Pick'em
    public void Validator_Should_Pass_For_Valid_Cases(
        int homeScore,
        int awayScore,
        double homeSpread,
        bool predictSUHome,
        bool? predictATSHome)
    {
        var result = MatchupPreviewValidator.Validate(
            homeScore,
            awayScore,
            homeSpread,
            predictedStraightUpWinner: predictSUHome ? _home : _away,
            predictedSpreadWinner: predictATSHome is null ? Guid.Empty : predictATSHome.Value ? _home : _away,
            homeFranchiseSeasonId: _home,
            awayFranchiseSeasonId: _away
        );

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validator_Should_Fail_When_StraightUp_Is_Wrong()
    {
        var result = MatchupPreviewValidator.Validate(
            homeScore: 21,
            awayScore: 27,
            homeSpread: -3,
            predictedStraightUpWinner: _home,
            predictedSpreadWinner: _away,
            homeFranchiseSeasonId: _home,
            awayFranchiseSeasonId: _away
        );

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Straight-up winner is incorrect"));
    }

    [Fact]
    public void Validator_Should_Fail_When_Spread_Winner_Is_Wrong()
    {
        var result = MatchupPreviewValidator.Validate(
            homeScore: 21,
            awayScore: 27,
            homeSpread: -3,
            predictedStraightUpWinner: _away,
            predictedSpreadWinner: _home, // wrong
            homeFranchiseSeasonId: _home,
            awayFranchiseSeasonId: _away
        );

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Spread winner is inconsistent"));
    }

    [Fact]
    public void Validator_Should_Fail_When_Tied_But_StraightUp_Winner_Predicted()
    {
        var result = MatchupPreviewValidator.Validate(
            homeScore: 17,
            awayScore: 17,
            homeSpread: -2,
            predictedStraightUpWinner: _home,
            predictedSpreadWinner: _home,
            homeFranchiseSeasonId: _home,
            awayFranchiseSeasonId: _away
        );

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Game is a tie but a winner was predicted"));
    }

    [Fact]
    public void Validator_Should_Fail_When_Push_But_Spread_Winner_Predicted()
    {
        var result = MatchupPreviewValidator.Validate(
            homeScore: 35,
            awayScore: 31,
            homeSpread: -4,
            predictedStraightUpWinner: _home,
            predictedSpreadWinner: _home, // should be null
            homeFranchiseSeasonId: _home,
            awayFranchiseSeasonId: _away
        );

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Spread prediction should be null"));
    }

    [Fact]
    public void Validator_Should_Fail_When_Both_SU_And_Spread_Wrong()
    {
        var result = MatchupPreviewValidator.Validate(
            homeScore: 24,
            awayScore: 31,
            homeSpread: -2.5,
            predictedStraightUpWinner: _home,
            predictedSpreadWinner: _home,
            homeFranchiseSeasonId: _home,
            awayFranchiseSeasonId: _away
        );

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Straight-up winner is incorrect"));
        result.Errors.Should().Contain(e => e.Contains("Spread winner is inconsistent"));
    }
}
