namespace SportsData.Producer.Application.Competitions.Queries.GetCompetitionsWithoutCompetitors;

/// <summary>
/// Data-integrity check: competitions on this pod's sport whose competitor
/// count is not 2 (missing, partial or duplicated CompetitionCompetitor
/// rows). API fans this out across Producers for the admin diagnostics page.
/// </summary>
public record GetCompetitionsWithoutCompetitorsQuery;
