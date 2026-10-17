namespace SportsData.Producer.Application.Competitions.Queries.GetCompetitionsWithoutMetrics;

/// <summary>
/// Data-coverage check: started competitions with no CompetitionMetric rows.
/// See <see cref="ICompetitionDiagnosticsScope"/>.
/// </summary>
/// <param name="SeasonYear">Null = the latest season with a started contest.</param>
public record GetCompetitionsWithoutMetricsQuery(int? SeasonYear);
