using MassTransit;

using Microsoft.EntityFrameworkCore;

using MongoDB.Driver;

using SportsData.Core.Common;
using SportsData.Core.Common.Parsing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Processing;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Data.Entities;
using SportsData.Provider.Infrastructure.Providers.Espn;

using System.Collections;

namespace SportsData.Provider.Application.Processors
{
    public interface IProcessResourceIndexItems
    {
        Task Process(ProcessResourceIndexItemCommand command);
    }

    public class ResourceIndexItemProcessor : IProcessResourceIndexItems
    {
        private readonly ILogger<ResourceIndexItemProcessor> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IProvideEspnApiData _espnApi;
        private readonly IDocumentStore _documentStore;
        private readonly IBus _bus;
        private readonly IDecodeDocumentProvidersAndTypes _decoder;
        private readonly IProvideHashes _hashProvider;
        private readonly IResourceIndexItemParser _resourceIndexItemParser;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public ResourceIndexItemProcessor(
            ILogger<ResourceIndexItemProcessor> logger,
            AppDataContext dataContext,
            IProvideEspnApiData espnApi,
            IDocumentStore documentStore,
            IBus bus,
            IDecodeDocumentProvidersAndTypes decoder,
            IProvideHashes hashProvider,
            IResourceIndexItemParser resourceIndexItemParser,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _espnApi = espnApi;
            _documentStore = documentStore;
            _bus = bus;
            _decoder = decoder;
            _hashProvider = hashProvider;
            _resourceIndexItemParser = resourceIndexItemParser;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task Process(ProcessResourceIndexItemCommand command)
        {
            var correlationId = Guid.NewGuid();
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = correlationId
                   }))
            {
                _logger.LogInformation("Started with {@command}", command);
                await ProcessInternal(command, correlationId);
            }
        }

        private async Task ProcessInternal(ProcessResourceIndexItemCommand command, Guid correlationId)
        {
            var urlHash = _hashProvider.GenerateHashFromUrl(command.Href);

            var resourceIndexItemEntity = await _dataContext.ResourceIndexItems
                .Where(x => x.ResourceIndexId == command.resourceIndexId &&
                            x.OriginalUrlHash == urlHash)
                .FirstOrDefaultAsync();

            if (resourceIndexItemEntity is not null)
            {
                // has it been retrieved with X units?
                // TODO: Move to config for check duration
                if (resourceIndexItemEntity.LastAccessed > DateTime.UtcNow.AddDays(-7))
                {
                    // log something
                    return;
                }
                resourceIndexItemEntity.LastAccessed = DateTime.UtcNow;
                resourceIndexItemEntity.ModifiedUtc = DateTime.UtcNow;
                resourceIndexItemEntity.ModifiedBy = Guid.Empty;
            }
            else
            {
                // create a record for this access
                await _dataContext.ResourceIndexItems.AddAsync(new ResourceIndexItem()
                {
                    Id = Guid.NewGuid(),
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.Empty,
                    Url = command.Href,
                    OriginalUrlHash = urlHash,
                    ResourceIndexId = command.resourceIndexId,
                    LastAccessed = DateTime.UtcNow
                });
            }

            await HandleValid(command, correlationId);

            await _dataContext.SaveChangesAsync();
        }

        private async Task HandleValid(ProcessResourceIndexItemCommand command, Guid correlationId)
        {
            var type = _decoder.GetTypeAndCollectionName(command.SourceDataProvider, command.Sport, command.DocumentType, command.SeasonYear);
            
            // get the item's json
            var itemJson = await _espnApi.GetResource(command.Href, true);

            // determine if we have this in the database
            var documentId = command.SeasonYear.HasValue
                ? long.Parse($"{command.Id}{command.SeasonYear.Value}")
                : command.Id;

            var dbItem = await _documentStore
                .GetFirstOrDefaultAsync<DocumentBase>(type.CollectionName, x => x.Id == documentId);

            // TODO: break these off into different handlers
            if (dbItem is null)
            {
                // no ?  save it and broadcast event
                await _documentStore.InsertOneAsync(type.CollectionName, new DocumentBase()
                {
                    Id = documentId,
                    Data = itemJson,
                    Sport = command.Sport,
                    DocumentType = command.DocumentType,
                    SourceDataProvider = command.SourceDataProvider
                });

                var evt = new DocumentCreated(
                    documentId.ToString(),
                    type.CollectionName,
                    command.Sport,
                    command.SeasonYear,
                    command.DocumentType,
                    command.SourceDataProvider,
                    correlationId,
                    CausationId.Provider.ResourceIndexItemProcessor);

                // TODO: Use transactional outbox pattern here?
                await _bus.Publish(evt);

                _logger.LogInformation("New document event published {@evt}", evt);
            }
            else
            {
                var evt = new DocumentCreated(
                    documentId.ToString(),
                    type.CollectionName,
                    command.Sport,
                    command.SeasonYear,
                    command.DocumentType,
                    command.SourceDataProvider,
                    correlationId,
                    CausationId.Provider.ResourceIndexItemProcessor);

                // TODO: Use transactional outbox pattern here?
                await _bus.Publish(evt);
                //await HandleExisting(dbItem, itemJson, dbObjects, filter, documentId, type.CollectionName, command, correlationId);
            }

            // Dive N-Level deep into any contained links and generate an event for it to be processed?
            // if so, we should specify the depth level on the event. processor can then be configured to not go too deep
            //if (command.Level == 0)
            //    await ProcessEmbeddedLinks(command, itemJson);
            
        }

        private async Task HandleExisting(DocumentBase dbItem,
            string itemJson,
            IMongoCollection<DocumentBase> dbObjects,
            FilterDefinition<DocumentBase> filter,
            long documentId,
            string collectionName,
            ProcessResourceIndexItemCommand command,
            Guid correlationId)
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
                collectionName,
                command.Sport,
                command.DocumentType,
                command.SourceDataProvider,
                correlationId,
                CausationId.Provider.ResourceIndexItemProcessor);

            // TODO: Use transactional outbox pattern here
            await _bus.Publish(evt);

            _logger.LogInformation("Document updated event {@evt}", evt);
        }

        private async Task ProcessEmbeddedLinks(ProcessResourceIndexItemCommand parentCommand, string itemJson)
        {
            var links = _resourceIndexItemParser.ExtractEmbeddedLinks(itemJson);

            //foreach (var link in links.Select(x =>
            //        new ProcessResourceIndexItemCommand(Guid.Empty, parentCommand.Level + 1,
            //            0, x.AbsoluteUri, parentCommand.Sport, parentCommand.SourceDataProvider,
            //            parentCommand.DocumentType, parentCommand.SeasonYear))
                        
                        

            await Task.CompletedTask;
        }
    }

    public record ProcessResourceIndexItemCommand(
        Guid resourceIndexId,
        int Level,
        int Id,
        string Href,
        Sport Sport,
        SourceDataProvider SourceDataProvider,
        DocumentType DocumentType,
        int? SeasonYear = null);
}
