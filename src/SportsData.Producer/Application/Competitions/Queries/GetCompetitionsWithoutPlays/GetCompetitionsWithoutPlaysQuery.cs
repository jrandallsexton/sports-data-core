namespace SportsData.Producer.Application.Competitions.Queries.GetCompetitionsWithoutPlays;

/// <summary>
/// Data-coverage check: started competitions with 10 or fewer plays. See
/// <see cref="ICompetitionDiagnosticsScope"/> for which games are expected.
/// </summary>
/// <param name="SeasonYear">Null = the latest season with a started contest.</param>
public record GetCompetitionsWithoutPlaysQuery(int? SeasonYear);
