using System;

namespace SportsData.Core.Eventing.Events;

/// <summary>
/// Test event used to generate synthetic load for KEDA autoscaling validation.
/// Published to RabbitMQ, consumed by Producer, enqueued to Hangfire.
/// </summary>
/// <param name="TestId">Unique identifier for this test run</param>
/// <param name="BatchNumber">Batch number within the test run</param>
/// <param name="JobNumber">Sequential job number within the batch</param>
/// <param name="PublishedUtc">When the event was published</param>
/// <param name="CorrelationId">Correlation identifier for tracking (set to TestId)</param>
public record LoadTestProducerEvent(
    Guid TestId,
    int BatchNumber,
    int JobNumber,
    DateTime PublishedUtc,
    Guid CorrelationId
);
