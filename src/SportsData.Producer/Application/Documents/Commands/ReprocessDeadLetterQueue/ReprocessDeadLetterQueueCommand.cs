namespace SportsData.Producer.Application.Documents.Commands.ReprocessDeadLetterQueue;

/// <summary>
/// Instructs the handler to pull up to <see cref="Count"/> messages from the
/// RabbitMQ dead-letter (error) queue and re-publish them for normal processing.
/// </summary>
/// <param name="Count">Maximum number of DLQ messages to reprocess.</param>
/// <param name="QueueName">
/// Override the target queue name. When null, the value is read from
/// <c>SportsData.Producer:DeadLetterQueue:QueueName</c> config, falling back to
/// <c>document-dead-letter</c>.
/// </param>
public record ReprocessDeadLetterQueueCommand(
    int Count,
    string? QueueName = null);
