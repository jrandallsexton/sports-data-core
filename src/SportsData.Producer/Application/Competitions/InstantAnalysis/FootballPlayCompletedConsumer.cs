using MassTransit;

using SportsData.Core.Eventing.Events.Contests.Football;
using SportsData.Core.Processing;

namespace SportsData.Producer.Application.Competitions.InstantAnalysis;

/// <summary>
/// Thin Ingest-pod consumer for FootballPlayCompleted: the instant-analysis
/// trigger. When the kill switch is on, it enqueues
/// <see cref="FootballPlayCompletedConsumerHandler"/> on the live queue so
/// analysis never waits behind bulk work; the per-game opt-in
/// (CompetitionStream.LiveAnalysisEnabled) is checked by the job, keeping
/// this shim free of database work.
///
/// The API has a same-named consumer of this event (SignalR last-play
/// fan-out). <see cref="FootballPlayCompletedConsumerDefinition"/> gives this
/// one its own queue so the two never compete on a shared broker (the local
/// stack).
/// </summary>
public class FootballPlayCompletedConsumer : IConsumer<FootballPlayCompleted>
{
    private readonly ILogger<FootballPlayCompletedConsumer> _logger;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;
    private readonly InstantAnalysisConfig _config;

    public FootballPlayCompletedConsumer(
        ILogger<FootballPlayCompletedConsumer> logger,
        IProvideBackgroundJobs backgroundJobProvider,
        InstantAnalysisConfig config)
    {
        _logger = logger;
        _backgroundJobProvider = backgroundJobProvider;
        _config = config;
    }

    public Task Consume(ConsumeContext<FootballPlayCompleted> context)
    {
        if (!_config.Enabled)
            return Task.CompletedTask;

        var message = context.Message;

        // Live queue via [Queue] on IFootballPlayCompletedConsumerHandler.Process.
        var jobId = _backgroundJobProvider.Enqueue<IFootballPlayCompletedConsumerHandler>(
            x => x.Process(message));

        _logger.LogInformation(
            "FootballPlayCompleted received; enqueued instant analysis. " +
            "ContestId={ContestId}, CompetitionId={CompetitionId}, PlayId={PlayId}, " +
            "HangfireJobId={HangfireJobId}, CorrelationId={CorrelationId}",
            message.ContestId,
            message.CompetitionId,
            message.PlayId,
            jobId,
            message.CorrelationId);

        return Task.CompletedTask;
    }
}
