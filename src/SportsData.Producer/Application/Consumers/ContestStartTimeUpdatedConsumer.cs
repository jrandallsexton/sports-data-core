using MassTransit;

using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Processing;

namespace SportsData.Producer.Application.Consumers;

/// <summary>
/// Thin Ingest-pod consumer for ContestStartTimeUpdated. Enqueues a Hangfire
/// job (ContestStartTimeUpdatedConsumerHandler) so the actual DB + Hangfire
/// reschedule work runs on a Worker pod. Keeps Ingest pods responsible only
/// for translating bus messages into background work, in line with the
/// Api/Ingest/Worker role split (see project_role_split memory).
///
/// Together with the per-sport CompetitionStreamScheduler recurring sweep,
/// this gives the streamer two paths to a correct schedule: the recurring
/// sweep (daily for MLB, weekly for football — the safety net) and the
/// event-driven reschedule (this consumer chain — near-real-time when ESPN
/// moves a game time).
/// </summary>
public class ContestStartTimeUpdatedConsumer : IConsumer<ContestStartTimeUpdated>
{
    private readonly ILogger<ContestStartTimeUpdatedConsumer> _logger;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;

    public ContestStartTimeUpdatedConsumer(
        ILogger<ContestStartTimeUpdatedConsumer> logger,
        IProvideBackgroundJobs backgroundJobProvider)
    {
        _logger = logger;
        _backgroundJobProvider = backgroundJobProvider;
    }

    public Task Consume(ConsumeContext<ContestStartTimeUpdated> context)
    {
        var message = context.Message;

        var jobId = _backgroundJobProvider.Enqueue<IContestStartTimeUpdatedConsumerHandler>(
            x => x.Process(message));

        _logger.LogInformation(
            "ContestStartTimeUpdated received; enqueued handler. " +
            "ContestId={ContestId}, NewStartTime={NewStartTime}, Sport={Sport}, " +
            "HangfireJobId={HangfireJobId}, CorrelationId={CorrelationId}",
            message.ContestId,
            message.NewStartTime,
            message.Sport,
            jobId,
            message.CorrelationId);

        return Task.CompletedTask;
    }
}
