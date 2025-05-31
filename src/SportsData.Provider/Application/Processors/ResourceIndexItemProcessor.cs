using MassTransit;

using Microsoft.EntityFrameworkCore;

using MongoDB.Driver;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Common.Parsing;
using SportsData.Core.Common.Routing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Processing;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Data.Entities;
using SportsData.Provider.Infrastructure.Providers.Espn;

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
        private readonly IGenerateRoutingKeys _routingKeyGenerator;
        private readonly IJsonHashCalculator _jsonHashCalculator;

        public ResourceIndexItemProcessor(
            ILogger<ResourceIndexItemProcessor> logger,
            AppDataContext dataContext,
            IProvideEspnApiData espnApi,
            IDocumentStore documentStore,
            IBus bus,
            IDecodeDocumentProvidersAndTypes decoder,
            IProvideHashes hashProvider,
            IResourceIndexItemParser resourceIndexItemParser,
            IProvideBackgroundJobs backgroundJobProvider,
            IGenerateRoutingKeys routingKeyGenerator,
            IJsonHashCalculator jsonHashCalculator)
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
            _routingKeyGenerator = routingKeyGenerator;
            _jsonHashCalculator = jsonHashCalculator;
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

        private async Task ProcessInternal(
            ProcessResourceIndexItemCommand command,
            Guid correlationId)
        {
            var urlHash = _hashProvider.GenerateHashFromUrl(command.Href);

            var now = DateTime.UtcNow;

            var resourceIndexItemEntity = await _dataContext.ResourceIndexItems
                .Where(x => x.ResourceIndexId == command.ResourceIndexId &&
                            x.OriginalUrlHash == urlHash)
                .FirstOrDefaultAsync();

            if (resourceIndexItemEntity is not null)
            {
                resourceIndexItemEntity.LastAccessed = now;
                resourceIndexItemEntity.ModifiedUtc = now;
                resourceIndexItemEntity.ModifiedBy = Guid.Empty;
            }
            else
            {
                await _dataContext.ResourceIndexItems.AddAsync(new ResourceIndexItem()
                {
                    Id = Guid.NewGuid(),
                    CreatedUtc = now,
                    CreatedBy = Guid.Empty,
                    Url = command.Href,
                    OriginalUrlHash = urlHash,
                    ResourceIndexId = command.ResourceIndexId,
                    LastAccessed = now
                });
            }

            await HandleValid(command, urlHash, correlationId);
            await _dataContext.SaveChangesAsync();
        }


        private async Task HandleValid(ProcessResourceIndexItemCommand command, string urlHash, Guid correlationId)
        {
            var type = _decoder.GetTypeAndCollectionName(command.SourceDataProvider, command.Sport, command.DocumentType, command.SeasonYear);
            var documentId = command.SeasonYear.HasValue
                ? long.Parse($"{command.Id}{command.SeasonYear.Value}")
                : command.Id;

            var documentKey = documentId.ToString();
            var itemJson = await _espnApi.GetResource(command.Href, true);
            var dbItem = await _documentStore.GetFirstOrDefaultAsync<DocumentBase>(type.CollectionName, x => x.Id == documentKey);

            if (dbItem is null)
            {
                await HandleNewDocumentAsync(command, urlHash, correlationId, type.CollectionName, documentKey, itemJson);
            }
            else
            {
                var newHash = _jsonHashCalculator.NormalizeAndHash(itemJson);
                var currentHash = _jsonHashCalculator.NormalizeAndHash(dbItem.Data);

                if (newHash != currentHash)
                {
                    await HandleUpdatedDocumentAsync(command, urlHash, correlationId, type.CollectionName, documentKey, itemJson);
                }
            }
        }

        private async Task HandleNewDocumentAsync(
            ProcessResourceIndexItemCommand command,
            string urlHash,
            Guid correlationId,
            string collectionName,
            string documentId,
            string json)
        {
            var document = new DocumentBase
            {
                Id = documentId,
                Data = json,
                Sport = command.Sport,
                DocumentType = command.DocumentType,
                SourceDataProvider = command.SourceDataProvider,
                Url = command.Href,
                UrlHash = urlHash
            };

            await _documentStore.InsertOneAsync(collectionName, document);

            var evt = new DocumentCreated(
                documentId,
                collectionName,
                _routingKeyGenerator.Generate(command.SourceDataProvider, command.Href),
                urlHash,
                command.Sport,
                command.SeasonYear,
                command.DocumentType,
                command.SourceDataProvider,
                correlationId,
                CausationId.Provider.ResourceIndexItemProcessor);

            await _bus.Publish(evt);
            _logger.LogInformation("DocumentCreated event published {@evt}", evt);
        }

        private async Task HandleUpdatedDocumentAsync(
            ProcessResourceIndexItemCommand command,
            string urlHash,
            Guid correlationId,
            string collectionName,
            string documentId,
            string json)
        {
            var document = new DocumentBase
            {
                Id = documentId,
                Data = json,
                Sport = command.Sport,
                DocumentType = command.DocumentType,
                SourceDataProvider = command.SourceDataProvider,
                Url = command.Href,
                UrlHash = urlHash
            };

            await _documentStore.ReplaceOneAsync(collectionName, documentId, document);

            var evt = new DocumentUpdated(
                documentId,
                collectionName,
                _routingKeyGenerator.Generate(command.SourceDataProvider, command.Href),
                urlHash,
                command.Sport,
                command.SeasonYear,
                command.DocumentType,
                command.SourceDataProvider,
                correlationId,
                CausationId.Provider.ResourceIndexItemProcessor);

            await _bus.Publish(evt);
            _logger.LogInformation("DocumentUpdated event published {@evt}", evt);
        }
    }

    public record ProcessResourceIndexItemCommand(
        Guid ResourceIndexId,
        int Id,
        string Href,
        Sport Sport,
        SourceDataProvider SourceDataProvider,
        DocumentType DocumentType,
        int? SeasonYear = null);
}
