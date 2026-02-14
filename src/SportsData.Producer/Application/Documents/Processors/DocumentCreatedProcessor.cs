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
            var document = await ObtainDocumentAsync(evt);
            
            if (IsInvalidDocument(document)) 
            {
                _logger.LogError(
                    "❌ DOC_CREATED_PROCESSOR_DOCUMENT_EMPTY: Document is empty, whitespace, or literal 'null'. " +
                    "DocumentId={DocumentId}",
                    evt.Id);
                return;
            }

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
                document!, // Already validated via IsInvalidDocument guard
                evt.MessageId,
                evt.CorrelationId,
                evt.ParentId,
                evt.SourceRef,
                evt.SourceUrlHash,
                evt.Ref,
                evt.AttemptCount,
                evt.IncludeLinkedDocumentTypes));

            _logger.LogInformation(
                "✅ DOC_CREATED_PROCESSOR_EXECUTE_COMPLETED: Document-specific processor completed. " +
                "ProcessorType={ProcessorType}, DocumentId={DocumentId}",
                processor.GetType().Name,
                evt.Id);
        }

        /// <summary>
        /// Obtains the document JSON either from the event payload (if inline) or by fetching from Provider.
        /// Small documents (<200KB) are included inline to avoid HTTP round-trips.
        /// Uses ValueTask for efficiency since inline documents complete synchronously.
        /// </summary>
        private async ValueTask<string?> ObtainDocumentAsync(DocumentCreated evt)
        {
            // Check if document was included inline in the event (within size limits)
            if (!string.IsNullOrWhiteSpace(evt.DocumentJson) && 
                !evt.DocumentJson.Trim().Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug(
                    "📦 DOC_CREATED_PROCESSOR_DOCUMENT_INLINE: Document included in event payload. " +
                    "DocumentLength={Length}, DocumentId={DocumentId}",
                    evt.DocumentJson.Length,
                    evt.Id);
                
                return evt.DocumentJson;
            }

            _logger.LogInformation(
                "📥 DOC_CREATED_PROCESSOR_FETCH_DOCUMENT: Document not in payload, fetching from Provider. " +
                "SourceUrlHash={SourceUrlHash}, DocumentId={DocumentId}",
                evt.SourceUrlHash,
                evt.Id);

            // Document exceeded size limit, fetch from Provider
            var document = await _provider.GetDocumentByUrlHash(evt.SourceUrlHash, evt.DocumentType);
            
            if (IsInvalidDocument(document))
            {
                _logger.LogError(
                    "❌ DOC_CREATED_PROCESSOR_DOCUMENT_NULL: Failed to obtain valid document from Provider. " +
                    "SourceUrlHash={SourceUrlHash}, DocumentId={DocumentId}",
                    evt.SourceUrlHash,
                    evt.Id);
                return null;
            }
            
            _logger.LogInformation(
                "✅ DOC_CREATED_PROCESSOR_DOCUMENT_FETCHED: Document fetched from Provider successfully. " +
                "DocumentLength={Length}, DocumentId={DocumentId}",
                document.Length,
                evt.Id);

            return document;
        }

        /// <summary>
        /// Validates that a document payload is not null, empty, whitespace, or the literal string "null".
        /// </summary>
        private static bool IsInvalidDocument(string? document)
        {
            return string.IsNullOrWhiteSpace(document) || 
                   document.Trim().Equals("null", StringComparison.OrdinalIgnoreCase);
        }
    }
}
