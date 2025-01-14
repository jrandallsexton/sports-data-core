using MassTransit;

using MongoDB.Driver;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Provider.Application.Jobs.Definitions;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Providers.Espn;

namespace SportsData.Provider.Application.Jobs
{
    public interface IProvideDocuments : IAmARecurringJob { }

    public class DocumentProviderJob<TDocumentJobDefinition> : IProvideDocuments where TDocumentJobDefinition : DocumentJobDefinition
    {
        private readonly ILogger<DocumentProviderJob<TDocumentJobDefinition>> _logger;
        private readonly DocumentJobDefinition _jobDefinition;
        private readonly IProvideEspnApiData _espnApi;
        private readonly DocumentService _documentService;
        private readonly IBus _bus;
        
        public DocumentProviderJob(
            TDocumentJobDefinition documentJobDefinition,
            ILogger<DocumentProviderJob<TDocumentJobDefinition>> logger,
            IProvideEspnApiData espnApi,
            DocumentService documentService,
            IBus bus)
        {
            _logger = logger;
            _jobDefinition = documentJobDefinition;
            _espnApi = espnApi;
            _documentService = documentService;
            _bus = bus;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation($"Started {nameof(DocumentProviderJob<TDocumentJobDefinition>)}");

            // Get the resource index
            var resourceIndex = await _espnApi.GetResourceIndex(_jobDefinition.Endpoint, _jobDefinition.EndpointMask);

            foreach (var item in resourceIndex.items)
            {
                // get the item's json
                var itemJson = await _espnApi.GetResource(item.href, true);
                
                // determine if we have this in the database
                var type = GetType(_jobDefinition.SourceDataProvider, _jobDefinition.DocumentType);

                var dbObjects = _documentService.Database.GetCollection<DocumentBase>(type.Name);
                var filter = Builders<DocumentBase>.Filter.Eq(x => x.Id, item.id);
                var dbResult = await dbObjects.FindAsync(filter);
                var dbItem = await dbResult.FirstOrDefaultAsync();

                if (dbItem is null)
                {
                    // no ?  save it and broadcast event
                    await dbObjects.InsertOneAsync(new DocumentBase()
                    {
                        Id = item.id,
                        Data = itemJson
                    });

                    var evt = new DocumentCreated()
                    {
                        Id = item.id.ToString(),
                        Name = type.Name,
                        SourceDataProvider = _jobDefinition.SourceDataProvider,
                        DocumentType = _jobDefinition.DocumentType
                    };
                    await _bus.Publish(evt);
                    _logger.LogInformation("New document event {@evt}", evt);
                }
                else
                {
                    var dbJson = dbItem.ToJson();

                    // has it changed ?
                    if (string.Compare(itemJson, dbJson, StringComparison.InvariantCultureIgnoreCase) == 0)
                        continue;

                    // yes: update and broadcast
                    await dbObjects.ReplaceOneAsync(filter, new DocumentBase()
                    {
                        Id = item.id,
                        Data = itemJson
                    });
                    var evt = new DocumentUpdated()
                    {
                        Id = item.id.ToString(),
                        Name = type.Name,
                        SourceDataProvider = _jobDefinition.SourceDataProvider,
                        DocumentType = _jobDefinition.DocumentType
                    };
                    await _bus.Publish(evt);
                    _logger.LogInformation("Document updated event {@evt}", evt);
                }
            }
        }

        private static Type GetType(SourceDataProvider sourceDataProvider, DocumentType docType)
        {
            switch (docType)
            {
                case DocumentType.Franchise:
                    return typeof(EspnFranchiseDto);
                case DocumentType.Athlete:
                case DocumentType.Award:
                case DocumentType.Event:
                case DocumentType.GameSummary:
                case DocumentType.Scoreboard:
                case DocumentType.Season:
                case DocumentType.Team:
                case DocumentType.TeamInformation:
                case DocumentType.Venue:
                case DocumentType.Weeks:
                default:
                    throw new ArgumentOutOfRangeException(nameof(docType), docType, null);
            }
        }
    }
}
