using SportsData.Core.Common;

namespace SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionMedia;

public record RefreshAllCompetitionMediaCommand(Sport Sport, int SeasonYear);

public record RefreshAllCompetitionMediaResult(
    Sport Sport,
    int SeasonYear,
    int TotalCompetitions,
    int EnqueuedJobs,
    string Message);
