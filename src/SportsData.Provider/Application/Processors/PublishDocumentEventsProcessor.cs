using SportsData.Core.Common;
using SportsData.Core.Common.Routing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Infrastructure.Clients.Provider.Commands;
using SportsData.Provider.Infrastructure.Data;

namespace SportsData.Provider.Application.Processors
{
    public interface IProcessPublishDocumentEvents
    {
        Task Process(PublishDocumentEventsCommand command);
    }

    public class PublishDocumentEventsProcessor : IProcessPublishDocumentEvents
    {
        private readonly ILogger<PublishDocumentEventsProcessor> _logger;
        private readonly IDocumentStore _documentStore;
        private readonly IDecodeDocumentProvidersAndTypes _decoder;
        private readonly IEventBus _bus;
        private readonly IGenerateRoutingKeys _routingKeyGenerator;

        public PublishDocumentEventsProcessor(
            ILogger<PublishDocumentEventsProcessor> logger,
            IDocumentStore documentStore,
            IDecodeDocumentProvidersAndTypes decoder,
            IEventBus bus,
            IGenerateRoutingKeys routingKeyGenerator)
        {
            _logger = logger;
            _documentStore = documentStore;
            _decoder = decoder;
            _bus = bus;
            _routingKeyGenerator = routingKeyGenerator;
        }

        public async Task Process(PublishDocumentEventsCommand command)
        {
            var correlationId = Guid.NewGuid();
            var causationId = Guid.NewGuid();

            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = correlationId,
                       ["CausationId"] = causationId,
                       ["Sport"] = command.Sport,
                       ["DocumentType"] = command.DocumentType,
                       ["Season"] = command.Season ?? 0,
                       ["SourceDataProvider"] = command.SourceDataProvider
                   }))
            {
                _logger.LogInformation(
                    "PublishDocumentEventsProcessor started. Sport={Sport}, DocumentType={DocumentType}, Season={Season}, Provider={Provider}",
                    command.Sport,
                    command.DocumentType,
                    command.Season,
                    command.SourceDataProvider);

                if (command.IncludeLinkedDocumentTypes?.Count > 0)
                {
                    _logger.LogInformation(
                        "Inclusion filter provided: {DocumentTypes}. Child documents will be filtered.",
                        string.Join(", ", command.IncludeLinkedDocumentTypes));
                }

                var typeAndName = _decoder.GetTypeAndCollectionName(
                    command.SourceDataProvider,
                    command.Sport,
                    command.DocumentType,
                    command.Season);

                _logger.LogInformation(
                    "Resolved collection. CollectionName={CollectionName}, TypeName={TypeName}",
                    typeAndName.CollectionName,
                    typeAndName.Type.Name);

                // Use batched processing to avoid loading all documents into memory
                // Default to 100, but allow override via command for operational flexibility
                var batchSize = command.BatchSize ?? 100;
                var totalPublished = 0;
                var batchNumber = 0;

                _logger.LogInformation(
                    "Beginning batched document retrieval. BatchSize={BatchSize} (CommandOverride={IsOverride})",
                    batchSize,
                    command.BatchSize.HasValue);

                await foreach (var batch in _documentStore.GetDocumentsInBatchesAsync<DocumentBase>(
                    typeAndName.CollectionName, 
                    batchSize))
                {
                    batchNumber++;
                    
                    _logger.LogInformation(
                        "Processing batch {BatchNumber}. Documents in batch: {BatchCount}",
                        batchNumber,
                        batch.Count);

                    var events = batch.Select(doc =>
                            new DocumentCreated(
                                doc.Id.ToString(),
                                null,
                                typeAndName.Type.Name,
                                doc.Uri,
                                doc.Uri, //TODO: This should be the source URL, not the URI
                                null,
                                doc.SourceUrlHash,
                                command.Sport,
                                command.Season,
                                command.DocumentType,
                                command.SourceDataProvider,
                                correlationId,
                                causationId,
                                IncludeLinkedDocumentTypes: command.IncludeLinkedDocumentTypes))
                        .ToList();

                    _logger.LogInformation(
                        "Publishing {EventCount} events from batch {BatchNumber}",
                        events.Count,
                        batchNumber);

                    foreach (var evt in events)
                    {
                        await _bus.Publish(evt);
                        totalPublished++;
                    }

                    _logger.LogInformation(
                        "Batch {BatchNumber} published successfully. Total published so far: {TotalPublished}",
                        batchNumber,
                        totalPublished);

                    // Allow memory to be reclaimed between batches
                    // The batch and events list will be GC'd before the next iteration
                }

                if (totalPublished == 0)
                {
                    _logger.LogWarning(
                        "No documents found in collection {CollectionName}. No events were published.",
                        typeAndName.CollectionName);
                }
                else
                {
                    _logger.LogInformation(
                        "PublishDocumentEventsProcessor completed successfully. Published {EventCount} DocumentCreated events across {BatchCount} batches. CorrelationId={CorrelationId}",
                        totalPublished,
                        batchNumber,
                        correlationId);
                }
            }
        }
    }
}
