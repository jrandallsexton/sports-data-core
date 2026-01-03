namespace SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionMetrics;

public record RefreshCompetitionMetricsCommand(int SeasonYear);

public record RefreshCompetitionMetricsResult(
    int SeasonYear,
    int TotalContests,
    int EnqueuedJobs,
    string Message);
