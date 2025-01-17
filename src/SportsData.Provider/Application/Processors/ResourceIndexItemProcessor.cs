using MassTransit;

using MongoDB.Driver;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Provider.Application.Documents;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Providers.Espn;

namespace SportsData.Provider.Application.Processors
{
    public interface IProcessResourceIndexes
    {
        Task Process(ProcessResourceIndexItemCommand command);
    }

    public class ResourceIndexItemProcessor : IProcessResourceIndexes
    {
        private readonly ILogger<ResourceIndexItemProcessor> _logger;
        private readonly IProvideEspnApiData _espnApi;
        private readonly DocumentService _documentService;
        private readonly IBus _bus;
        private readonly IDecodeDocumentProvidersAndTypes _decoder;

        public ResourceIndexItemProcessor(
            ILogger<ResourceIndexItemProcessor> logger,
            IProvideEspnApiData espnApi,
            DocumentService documentService,
            IBus bus,
            IDecodeDocumentProvidersAndTypes decoder)
        {
            _logger = logger;
            _espnApi = espnApi;
            _documentService = documentService;
            _bus = bus;
            _decoder = decoder;
        }

        public async Task Process(ProcessResourceIndexItemCommand command)
        {
            _logger.LogInformation("Started with {@command}", command);

            var type = _decoder.GetType(command.SourceDataProvider, command.DocumentType);

            var dbObjects = _documentService.Database.GetCollection<DocumentBase>(type.Name);

            // get the item's json
            var itemJson = await _espnApi.GetResource(command.Href, true);

            // determine if we have this in the database
            var documentId = command.SeasonYear.HasValue
                ? int.Parse($"{command.Id}{command.SeasonYear.Value}")
                : command.Id;

            var filter = Builders<DocumentBase>.Filter.Eq(x => x.Id, documentId);
            var dbResult = await dbObjects.FindAsync(filter);
            var dbItem = await dbResult.FirstOrDefaultAsync();

            // TODO: break these off into different handlers
            if (dbItem is null)
            {
                // no ?  save it and broadcast event
                await dbObjects.InsertOneAsync(new DocumentBase()
                {
                    Id = documentId,
                    Data = itemJson
                });

                var evt = new DocumentCreated(
                    documentId.ToString(),
                    type.Name,
                    command.Sport,
                    command.DocumentType,
                    command.SourceDataProvider);

                // TODO: Use transactional outbox pattern here
                await _bus.Publish(evt);

                _logger.LogInformation("New document event {@evt}", evt);
            }
            else
            {
                var dbJson = dbItem.ToJson();

                // has it changed ?
                if (string.Compare(itemJson, dbJson, StringComparison.InvariantCultureIgnoreCase) == 0)
                    return;

                // yes: update and broadcast
                await dbObjects.ReplaceOneAsync(filter, new DocumentBase()
                {
                    Id = documentId,
                    Data = itemJson
                });

                var evt = new DocumentUpdated(
                    documentId.ToString(),
                    type.Name,
                    command.Sport,
                    command.DocumentType,
                    command.SourceDataProvider);

                // TODO: Use transactional outbox pattern here
                await _bus.Publish(evt);

                _logger.LogInformation("Document updated event {@evt}", evt);
            }
        }
    }

    public record ProcessResourceIndexItemCommand(
        int Id,
        string Href,
        Sport Sport,
        SourceDataProvider SourceDataProvider,
        DocumentType DocumentType,
        int? SeasonYear = null);
}
