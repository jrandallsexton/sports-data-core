using SportsData.Core.Eventing.Events.Picks;

namespace SportsData.Notification.Application.Dispatching;

/// <summary>
/// What actually happened to a scored pick. This is the resolution slot a
/// phrase is chosen from — it's what makes the copy pick-aware rather than a
/// generic "you lost".
///
/// <para>
/// Ordering here is meaningless; precedence lives in
/// <see cref="PickSituationResolver"/> and runs most-specific-first.
/// </para>
/// </summary>
public enum PickSituation
{
    // ─── Losses ───────────────────────────────────────────────────────────
    GenericLoss = 0,

    /// <summary>Picked side was shut out.</summary>
    ShutoutLoss = 1,

    /// <summary>Lost by three scores or more.</summary>
    BlowoutLoss = 2,

    /// <summary>Took a double-digit underdog and it lost.</summary>
    BigDogLoss = 3,

    /// <summary>Took a double-digit favourite and it lost anyway.</summary>
    FavoriteChoked = 4,

    /// <summary>Lost by a field goal or less.</summary>
    SqueakerLoss = 5,

    /// <summary>ATS only: missed the cover by a point or less.</summary>
    NarrowMissAts = 6,

    /// <summary>
    /// ATS only: the picked side WON the game but failed to cover. The
    /// scoreboard says victory, the pick says loss — copy must not call this
    /// a defeat.
    /// </summary>
    WonButDidNotCover = 7,

    // ─── Wins ─────────────────────────────────────────────────────────────
    GenericWin = 100,

    /// <summary>Took a double-digit underdog and it won.</summary>
    DogWin = 101,

    /// <summary>Took a big favourite and it duly won.</summary>
    ChalkWin = 102,

    /// <summary>Won by three scores or more.</summary>
    BlowoutWin = 103,

    /// <summary>Won by a field goal or less.</summary>
    UglyWin = 104,

    /// <summary>
    /// ATS only: the pick cashed even though the picked side LOST the game.
    /// Copy must not congratulate them on a victory that didn't happen.
    /// </summary>
    CoveredInDefeat = 105
}

/// <summary>
/// Maps a scored pick onto a <see cref="PickSituation"/> using only the facts
/// already on <see cref="UserPickScored"/> — no lookups, no service calls.
///
/// <para>
/// Every branch terminates in a generic bucket, so resolution is total: the
/// engine can never fail to produce a slot, and therefore never fails to
/// produce copy.
/// </para>
///
/// <para>
/// SPREAD AVAILABILITY: the spread-dependent situations need the picked
/// side's line. Today <c>PickedSpread</c> is populated only for
/// AgainstTheSpread leagues (PickScoringProcessor gates on PickType for
/// display reasons), so a straight-up pick on a 14-point dog currently falls
/// through to the generic buckets. A future <c>MarketSpread</c> on the event
/// closes that gap; this resolver reads whichever is present, so it needs no
/// change when that lands. Degraded, never broken.
/// </para>
/// </summary>
public static class PickSituationResolver
{
    // Football-shaped thresholds, in one place so they're tunable together.
    private const int BlowoutMargin = 21;   // three scores
    private const int SqueakerMargin = 3;   // one field goal
    private const double BigLine = 10.0;    // "double-digit" dog or favourite
    private const double ChalkLine = 14.0;  // two scores — properly heavy chalk
    private const double NarrowAtsMiss = 1.0;

    public static PickSituation Resolve(UserPickScored msg)
    {
        // Without a resolved side there's no picked/opponent score to reason
        // about (Over/Under picks, or an unresolvable match).
        if (msg.PickedIsHome is not { } pickedIsHome)
            return msg.IsCorrect == true ? PickSituation.GenericWin : PickSituation.GenericLoss;

        var pickedScore = pickedIsHome ? msg.HomeScore : msg.AwayScore;
        var opponentScore = pickedIsHome ? msg.AwayScore : msg.HomeScore;
        var margin = pickedScore - opponentScore;

        // Negative = favoured, positive = underdog, from the picked side's
        // perspective. Null when the league is straight-up (see class doc).
        var line = msg.PickedSpread;

        return msg.IsCorrect == true
            ? ResolveWin(margin, line)
            : ResolveLoss(margin, pickedScore, line);
    }

    private static PickSituation ResolveLoss(int margin, int pickedScore, double? line)
    {
        // Most-specific first. Humiliation outranks arithmetic: being shut out
        // is the story even if the margin also qualifies as a blowout.
        // Guarded on an actual defeat so a 0-0 tie can't read as a shutout.
        if (pickedScore == 0 && margin < 0)
            return PickSituation.ShutoutLoss;

        // An ATS near-miss is a sharper sting than any margin bucket — the
        // pick was one point from cashing.
        if (line is { } spread && Math.Abs(margin + spread) <= NarrowAtsMiss)
            return PickSituation.NarrowMissAts;

        // SCOREBOARD vs PICK. IsCorrect means the pick COVERED, not that the
        // picked side won, and in an ATS league those diverge: a -7 favourite
        // winning by 3 loses the pick while winning the game. Every bucket
        // below is phrased around defeat, so a positive margin must divert
        // here or the copy would call a victory a loss.
        if (margin > 0)
            return PickSituation.WonButDidNotCover;

        if (line is { } dogLine && dogLine >= BigLine)
            return PickSituation.BigDogLoss;

        if (line is { } favLine && favLine <= -BigLine)
            return PickSituation.FavoriteChoked;

        if (margin <= -BlowoutMargin)
            return PickSituation.BlowoutLoss;

        if (margin >= -SqueakerMargin)
            return PickSituation.SqueakerLoss;

        return PickSituation.GenericLoss;
    }

    private static PickSituation ResolveWin(int margin, double? line)
    {
        // Mirror of the divergence above: a +14 dog losing 24-20 CASHES the
        // pick while losing the game. Diverted first so no win bucket can
        // congratulate a team that lost.
        if (margin <= 0)
            return PickSituation.CoveredInDefeat;

        // Beating a big number is the story regardless of how it looked.
        if (line is { } dogLine && dogLine >= BigLine)
            return PickSituation.DogWin;

        // Heavy chalk winning is the least impressive outcome in the product,
        // and it's checked before margin so a 30-point favourite blowing
        // someone out still reads as chalk rather than as a statement win.
        if (line is { } favLine && favLine <= -ChalkLine)
            return PickSituation.ChalkWin;

        if (margin >= BlowoutMargin)
            return PickSituation.BlowoutWin;

        if (margin <= SqueakerMargin)
            return PickSituation.UglyWin;

        return PickSituation.GenericWin;
    }
}
