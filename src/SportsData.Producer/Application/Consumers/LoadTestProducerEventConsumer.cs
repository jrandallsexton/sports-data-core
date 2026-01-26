using System;

using MassTransit;

using SportsData.Core.Eventing.Events;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Jobs;

namespace SportsData.Producer.Application.Consumers;

/// <summary>
/// Consumes LoadTestProducerEvent from RabbitMQ and enqueues Hangfire job.
/// Used for KEDA autoscaling validation.
/// </summary>
public class LoadTestProducerEventConsumer : IConsumer<LoadTestProducerEvent>
{
    private readonly IProvideBackgroundJobs _backgroundJobProvider;
    private readonly ILogger<LoadTestProducerEventConsumer> _logger;

    public LoadTestProducerEventConsumer(
        IProvideBackgroundJobs backgroundJobProvider,
        ILogger<LoadTestProducerEventConsumer> logger)
    {
        _backgroundJobProvider = backgroundJobProvider;
        _logger = logger;
    }

    public Task Consume(ConsumeContext<LoadTestProducerEvent> context)
    {
        var message = context.Message;

        _logger.LogDebug(
            "[KEDA-Test] Received load test event. TestId={TestId}, Batch={BatchNumber}, Job={JobNumber}",
            message.TestId, message.BatchNumber, message.JobNumber);

        // Enqueue to Hangfire - this is what KEDA monitors
        _backgroundJobProvider.Enqueue<IProcessLoadTestJob>(job =>
            job.ProcessAsync(message.TestId, message.BatchNumber, message.JobNumber, message.PublishedUtc));

        _logger.LogInformation(
            "[KEDA-Test] Enqueued load test job to Hangfire. TestId={TestId}, Batch={BatchNumber}, Job={JobNumber}",
            message.TestId, message.BatchNumber, message.JobNumber);

        return Task.CompletedTask;
    }
}
