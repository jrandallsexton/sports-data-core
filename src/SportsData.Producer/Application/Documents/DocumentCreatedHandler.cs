using MassTransit;

using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Documents.Processors;

namespace SportsData.Producer.Application.Documents
{
    public class DocumentCreatedHandler :
        IConsumer<DocumentCreated>
    {
        private readonly ILogger<DocumentCreatedHandler> _logger;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IEventBus _eventBus;
        private readonly IMessageDeliveryScope _deliveryScope;

        // TODO: Look into middleware for filtering these based on Sport (mode) for the producer instance

        public DocumentCreatedHandler(
            ILogger<DocumentCreatedHandler> logger,
            IProvideBackgroundJobs backgroundJobProvider,
            IEventBus eventBus,
            IMessageDeliveryScope deliveryScope)
        {
            _logger = logger;
            _backgroundJobProvider = backgroundJobProvider;
            _eventBus = eventBus;
            _deliveryScope = deliveryScope;
        }

        public async Task Consume(ConsumeContext<DocumentCreated> context)
        {
            var message = context.Message;
            
            // Extract retry context from headers once for use throughout the method
            var retryReason = context.Headers.Get<string>("RetryReason", "Unknown") ?? "Unknown";
            
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                        ["AttemptCount"] = message.AttemptCount,
                        ["CausationId"] = message.CausationId,
                        ["CorrelationId"] = message.CorrelationId,
                        ["DocumentId"] = message.Id,
                        ["DocumentType"] = message.DocumentType,
                        ["MessageId"] = message.MessageId,
                        ["Ref"] = message.Ref?.ToString() ?? string.Empty,
                        ["RetryReason"] = retryReason,
                        ["SourceDataProvider"] = message.SourceDataProvider,
                        ["SourceUrlHash"] = message.SourceUrlHash,
                        ["Sport"] = message.Sport
                   }))
            {
                // Check for dead-letter header and skip processing to prevent infinite loops
                // Check for dead-letter header and skip processing to prevent infinite loops
                var isDeadLetter = context.Headers.Get<bool>("DeadLetter", false) ?? false;
                if (isDeadLetter)
                {
                    var deadLetterReason = context.Headers.Get<string>("DeadLetterReason", "Unknown") ?? "Unknown";
                    _logger.LogWarning(
                        "HANDLER_DEADLETTER_SKIP: Skipping dead-letter event. Reason={DeadLetterReason}",
                        deadLetterReason);
                    return;
                }

                _logger.LogInformation("HANDLER_ENTRY: DocumentCreated event received.");

                const int maxAttempts = 10;

                if (message.AttemptCount >= maxAttempts)
                {
                    _logger.LogError("HANDLER_MAX_RETRIES: Maximum retry attempts ({MaxAttempts}) reached for document. Publishing dead-letter event.", maxAttempts);
                    
                    // Publish dead-letter event using Direct mode (bypasses outbox, no DbContext needed)
                    var deadLetterHeaders = new Dictionary<string, object>
                    {
                        ["DeadLetter"] = true,
                        ["DeadLetterReason"] = "MaxRetriesExceeded",
                        ["FailureReason"] = retryReason
                    };
                    
                    using (_deliveryScope.Use(DeliveryMode.Direct))
                    {
                        await _eventBus.Publish(message, deadLetterHeaders);
                    }
                    return;
                }

                try
                {
                    if (message.AttemptCount == 0)
                    {
                        _logger.LogInformation(
                            "HANDLER_ENQUEUE_IMMEDIATE: First attempt - enqueueing background job immediately.");
                        
                        var jobId = _backgroundJobProvider.Enqueue<DocumentCreatedProcessor>(x => x.Process(message));
                        
                        _logger.LogInformation(
                            "HANDLER_ENQUEUED: Background job enqueued successfully. {JobId}",
                            jobId);
                        
                        _logger.LogInformation(
                            "HANDLER_EXIT: Handler completed successfully (immediate enqueue).");
                        
                        return;
                    }

                    var backoffSeconds = message.AttemptCount switch
                    {
                        1 => 0,
                        2 => 10,
                        3 => 30,
                        4 => 60,
                        5 => 120,
                        _ => 300
                    };

                    using (_logger.BeginScope(new Dictionary<string, object> { ["BackoffSeconds"] = backoffSeconds }))
                    {
                        if (backoffSeconds > 0)
                        {
                            _logger.LogWarning("HANDLER_SCHEDULE_DELAYED: Scheduling retry with backoff.");
                        }
                        else
                        {
                            _logger.LogInformation("HANDLER_SCHEDULE_IMMEDIATE: Scheduling retry immediately (no backoff).");
                        }

                        var scheduledJobId = _backgroundJobProvider.Schedule<DocumentCreatedProcessor>(
                            x => x.Process(message),
                            delay: TimeSpan.FromSeconds(backoffSeconds)
                        );

                        _logger.LogInformation("HANDLER_SCHEDULED: Background job scheduled successfully. HangfireJobId={HangfireJobId}", scheduledJobId);
                    }
                    
                    _logger.LogInformation("HANDLER_EXIT: Handler completed successfully (scheduled retry).");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "HANDLER_EXCEPTION: Unhandled exception in DocumentCreatedHandler.");
                    
                    // Re-throw to let MassTransit handle retry/error queue
                    throw;
                }
            }

            await Task.CompletedTask;
        }

    }
}
