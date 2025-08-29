using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Producer.Application.Documents.Processors.Commands;

namespace SportsData.Producer.Application.Documents.Processors
{
    public interface IProcessDocumentCreatedEvents
    {
        Task Process(DocumentCreated evt);
    }

    public class DocumentCreatedProcessor : IProcessDocumentCreatedEvents
    {
        private readonly ILogger<DocumentCreatedProcessor> _logger;
        private readonly IProvideProviders _provider;
        private readonly IDocumentProcessorFactory _documentProcessorFactory;

        public DocumentCreatedProcessor(
            ILogger<DocumentCreatedProcessor> logger,
            IProvideProviders provider,
            IDocumentProcessorFactory documentProcessorFactory)
        {
            _logger = logger;
            _provider = provider;
            _documentProcessorFactory = documentProcessorFactory;
        }

        public async Task Process(DocumentCreated evt)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = evt.CorrelationId
                   }))
            {
                _logger.LogInformation("Began with {@command}", evt);

                await ProcessInternal(evt);
            }
        }

        private async Task ProcessInternal(DocumentCreated evt)
        {
            // call Provider to obtain the new document
            var document = evt.DocumentJson ?? await _provider.GetDocumentByUrlHash(evt.SourceUrlHash);

            if (document is null or "null")
            {
                _logger.LogError("Failed to obtain document");
                return;
            }

            if (string.IsNullOrEmpty(document)) 
            {
                _logger.LogError("Document is empty or null");
                return;
            }

            _logger.LogInformation("Obtained new document from Provider {@DocumentType}", evt.DocumentType);

            var processor = _documentProcessorFactory.GetProcessor(
                evt.SourceDataProvider,
                evt.Sport,
                evt.DocumentType,
                DocumentAction.Created);

            await processor.ProcessAsync(new ProcessDocumentCommand(
                evt.SourceDataProvider,
                evt.Sport,
                evt.SeasonYear,
                evt.DocumentType,
                document,
                evt.CorrelationId,
                evt.ParentId,
                evt.Ref,
                evt.SourceUrlHash,
                evt.SourceRef,
                evt.AttemptCount));
        }
    }
}
