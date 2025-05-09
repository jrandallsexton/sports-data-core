using MassTransit;

using SportsData.Core.Common;
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

        public PublishDocumentEventsProcessor(
            ILogger<PublishDocumentEventsProcessor> logger,
            IDocumentStore documentStore,
            IDecodeDocumentProvidersAndTypes decoder,
            IPublishEndpoint bus)
        {
            _logger = logger;
            _documentStore = documentStore;
            _decoder = decoder;
            _bus = bus;
        }

        public async Task Process(PublishDocumentEventsCommand command)
        {
            // TODO: Queue the request and return an Accepted
            var typeAndName = _decoder.GetTypeAndCollectionName(
                command.SourceDataProvider,
                command.Sport,
                command.DocumentType,
                command.Season);

            var dbDocuments = await _documentStore.GetAllDocumentsAsync<DocumentBase>(typeAndName.CollectionName);

            var correlationId = Guid.NewGuid();
            var causationId = Guid.NewGuid();

            // TODO: Tackle correlation and causation ids
            var events = dbDocuments.Select(tmp =>
                    new DocumentCreated(
                        tmp.Id.ToString(),
                        typeAndName.Type.Name,
                        command.Sport,
                        command.Season,
                        command.DocumentType,
                        command.SourceDataProvider,
                        correlationId,
                        causationId))
                .ToList();

            // TODO: Batch or not?
            foreach (var evt in events)
            {
                await _bus.Publish(evt);
            }
            //await _bus.PublishBatch(events);

            _logger.LogInformation($"Published {events.Count} events.");
        }
    }
}
