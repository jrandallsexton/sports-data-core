using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Common.Parsing;
using SportsData.Core.Common.Routing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
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
        private readonly IPublishEndpoint _publisher;
        private readonly IDecodeDocumentProvidersAndTypes _decoder;
        private readonly IResourceIndexItemParser _resourceIndexItemParser;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IGenerateRoutingKeys _routingKeyGenerator;
        private readonly IJsonHashCalculator _jsonHashCalculator;

        public ResourceIndexItemProcessor(
            ILogger<ResourceIndexItemProcessor> logger,
            AppDataContext dataContext,
            IProvideEspnApiData espnApi,
            IDocumentStore documentStore,
            IPublishEndpoint publisher,
            IDecodeDocumentProvidersAndTypes decoder,
            IResourceIndexItemParser resourceIndexItemParser,
            IProvideBackgroundJobs backgroundJobProvider,
            IGenerateRoutingKeys routingKeyGenerator,
            IJsonHashCalculator jsonHashCalculator)
        {
            _logger = logger;
            _dataContext = dataContext;
            _espnApi = espnApi;
            _documentStore = documentStore;
            _publisher = publisher;
            _decoder = decoder;
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
            var urlHash = HashProvider.GenerateHashFromUri(command.Uri);
            var now = DateTime.UtcNow;

            var resourceIndexItemEntity = await _dataContext.ResourceIndexItems
                .Where(x => x.ResourceIndexId == command.ResourceIndexId &&
                            x.UrlHash == urlHash)
                .FirstOrDefaultAsync();

            if (resourceIndexItemEntity is not null)
            {
                resourceIndexItemEntity.LastAccessed = now;
                resourceIndexItemEntity.ModifiedUtc = now;
                resourceIndexItemEntity.ModifiedBy = Guid.Empty;
            }
            else
            {
                await _dataContext.ResourceIndexItems.AddAsync(new ResourceIndexItem
                {
                    Id = Guid.NewGuid(),
                    CreatedUtc = now,
                    CreatedBy = Guid.Empty,
                    Uri = command.Uri,
                    UrlHash = urlHash,
                    ResourceIndexId = command.ResourceIndexId,
                    LastAccessed = now
                });
            }

            await HandleValid(command, urlHash, correlationId);
            await _dataContext.SaveChangesAsync();
        }

        private async Task HandleValid(ProcessResourceIndexItemCommand command, string urlHash, Guid correlationId)
        {
            var collectionName = command.Sport.ToString();

            var itemJson = await _espnApi.GetResource(command.Uri.ToCleanUrl(), true);

            var dbItem = await _documentStore
                .GetFirstOrDefaultAsync<DocumentBase>(collectionName, x => x.Id == urlHash);

            if (dbItem is null)
            {
                await HandleNewDocumentAsync(command, urlHash, correlationId, collectionName, itemJson);
            }
            else
            {
                var newHash = _jsonHashCalculator.NormalizeAndHash(itemJson);
                var currentHash = _jsonHashCalculator.NormalizeAndHash(dbItem.Data);

                if (newHash != currentHash)
                {
                    await HandleUpdatedDocumentAsync(command, urlHash, correlationId, collectionName, itemJson);
                }
            }
        }

        private async Task HandleNewDocumentAsync(
            ProcessResourceIndexItemCommand command,
            string urlHash,
            Guid correlationId,
            string collectionName,
            string json)
        {
            var document = new DocumentBase
            {
                Id = urlHash,
                Data = json,
                Sport = command.Sport,
                DocumentType = command.DocumentType,
                SourceDataProvider = command.SourceDataProvider,
                Uri = command.Uri,
                UrlHash = urlHash,
                RoutingKey = urlHash.Substring(0, 3).ToUpperInvariant()
            };

            await _documentStore.InsertOneAsync(collectionName, document);

            _logger.LogInformation("Persisted document {Document}", document);

            var evt = new DocumentCreated(
                urlHash,
                command.ParentId,
                collectionName,
                _routingKeyGenerator.Generate(command.SourceDataProvider, command.Uri),
                urlHash,
                command.Sport,
                command.SeasonYear,
                command.DocumentType,
                command.SourceDataProvider,
                correlationId,
                CausationId.Provider.ResourceIndexItemProcessor);

            await _publisher.Publish(evt);
            _logger.LogInformation("DocumentCreated event published {@evt}", evt);
        }

        private async Task HandleUpdatedDocumentAsync(
            ProcessResourceIndexItemCommand command,
            string urlHash,
            Guid correlationId,
            string collectionName,
            string json)
        {
            var document = new DocumentBase
            {
                Id = urlHash, // ✅ Use UrlHash consistently
                Data = json,
                Sport = command.Sport,
                DocumentType = command.DocumentType,
                SourceDataProvider = command.SourceDataProvider,
                Uri = command.Uri,
                UrlHash = urlHash,
                RoutingKey = urlHash.Substring(0, 3).ToUpperInvariant()
            };

            await _documentStore.ReplaceOneAsync(collectionName, urlHash, document);

            var evt = new DocumentUpdated(
                urlHash,
                command.ParentId,
                collectionName,
                _routingKeyGenerator.Generate(command.SourceDataProvider, command.Uri),
                urlHash,
                command.Sport,
                command.SeasonYear,
                command.DocumentType,
                command.SourceDataProvider,
                correlationId,
                CausationId.Provider.ResourceIndexItemProcessor);

            await _publisher.Publish(evt);
            _logger.LogInformation("DocumentUpdated event published {@evt}", evt);
        }

    }

    public record ProcessResourceIndexItemCommand(
        Guid ResourceIndexId,
        int Id,
        Uri Uri,
        Sport Sport,
        SourceDataProvider SourceDataProvider,
        DocumentType DocumentType,
        string? ParentId,
        int? SeasonYear = null);
}
