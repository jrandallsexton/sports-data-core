using Hangfire;

namespace SportsData.Producer.Application.Competitions.Reconcile;

/// <summary>
/// Recurring backstop that recovers stranded <c>CompetitionStream</c>
/// rows whose streamer process died before publishing <c>ContestCompleted</c>
/// (KEDA scale-down, pod OOM, the 5h MaxStreamDuration cap, etc.).
///
/// For each stranded row, the job re-checks ESPN status directly; on
/// <c>STATUS_FINAL</c>, it publishes <c>ContestCompleted</c> + a
/// <c>DocumentRequested(Event)</c> refresh — mirroring the exact
/// finalization path the streamer would have taken if it had reached
/// the publish points itself. Idempotency on the consumer side absorbs
/// any race where a slow streamer also reaches publish.
///
/// See docs/contest-finalization-reconcile-backstop.md Step 3.
/// </summary>
public interface IFinalizationReconcileJob
{
    /// <summary>
    /// Routed to the Hangfire "daemon" queue so only the Producer Daemon
    /// role picks it up — same routing rationale as
    /// <c>ICompetitionBroadcastingJob</c>. Hangfire reads <c>[Queue]</c>
    /// from the method on the type expression in the lambda, so the
    /// attribute belongs on the interface, not the implementation.
    /// </summary>
    [Queue("daemon")]
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
