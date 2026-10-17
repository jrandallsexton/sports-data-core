namespace SportsData.Api.Application.Diagnostics.Queries.GetCompetitionsWithoutPlays;

/// <param name="SeasonYear">Null = each Producer's latest season with a started contest.</param>
public record GetCompetitionsWithoutPlaysQuery(int? SeasonYear = null);
