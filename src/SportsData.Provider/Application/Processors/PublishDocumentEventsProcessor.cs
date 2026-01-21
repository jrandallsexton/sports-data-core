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

                var dbDocuments = await _documentStore.GetAllDocumentsAsync<DocumentBase>(typeAndName.CollectionName);

                _logger.LogInformation(
                    "Retrieved {DocumentCount} documents from collection {CollectionName}",
                    dbDocuments.Count,
                    typeAndName.CollectionName);

                if (dbDocuments.Count == 0)
                {
                    _logger.LogWarning(
                        "No documents found in collection {CollectionName}. No events will be published.",
                        typeAndName.CollectionName);
                    return;
                }

                var events = dbDocuments.Select(doc =>
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
                    "Created {EventCount} DocumentCreated events. Beginning publication. CorrelationId={CorrelationId}",
                    events.Count,
                    correlationId);

                var publishedCount = 0;
                var batchSize = 50;

                foreach (var evt in events)
                {
                    await _bus.Publish(evt);
                    publishedCount++;

                    // Log progress for large batches
                    if (publishedCount % batchSize == 0)
                    {
                        _logger.LogInformation(
                            "Publishing progress: {Published}/{Total} events published ({Percentage:F1}%)",
                            publishedCount,
                            events.Count,
                            (publishedCount / (double)events.Count) * 100);
                    }
                }

                _logger.LogInformation(
                    "PublishDocumentEventsProcessor completed successfully. Published {EventCount} DocumentCreated events. CorrelationId={CorrelationId}",
                    events.Count,
                    correlationId);
            }
        }
    }
}
