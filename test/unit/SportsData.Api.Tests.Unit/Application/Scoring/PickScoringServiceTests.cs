using AutoFixture;

using FluentAssertions;

using SportsData.Api.Application;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Api.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Scoring;

public class PickScoringServiceTests : ApiTestBase<PickScoringService>
{
    private readonly PickScoringService _sut = new(); // no dependencies

    [Fact]
    public void ScorePick_StraightUp_CorrectPick_SetsCorrectTrueAndAwardsPoint()
    {
        // Arrange
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.PickType, PickType.StraightUp)
            .Create();

        var winnerId = Guid.NewGuid();

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.FranchiseId, winnerId)
            .Create();

        var result = Fixture.Build<MatchupResult>()
            .With(r => r.WinnerFranchiseSeasonId, winnerId)
            .Create();

        // Act
        _sut.ScorePick(group, pick, result);

        // Assert
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
            .Create();

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.FranchiseId, Guid.NewGuid())
            .Create();

        var result = Fixture.Build<MatchupResult>()
            .With(r => r.WinnerFranchiseSeasonId, Guid.NewGuid()) // different
            .Create();

        _sut.ScorePick(group, pick, result);

        pick.IsCorrect.Should().BeFalse();
        pick.PointsAwarded.Should().Be(0);
        pick.ScoredAt.Should().NotBeNull();
        pick.WasAgainstSpread.Should().BeFalse();
    }

    [Fact]
    public void ScorePick_AgainstTheSpread_WithSpreadWinner_MatchesCorrectly()
    {
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.PickType, PickType.AgainstTheSpread)
            .Create();

        var spreadWinnerId = Guid.NewGuid();

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.FranchiseId, spreadWinnerId)
            .Create();

        var result = Fixture.Build<MatchupResult>()
            .With(r => r.SpreadWinnerFranchiseSeasonId, spreadWinnerId)
            .Create();

        _sut.ScorePick(group, pick, result);

        pick.IsCorrect.Should().BeTrue();
        pick.PointsAwarded.Should().Be(1);
        pick.ScoredAt.Should().NotBeNull();
        pick.WasAgainstSpread.Should().BeTrue();
    }

    [Fact]
    public void ScorePick_AgainstTheSpread_NoSpreadWinner_FallbacksToStraightUp()
    {
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.PickType, PickType.AgainstTheSpread)
            .Create();

        var winnerId = Guid.NewGuid();

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.FranchiseId, winnerId)
            .Create();

        var result = Fixture.Build<MatchupResult>()
            .With(r => r.SpreadWinnerFranchiseSeasonId, (Guid?)null)
            .With(r => r.WinnerFranchiseSeasonId, winnerId)
            .Create();

        _sut.ScorePick(group, pick, result);

        pick.IsCorrect.Should().BeTrue();
        pick.PointsAwarded.Should().Be(1);
        pick.ScoredAt.Should().NotBeNull();
        pick.WasAgainstSpread.Should().BeTrue();
    }

    [Fact]
    public void ScorePick_MissingFranchiseId_SetsIncorrect()
    {
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.PickType, PickType.StraightUp)
            .Create();

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(p => p.FranchiseId, (Guid?)null)
            .Create();

        var result = Fixture.Create<MatchupResult>();

        _sut.ScorePick(group, pick, result);

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

        var pick = Fixture.Create<PickemGroupUserPick>();
        var result = Fixture.Create<MatchupResult>();

        _sut.Invoking(s => s.ScorePick(group, pick, result))
            .Should().NotThrow();

        // No-op test — add logic later
    }

    [Fact]
    public void ScorePick_UnknownPickType_Throws()
    {
        var group = Fixture.Build<PickemGroup>()
            .With(g => g.PickType, (PickType)999)
            .Create();

        var pick = Fixture.Create<PickemGroupUserPick>();
        var result = Fixture.Create<MatchupResult>();

        _sut.Invoking(s => s.ScorePick(group, pick, result))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Unsupported PickType*");
    }
}
