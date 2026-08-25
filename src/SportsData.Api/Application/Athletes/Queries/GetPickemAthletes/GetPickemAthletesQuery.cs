namespace SportsData.Api.Application.Athletes.Queries.GetPickemAthletes;

public record GetPickemAthletesQuery(
    string Sport,
    string League,
    string Position,
    int SeasonYear,
    int Week);
