namespace SportsData.Api.Application.UI.Leagues.Dtos;

/// <summary>
/// Distinct calendar dates (US Eastern) that have at least one scheduled game for
/// a sport/league within a requested window. Backs the create-league date
/// picker's blackout-date logic — the FE enables these dates and treats the rest
/// of the range as no-game days.
/// </summary>
public record GameDatesDto(IReadOnlyList<DateOnly> GameDates);
