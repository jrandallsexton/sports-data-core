using MassTransit;

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

        // TODO: Look into middleware for filtering these based on Sport (mode) for the producer instance

        public DocumentCreatedHandler(
            ILogger<DocumentCreatedHandler> logger, IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task Consume(ConsumeContext<DocumentCreated> context)
        {
            var message = context.Message;
            
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = message.CorrelationId,
                       ["CausationId"] = message.CausationId,
                       ["DocumentType"] = message.DocumentType,
                       ["DocumentId"] = message.Id,
                       ["Sport"] = message.Sport,
                       ["SourceDataProvider"] = message.SourceDataProvider,
                       ["AttemptCount"] = message.AttemptCount
                   }))
            {
                _logger.LogInformation(
                    "HANDLER_ENTRY: DocumentCreated event received. " +
                    "DocumentType={DocumentType}, Sport={Sport}, Provider={Provider}, " +
                    "AttemptCount={AttemptCount}, DocumentId={DocumentId}",
                    message.DocumentType,
                    message.Sport,
                    message.SourceDataProvider,
                    message.AttemptCount,
                    message.Id);

                const int maxAttempts = 10;

                if (message.AttemptCount >= maxAttempts)
                {
                    _logger.LogError(
                        "HANDLER_MAX_RETRIES: Maximum retry attempts ({Max}) reached for document. Dropping message. " +
                        "DocumentId={DocumentId}, DocumentType={DocumentType}, Ref={Ref}",
                        maxAttempts, 
                        message.Id,
                        message.DocumentType,
                        message.Ref);
                    return;
                }

                try
                {
                    if (message.AttemptCount == 0)
                    {
                        _logger.LogInformation(
                            "HANDLER_ENQUEUE_IMMEDIATE: First attempt - enqueueing background job immediately. " +
                            "DocumentId={DocumentId}",
                            message.Id);
                        
                        var jobId = _backgroundJobProvider.Enqueue<DocumentCreatedProcessor>(x => x.Process(message));
                        
                        _logger.LogInformation(
                            "HANDLER_ENQUEUED: Background job enqueued successfully. " +
                            "HangfireJobId={JobId}, DocumentId={DocumentId}",
                            jobId,
                            message.Id);
                        
                        _logger.LogInformation(
                            "HANDLER_EXIT: Handler completed successfully (immediate enqueue). " +
                            "DocumentId={DocumentId}",
                            message.Id);
                        
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

                    if (backoffSeconds > 0)
                    {
                        _logger.LogWarning(
                            "HANDLER_SCHEDULE_DELAYED: Scheduling retry with backoff. " +
                            "DocumentId={DocumentId}, BackoffSeconds={Delay}, AttemptCount={Attempt}",
                            message.Id, 
                            backoffSeconds, 
                            message.AttemptCount);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "HANDLER_SCHEDULE_IMMEDIATE: Scheduling retry immediately (no backoff). " +
                            "DocumentId={DocumentId}, AttemptCount={Attempt}",
                            message.Id,
                            message.AttemptCount);
                    }

                    var scheduledJobId = _backgroundJobProvider.Schedule<DocumentCreatedProcessor>(
                        x => x.Process(message),
                        delay: TimeSpan.FromSeconds(backoffSeconds)
                    );
                    
                    _logger.LogInformation(
                        "HANDLER_SCHEDULED: Background job scheduled successfully. " +
                        "HangfireJobId={JobId}, DocumentId={DocumentId}, DelaySeconds={Delay}",
                        scheduledJobId,
                        message.Id,
                        backoffSeconds);
                    
                    _logger.LogInformation(
                        "HANDLER_EXIT: Handler completed successfully (scheduled retry). " +
                        "DocumentId={DocumentId}",
                        message.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "HANDLER_EXCEPTION: Unhandled exception in DocumentCreatedHandler. " +
                        "DocumentId={DocumentId}, DocumentType={DocumentType}, AttemptCount={AttemptCount}",
                        message.Id,
                        message.DocumentType,
                        message.AttemptCount);
                    
                    // Re-throw to let MassTransit handle retry/error queue
                    throw;
                }
            }

            await Task.CompletedTask;
        }

    }
}
