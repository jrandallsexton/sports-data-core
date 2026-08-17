using FluentAssertions;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Picks;
using SportsData.Notification.Application.Dispatching;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Dispatching;

/// <summary>
/// The situation taxonomy is what makes SmackBot's copy pick-aware rather
/// than a generic "you lost", so the precedence between overlapping buckets
/// is the behaviour worth pinning down.
/// See docs/features/smackbot-voice.md.
/// </summary>
public class PickSituationResolverTests
{
    /// <param name="pickedSpread">
    /// Signed from the picked side: negative = favoured, positive = underdog.
    /// Null models a StraightUp league, where the publisher doesn't send it.
    /// </param>
    private static UserPickScored Pick(
        int pickedScore,
        int opponentScore,
        bool isCorrect,
        double? pickedSpread = null,
        bool? pickedIsHome = true,
        double? marketSpread = null)
        => new(
            Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            AwayName: null, HomeName: null,
            AwayAbbreviation: "AWY", HomeAbbreviation: "HOM",
            // Place the scores on whichever side pickedIsHome names, so an
            // away-side case exercises the real away path rather than
            // silently feeding the resolver the opponent's score as the pick.
            AwayScore: pickedIsHome == false ? pickedScore : opponentScore,
            HomeScore: pickedIsHome == false ? opponentScore : pickedScore,
            IsCorrect: isCorrect,
            PickedIsHome: pickedIsHome,
            PickedSpread: pickedSpread,
            // ATS events carry the same line in both fields, mirroring the
            // publisher; straight-up cases pass marketSpread alone.
            MarketSpread: marketSpread ?? pickedSpread,
            LeagueId: Guid.NewGuid(), LeagueName: "Sluggers",
            Sport: Sport.FootballNcaa, SeasonYear: 2026,
            CorrelationId: Guid.NewGuid(), CausationId: Guid.NewGuid());

    // ─── Losses ───────────────────────────────────────────────────────────

    [Fact]
    public void Shutout_OutranksBlowout()
    {
        // Being shut out is the story even when the margin also qualifies as
        // a blowout — humiliation beats arithmetic.
        PickSituationResolver.Resolve(Pick(0, 35, isCorrect: false))
            .Should().Be(PickSituation.ShutoutLoss);
    }

    [Fact]
    public void NarrowAtsMiss_OutranksMarginBuckets()
    {
        // Favoured by 7 and won by 6 — the game was won, the cover was not,
        // by a single point. Sharper than any margin bucket.
        // Adjusted result = margin + spread = 6 + (-7) = -1.
        PickSituationResolver.Resolve(Pick(26, 20, isCorrect: false, pickedSpread: -7))
            .Should().Be(PickSituation.NarrowMissAts);
    }

    [Fact]
    public void BigDogLoss_WhenUnderdogOfTenOrMore()
    {
        // Dog by 14 and buried by 30 — nowhere near the cover, so the
        // big-dog framing is the story.
        PickSituationResolver.Resolve(Pick(3, 33, isCorrect: false, pickedSpread: 14))
            .Should().Be(PickSituation.BigDogLoss);
    }

    [Fact]
    public void NarrowAtsMiss_OutranksBigDogLoss_WhenBothApply()
    {
        // A 14-point dog losing by exactly 14 is a cover missed by nothing.
        // In an ATS league that near-miss is the sharper story than "you took
        // a dog"; in a StraightUp league no spread is sent, so BigDogLoss is
        // reached instead. Pinning the precedence because it isn't obvious.
        PickSituationResolver.Resolve(Pick(10, 24, isCorrect: false, pickedSpread: 14))
            .Should().Be(PickSituation.NarrowMissAts);
    }

    [Fact]
    public void FavoriteChoked_WhenFavouredByTenOrMore()
    {
        PickSituationResolver.Resolve(Pick(17, 20, isCorrect: false, pickedSpread: -13))
            .Should().Be(PickSituation.FavoriteChoked);
    }

    [Fact]
    public void BlowoutLoss_AtThreeScores()
    {
        PickSituationResolver.Resolve(Pick(7, 28, isCorrect: false))
            .Should().Be(PickSituation.BlowoutLoss);
    }

    [Fact]
    public void SqueakerLoss_WithinAFieldGoal()
    {
        PickSituationResolver.Resolve(Pick(21, 24, isCorrect: false))
            .Should().Be(PickSituation.SqueakerLoss);
    }

    [Fact]
    public void GenericLoss_WhenNothingSpecificApplies()
    {
        PickSituationResolver.Resolve(Pick(14, 24, isCorrect: false))
            .Should().Be(PickSituation.GenericLoss);
    }

    // ─── Wins ─────────────────────────────────────────────────────────────

    [Fact]
    public void DogWin_OutranksMargin()
    {
        // Beating a big number is the story regardless of how it looked.
        PickSituationResolver.Resolve(Pick(31, 3, isCorrect: true, pickedSpread: 11))
            .Should().Be(PickSituation.DogWin);
    }

    [Fact]
    public void ChalkWin_OutranksBlowout()
    {
        // A 21-point favourite winning by 28 is still just chalk — the
        // deliberately least impressive outcome in the product.
        PickSituationResolver.Resolve(Pick(35, 7, isCorrect: true, pickedSpread: -21))
            .Should().Be(PickSituation.ChalkWin);
    }

