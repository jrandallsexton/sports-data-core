namespace SportsData.Producer.Application.Seasons.Queries.GetCurrentSeason;

/// <summary>
/// Returns the current-or-upcoming <see cref="SportsData.Core.Dtos.Canonical.CurrentSeasonDto"/>
/// for this Producer's sport DB, with its phases. "Current" is resolved from the
/// data (earliest season whose EndDate is still in the future), so it returns the
/// in-progress season during play and the next upcoming season during the
/// off-season — without a calendar heuristic.
/// </summary>
public record GetCurrentSeasonQuery();
