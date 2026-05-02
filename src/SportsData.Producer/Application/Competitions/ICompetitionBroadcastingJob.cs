namespace SportsData.Producer.Application.Competitions;

/// <summary>
/// Sport-neutral marker for the live-competition broadcasting Hangfire job.
/// One implementation per sport is registered in the per-mode DI container
/// (e.g. football: FootballCompetitionStreamer; baseball: pending).
/// </summary>
public interface ICompetitionBroadcastingJob
{
    Task ExecuteAsync(StreamCompetitionCommand command, CancellationToken cancellationToken);
}
