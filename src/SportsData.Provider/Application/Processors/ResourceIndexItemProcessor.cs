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

using System.Diagnostics.Metrics;

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

        private readonly Counter<long> _mongoCacheHitCounter;
        private readonly Counter<long> _espnLiveFetchCounter;

        public ResourceIndexItemProcessor(
            ILogger<ResourceIndexItemProcessor> logger,
            AppDataContext dataContext,
            IProvideEspnApiData espnApi,
            IDocumentStore documentStore,
            IEventBus publisher,
            IJsonHashCalculator jsonHashCalculator,
            IConfiguration commonConfig,
            IGenerateExternalRefIdentities identityGenerator,
            IDocumentInclusionService documentInclusionService,
            IMeterFactory meterFactory)
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

            var meter = meterFactory.Create("SportsData.Provider.Espn");
            _mongoCacheHitCounter = meter.CreateCounter<long>("espn.cache.hit", description: "Documents served from MongoDB cache (no ESPN API call)");
            _espnLiveFetchCounter = meter.CreateCounter<long>("espn.live.fetch", description: "Documents fetched live from ESPN API");
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

                        _mongoCacheHitCounter.Add(1);

                        // For historical data, check if this exact content was already published.
                        // If so, skip — Producer already processed it and re-publishing only creates churn.
                        if (!IsCurrentSeason(command.SeasonYear) &&
                            dbItem.LastPublishedContentHash is not null)
                        {
                            var contentHash = _jsonHashCalculator.NormalizeAndHash(dbItem.Data);
                            if (contentHash == dbItem.LastPublishedContentHash)
                            {
                                _logger.LogInformation(
                                    "ESPN {CacheResult} for {DocumentType}. Content unchanged since last publish, skipping. UrlHash={UrlHash}",
                                    "HIT-SUPPRESSED", command.DocumentType, urlHash);
                                return;
                            }
                        }

                        _logger.LogInformation(
                            "ESPN {CacheResult} for {DocumentType}. UrlHash={UrlHash}",
                            "HIT", command.DocumentType, urlHash);

                        // Publish DocumentCreated with cached data (NO ESPN call!)
                        await PublishDocumentCreatedAsync(command, urlHash, correlationId, dbItem.Data, command.NotifyOnCompletion);

                        // Record the content hash so subsequent requests for this document are suppressed
                        await UpdateLastPublishedHashAsync(command.DocumentType.ToString(), urlHash, dbItem.Data);
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
            string itemJson;

            if (command.InlineJson is not null)
            {
                // Hybrid resource index: ESPN's individual $ref returns 404, but the
                // parent index response included the full item data inline.
                _logger.LogDebug(
                    "Using inline JSON from hybrid resource index. UrlHash={UrlHash}, DocumentType={DocumentType}",
                    urlHash, command.DocumentType);
                itemJson = command.InlineJson;
            }
            else
            {
                _espnLiveFetchCounter.Add(1);
                _logger.LogInformation(
                    "ESPN {CacheResult} for {DocumentType}. UrlHash={UrlHash}, Uri={Uri}",
                    "LIVE", command.DocumentType, urlHash, command.Uri);

                var result = await _espnApi.GetResource(command.Uri.ToCleanUri(), bypassCache: command.BypassCache);

                if (!result.IsSuccess)
                {
                    _logger.LogError(
                        "ESPN request failed: Status={Status}, Uri={Uri}, DocumentType={DocumentType}",
                        result.Status,
                        command.Uri,
                        command.DocumentType);
                    return; // Clean exit - ESPN client already logged details
                }

                itemJson = result.Value; // Guaranteed non-empty and valid JSON
            }

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
                    // Content has genuinely changed — persist the new version and notify downstream.
                    await HandleUpdatedDocumentAsync(command, urlHash, correlationId, collectionName, itemJson);
                }
                else
                {
                    // ESPN returned the same content already stored in MongoDB.
                    // Skip the Mongo replace — there is nothing to update.
                    // Still publish DocumentCreated so downstream processors continue normally.
                    // NOTE: BypassCache=true means "always fetch fresh from ESPN (skip the read-cache
                    // path)". It must NOT force a write when the content is identical — doing so causes
                    // spurious "Mongo replaced document" log noise on every historical sourcing run.
                    _logger.LogDebug(
                        "ESPN fetch returned unchanged content, skipping Mongo replace. " +
                        "UrlHash={UrlHash}, DocumentType={DocumentType}, BypassCache={BypassCache}",
                        urlHash, command.DocumentType, command.BypassCache);

                    await PublishDocumentCreatedAsync(command, urlHash, correlationId, itemJson, command.NotifyOnCompletion);
                    await UpdateLastPublishedHashAsync(collectionName, urlHash, itemJson);
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
            var providerRef = new Uri($"{baseUrl}documents/urlHash/{command.DocumentType}/{urlHash}");

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
                command.MessageId,  // Use parent MessageId as CausationId
                0,
                command.IncludeLinkedDocumentTypes,
                null,  // RequestedDependencies
                command.NotifyOnCompletion);

            await _publisher.Publish(evt);
            await UpdateLastPublishedHashAsync(collectionName, urlHash, json);

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
            var providerRef = new Uri($"{baseUrl}documents/urlHash/{command.DocumentType}/{urlHash}");

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
                command.MessageId,  // Use parent MessageId as CausationId
                0,
                command.IncludeLinkedDocumentTypes,
                null,  // RequestedDependencies
                command.NotifyOnCompletion);

            await _publisher.Publish(evt);
            await UpdateLastPublishedHashAsync(collectionName, urlHash, json);

            _logger.LogInformation("DocumentCreated event published (update). UrlHash={UrlHash}, DocumentType={DocumentType}",
                urlHash,
                command.DocumentType);
        }

        private async Task PublishDocumentCreatedAsync(
            ProcessResourceIndexItemCommand command,
            string urlHash,
            Guid correlationId,
            string jsonFromCache,
            bool notifyOnCompletion)
        {
            // TODO: pull this from CommonConfig and make it available within the class root
            var baseUrl = _commonConfig["CommonConfig:ProviderClientConfig:ApiUrl"];
            var providerRef = new Uri($"{baseUrl}documents/urlHash/{command.DocumentType}/{urlHash}");

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
                command.MessageId,  // Use parent MessageId as CausationId
                0,
                command.IncludeLinkedDocumentTypes,
                null,  // RequestedDependencies
                notifyOnCompletion);

            await _publisher.Publish(evt);

            _logger.LogInformation("DocumentCreated event published (from cache). UrlHash={UrlHash}, DocumentType={DocumentType}",
                urlHash,
                command.DocumentType);
        }

        /// <summary>
        /// Determines whether the given season year represents active/current data that should
        /// always be published (even if content hasn't changed), matching the ShouldBypassCache
        /// logic in ResourceIndexJob.
        /// </summary>
        private bool IsCurrentSeason(int? seasonYear)
        {
            var currentSeasonStr = _commonConfig["CommonConfig:CurrentSeason"];
            if (string.IsNullOrWhiteSpace(currentSeasonStr) || !int.TryParse(currentSeasonStr, out var currentSeason))
                return true; // feature disabled or misconfigured — treat as current (always publish)
            if (currentSeason == 0)
                return true; // feature disabled
            if (!seasonYear.HasValue)
                return true; // non-seasonal resource — always publish
            return seasonYear.Value >= currentSeason;
        }

        private async Task UpdateLastPublishedHashAsync(string collectionName, string urlHash, string json)
        {
            try
            {
                var contentHash = _jsonHashCalculator.NormalizeAndHash(json);
                await _documentStore.UpdateFieldAsync<DocumentBase>(
                    collectionName, urlHash, nameof(DocumentBase.LastPublishedContentHash), contentHash);
            }
            catch (Exception ex)
            {
                // Non-critical — worst case, the next request re-publishes
                _logger.LogWarning(ex,
                    "Failed to update LastPublishedContentHash. UrlHash={UrlHash}, DocumentType={DocumentType}",
                    urlHash, collectionName);
            }
        }

    }

    public record ProcessResourceIndexItemCommand(
        Guid CorrelationId,
        Guid CausationId,
        Guid MessageId,
        Guid ResourceIndexId,
        string Id,
        Uri Uri,
        Sport Sport,
        SourceDataProvider SourceDataProvider,
        DocumentType DocumentType,
        string? ParentId,
        int? SeasonYear = null,
        bool BypassCache = false,
        IReadOnlyCollection<DocumentType>? IncludeLinkedDocumentTypes = null,
        bool NotifyOnCompletion = false,
        string? InlineJson = null);
}
