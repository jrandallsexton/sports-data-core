using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Provider.Application.Services;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Data.Entities;

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
        private readonly IEventBus _publisher;
        private readonly IJsonHashCalculator _jsonHashCalculator;
        private readonly IConfiguration _commonConfig;
        private readonly IGenerateExternalRefIdentities _identityGenerator;
        private readonly IDocumentInclusionService _documentInclusionService;

        public ResourceIndexItemProcessor(
            ILogger<ResourceIndexItemProcessor> logger,
            AppDataContext dataContext,
            IProvideEspnApiData espnApi,
            IDocumentStore documentStore,
            IEventBus publisher,
            IJsonHashCalculator jsonHashCalculator,
            IConfiguration commonConfig,
            IGenerateExternalRefIdentities identityGenerator,
            IDocumentInclusionService documentInclusionService)
        {
            _logger = logger;
            _dataContext = dataContext;
            _espnApi = espnApi;
            _documentStore = documentStore;
            _publisher = publisher;
            _jsonHashCalculator = jsonHashCalculator;
            _commonConfig = commonConfig;
            _identityGenerator = identityGenerator;
            _documentInclusionService = documentInclusionService;
        }

        public async Task Process(ProcessResourceIndexItemCommand command)
        {
            // ✅ Use the correlationId from the command instead of generating new one
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = command.CorrelationId,
                ["CausationId"] = command.CausationId,
                ["DocumentType"] = command.DocumentType,
                ["SourceUrlHash"] = command.Id
            }))
            {
                _logger.LogInformation(
                    "Processing resource index item. Uri={Uri}, DocumentType={DocumentType}",
                    command.Uri, command.DocumentType);
                
                await ProcessInternal(command, command.CorrelationId);
            }
        }

        private async Task ProcessInternal(
            ProcessResourceIndexItemCommand command,
            Guid correlationId)
        {
            var identity = _identityGenerator.Generate(command.Uri);
            var now = DateTime.UtcNow;

            try
            {
                // ✅ ONLY track ResourceIndexItems if they belong to an actual ResourceIndex job
                // Ad-hoc document requests (from DocumentRequested events) should NOT create ResourceIndexItems
                if (command.ResourceIndexId != Guid.Empty)
                {
                    var resourceIndexItemEntity = await _dataContext.ResourceIndexItems
                        .Where(x => x.Id == identity.CanonicalId ||
                                   (x.ResourceIndexId == command.ResourceIndexId && x.SourceUrlHash == identity.UrlHash))
                        .FirstOrDefaultAsync();

                    if (resourceIndexItemEntity is not null)
                    {
                        resourceIndexItemEntity.LastAccessed = now;
                        resourceIndexItemEntity.ModifiedUtc = now;
                        resourceIndexItemEntity.ModifiedBy = Guid.Empty;
                    }
                    else
                    {
                        resourceIndexItemEntity = new ResourceIndexItem
                        {
                            Id = identity.CanonicalId,
                            CreatedUtc = now,
                            CreatedBy = Guid.Empty,
                            Uri = command.Uri,
                            SourceUrlHash = identity.UrlHash,
                            ResourceIndexId = command.ResourceIndexId,
                            LastAccessed = now
                        };
                        await _dataContext.ResourceIndexItems.AddAsync(resourceIndexItemEntity);
                    }
                }
                else
                {
                    _logger.LogDebug(
                        "Skipping ResourceIndexItem creation for ad-hoc request. Uri={Uri}, CorrelationId={CorrelationId}",
                        command.Uri,
                        correlationId);
                }

                await HandleValid(command, identity.UrlHash, correlationId);

                // Only save if we have changes (ResourceIndexItem tracking or outbox messages)
                if (_dataContext.ChangeTracker.HasChanges())
                {
                    await _dataContext.SaveChangesAsync();
                }
            }
            catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
            {
                // This should only happen now if ResourceIndexId != Guid.Empty
                _logger.LogWarning(ex, "Duplicate key detected for {Uri}. Retrying as update.", command.Uri);

                // Clear change tracker to avoid pollution from failed insert and outbox messages
                _dataContext.ChangeTracker.Clear();

                // Retry as update only - fetch by unique constraint to be sure
                var existing = await _dataContext.ResourceIndexItems
                    .Where(x => x.ResourceIndexId == command.ResourceIndexId && x.SourceUrlHash == identity.UrlHash)
                    .FirstOrDefaultAsync();

                if (existing != null)
                {
                    existing.LastAccessed = now;
                    existing.ModifiedUtc = now;
                    existing.ModifiedBy = Guid.Empty;
                    _dataContext.ResourceIndexItems.Update(existing);

                    // Re-run business logic to re-queue outbox messages
                    await HandleValid(command, identity.UrlHash, correlationId);

                    await _dataContext.SaveChangesAsync();
                }
                else
                {
                    // Should not happen if 23505 was thrown, but rethrow if we can't find it
                    throw;
                }
            }
        }

        private async Task HandleValid(
            ProcessResourceIndexItemCommand command,
            string urlHash,
            Guid correlationId)
        {
            var collectionName = command.DocumentType.ToString();

            // Check MongoDB FIRST to avoid unnecessary ESPN API calls
            var dbItem = await _documentStore
                .GetFirstOrDefaultAsync<DocumentBase>(collectionName, x => x.Id == urlHash);

            // If document exists in MongoDB and we're not bypassing cache, use cached version
            if (dbItem is not null && !command.BypassCache)
            {
                // Validate cached data before trusting it
                if (string.IsNullOrWhiteSpace(dbItem.Data))
                {
                    _logger.LogWarning(
                        "Cached document has null/empty data, falling back to ESPN fetch. UrlHash={UrlHash}, DocumentType={DocumentType}",
                        urlHash,
                        command.DocumentType);
                }
                else
                {
                    try
                    {
                        // Verify it's parseable JSON
                        System.Text.Json.JsonDocument.Parse(dbItem.Data).Dispose();
                        
                        _logger.LogInformation(
                            "Document found in MongoDB cache, using cached version. UrlHash={UrlHash}, DocumentType={DocumentType}",
                            urlHash,
                            command.DocumentType);
                        
                        // Publish DocumentCreated with cached data (NO ESPN call!)
                        await PublishDocumentCreatedAsync(command, urlHash, correlationId, dbItem.Data);
                        return;
                    }
                    catch (System.Text.Json.JsonException ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Cached document has invalid JSON, falling back to ESPN fetch. UrlHash={UrlHash}, DocumentType={DocumentType}",
                            urlHash,
                            command.DocumentType);
                    }
                }
                
                // Fall through to ESPN fetch if validation failed
            }

            // Either document doesn't exist OR we're bypassing cache - fetch from ESPN
            var itemJson = await _espnApi.GetResource(command.Uri.ToCleanUri(), bypassCache: command.BypassCache);

            if (dbItem is null)
            {
                await HandleNewDocumentAsync(command, urlHash, correlationId, collectionName, itemJson);
            }
            else
            {
                var newHash = _jsonHashCalculator.NormalizeAndHash(itemJson);
                var currentHash = _jsonHashCalculator.NormalizeAndHash(dbItem.Data);

                if (newHash != currentHash || command.BypassCache)
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
                SourceUrlHash = urlHash,
                RoutingKey = urlHash.Substring(0, 3).ToUpperInvariant()
            };

            await _documentStore.InsertOneAsync(collectionName, document);

            _logger.LogInformation("Persisted document {Document}", document);

            // TODO: pull this from CommonConfig and make it available within the class root
            var baseUrl = _commonConfig["CommonConfig:ProviderClientConfig:ApiUrl"];
            var providerRef = new Uri($"{baseUrl}documents/{urlHash}");

            // Use the DocumentInclusionService to determine if JSON should be included
            var jsonDoc = _documentInclusionService.GetIncludableJson(json);

            var evt = new DocumentCreated(
                urlHash,
                command.ParentId,
                collectionName,
                providerRef,
                command.Uri,
                jsonDoc,
                urlHash,
                command.Sport,
                command.SeasonYear,
                command.DocumentType,
                command.SourceDataProvider,
                correlationId,
                CausationId.Provider.ResourceIndexItemProcessor,
                0,
                command.IncludeLinkedDocumentTypes);

            await _publisher.Publish(evt);

            _logger.LogInformation("DocumentCreated event published. UrlHash={UrlHash}, DocumentType={DocumentType}", 
                urlHash, 
                command.DocumentType);
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
                Id = urlHash, // ✅ Use SourceUrlHash consistently
                Data = json,
                Sport = command.Sport,
                DocumentType = command.DocumentType,
                SourceDataProvider = command.SourceDataProvider,
                Uri = command.Uri,
                SourceUrlHash = urlHash,
                RoutingKey = urlHash.Substring(0, 3).ToUpperInvariant()
            };

            await _documentStore.ReplaceOneAsync(collectionName, urlHash, document);

            // TODO: pull this from CommonConfig and make it available within the class root
            var baseUrl = _commonConfig["CommonConfig:ProviderClientConfig:ApiUrl"];
            var providerRef = new Uri($"{baseUrl}documents/{urlHash}");

            // Use the DocumentInclusionService to determine if JSON should be included
            var jsonDoc = _documentInclusionService.GetIncludableJson(json);

            // TODO: Put this back to DocumentUpdated (need the handler in Producer first)
            var evt = new DocumentCreated(
                urlHash,
                command.ParentId,
                collectionName,
                providerRef,
                command.Uri,
                jsonDoc,
                urlHash,
                command.Sport,
                command.SeasonYear,
                command.DocumentType,
                command.SourceDataProvider,
                correlationId,
                CausationId.Provider.ResourceIndexItemProcessor,
                0,
                command.IncludeLinkedDocumentTypes);

            await _publisher.Publish(evt);

            _logger.LogInformation("DocumentCreated event published (update). UrlHash={UrlHash}, DocumentType={DocumentType}", 
                urlHash, 
                command.DocumentType);
        }

        private async Task PublishDocumentCreatedAsync(
            ProcessResourceIndexItemCommand command,
            string urlHash,
            Guid correlationId,
            string jsonFromCache)
        {
            // TODO: pull this from CommonConfig and make it available within the class root
            var baseUrl = _commonConfig["CommonConfig:ProviderClientConfig:ApiUrl"];
            var providerRef = new Uri($"{baseUrl}documents/{urlHash}");

            // Use the DocumentInclusionService to determine if JSON should be included
            var jsonDoc = _documentInclusionService.GetIncludableJson(jsonFromCache);

            var evt = new DocumentCreated(
                urlHash,
                command.ParentId,
                command.DocumentType.ToString(),
                providerRef,
                command.Uri,
                jsonDoc,
                urlHash,
                command.Sport,
                command.SeasonYear,
                command.DocumentType,
                command.SourceDataProvider,
                correlationId,
                CausationId.Provider.ResourceIndexItemProcessor,
                0,
                command.IncludeLinkedDocumentTypes);

            await _publisher.Publish(evt);

            _logger.LogInformation("DocumentCreated event published (from cache). UrlHash={UrlHash}, DocumentType={DocumentType}", 
                urlHash, 
                command.DocumentType);
        }

    }

    public record ProcessResourceIndexItemCommand(
        Guid CorrelationId,
        Guid CausationId,
        Guid ResourceIndexId,
        string Id,
        Uri Uri,
        Sport Sport,
        SourceDataProvider SourceDataProvider,
        DocumentType DocumentType,
        string? ParentId,
        int? SeasonYear = null,
        bool BypassCache = false,
        IReadOnlyCollection<DocumentType>? IncludeLinkedDocumentTypes = null);
}
