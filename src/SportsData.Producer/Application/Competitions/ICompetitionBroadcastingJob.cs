using Hangfire;

namespace SportsData.Producer.Application.Competitions;

/// <summary>
/// Sport-neutral marker for the live-competition broadcasting Hangfire job.
/// One implementation per sport is registered in the per-mode DI container
/// (football: FootballCompetitionStreamer; baseball: BaseballCompetitionStreamer).
/// </summary>
public interface ICompetitionBroadcastingJob
{
    /// <summary>
    /// Routed to the Hangfire "daemon" queue so only the Producer Daemon role
    /// (introduced in PR A) picks it up. Worker pods listen on this queue too
    /// during the PR A–C transition (see docs/contest-finalization-reconcile-
    /// backstop.md Step 4) so streamers continue to run even before the Daemon
    /// Deployment is activated; PR D will drop daemon from Worker's queue list
    /// to complete the cutover.
    ///
    /// Hangfire reads [Queue] from the method on the type expression in the
    /// lambda — since enqueue sites use Enqueue&lt;ICompetitionBroadcastingJob&gt;
    /// (rather than the concrete streamer type), this attribute belongs on the
    /// interface, not the implementations.
    /// </summary>
    [Queue("daemon")]
    Task ExecuteAsync(StreamCompetitionCommand command, CancellationToken cancellationToken);
}
