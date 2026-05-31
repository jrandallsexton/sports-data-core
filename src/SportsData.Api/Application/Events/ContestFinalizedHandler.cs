using MassTransit;

using SportsData.Api.Application.Scoring;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Events
{
    /// <summary>
    /// Closes the live-scoring loop. Producer's sport-specific
    /// ContestEnrichmentProcessor publishes <see cref="ContestFinalized"/>
    /// once the canonical Contest row has been enriched with final scores,
    /// winner, odds results, and FinalizedUtc. This shim enqueues a
    /// <see cref="ScoreContestCommand"/> via Hangfire so the existing
    /// <see cref="ContestScoringProcessor"/> path runs as soon as the
    /// scoreable data is in place.
    ///
    /// Replaces the prior ContestCompletedHandler which fired off
    /// <see cref="ContestCompleted"/> the moment STATUS_FINAL was detected —
    /// well before enrichment had written the values picks scoring depends
    /// on. See docs/contest-finalization-event-restructure.md.
    ///
    /// Per the "ingest consumers must be thin Hangfire-spawn shims"
    /// convention, this consumer does no inline DB work. The processor
    /// handles all actual scoring work, including idempotency via its
    /// already-scored short-circuit — safe against at-least-once redelivery
    /// (broker redelivery, admin replay, pod restart, etc.).
    /// </summary>
    public class ContestFinalizedHandler : IConsumer<ContestFinalized>
    {
        private readonly ILogger<ContestFinalizedHandler> _logger;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public ContestFinalizedHandler(
            ILogger<ContestFinalizedHandler> logger,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public Task Consume(ConsumeContext<ContestFinalized> context)
        {
            var msg = context.Message;

            _logger.LogInformation(
                "ContestFinalized consume: received. ContestId={ContestId}, Sport={Sport}, CausationId={CausationId}, CorrelationId={CorrelationId}, MessageId={MessageId}",
                msg.ContestId, msg.Sport, msg.CausationId, msg.CorrelationId, context.MessageId);

            var cmd = new ScoreContestCommand(msg.ContestId, msg.CorrelationId);
            _backgroundJobProvider.Enqueue<IScoreContests>(p => p.Process(cmd));

            _logger.LogInformation(
                "ContestFinalized consume: ScoreContestCommand enqueued. ContestId={ContestId}, CorrelationId={CorrelationId}",
                msg.ContestId, msg.CorrelationId);

            return Task.CompletedTask;
        }
    }
}
