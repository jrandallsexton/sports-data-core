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

        public DocumentCreatedHandler(
            ILogger<DocumentCreatedHandler> logger, IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task Consume(ConsumeContext<DocumentCreated> context)
        {
            _logger.LogInformation("New document event received: {@message}", context.Message);
            using (_logger.BeginScope(new Dictionary<string, Guid>()
                    {
                       { "CorrelationId", context.Message.CorrelationId }
                   }))

                _backgroundJobProvider.Enqueue<DocumentCreatedProcessor>(x => x.Process(context.Message));
        }
    }
}
