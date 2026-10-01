using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Scoring;

public class PickScoringServiceSimulatedBetsTests
{
    private static readonly Guid HomeId = Guid.NewGuid();
    private static readonly Guid AwayId = Guid.NewGuid();

    private readonly PickScoringService _sut = new(
        NullLogger<PickScoringService>.Instance,
        new DateTimeProvider());

    private static readonly MatchupPricing Priced = new(
        AwayMoneyLine: 240,
        HomeMoneyLine: -300,
        AwaySpreadPrice: -105,
        HomeSpreadPrice: -115);

    #region StraightUp

    [Fact]
    public void StraightUp_UnderdogWins_PaysNetProfitOnTheMoneyline()
    {
        var pick = Pick(AwayId);

        _sut.ScoreSimulatedBets(Group(PickType.StraightUp), null, pick, Result(home: 17, away: 24), Priced);

        pick.PointsSU.Should().Be(2.40m);
        pick.PointsATS.Should().BeNull();
        pick.PointsOU.Should().BeNull();
    }

    [Fact]
    public void StraightUp_FavoriteWins_PaysNetProfitRoundedToFourPlaces()
    {
        var pick = Pick(HomeId);

        _sut.ScoreSimulatedBets(Group(PickType.StraightUp), null, pick, Result(home: 24, away: 17), Priced);

        pick.PointsSU.Should().Be(0.3333m, "100/300, rounded to the column's scale");
    }

    [Fact]
    public void StraightUp_Loss_IsMinusOne()
    {
        var pick = Pick(HomeId);

        _sut.ScoreSimulatedBets(Group(PickType.StraightUp), null, pick, Result(home: 17, away: 24), Priced);

        pick.PointsSU.Should().Be(-1m);
    }

    [Fact]
    public void StraightUp_Tie_IsZero()
    {
        var pick = Pick(HomeId);
        var result = Result(home: 21, away: 21);
        result.WinnerFranchiseSeasonId = null;

        _sut.ScoreSimulatedBets(Group(PickType.StraightUp), null, pick, result, Priced);

        pick.PointsSU.Should().Be(0m);
    }

    [Fact]
    public void None_IsScoredAsStraightUp()
    {
        var pick = Pick(AwayId);

        _sut.ScoreSimulatedBets(Group(PickType.None), null, pick, Result(home: 17, away: 24), Priced);

        pick.PointsSU.Should().Be(2.40m);
    }

    [Fact]
    public void StraightUp_NoMoneyline_IsNull()
    {
        var pick = Pick(AwayId);

        _sut.ScoreSimulatedBets(
            Group(PickType.StraightUp), null, pick, Result(home: 17, away: 24),
            Priced with { AwayMoneyLine = null });

        pick.PointsSU.Should().BeNull();
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(0)]
    [InlineData(99)]
    public void StraightUp_ValueThatIsNotAnAmericanPrice_IsNull(int moneyLine)
    {
        var pick = Pick(AwayId);

        _sut.ScoreSimulatedBets(
            Group(PickType.StraightUp), null, pick, Result(home: 17, away: 24),
            Priced with { AwayMoneyLine = moneyLine });

        pick.PointsSU.Should().BeNull();
    }

    [Fact]
    public void StraightUp_EvenMoney_WinsOne()
    {
        var pick = Pick(AwayId);

        _sut.ScoreSimulatedBets(
            Group(PickType.StraightUp), null, pick, Result(home: 17, away: 24),
            Priced with { AwayMoneyLine = 100 });

        pick.PointsSU.Should().Be(1m);
    }

    #endregion

    #region AgainstTheSpread

    [Fact]
    public void AgainstTheSpread_Cover_PaysNetProfitOnTheSpreadPrice()
    {
        // Home -7, wins by 10: covers. Home spread price -115 pays 100/115.
        var pick = Pick(HomeId);

        _sut.ScoreSimulatedBets(Group(PickType.AgainstTheSpread), -7.0, pick, Result(home: 27, away: 17), Priced);

        pick.PointsATS.Should().Be(0.8696m);
        pick.PointsSU.Should().BeNull("only the league's own pick type is simulated");
        pick.PointsOU.Should().BeNull();
    }

    [Fact]
    public void AgainstTheSpread_AwayCover_UsesTheAwayPrice()
    {
        // Home -7, wins by 3: away covers at -105.
        var pick = Pick(AwayId);

        _sut.ScoreSimulatedBets(Group(PickType.AgainstTheSpread), -7.0, pick, Result(home: 20, away: 17), Priced);

        pick.PointsATS.Should().Be(0.9524m);
    }

    [Fact]
    public void AgainstTheSpread_FailsToCover_IsMinusOne()
    {
        // Home won the game but not by 7: the favorite pick loses the bet.
        var pick = Pick(HomeId);

        _sut.ScoreSimulatedBets(Group(PickType.AgainstTheSpread), -7.0, pick, Result(home: 20, away: 17), Priced);

        pick.PointsATS.Should().Be(-1m);
    }

