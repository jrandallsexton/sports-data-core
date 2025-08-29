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
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = context.Message.CorrelationId
                   }))
            {
                const int maxAttempts = 10;

                if (context.Message.AttemptCount >= maxAttempts)
                {
                    _logger.LogError("Maximum retry attempts ({Max}) reached for document {Id}. Dropping message.",
                        maxAttempts, context.Message.Id);
                    return;
                }

                _logger.LogInformation("New document event received (Attempt {Attempt}): {@Message}",
                    context.Message.AttemptCount, context.Message);

                if (context.Message.AttemptCount == 0)
                {
                    _backgroundJobProvider.Enqueue<DocumentCreatedProcessor>(x => x.Process(context.Message));
                    return;
                }

                var backoffSeconds = context.Message.AttemptCount switch
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
                    _logger.LogWarning("Delaying reprocessing of document {Id} by {Delay}s (Attempt {Attempt})",
                        context.Message.Id, backoffSeconds, context.Message.AttemptCount);
                }

                _backgroundJobProvider.Schedule<DocumentCreatedProcessor>(
                    x => x.Process(context.Message),
                    delay: TimeSpan.FromSeconds(backoffSeconds)
                );
            }

            await Task.CompletedTask;
        }

    }
}
