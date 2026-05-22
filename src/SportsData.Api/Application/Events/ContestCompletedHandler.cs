using MassTransit;

using SportsData.Api.Application.Scoring;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Events
{
    /// <summary>
    /// Closes the live-scoring loop. Producer's
    /// <see cref="ContestStreamerBase{T}"/> publishes
    /// <see cref="ContestCompleted"/> the moment STATUS_FINAL is detected; this
    /// thin shim enqueues a <see cref="ScoreContestCommand"/> via Hangfire so
    /// the existing <see cref="ContestScoringProcessor"/> path runs as soon as
    /// possible rather than waiting on the daily cron backstop.
    ///
    /// Per the "ingest consumers must be thin Hangfire-spawn shims" convention,
    /// this consumer does no inline DB work. The processor handles all the
    /// actual scoring work, including idempotency via its already-scored
    /// short-circuit — safe against at-least-once redelivery (broker
    /// redelivery, admin replay of a completed game, pod restart, etc.).
    /// </summary>
    public class ContestCompletedHandler : IConsumer<ContestCompleted>
    {
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

            var cmd = new ScoreContestCommand(msg.ContestId, msg.CorrelationId);
            _backgroundJobProvider.Enqueue<IScoreContests>(p => p.Process(cmd));

            _logger.LogInformation(
                "ContestCompleted consume: ScoreContestCommand enqueued. ContestId={ContestId}, CorrelationId={CorrelationId}",
                msg.ContestId, msg.CorrelationId);

            return Task.CompletedTask;
        }
    }
}
