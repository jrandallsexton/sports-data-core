namespace SportsData.Api.Application.Diagnostics.Queries.GetCompetitionsWithoutMetrics;

/// <param name="SeasonYear">Null = each Producer's latest season with a started contest.</param>
public record GetCompetitionsWithoutMetricsQuery(int? SeasonYear = null);
