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
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = evt.CorrelationId,
                ["CausationId"] = evt.CausationId,
                ["DocumentType"] = evt.DocumentType,
                ["DocumentId"] = evt.Id,
                ["SeasonYear"] = evt.SeasonYear ?? 0,
                ["Sport"] = evt.Sport,
                ["SourceDataProvider"] = evt.SourceDataProvider
            }))
            {
                _logger.LogInformation(
                    "🚀 DOC_CREATED_PROCESSOR_ENTRY: Hangfire job started. " +
                    "DocumentType={DocumentType}, Sport={Sport}, Provider={Provider}, " +
                    "SourceUrlHash={SourceUrlHash}, AttemptCount={AttemptCount}, DocumentId={DocumentId}",
                    evt.DocumentType, 
                    evt.Sport,
                    evt.SourceDataProvider,
                    evt.SourceUrlHash, 
                    evt.AttemptCount,
                    evt.Id);

                try
                {
                    await ProcessInternal(evt);
                    
                    sw.Stop();
                    
                    _logger.LogInformation(
                        "✅ DOC_CREATED_PROCESSOR_COMPLETED: Document processing completed successfully. " +
                        "DocumentType={DocumentType}, DurationMs={DurationMs}, DocumentId={DocumentId}",
                        evt.DocumentType, 
                        sw.ElapsedMilliseconds,
                        evt.Id);
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    
                    _logger.LogError(ex,
                        "💥 DOC_CREATED_PROCESSOR_FAILED: Document processing failed. " +
                        "DocumentType={DocumentType}, DurationMs={DurationMs}, DocumentId={DocumentId}, " +
                        "ErrorMessage={ErrorMessage}",
                        evt.DocumentType,
                        sw.ElapsedMilliseconds,
                        evt.Id,
                        ex.Message);
                    
                    throw;
                }
            }
        }

        private async Task ProcessInternal(DocumentCreated evt)
        {
            _logger.LogInformation(
                "📥 DOC_CREATED_PROCESSOR_FETCH_DOCUMENT: Fetching document from Provider. " +
                "SourceUrlHash={SourceUrlHash}, DocumentId={DocumentId}",
                evt.SourceUrlHash,
                evt.Id);

            // call Provider to obtain the new document
            var document = evt.DocumentJson ?? await _provider.GetDocumentByUrlHash(evt.SourceUrlHash);

            if (document is null or "null")
            {
                _logger.LogError(
                    "❌ DOC_CREATED_PROCESSOR_DOCUMENT_NULL: Failed to obtain document from Provider. " +
                    "SourceUrlHash={SourceUrlHash}, DocumentId={DocumentId}",
                    evt.SourceUrlHash,
                    evt.Id);
                return;
            }

            if (string.IsNullOrEmpty(document)) 
            {
                _logger.LogError(
                    "❌ DOC_CREATED_PROCESSOR_DOCUMENT_EMPTY: Document is empty. " +
                    "SourceUrlHash={SourceUrlHash}, DocumentId={DocumentId}",
                    evt.SourceUrlHash,
                    evt.Id);
                return;
            }

            var documentLength = document.Length;
            _logger.LogInformation(
                "✅ DOC_CREATED_PROCESSOR_DOCUMENT_OBTAINED: Document fetched successfully. " +
                "DocumentType={DocumentType}, DocumentLength={Length}, DocumentId={DocumentId}",
                evt.DocumentType,
                documentLength,
                evt.Id);

            _logger.LogInformation(
                "🔍 DOC_CREATED_PROCESSOR_GET_PROCESSOR: Looking up document processor from factory. " +
                "Provider={Provider}, Sport={Sport}, DocumentType={DocumentType}, DocumentId={DocumentId}",
                evt.SourceDataProvider,
                evt.Sport,
                evt.DocumentType,
                evt.Id);

            var processor = _documentProcessorFactory.GetProcessor(
                evt.SourceDataProvider,
                evt.Sport,
                evt.DocumentType,
                DocumentAction.Created);

            _logger.LogInformation(
                "✅ DOC_CREATED_PROCESSOR_FOUND: Document processor found. " +
                "ProcessorType={ProcessorType}, DocumentId={DocumentId}",
                processor.GetType().Name,
                evt.Id);

            _logger.LogInformation(
                "⚙️ DOC_CREATED_PROCESSOR_EXECUTE: Executing document-specific processor. " +
                "ProcessorType={ProcessorType}, DocumentId={DocumentId}",
                processor.GetType().Name,
                evt.Id);

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
                evt.AttemptCount,
                evt.IncludeLinkedDocumentTypes));

            _logger.LogInformation(
                "✅ DOC_CREATED_PROCESSOR_EXECUTE_COMPLETED: Document-specific processor completed. " +
                "ProcessorType={ProcessorType}, DocumentId={DocumentId}",
                processor.GetType().Name,
                evt.Id);
        }
    }
}
