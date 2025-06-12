using MassTransit;

using SportsData.Core.Common;
using SportsData.Core.Common.Routing;
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
        private readonly IPublishEndpoint _bus;
        private readonly IGenerateRoutingKeys _routingKeyGenerator;

        public PublishDocumentEventsProcessor(
            ILogger<PublishDocumentEventsProcessor> logger,
            IDocumentStore documentStore,
            IDecodeDocumentProvidersAndTypes decoder,
            IPublishEndpoint bus,
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
            var typeAndName = _decoder.GetTypeAndCollectionName(
                command.SourceDataProvider,
                command.Sport,
                command.DocumentType,
                command.Season);

            var dbDocuments = await _documentStore.GetAllDocumentsAsync<DocumentBase>(typeAndName.CollectionName);

            var correlationId = Guid.NewGuid();
            var causationId = Guid.NewGuid();

            var events = dbDocuments.Select(tmp =>
                    new DocumentCreated(
                        tmp.Id.ToString(),
                        null,
                        typeAndName.Type.Name,
                        tmp.Uri,
                        tmp.SourceUrlHash,
                        command.Sport,
                        command.Season,
                        command.DocumentType,
                        command.SourceDataProvider,
                        correlationId,
                        causationId))
                .ToList();

            foreach (var evt in events)
            {
                await _bus.Publish(evt);
            }

            _logger.LogInformation($"Published {events.Count} events.");
        }
    }
}
