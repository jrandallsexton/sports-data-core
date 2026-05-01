using MassTransit;

using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Processing;

namespace SportsData.Producer.Application.Consumers;

/// <summary>
/// Thin Ingest-pod consumer for CompetitorScoreUpdated. Enqueues a Hangfire
/// job (CompetitorScoreUpdatedConsumerHandler) so the actual DB work runs on
/// a Worker pod. Keeps Ingest pods responsible only for translating bus
/// messages into background work, in line with the Api/Ingest/Worker role
/// split (see project_role_split memory).
/// </summary>
public class CompetitorScoreUpdatedConsumer : IConsumer<CompetitorScoreUpdated>
{
    private readonly ILogger<CompetitorScoreUpdatedConsumer> _logger;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;

    public CompetitorScoreUpdatedConsumer(
        ILogger<CompetitorScoreUpdatedConsumer> logger,
        IProvideBackgroundJobs backgroundJobProvider)
    {
        _logger = logger;
        _backgroundJobProvider = backgroundJobProvider;
    }

    public Task Consume(ConsumeContext<CompetitorScoreUpdated> context)
    {
        var message = context.Message;

        var jobId = _backgroundJobProvider.Enqueue<ICompetitorScoreUpdatedConsumerHandler>(
            x => x.Process(message));

        _logger.LogInformation(
            "CompetitorScoreUpdated received; enqueued handler. " +
            "ContestId={ContestId}, FranchiseSeasonId={FranchiseSeasonId}, Score={Score}, " +
            "HangfireJobId={HangfireJobId}, CorrelationId={CorrelationId}",
            message.ContestId,
            message.FranchiseSeasonId,
            message.Score,
            jobId,
            message.CorrelationId);

        return Task.CompletedTask;
    }
}
