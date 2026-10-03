namespace SportsData.Api.Application.UI.Picks.Advisor;

/// <summary>
/// Tunables for the StatBot advisor. Bound from
/// <c>SportsData.Api:PickAdvisor</c>; every value has a code default so an
/// absent section changes nothing. Kept in one block so "scale by slate
/// size" or a retuned threshold is a one-place change later.
/// See docs/features/statbot-advisor.md.
/// </summary>
public sealed class PickAdvisorOptions
{
    /// <summary>
    /// Below this win/cover probability for the model's side, a game is a
    /// "coin flip" and eligible to be flipped to the other side.
    /// </summary>
    public double CoinFlipThreshold { get; set; } = 0.60;

    /// <summary>How many coin flips the QB Draw level flips at most.</summary>
    public int QbDrawFlipCount { get; set; } = 3;

    /// <summary>
    /// Recommendation thresholds, in units of a typical week's swing for a
    /// slate this size (per-game spread across scored member-weeks × this
    /// week's games): the deficit spread over the league weeks left, below
    /// <see cref="GoalLineMaxUnits"/> recommends Goal-line, below
    /// <see cref="QbDrawMaxUnits"/> QB Draw, anything above Hail Mary. A
    /// non-positive deficit is always Prevent. Only this week's slate is
    /// known; future game counts are never forecast (owner, 2026-09-25).
    /// </summary>
    public double GoalLineMaxUnits { get; set; } = 1.0;

    public double QbDrawMaxUnits { get; set; } = 2.0;

    /// <summary>
    /// When the league has too few scored weeks for a spread, the unit
    /// falls back to this fraction of the leader's points per game.
    /// </summary>
    public double FallbackUnitFraction { get; set; } = 0.15;

    /// <summary>Floor for the fallback unit so a young league cannot divide by ~0.</summary>
    public double MinUnit { get; set; } = 0.05;
}
