using MassTransit;

using SportsData.Core.Eventing.Events.Documents;

namespace SportsData.Producer.Application.Documents
{
    /// <summary>
    /// Consumes DocumentDeadLetter events for monitoring and observability.
    /// These events represent documents that failed processing after max retries.
    /// </summary>
    public class DocumentDeadLetterConsumer : IConsumer<DocumentDeadLetter>
    {
        private readonly ILogger<DocumentDeadLetterConsumer> _logger;

        public DocumentDeadLetterConsumer(ILogger<DocumentDeadLetterConsumer> logger)
        {
            _logger = logger;
        }

        public Task Consume(ConsumeContext<DocumentDeadLetter> context)
        {
            var message = context.Message;

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["DocumentId"] = message.Id,
                ["DocumentType"] = message.DocumentType,
                ["Sport"] = message.Sport,
                ["SourceUrlHash"] = message.SourceUrlHash,
                ["CorrelationId"] = message.CorrelationId,
                ["CausationId"] = message.CausationId,
                ["Ref"] = message.Ref?.ToString() ?? string.Empty,
                ["SourceRef"] = message.SourceRef.ToString()
            }))
            {
                _logger.LogError(
                    "DEAD_LETTER: Document failed after {AttemptCount} attempts. Reason: {Reason}",
                    message.AttemptCount,
                    message.Reason);
            }

            return Task.CompletedTask;
        }
    }
}