    [Fact]
    public void AgainstTheSpread_Push_IsZero()
    {
        var pick = Pick(HomeId);

        _sut.ScoreSimulatedBets(Group(PickType.AgainstTheSpread), -3.0, pick, Result(home: 27, away: 24), Priced);

        pick.PointsATS.Should().Be(0m);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    public void AgainstTheSpread_NoOrZeroSpread_IsNull(double? spread)
    {
        // Scoring falls back to straight-up here, but no spread bet existed.
        var pick = Pick(HomeId);

        _sut.ScoreSimulatedBets(Group(PickType.AgainstTheSpread), spread, pick, Result(home: 27, away: 17), Priced);

        pick.PointsATS.Should().BeNull();
        pick.PointsSU.Should().BeNull();
    }

    [Fact]
    public void AgainstTheSpread_NoSpreadPrice_IsNull()
    {
        var pick = Pick(HomeId);

        _sut.ScoreSimulatedBets(
            Group(PickType.AgainstTheSpread), -7.0, pick, Result(home: 27, away: 17),
            Priced with { HomeSpreadPrice = null });

        pick.PointsATS.Should().BeNull();
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(1e30)]
    public void AgainstTheSpread_NonFiniteOrOutOfRangePrice_IsNull_NotAThrow(double price)
    {
        var pick = Pick(HomeId);

        _sut.ScoreSimulatedBets(
            Group(PickType.AgainstTheSpread), -7.0, pick, Result(home: 27, away: 17),
            Priced with { HomeSpreadPrice = price });

        pick.PointsATS.Should().BeNull();
    }

    #endregion

    #region Common

    [Fact]
    public void OverUnder_LeavesEveryColumnNull()
    {
        var pick = Pick(HomeId);

        _sut.ScoreSimulatedBets(Group(PickType.OverUnder), -7.0, pick, Result(home: 27, away: 17), Priced);

        pick.PointsSU.Should().BeNull();
        pick.PointsATS.Should().BeNull();
        pick.PointsOU.Should().BeNull();
    }

    [Fact]
    public void NoTeamPicked_IsNull()
    {
        var pick = Pick(null);

        _sut.ScoreSimulatedBets(Group(PickType.StraightUp), null, pick, Result(home: 27, away: 17), Priced);

        pick.PointsSU.Should().BeNull();
    }

    [Fact]
    public void NoPricing_IsNull()
    {
        var pick = Pick(HomeId);

        _sut.ScoreSimulatedBets(Group(PickType.StraightUp), null, pick, Result(home: 27, away: 17), null);

        pick.PointsSU.Should().BeNull();
    }

    [Fact]
    public void ClearsStaleColumnsThatNoLongerApply()
    {
        // e.g. the league switched pick type and the pick was re-scored.
        var pick = Pick(HomeId);
        pick.PointsATS = 0.9091m;
        pick.PointsOU = -1m;

        _sut.ScoreSimulatedBets(Group(PickType.StraightUp), -7.0, pick, Result(home: 27, away: 17), Priced);

        pick.PointsSU.Should().Be(0.3333m);
        pick.PointsATS.Should().BeNull();
        pick.PointsOU.Should().BeNull();
    }

    [Fact]
    public void UnfinalizedResult_LeavesThePickUnchanged()
    {
        var pick = Pick(HomeId);
        pick.PointsSU = 0.5m;
        var result = Result(home: 27, away: 17);
        result.FinalizedUtc = null;

        _sut.ScoreSimulatedBets(Group(PickType.StraightUp), null, pick, result, Priced);

        pick.PointsSU.Should().Be(0.5m);
    }

    [Fact]
    public void DoesNotTouchLeagueScoring()
    {
        var pick = Pick(HomeId);
        pick.IsCorrect = false;
        pick.PointsAwarded = 0;
        pick.ScoredAt = null;

        _sut.ScoreSimulatedBets(Group(PickType.StraightUp), null, pick, Result(home: 27, away: 17), Priced);

        pick.IsCorrect.Should().BeFalse();
        pick.PointsAwarded.Should().Be(0);
        pick.ScoredAt.Should().BeNull();
    }

    #endregion

    private static PickemGroup Group(PickType pickType) => new()
    {
        Id = Guid.NewGuid(),
        Name = "test",
        Sport = Sport.FootballNcaa,
        League = League.NCAAF,
        PickType = pickType
    };

    private static PickemGroupUserPick Pick(Guid? franchiseSeasonId) => new()
    {
        Id = Guid.NewGuid(),
        FranchiseSeasonId = franchiseSeasonId
    };

    private static MatchupResult Result(int home, int away) => new()
    {
        ContestId = Guid.NewGuid(),
        HomeFranchiseSeasonId = HomeId,
        AwayFranchiseSeasonId = AwayId,
        HomeScore = home,
        AwayScore = away,
        WinnerFranchiseSeasonId = home > away ? HomeId : away > home ? AwayId : null,
        FinalizedUtc = new DateTime(2026, 9, 27, 3, 0, 0, DateTimeKind.Utc)
    };
}
