namespace SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionMedia;

public record RefreshAllCompetitionMediaCommand(int SeasonYear);

public record RefreshAllCompetitionMediaResult(
    int SeasonYear,
    int TotalCompetitions,
    int EnqueuedJobs,
    string Message);
