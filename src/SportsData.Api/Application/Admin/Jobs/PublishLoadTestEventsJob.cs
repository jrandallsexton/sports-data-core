using SportsData.Api.Application.Admin.Commands.GenerateLoadTest;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events;

namespace SportsData.Api.Application.Admin.Jobs;

/// <summary>
/// Background job that publishes load test events to RabbitMQ.
/// Executed asynchronously to avoid blocking the HTTP request.
/// </summary>
public interface IPublishLoadTestEventsJob
{
    Task ExecuteAsync(Guid testId, int count, LoadTestTarget target, int batchSize, DateTime publishedUtc);
}

public class PublishLoadTestEventsJob : IPublishLoadTestEventsJob
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<PublishLoadTestEventsJob> _logger;
    private readonly AppDataContext _dbContext;

    public PublishLoadTestEventsJob(
        IEventBus eventBus,
        ILogger<PublishLoadTestEventsJob> logger,
        AppDataContext dbContext)
    {
        _eventBus = eventBus;
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task ExecuteAsync(Guid testId, int count, LoadTestTarget target, int batchSize, DateTime publishedUtc)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = testId,
            ["TestId"] = testId,
            ["Count"] = count,
            ["Target"] = target,
            ["BatchSize"] = batchSize
        }))
        {
            _logger.LogInformation(
                "[KEDA-Test] Starting background job to publish load test events. TestId={TestId}, Count={Count}, Target={Target}, BatchSize={BatchSize}",
                testId, count, target, batchSize);

            var totalBatches = (int)Math.Ceiling((double)count / batchSize);

            try
            {
                for (int batch = 0; batch < totalBatches; batch++)
                {
                    var startIdx = batch * batchSize;
                    var endIdx = Math.Min(startIdx + batchSize, count);
                    var batchNumber = batch + 1;
                    var publishTasks = new List<Task>();

                    for (int i = startIdx; i < endIdx; i++)
                    {
                        var jobNumber = i + 1;

                        if (target == LoadTestTarget.Producer || target == LoadTestTarget.Both)
                        {
                            var producerEvent = new LoadTestProducerEvent(
                                TestId: testId,
                                BatchNumber: batchNumber,
                                JobNumber: jobNumber,
                                PublishedUtc: publishedUtc,
                                CorrelationId: testId
                            );
                            publishTasks.Add(_eventBus.Publish(producerEvent, CancellationToken.None));
                        }

                        if (target == LoadTestTarget.Provider || target == LoadTestTarget.Both)
                        {
                            var providerEvent = new LoadTestProviderEvent(
                                TestId: testId,
                                BatchNumber: batchNumber,
                                JobNumber: jobNumber,
                                PublishedUtc: publishedUtc,
                                CorrelationId: testId
                            );
                            publishTasks.Add(_eventBus.Publish(providerEvent, CancellationToken.None));
                        }
                    }

                    // Await batch completion before moving to next batch to limit concurrency
                    await Task.WhenAll(publishTasks);
                }

                // Trigger MassTransit outbox to flush queued messages
                await _dbContext.SaveChangesAsync(CancellationToken.None);

                var totalEvents = target == LoadTestTarget.Both ? count * 2 : count;
                _logger.LogInformation(
                    "[KEDA-Test] Load test events published successfully. TestId={TestId}, EventsPublished={EventsPublished}",
                    testId, totalEvents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[KEDA-Test] Failed to publish load test events. TestId={TestId}", testId);
                throw;
            }
        }
    }
}
