using MassTransit;

using SportsData.Core.Eventing.Events;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Jobs;

namespace SportsData.Provider.Application.Consumers;

/// <summary>
/// Consumes LoadTestProviderEvent from RabbitMQ and enqueues Hangfire job.
/// Used for KEDA autoscaling validation.
/// </summary>
public class LoadTestProviderEventConsumer : IConsumer<LoadTestProviderEvent>
{
    private readonly IProvideBackgroundJobs _backgroundJobProvider;
    private readonly ILogger<LoadTestProviderEventConsumer> _logger;

    public LoadTestProviderEventConsumer(
        IProvideBackgroundJobs backgroundJobProvider,
        ILogger<LoadTestProviderEventConsumer> logger)
    {
        _backgroundJobProvider = backgroundJobProvider;
        _logger = logger;
    }

    public Task Consume(ConsumeContext<LoadTestProviderEvent> context)
    {
        var message = context.Message;

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = message.CorrelationId,
            ["TestId"] = message.TestId,
            ["BatchNumber"] = message.BatchNumber,
            ["JobNumber"] = message.JobNumber
        }))
        {
            _logger.LogDebug(
                "[KEDA-Test] Received load test event. TestId={TestId}, Batch={BatchNumber}, Job={JobNumber}",
                message.TestId, message.BatchNumber, message.JobNumber);

            // Enqueue to Hangfire - this creates backpressure for KEDA to scale against
            _backgroundJobProvider.Enqueue<IProcessLoadTestJob>(job =>
                job.ProcessAsync(message.TestId, message.BatchNumber, message.JobNumber, message.PublishedUtc));

            _logger.LogInformation(
                "[KEDA-Test] Enqueued load test job to Hangfire. TestId={TestId}, Batch={BatchNumber}, Job={JobNumber}",
                message.TestId, message.BatchNumber, message.JobNumber);
        }

        return Task.CompletedTask;
    }
}
