namespace SportsData.Producer.Application.Contests.Queries.GetGameDates;

/// <summary>
/// Distinct calendar dates (US Eastern) that have at least one scheduled game in
/// the [FromUtc, ToUtc] window. Either bound may be null (open-ended). Sport is
/// implicit — the per-sport Producer instance answers for its own sport.
/// </summary>
public record GetGameDatesQuery(DateTime? FromUtc, DateTime? ToUtc);
