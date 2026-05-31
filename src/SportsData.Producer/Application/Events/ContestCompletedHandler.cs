using MassTransit;

using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Contests;

namespace SportsData.Producer.Application.Events
{
    /// <summary>
    /// Producer-side consumer of <see cref="ContestCompleted"/>. Schedules
    /// <see cref="EnrichContestCommand"/> via Hangfire so the sport-specific
    /// <see cref="IEnrichContests"/> processor runs as soon as possible,
    /// rather than waiting on the daily <c>ContestEnrichmentJob</c> cron
    /// backstop.
    ///
    /// Why scheduled (delayed) instead of enqueued immediately:
    /// <see cref="ContestCompleted"/> is published the moment STATUS_FINAL is
    /// detected by Producer's <c>CompetitionStreamerBase</c>. On the same
    /// code path, <c>PublishContestRefreshOnFinalAsync</c> publishes a
    /// <c>DocumentRequested(Event)</c> to re-source the canonical Event/Status
    /// docs — that round-trip is asynchronous (Provider → fetch → publish →
    /// Producer consumer → status processor). The canonical
    /// <c>CompetitionStatus</c> row therefore typically does not show
    /// STATUS_FINAL at the instant Producer receives ContestCompleted.
    ///
    /// The enrichment processor bails cleanly when status isn't FINAL, so an
    /// immediate enqueue often no-ops. Deferring by
    /// <see cref="EnrichmentDelaySeconds"/> gives the re-source path a window
    /// to land. The daily ContestEnrichmentJob cron catches any case where
    /// the delay wasn't enough.
    ///
    /// Per the "ingest consumers must be thin Hangfire-spawn shims"
    /// convention, this consumer does no inline DB work.
    /// </summary>
    public class ContestCompletedHandler : IConsumer<ContestCompleted>
    {
        private const int EnrichmentDelaySeconds = 30;

        private readonly ILogger<ContestCompletedHandler> _logger;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public ContestCompletedHandler(
            ILogger<ContestCompletedHandler> logger,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public Task Consume(ConsumeContext<ContestCompleted> context)
        {
            var msg = context.Message;

            _logger.LogInformation(
                "ContestCompleted consume: received. ContestId={ContestId}, CompetitionId={CompetitionId}, Sport={Sport}, CausationId={CausationId}, CorrelationId={CorrelationId}, MessageId={MessageId}",
                msg.ContestId, msg.CompetitionId, msg.Sport, msg.CausationId, msg.CorrelationId, context.MessageId);

            var cmd = new EnrichContestCommand(msg.ContestId, msg.CorrelationId);
            _backgroundJobProvider.Schedule<IEnrichContests>(
                p => p.Process(cmd),
                TimeSpan.FromSeconds(EnrichmentDelaySeconds));

            _logger.LogInformation(
                "ContestCompleted consume: EnrichContestCommand scheduled. ContestId={ContestId}, CorrelationId={CorrelationId}, DelaySeconds={DelaySeconds}",
                msg.ContestId, msg.CorrelationId, EnrichmentDelaySeconds);

            return Task.CompletedTask;
        }
    }
}
