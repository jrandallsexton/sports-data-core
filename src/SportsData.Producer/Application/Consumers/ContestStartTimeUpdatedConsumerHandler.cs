using SportsData.Core.Eventing.Events.Contests;
using SportsData.Producer.Application.Competitions;

namespace SportsData.Producer.Application.Consumers;

public interface IContestStartTimeUpdatedConsumerHandler
{
    Task Process(ContestStartTimeUpdated evt);
}

/// <summary>
/// Hangfire job that does the actual work for a ContestStartTimeUpdated event:
/// invokes <see cref="CompetitionStreamScheduler.RescheduleForContestAsync"/> to
/// cancel-and-reschedule the existing CompetitionStream Hangfire job when ESPN
/// moves a game's start time mid-day.
///
/// Spawned by <see cref="ContestStartTimeUpdatedConsumer"/> (Ingest) and executed
/// on Worker pods so Ingest stays thin.
/// </summary>
public class ContestStartTimeUpdatedConsumerHandler : IContestStartTimeUpdatedConsumerHandler
{
    private readonly ILogger<ContestStartTimeUpdatedConsumerHandler> _logger;
    private readonly CompetitionStreamScheduler _scheduler;

    public ContestStartTimeUpdatedConsumerHandler(
        ILogger<ContestStartTimeUpdatedConsumerHandler> logger,
        CompetitionStreamScheduler scheduler)
    {
        _logger = logger;
        _scheduler = scheduler;
    }

    public async Task Process(ContestStartTimeUpdated evt)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = evt.CorrelationId,
            ["CausationId"] = evt.CausationId,
            ["ContestId"] = evt.ContestId,
            ["NewStartTime"] = evt.NewStartTime,
            ["Sport"] = evt.Sport
        });

        _logger.LogInformation("ContestStartTimeUpdatedConsumerHandler started.");

        // The scheduler reads Competition.Date directly from the DB rather
        // than using NewStartTime off the event — keeps the reschedule
        // idempotent under out-of-order delivery (the latest DB value wins,
        // not whatever stale event was last delivered).
        var rescheduled = await _scheduler.RescheduleForContestAsync(evt.ContestId, CancellationToken.None);

        _logger.LogInformation(
            "ContestStartTimeUpdatedConsumerHandler completed. Rescheduled={Rescheduled}",
            rescheduled);
    }
}