    [Fact]
    public void BlowoutWin_WithoutABigLine()
    {
        PickSituationResolver.Resolve(Pick(35, 7, isCorrect: true))
            .Should().Be(PickSituation.BlowoutWin);
    }

    [Fact]
    public void UglyWin_WithinAFieldGoal()
    {
        PickSituationResolver.Resolve(Pick(24, 21, isCorrect: true))
            .Should().Be(PickSituation.UglyWin);
    }

    [Fact]
    public void GenericWin_WhenNothingSpecificApplies()
    {
        PickSituationResolver.Resolve(Pick(24, 14, isCorrect: true))
            .Should().Be(PickSituation.GenericWin);
    }

    // ─── Degradation ──────────────────────────────────────────────────────

    [Fact]
    public void UnresolvedSide_FallsBackToGeneric()
    {
        // Over/Under picks carry no PickedIsHome, so there's no picked score
        // to reason about. Must still resolve rather than throw.
        PickSituationResolver.Resolve(Pick(0, 0, isCorrect: false, pickedIsHome: null))
            .Should().Be(PickSituation.GenericLoss);
        PickSituationResolver.Resolve(Pick(0, 0, isCorrect: true, pickedIsHome: null))
            .Should().Be(PickSituation.GenericWin);
    }

    [Fact]
    public void StraightUpBigDogLoss_ResolvesViaMarketSpread()
    {
        // THE FLAGSHIP CASE — "took a 14-point dog straight up and lost".
        // PickedSpread is null in a StraightUp league (display semantics);
        // MarketSpread carries the line, and the dog buckets resolve from it.
        // This test previously documented the gap; MarketSpread closed it.
        PickSituationResolver.Resolve(
                Pick(10, 24, isCorrect: false, pickedSpread: null, marketSpread: 14))
            .Should().Be(PickSituation.BigDogLoss);
    }

    [Fact]
    public void StraightUpDogAtExactlyTheLine_IsNotANearMiss()
    {
        // Same 14-point margin as the ATS near-miss case, but scored straight
        // up: there was no cover to miss, so cover-relative situations must
        // never fire from MarketSpread alone.
        PickSituationResolver.Resolve(
                Pick(10, 24, isCorrect: false, pickedSpread: null, marketSpread: 14))
            .Should().NotBe(PickSituation.NarrowMissAts);
    }

    [Fact]
    public void PreMarketSpreadEvent_StillDegradesToAMarginBucket()
    {
        // An event published before MarketSpread existed carries null in both
        // fields and must keep degrading to the margin buckets, not throw.
        PickSituationResolver.Resolve(
                Pick(10, 24, isCorrect: false, pickedSpread: null, marketSpread: null))
            .Should().Be(PickSituation.GenericLoss);
    }

    [Fact]
    public void EveryResolutionIsTotal_NoUnmappedCombination()
    {
        // The engine must never fail to produce a slot, since a missing slot
        // would mean no copy at all.
        for (var picked = 0; picked <= 45; picked += 5)
        for (var opp = 0; opp <= 45; opp += 5)
        foreach (var line in new double?[] { null, -21, -14, -7, 0, 7, 14, 21 })
        foreach (var correct in new[] { true, false })
        {
            var act = () => PickSituationResolver.Resolve(
                Pick(picked, opp, correct, line));
            act.Should().NotThrow();
        }
    }

    // ─── Pick outcome vs scoreboard outcome ───────────────────────────────

    [Fact]
    public void CoveredInDefeat_WhenThePickCashesButTheTeamLost()
    {
        // A +14 dog losing 24-20 COVERS: IsCorrect is true while the margin is
        // negative. Without the divert this resolved to DogWin and the copy
        // would have congratulated a team that lost.
        PickSituationResolver.Resolve(Pick(20, 24, isCorrect: true, pickedSpread: 14))
            .Should().Be(PickSituation.CoveredInDefeat);
    }

    [Fact]
    public void WonButDidNotCover_WhenTheTeamWinsAndThePickDoesNot()
    {
        // Favoured by 14, wins by only 4 — game won, cover missed. Not a
        // narrow miss (10 points short), so it must not land in a defeat
        // bucket that would call a victory a loss.
        PickSituationResolver.Resolve(Pick(24, 20, isCorrect: false, pickedSpread: -14))
            .Should().Be(PickSituation.WonButDidNotCover);
    }

    [Fact]
    public void AwaySidePick_IsScoredFromTheAwayPerspective()
    {
        // Guards the helper fix: with pickedIsHome false the picked score must
        // land on AwayScore, so this is a 28-point away blowout WIN.
        PickSituationResolver.Resolve(
                Pick(35, 7, isCorrect: true, pickedIsHome: false))
            .Should().Be(PickSituation.BlowoutWin);
    }

    [Fact]
    public void ZeroZeroTie_IsNotReportedAsAShutout()
    {
        // 0-0 with the pick not covering: the picked side scored nothing, but
        // calling it a shutout defeat would be wrong — nobody lost.
        PickSituationResolver.Resolve(Pick(0, 0, isCorrect: false))
            .Should().NotBe(PickSituation.ShutoutLoss);
    }
}
