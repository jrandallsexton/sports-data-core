using MassTransit;

using MongoDB.Driver;

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
        private readonly DocumentService _documentService;
        private readonly IDecodeDocumentProvidersAndTypes _decoder;
        private readonly IPublishEndpoint _bus;

        public PublishDocumentEventsProcessor(
            ILogger<PublishDocumentEventsProcessor> logger,
            DocumentService documentService,
            IDecodeDocumentProvidersAndTypes decoder,
            IPublishEndpoint bus)
        {
            _logger = logger;
            _documentService = documentService;
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

            var dbObjects = _documentService.Database.GetCollection<DocumentBase>(typeAndName.CollectionName);

            // https://www.mongodb.com/docs/drivers/csharp/current/fundamentals/crud/read-operations/retrieve/
            var filter = Builders<DocumentBase>.Filter.Empty;
            var dbCursor = await dbObjects.FindAsync(filter);
            var dbDocuments = await dbCursor.ToListAsync();

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
