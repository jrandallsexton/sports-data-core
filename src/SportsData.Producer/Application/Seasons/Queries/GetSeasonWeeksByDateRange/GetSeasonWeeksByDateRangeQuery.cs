namespace SportsData.Producer.Application.Seasons.Queries.GetSeasonWeeksByDateRange;

/// <summary>
/// Returns the <see cref="SportsData.Core.Dtos.Canonical.CanonicalSeasonWeekDto"/>s
/// whose <c>[StartDate, EndDate]</c> overlaps the requested
/// <c>[From, To]</c> range. Used by the API to resolve which
/// <c>SeasonWeek</c>(s) a windowed pick'em league should bootstrap, replacing
/// the prior "always use the current week" assumption that produced orphan
/// empty <c>PickemGroupWeek</c> rows for windowed leagues.
/// </summary>
public record GetSeasonWeeksByDateRangeQuery(DateTime From, DateTime To);
