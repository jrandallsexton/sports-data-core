namespace SportsData.Api.Application.Athletes.Queries.GetPickemAthletes;

/// <param name="Phase">Week-number phase slug — "preseason" | "regular" | "postseason". Week numbers restart per phase.</param>
public record GetPickemAthletesQuery(
    string Sport,
    string League,
    string Position,
    int SeasonYear,
    int Week,
    string Phase = "regular");
