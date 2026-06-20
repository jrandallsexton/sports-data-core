using MassTransit;

using Microsoft.AspNetCore.SignalR;

using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Events
{
    /// <summary>
    /// Closes the live-scoring loop AND fans out the enriched final-state
    /// update to connected SignalR clients. Producer's sport-specific
    /// ContestEnrichmentProcessor publishes <see cref="ContestFinalized"/>
    /// once the canonical Contest row has been enriched with final scores,
    /// winner, odds results, and FinalizedUtc.
    ///
    /// Two responsibilities:
    ///   1. Enqueue a <see cref="ScorePicksCommand"/> via Hangfire so the
    ///      existing <see cref="PickScoringProcessor"/> path runs as soon
    ///      as the scoreable data is in place.
    ///   2. Broadcast over SignalR so the picks page can flip the matchup
    ///      card from "raw STATUS_FINAL" (no cover line, no SU checkmark)
    ///      to fully enriched without a page refresh. The 30s window
    ///      between STATUS_FINAL and ContestFinalized is exactly the
    ///      window where the card sits half-rendered; this broadcast
    ///      closes it.
    ///
    /// Replaces the prior ContestCompletedHandler which fired off
    /// <see cref="ContestCompleted"/> the moment STATUS_FINAL was detected —
    /// well before enrichment had written the values picks scoring depends
    /// on. See docs/contest-finalization-event-restructure.md.
    ///
    /// Per the "ingest consumers must be thin Hangfire-spawn shims"
    /// convention, the SCORING path does no inline DB work — the
    /// processor handles all actual scoring work, including idempotency
    /// via its already-scored short-circuit (safe against at-least-once
    /// redelivery). The SignalR fan-out is a pure broadcast of the
    /// already-canonical message body, so it's still consistent with the
    /// thin-shim rule.
    /// </summary>
    public class ContestFinalizedHandler : IConsumer<ContestFinalized>
    {
        private readonly ILogger<ContestFinalizedHandler> _logger;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IHubContext<NotificationHub> _hubContext;

        public ContestFinalizedHandler(
            ILogger<ContestFinalizedHandler> logger,
            IProvideBackgroundJobs backgroundJobProvider,
            IHubContext<NotificationHub> hubContext)
        {
            _logger = logger;
            _backgroundJobProvider = backgroundJobProvider;
            _hubContext = hubContext;
        }

        public async Task Consume(ConsumeContext<ContestFinalized> context)
        {
            var msg = context.Message;

            _logger.LogInformation(
                "ContestFinalized consume: received. ContestId={ContestId}, Sport={Sport}, CausationId={CausationId}, CorrelationId={CorrelationId}, MessageId={MessageId}",
                msg.ContestId, msg.Sport, msg.CausationId, msg.CorrelationId, context.MessageId);

            var cmd = new ScorePicksCommand(msg.ContestId, msg.CorrelationId);
            _backgroundJobProvider.Enqueue<IScorePicks>(p => p.Process(cmd));

            _logger.LogInformation(
                "ContestFinalized consume: ScorePicksCommand enqueued. ContestId={ContestId}, CorrelationId={CorrelationId}",
                msg.ContestId, msg.CorrelationId);

            // SignalR broadcast — same Clients.All pattern as the existing
            // ContestStatusChanged / ContestScoreChanged handlers. Web
            // merges the enriched fields into ContestUpdatesContext so
            // GameStatus + FinalScoreResult re-render with the cover line
            // and the SU checkmark.
            await _hubContext.Clients
                .All
                .SendAsync("ContestFinalized", msg, context.CancellationToken);

            _logger.LogInformation(
                "ContestFinalized consume: SignalR Clients.All.SendAsync completed. ContestId={ContestId}, CorrelationId={CorrelationId}, MessageId={MessageId}",
                msg.ContestId, msg.CorrelationId, context.MessageId);
        }
    }
}
