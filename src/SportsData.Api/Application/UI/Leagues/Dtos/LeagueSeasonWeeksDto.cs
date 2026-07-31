namespace SportsData.Api.Application.UI.Leagues.Dtos;

/// <summary>
/// The season calendar as the create-league form consumes it: every week that
/// can hold games (Off Season excluded), ordered by start date, labeled with
/// its phase where numbering is ambiguous ("Week 4" vs "Preseason - Week 4" —
/// week numbers RESTART per phase; see docs/features/
/// league-join-policy-and-discovery.md, WeekRange section).
/// </summary>
public record LeagueSeasonWeeksDto(int SeasonYear, IReadOnlyList<LeagueSeasonWeekOptionDto> Weeks);

public record LeagueSeasonWeekOptionDto(
    Guid Id,
    int Number,
    string Label,
    string PhaseName,
    DateTime StartDateUtc,
    DateTime EndDateUtc);
