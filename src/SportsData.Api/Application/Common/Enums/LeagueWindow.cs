namespace SportsData.Api.Application.Common.Enums;

/// <summary>
/// The shape of a league's play window, captured EXPLICITLY at creation
/// rather than inferred from StartsOn/EndsOn null-ness. Inference works today
/// only by accident: WeekRange is unwired in the UI, so null/null can only
/// mean FullSeason — but once WeekRange ships as week-to-date translation it
/// becomes indistinguishable from DateRange in the columns, and "the user
/// chose weeks" is unreconstructible. LeagueJoinExpiryCalculator branches on
/// this; future window-specific rules (e.g. WeekRange-aware drop-week
/// defaults) depend on it being captured now.
/// </summary>
public enum LeagueWindow
{
    /// <summary>Whole season; StartsOn/EndsOn both null.</summary>
    FullSeason = 0,

    /// <summary>
    /// Commissioner picked season weeks. Not yet wired in the create UI
    /// (week-to-date translation is a BE follow-up) — captured for when it
    /// is.
    /// </summary>
    WeekRange = 1,

    /// <summary>Commissioner picked explicit dates; EndsOn is authored.</summary>
    DateRange = 2
}
