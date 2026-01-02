using AutoFixture;
using FluentAssertions;
using SportsData.Api.Application;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Api.Application.Common.Enums;
using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Scoring;

public class PickScoringServiceTests : ApiTestBase<PickScoringService>
{
    private readonly PickScoringService _sut = new(); // no dependencies

    #region StraightUp Tests

    [Fact]
    public void ScorePick_StraightUp_CorrectPick_SetsCorrectTrueAndAwardsPoint()
    {
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.PickType, PickType.StraightUp)
            .With(g => g.UseConfidencePoints, false)
            .Create();

        var matchup = Fixture.Create<PickemGroupMatchup>();

        var winnerId = Guid.NewGuid();
        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.FranchiseId, winnerId)
            .Create();

        var result = Fixture.Build<MatchupResult>()
            .With(r => r.WinnerFranchiseSeasonId, winnerId)
            .Create();

        _sut.ScorePick(group, matchup.HomeSpread, pick, result);

        pick.IsCorrect.Should().BeTrue();
        pick.PointsAwarded.Should().Be(1);
        pick.ScoredAt.Should().NotBeNull();
        pick.WasAgainstSpread.Should().BeFalse();
    }

    [Fact]
    public void ScorePick_StraightUp_IncorrectPick_SetsCorrectFalseAndAwardsZero()
    {
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.PickType, PickType.StraightUp)
            .With(g => g.UseConfidencePoints, false)
            .Create();

        var matchup = Fixture.Create<PickemGroupMatchup>();

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.FranchiseId, Guid.NewGuid())
            .Create();

        var result = Fixture.Build<MatchupResult>()
            .With(r => r.WinnerFranchiseSeasonId, Guid.NewGuid())
            .Create();

        _sut.ScorePick(group, matchup.HomeSpread, pick, result);

        pick.IsCorrect.Should().BeFalse();
        pick.PointsAwarded.Should().Be(0);
        pick.ScoredAt.Should().NotBeNull();
        pick.WasAgainstSpread.Should().BeFalse();
    }

    [Fact]
    public void ScorePick_StraightUp_WithConfidencePoints_CorrectPick_AwardsConfidencePoints()
    {
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.PickType, PickType.StraightUp)
            .With(g => g.UseConfidencePoints, true)
            .Create();

        var winnerId = Guid.NewGuid();
        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.FranchiseId, winnerId)
            .With(p => p.ConfidencePoints, 10)
            .Create();

        var result = Fixture.Build<MatchupResult>()
            .With(r => r.WinnerFranchiseSeasonId, winnerId)
            .Create();

        _sut.ScorePick(group, null, pick, result);

        pick.IsCorrect.Should().BeTrue();
        pick.PointsAwarded.Should().Be(10);
        pick.ScoredAt.Should().NotBeNull();
        pick.WasAgainstSpread.Should().BeFalse();
    }

    [Fact]
    public void ScorePick_StraightUp_WithConfidencePoints_IncorrectPick_AwardsZero()
    {
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.PickType, PickType.StraightUp)
            .With(g => g.UseConfidencePoints, true)
            .Create();

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.FranchiseId, Guid.NewGuid())
            .With(p => p.ConfidencePoints, 10)
            .Create();

        var result = Fixture.Build<MatchupResult>()
            .With(r => r.WinnerFranchiseSeasonId, Guid.NewGuid())
            .Create();

        _sut.ScorePick(group, null, pick, result);

        pick.IsCorrect.Should().BeFalse();
        pick.PointsAwarded.Should().Be(0);
        pick.ScoredAt.Should().NotBeNull();
        pick.WasAgainstSpread.Should().BeFalse();
    }

    [Fact]
    public void ScorePick_StraightUp_WithConfidencePoints_NullConfidence_AwardsZero()
    {
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.PickType, PickType.StraightUp)
            .With(g => g.UseConfidencePoints, true)
            .Create();

        var winnerId = Guid.NewGuid();
        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.FranchiseId, winnerId)
            .With(p => p.ConfidencePoints, (int?)null)
            .Create();

        var result = Fixture.Build<MatchupResult>()
            .With(r => r.WinnerFranchiseSeasonId, winnerId)
            .Create();

        _sut.ScorePick(group, null, pick, result);

        pick.IsCorrect.Should().BeTrue();
        pick.PointsAwarded.Should().Be(0);
        pick.ScoredAt.Should().NotBeNull();
    }

    #endregion

    #region AgainstTheSpread Tests

    [Fact]
    public void ScorePick_AgainstTheSpread_HomeFavorite_CoversSpread_CorrectPick()
    {
        // Home favored by 7 (spread = -7), Home wins 28-17 (covers by 4)
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.PickType, PickType.AgainstTheSpread)
            .With(g => g.UseConfidencePoints, false)
            .Create();

        var homeFranchiseSeasonId = Guid.NewGuid();
        var awayFranchiseSeasonId = Guid.NewGuid();

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.FranchiseId, homeFranchiseSeasonId)
            .Create();

        var result = Fixture.Build<MatchupResult>()
            .With(r => r.HomeFranchiseSeasonId, homeFranchiseSeasonId)
            .With(r => r.AwayFranchiseSeasonId, awayFranchiseSeasonId)
            .With(r => r.HomeScore, 28)
            .With(r => r.AwayScore, 17)
            .With(r => r.Spread, -7.0)
            .Create();

        _sut.ScorePick(group, result.Spread, pick, result);

        pick.IsCorrect.Should().BeTrue();
        pick.PointsAwarded.Should().Be(1);
        pick.ScoredAt.Should().NotBeNull();
        pick.WasAgainstSpread.Should().BeTrue();
    }

    [Fact]
    public void ScorePick_AgainstTheSpread_HomeFavorite_DoesNotCoverSpread_IncorrectPick()
    {
        // Home favored by 7 (spread = -7), Home wins 24-21 (doesn't cover)
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.PickType, PickType.AgainstTheSpread)
            .With(g => g.UseConfidencePoints, false)
            .Create();

        var homeFranchiseSeasonId = Guid.NewGuid();
        var awayFranchiseSeasonId = Guid.NewGuid();

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.FranchiseId, homeFranchiseSeasonId)
            .Create();

        var result = Fixture.Build<MatchupResult>()
            .With(r => r.HomeFranchiseSeasonId, homeFranchiseSeasonId)
            .With(r => r.AwayFranchiseSeasonId, awayFranchiseSeasonId)
            .With(r => r.HomeScore, 24)
            .With(r => r.AwayScore, 21)
            .With(r => r.Spread, -7.0)
            .Create();

        _sut.ScorePick(group, result.Spread, pick, result);

        pick.IsCorrect.Should().BeFalse();
        pick.PointsAwarded.Should().Be(0);
        pick.ScoredAt.Should().NotBeNull();
        pick.WasAgainstSpread.Should().BeTrue();
    }

    [Fact]
    public void ScorePick_AgainstTheSpread_AwayFavorite_CoversSpread_CorrectPick()
    {
        // Away favored by 3.5 (spread = 3.5), Away wins 27-20 (covers)
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.PickType, PickType.AgainstTheSpread)
            .With(g => g.UseConfidencePoints, false)
            .Create();

        var homeFranchiseSeasonId = Guid.NewGuid();
        var awayFranchiseSeasonId = Guid.NewGuid();

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.FranchiseId, awayFranchiseSeasonId)
            .Create();

        var result = Fixture.Build<MatchupResult>()
            .With(r => r.HomeFranchiseSeasonId, homeFranchiseSeasonId)
            .With(r => r.AwayFranchiseSeasonId, awayFranchiseSeasonId)
            .With(r => r.HomeScore, 20)
            .With(r => r.AwayScore, 27)
            .With(r => r.Spread, 3.5)
            .Create();

        _sut.ScorePick(group, result.Spread, pick, result);

        pick.IsCorrect.Should().BeTrue();
        pick.PointsAwarded.Should().Be(1);
        pick.ScoredAt.Should().NotBeNull();
        pick.WasAgainstSpread.Should().BeTrue();
    }

    [Fact]
    public void ScorePick_AgainstTheSpread_AwayFavorite_DoesNotCoverSpread_IncorrectPick()
    {
        // Away favored by 10 (spread = 10), Away wins 24-17 (doesn't cover)
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.PickType, PickType.AgainstTheSpread)
            .With(g => g.UseConfidencePoints, false)
            .Create();

        var homeFranchiseSeasonId = Guid.NewGuid();
        var awayFranchiseSeasonId = Guid.NewGuid();

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.FranchiseId, awayFranchiseSeasonId)
            .Create();

        var result = Fixture.Build<MatchupResult>()
            .With(r => r.HomeFranchiseSeasonId, homeFranchiseSeasonId)
            .With(r => r.AwayFranchiseSeasonId, awayFranchiseSeasonId)
            .With(r => r.HomeScore, 17)
            .With(r => r.AwayScore, 24)
            .With(r => r.Spread, 10.0)
            .Create();

        _sut.ScorePick(group, result.Spread, pick, result);

        pick.IsCorrect.Should().BeFalse();
        pick.PointsAwarded.Should().Be(0);
        pick.ScoredAt.Should().NotBeNull();
        pick.WasAgainstSpread.Should().BeTrue();
    }

    [Fact]
    public void ScorePick_AgainstTheSpread_Push_HomeFavorite_PickIsIncorrect()
    {
        // Home favored by 7 (spread = -7), Home wins 24-17 exactly (push)
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.PickType, PickType.AgainstTheSpread)
            .With(g => g.UseConfidencePoints, false)
            .Create();

        var homeFranchiseSeasonId = Guid.NewGuid();
        var awayFranchiseSeasonId = Guid.NewGuid();

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.FranchiseId, homeFranchiseSeasonId)
            .Create();

        var result = Fixture.Build<MatchupResult>()
            .With(r => r.HomeFranchiseSeasonId, homeFranchiseSeasonId)
            .With(r => r.AwayFranchiseSeasonId, awayFranchiseSeasonId)
            .With(r => r.HomeScore, 24)
            .With(r => r.AwayScore, 17)
            .With(r => r.Spread, -7.0)
            .Create();

        _sut.ScorePick(group, result.Spread, pick, result);

        pick.IsCorrect.Should().BeFalse("a push should result in IsCorrect = false");
        pick.PointsAwarded.Should().Be(0);
        pick.ScoredAt.Should().NotBeNull();
        pick.WasAgainstSpread.Should().BeTrue();
    }

    [Fact]
    public void ScorePick_AgainstTheSpread_Push_AwayFavorite_PickIsIncorrect()
    {
        // Away favored by 6 (spread = 6), Away wins 23-17 exactly (push)
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.PickType, PickType.AgainstTheSpread)
            .With(g => g.UseConfidencePoints, false)
            .Create();

        var homeFranchiseSeasonId = Guid.NewGuid();
        var awayFranchiseSeasonId = Guid.NewGuid();

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.FranchiseId, awayFranchiseSeasonId)
            .Create();

        var result = Fixture.Build<MatchupResult>()
            .With(r => r.HomeFranchiseSeasonId, homeFranchiseSeasonId)
            .With(r => r.AwayFranchiseSeasonId, awayFranchiseSeasonId)
            .With(r => r.HomeScore, 17)
            .With(r => r.AwayScore, 23)
            .With(r => r.Spread, 6.0)
            .Create();

        _sut.ScorePick(group, result.Spread, pick, result);

        pick.IsCorrect.Should().BeFalse("a push should result in IsCorrect = false");
        pick.PointsAwarded.Should().Be(0);
        pick.ScoredAt.Should().NotBeNull();
        pick.WasAgainstSpread.Should().BeTrue();
    }

    [Fact]
    public void ScorePick_AgainstTheSpread_WithConfidencePoints_CorrectPick_AwardsConfidencePoints()
    {
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.PickType, PickType.AgainstTheSpread)
            .With(g => g.UseConfidencePoints, true)
            .Create();

        var homeFranchiseSeasonId = Guid.NewGuid();
        var awayFranchiseSeasonId = Guid.NewGuid();

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.FranchiseId, homeFranchiseSeasonId)
            .With(p => p.ConfidencePoints, 12)
            .Create();

        var result = Fixture.Build<MatchupResult>()
            .With(r => r.HomeFranchiseSeasonId, homeFranchiseSeasonId)
            .With(r => r.AwayFranchiseSeasonId, awayFranchiseSeasonId)
            .With(r => r.HomeScore, 28)
            .With(r => r.AwayScore, 14)
            .With(r => r.Spread, -7.0)
            .Create();

        _sut.ScorePick(group, result.Spread, pick, result);

        pick.IsCorrect.Should().BeTrue();
        pick.PointsAwarded.Should().Be(12);
        pick.ScoredAt.Should().NotBeNull();
        pick.WasAgainstSpread.Should().BeTrue();
    }

    [Fact]
    public void ScorePick_AgainstTheSpread_WithConfidencePoints_IncorrectPick_AwardsZero()
    {
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.PickType, PickType.AgainstTheSpread)
            .With(g => g.UseConfidencePoints, true)
            .Create();

        var homeFranchiseSeasonId = Guid.NewGuid();
        var awayFranchiseSeasonId = Guid.NewGuid();

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.FranchiseId, homeFranchiseSeasonId)
            .With(p => p.ConfidencePoints, 12)
            .Create();

        var result = Fixture.Build<MatchupResult>()
            .With(r => r.HomeFranchiseSeasonId, homeFranchiseSeasonId)
            .With(r => r.AwayFranchiseSeasonId, awayFranchiseSeasonId)
            .With(r => r.HomeScore, 21)
            .With(r => r.AwayScore, 24)
            .With(r => r.Spread, -7.0)
            .Create();

        _sut.ScorePick(group, result.Spread, pick, result);

        pick.IsCorrect.Should().BeFalse();
        pick.PointsAwarded.Should().Be(0);
        pick.ScoredAt.Should().NotBeNull();
        pick.WasAgainstSpread.Should().BeTrue();
    }

    [Fact]
    public void ScorePick_AgainstTheSpread_NoSpreadProvided_FallbacksToStraightUp()
    {
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.PickType, PickType.AgainstTheSpread)
            .With(g => g.UseConfidencePoints, false)
            .Create();

        var winnerId = Guid.NewGuid();

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.FranchiseId, winnerId)
            .Create();

        var result = Fixture.Build<MatchupResult>()
            .With(r => r.WinnerFranchiseSeasonId, winnerId)
            .With(r => r.Spread, (double?)null)
            .Create();

        _sut.ScorePick(group, null, pick, result);

        pick.IsCorrect.Should().BeTrue();
        pick.PointsAwarded.Should().Be(1);
        pick.ScoredAt.Should().NotBeNull();
        pick.WasAgainstSpread.Should().BeTrue();
    }

    [Fact]
    public void ScorePick_AgainstTheSpread_ZeroSpread_FallbacksToStraightUp()
    {
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.PickType, PickType.AgainstTheSpread)
            .With(g => g.UseConfidencePoints, false)
            .Create();

        var winnerId = Guid.NewGuid();

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.FranchiseId, winnerId)
            .Create();

        var result = Fixture.Build<MatchupResult>()
            .With(r => r.WinnerFranchiseSeasonId, winnerId)
            .With(r => r.Spread, 0.0)
            .Create();

        _sut.ScorePick(group, 0.0, pick, result);

        pick.IsCorrect.Should().BeTrue();
        pick.PointsAwarded.Should().Be(1);
        pick.ScoredAt.Should().NotBeNull();
        pick.WasAgainstSpread.Should().BeTrue();
    }

    #endregion

    #region General Tests

    [Fact]
    public void ScorePick_MissingFranchiseId_SetsIncorrect()
    {
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.PickType, PickType.StraightUp)
            .Create();

        var matchup = Fixture.Create<PickemGroupMatchup>();

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.FranchiseId, (Guid?)null)
            .Create();

        var result = Fixture.Create<MatchupResult>();

        _sut.ScorePick(group, matchup.HomeSpread, pick, result);

        pick.IsCorrect.Should().BeFalse();
        pick.PointsAwarded.Should().Be(0);
        pick.ScoredAt.Should().NotBeNull();
    }

    [Fact]
    public void ScorePick_OverUnder_DoesNothing()
    {
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.PickType, PickType.OverUnder)
            .Create();

        var matchup = Fixture.Create<PickemGroupMatchup>();

        var pick = Fixture.Create<PickemGroupUserPick>();
        var result = Fixture.Create<MatchupResult>();

        _sut.Invoking(s => s.ScorePick(group, matchup.HomeSpread, pick, result))
            .Should().NotThrow();
    }

    [Fact]
    public void ScorePick_UnknownPickType_Throws()
    {
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.PickType, (PickType)999)
            .Create();

        var matchup = Fixture.Create<PickemGroupMatchup>();

        var pick = Fixture.Create<PickemGroupUserPick>();
        var result = Fixture.Create<MatchupResult>();

        _sut.Invoking(s => s.ScorePick(group, matchup.HomeSpread, pick, result))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Unsupported PickType*");
    }

    #endregion
}
