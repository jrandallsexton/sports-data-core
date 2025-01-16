using MassTransit;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Producer.Application.Documents.Processors;
using SportsData.Producer.Infrastructure.Data;

namespace SportsData.Producer.Application.Documents
{
    public class DocumentCreatedHandler :
        IConsumer<DocumentCreated>
    {
        private readonly ILogger<DocumentCreatedHandler> _logger;
        private readonly IProvideProviders _provider;
        private readonly IBus _bus;
        private readonly AppDataContext _dataContext;
        private readonly IDocumentProcessorFactory _documentProcessorFactory;

        public DocumentCreatedHandler(
            ILogger<DocumentCreatedHandler> logger,
            IProvideProviders provider,
            IBus bus,
            AppDataContext dataContext,
            IDocumentProcessorFactory documentProcessorFactory)
        {
            _logger = logger;
            _provider = provider;
            _bus = bus;
            _dataContext = dataContext;
            _documentProcessorFactory = documentProcessorFactory;
        }

        public async Task Consume(ConsumeContext<DocumentCreated> context)
        {
            _logger.LogInformation("new document event received: {@message}", context.Message);

            // TODO: Remove this
            if (context.Message.DocumentType == DocumentType.TeamInformation)
                return;

            // call Provider to obtain new document
            var document = await _provider.GetDocumentByIdAsync(
                context.Message.SourceDataProvider,
                context.Message.DocumentType,
                int.Parse(context.Message.Id));

            if (document is null or "null")
            {
                _logger.LogError("Failed to obtain document: {@doc}", context.Message);
                return;
            }

            _logger.LogInformation("obtained new document from Provider");

            var processor = _documentProcessorFactory.GetProcessor(
                context.Message.SourceDataProvider,
                context.Message.DocumentType);

            // TODO: pass this to an on-demand Hangfire job?
            await processor.ProcessAsync(new ProcessDocumentCommand(context.Message.SourceDataProvider, document, context.CorrelationId ?? Guid.NewGuid()));

       }
    }
}
