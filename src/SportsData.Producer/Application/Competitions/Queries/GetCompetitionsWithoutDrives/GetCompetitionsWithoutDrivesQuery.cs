namespace SportsData.Producer.Application.Competitions.Queries.GetCompetitionsWithoutDrives;

/// <summary>
/// Data-coverage check (football only): started competitions that have
/// plays, but none linked to a drive. See <see cref="ICompetitionDiagnosticsScope"/>.
/// </summary>
/// <param name="SeasonYear">Null = the latest season with a started contest.</param>
public record GetCompetitionsWithoutDrivesQuery(int? SeasonYear);
