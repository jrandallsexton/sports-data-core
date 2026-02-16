using Hangfire;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Jobs.Definitions;
using SportsData.Provider.Application.Processors;
using SportsData.Provider.Infrastructure.Data;

namespace SportsData.Provider.Application.Jobs
{
    [DisableConcurrentExecution(300)] // 5 minutes (outer gate)
    public class ResourceIndexJob : IProcessResourceIndexes
    {
        private readonly ILogger<ResourceIndexJob> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IProvideEspnApiData _espnApi;
        private readonly IDecodeDocumentProvidersAndTypes _decoder;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        private const int PageSize = 250;

        public ResourceIndexJob(
            ILogger<ResourceIndexJob> logger,
            AppDataContext dataContext,
            IProvideEspnApiData espnApi,
            IDecodeDocumentProvidersAndTypes decoder,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _espnApi = espnApi;
            _decoder = decoder;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task ExecuteAsync(DocumentJobDefinition jobDefinition)
        {
            // Get the historical sourcing correlationId from CreatedBy
            var correlationId = await _dataContext.ResourceIndexJobs
                .Where(x => x.Id == jobDefinition.ResourceIndexId)
                .Select(x => x.CreatedBy)
                .FirstOrDefaultAsync();
            
            // Fall back to ResourceIndexId if not set (for recurring jobs)
            if (correlationId == Guid.Empty)
                correlationId = jobDefinition.ResourceIndexId;

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["TierName"] = jobDefinition.DocumentType,
                ["SeasonYear"] = jobDefinition.SeasonYear ?? 0,
                ["ResourceIndexId"] = jobDefinition.ResourceIndexId
            }))
            {
                // ✅ Check if upstream tiers completed successfully (for historical sourcing only)
                if (correlationId != jobDefinition.ResourceIndexId) // Historical sourcing detected
                {
                    var shouldCancel = await ShouldCancelDueToUpstreamFailureAsync(correlationId, jobDefinition.DocumentType);
                    if (shouldCancel)
                    {
                        _logger.LogError(
                            "TIER_CANCELLED: Upstream tier failed. Cancelling tier. Tier={Tier}, SeasonYear={Season}",
                            jobDefinition.DocumentType, jobDefinition.SeasonYear);
                        
                        // Mark this job as cancelled/failed
                        await _dataContext.ResourceIndexJobs
                            .Where(x => x.Id == jobDefinition.ResourceIndexId)
                            .ExecuteUpdateAsync(s => s
                                .SetProperty(x => x.IsQueued, _ => false)
                                .SetProperty(x => x.IsEnabled, _ => false));
                        
                        return;
                    }
                }
                
                _logger.LogInformation(
                    "TIER_STARTED: Tier={Tier}, SeasonYear={Season}, Shape={Shape}, ResourceIndexId={ResourceIndexId}",
                    jobDefinition.DocumentType, jobDefinition.SeasonYear, 
                    jobDefinition.Shape, jobDefinition.ResourceIndexId);
                
                var startTime = DateTime.UtcNow;
                await ExecuteInternal(jobDefinition, correlationId);
                var duration = DateTime.UtcNow - startTime;
                
                _logger.LogInformation(
                    "TIER_COMPLETED: Tier={Tier}, SeasonYear={Season}, DurationMin={DurationMin:F2}, " +
                    "ResourceIndexId={ResourceIndexId}",
                    jobDefinition.DocumentType, jobDefinition.SeasonYear, 
                    duration.TotalMinutes, jobDefinition.ResourceIndexId);
            }
        }

        /// <summary>
        /// Checks if this tier should be cancelled due to upstream tier failures or incomplete processing.
        /// Only applies to historical sourcing runs (where CreatedBy contains a correlation ID).
        /// </summary>
        /// <param name="correlationId">The correlation ID for this historical sourcing run</param>
        /// <param name="currentTier">The current tier being evaluated</param>
        /// <returns>True if the tier should be cancelled, false if it can proceed</returns>
        private async Task<bool> ShouldCancelDueToUpstreamFailureAsync(Guid correlationId, DocumentType currentTier)
        {
            // Define tier dependencies
            var upstreamTiers = currentTier switch
            {
                DocumentType.Venue => new[] { DocumentType.Season },
                DocumentType.TeamSeason => new[] { DocumentType.Season, DocumentType.Venue },
                DocumentType.AthleteSeason => new[] { DocumentType.Season, DocumentType.Venue, DocumentType.TeamSeason },
                _ => Array.Empty<DocumentType>()
            };

            if (!upstreamTiers.Any())
                return false; // No upstream dependencies - can proceed

            // IMPORTANT: Only look at jobs with the SAME correlationId (same historical sourcing run)
            var upstreamJobs = await _dataContext.ResourceIndexJobs
                .Where(x => x.CreatedBy == correlationId && upstreamTiers.Contains(x.DocumentType))
                .Select(x => new { 
                    x.DocumentType, 
                    x.LastCompletedUtc, 
                    x.IsEnabled,
                    x.ProcessingStartedUtc,
                    x.ProcessingInstanceId 
                })
                .ToListAsync();

            foreach (var tier in upstreamTiers)
            {
                var job = upstreamJobs.FirstOrDefault(x => x.DocumentType == tier);
                
                if (job == null)
                {
                    _logger.LogWarning(
                        "Upstream tier {Tier} not found for this historical sourcing run. CorrelationId={CorrelationId}",
                        tier, correlationId);
                    return true; // Should cancel - tier doesn't exist for this correlation
                }
                
                if (!job.IsEnabled)
                {
                    _logger.LogWarning(
                        "Upstream tier {Tier} is disabled (likely failed). CorrelationId={CorrelationId}",
                        tier, correlationId);
                    return true; // Should cancel - tier was disabled/failed
                }
                
                // ✅ Check if tier is currently processing (has ProcessingInstanceId)
                // If it's processing, we need to wait
                if (job.ProcessingInstanceId.HasValue)
                {
                    _logger.LogWarning(
                        "Upstream tier {Tier} is currently processing. Will check again on retry. CorrelationId={CorrelationId}",
                        tier, correlationId);
                    
                    // Throw exception to trigger Hangfire retry
                    throw new InvalidOperationException(
                        $"Upstream tier {tier} is still processing. Job will retry automatically.");
                }
                
                // ✅ Check if tier started processing but never completed (likely failed)
                if (job.ProcessingStartedUtc.HasValue && !job.LastCompletedUtc.HasValue)
                {
                    _logger.LogError(
                        "Upstream tier {Tier} started but never completed (likely failed). CorrelationId={CorrelationId}",
                        tier, correlationId);
                    return true; // Should cancel - tier failed
                }
                
                if (!job.LastCompletedUtc.HasValue)
                {
                    _logger.LogWarning(
                        "Upstream tier {Tier} has not started yet. Will check again on retry. CorrelationId={CorrelationId}",
                        tier, correlationId);
                    
                    // Throw exception to trigger Hangfire retry
                    throw new InvalidOperationException(
                        $"Upstream tier {tier} has not started yet. Job will retry automatically.");
                }
            }

            _logger.LogInformation(
                "All upstream tiers completed successfully for this historical sourcing run. Proceeding with {CurrentTier}. CorrelationId={CorrelationId}",
                currentTier, correlationId);
            
            return false; // Can proceed - all upstream tiers completed successfully
        }

        private async Task ProcessLeaf(
            DocumentJobDefinition jobDefinition,
            Guid id,
            Guid me,
            Guid correlationId)
        {
            try
            {
                // Treat the endpoint itself as the single item in this "index"
                var href = jobDefinition.Endpoint;
                var hrefHash = HashProvider.GenerateHashFromUri(href!.ToCleanUri());

                var cmd = new ProcessResourceIndexItemCommand(
                    correlationId,                            // ✅ CorrelationId FIRST
                    CausationId.Provider.ResourceIndexJob,
                    Guid.NewGuid(),                           // MessageId for this command
                    id,                                       // ResourceIndexId
                    hrefHash,                                 // Id (UrlHash)
                    href!,                                    // Uri
                    jobDefinition.Sport,
                    jobDefinition.SourceDataProvider,
                    jobDefinition.DocumentType,
                    null,                                     // parentId
                    jobDefinition.SeasonYear,
                    true,
                    jobDefinition.IncludeLinkedDocumentTypes
                );

                _backgroundJobProvider.Enqueue<IProcessResourceIndexItems>(p => p.Process(cmd));

                // Mark complete (owner-gated), consistent with index path
                await _dataContext.ResourceIndexJobs
                    .Where(x => x.Id == id && x.ProcessingInstanceId == me)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsQueued, false)
                        .SetProperty(x => x.LastAccessedUtc, DateTime.UtcNow)
                        .SetProperty(x => x.TotalPageCount, (int?)1)   // single "page"
                        .SetProperty(x => x.LastPageIndex, (int?)1)
                        .SetProperty(x => x.LastCompletedUtc, DateTime.UtcNow));

                _logger.LogInformation("Leaf RI enqueued as item: {Endpoint} ({DocumentType})",
                    href, jobDefinition.DocumentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception during Leaf handling for ResourceIndex {Id}", id);
                throw; // Hangfire will retry
            }
            finally
            {
                // Release ownership
                await _dataContext.ResourceIndexJobs
                    .Where(x => x.Id == id && x.ProcessingInstanceId == me)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.ProcessingInstanceId, (Guid?)null)
                        .SetProperty(x => x.ProcessingStartedUtc, (DateTime?)null));
            }

            return; // don't fall through to index traversal
        }

        private async Task ExecuteInternal(DocumentJobDefinition jobDefinition, Guid correlationId)
        {
            _logger.LogInformation("Begin processing {@JobDefinition}", jobDefinition);

            var me = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var id = jobDefinition.ResourceIndexId;

            // ---- Atomic claim -------------------------------------------------
            var claimed = await _dataContext.ResourceIndexJobs
                .Where(x => x.Id == id && x.ProcessingInstanceId == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.ProcessingInstanceId, _ => me)
                    .SetProperty(x => x.ProcessingStartedUtc, _ => now)
                    .SetProperty(x => x.IsQueued, _ => true)) == 1;

            if (!claimed)
            {
                _logger.LogWarning("ResourceIndex {ResourceIndexId} already owned; skipping.", id);
                return;
            }

            if (jobDefinition.Shape == ResourceShape.Leaf)
            {
                await ProcessLeaf(jobDefinition, id, me, correlationId);
                return;
            }

            try
            {
                // ---- Initial page fetch ---------------------------------------
                var url = jobDefinition.StartPage.HasValue
                    ? $"{jobDefinition.Endpoint}?limit={PageSize}&page={jobDefinition.StartPage.Value}"
                    : $"{jobDefinition.Endpoint}?limit={PageSize}";

                if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var firstUri))
                {
                    _logger.LogError("Invalid URI: {Url}", url);
                    throw new InvalidOperationException($"Invalid URI: {url}");
                }

                var resourceIndexDto = await _espnApi.GetResourceIndex(firstUri, jobDefinition.EndpointMask);

                // Owner-gated state: last access + total page count
                await _dataContext.ResourceIndexJobs
                    .Where(x => x.Id == id && x.ProcessingInstanceId == me)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.LastAccessedUtc, _ => DateTime.UtcNow)
                        .SetProperty(x => x.TotalPageCount, _ => resourceIndexDto.PageCount));

                _logger.LogInformation("Index has {Count} items across {Pages} pages starting at {Page}",
                    resourceIndexDto.Count, resourceIndexDto.PageCount, resourceIndexDto.PageIndex);

                // (Optional sanity) existing docs count – harmless read
                var collectionName = _decoder.GetCollectionName(
                    jobDefinition.SourceDataProvider,
                    jobDefinition.Sport,
                    jobDefinition.DocumentType,
                    jobDefinition.SeasonYear);

                _logger.LogInformation("Target collection {Collection}", collectionName);
                // NOTE: left in place in case you want to observe coverage; no short-circuiting

                var totalItemsEnqueued = 0;

                // ---- Page loop -------------------------------------------------
                while (true)
                {
                    // Stop criteria 1: overshoot guard
                    if (resourceIndexDto.PageIndex > resourceIndexDto.PageCount)
                    {
                        _logger.LogInformation("PageIndex {Idx} > PageCount {Cnt}. Finishing.", resourceIndexDto.PageIndex, resourceIndexDto.PageCount);
                        break;
                    }

                    _logger.LogInformation("Processing page {Current}/{Total} for {DocType}",
                        resourceIndexDto.PageIndex, resourceIndexDto.PageCount, jobDefinition.DocumentType);

                    // Enqueue items for processing
                    foreach (var item in resourceIndexDto.Items)
                    {
                        var cmd = new ProcessResourceIndexItemCommand(
                            correlationId,  // ✅ CorrelationId FIRST
                            CausationId.Provider.ResourceIndexJob,
                            Guid.NewGuid(), // MessageId for this command
                            id,             // ResourceIndexId
                            HashProvider.GenerateHashFromUri(item.Ref.ToCleanUri()), // Id (UrlHash)
                            item.Ref,
                            jobDefinition.Sport,
                            jobDefinition.SourceDataProvider,
                            jobDefinition.DocumentType,
                            null,
                            jobDefinition.SeasonYear,
                            true,
                            jobDefinition.IncludeLinkedDocumentTypes);

                        _backgroundJobProvider.Enqueue<IProcessResourceIndexItems>(p => p.Process(cmd));
                        totalItemsEnqueued++;
                    }

                    // Page-scoped, owner-gated, monotonic progress
                    var currentPage = resourceIndexDto.PageIndex;
                    await _dataContext.ResourceIndexJobs
                        .Where(x => x.Id == id
                                    && x.ProcessingInstanceId == me
                                    && (x.LastPageIndex == null || x.LastPageIndex < currentPage))
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastPageIndex, _ => currentPage));

                    // Stop criteria 2: last page reached
                    if (resourceIndexDto.PageIndex >= resourceIndexDto.PageCount)
                    {
                        _logger.LogInformation("Final page {Page} reached. {@Dto}", resourceIndexDto.PageIndex, resourceIndexDto);
                        break;
                    }

                    // Next page
                    var nextPage = resourceIndexDto.PageIndex + 1;
                    var nextUrl = $"{jobDefinition.Endpoint}?limit={PageSize}&page={nextPage}";
                    _logger.LogInformation("Fetching next page. {NextUrl}", nextUrl);
                    resourceIndexDto = await _espnApi.GetResourceIndex(new Uri(nextUrl, UriKind.RelativeOrAbsolute), jobDefinition.EndpointMask);
                }

                // ---- Mark complete (owner-gated) ------------------------------
                await _dataContext.ResourceIndexJobs
                    .Where(x => x.Id == id && x.ProcessingInstanceId == me)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsQueued, _ => false)
                        .SetProperty(x => x.LastCompletedUtc, _ => DateTime.UtcNow));

                _logger.LogInformation(
                    "TIER_SOURCING_COMPLETED: Tier={Tier}, TotalDocumentsEnqueued={Count}, ResourceIndexId={ResourceIndexId}",
                    jobDefinition.DocumentType, totalItemsEnqueued, id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception during ResourceIndexJob execution for {Id}", id);
                throw; // Hangfire will retry
            }
            finally
            {
                // Always release ownership (owner-gated)
                await _dataContext.ResourceIndexJobs
                    .Where(x => x.Id == id && x.ProcessingInstanceId == me)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.ProcessingInstanceId, _ => (Guid?)null)
                        .SetProperty(x => x.ProcessingStartedUtc, _ => (DateTime?)null));
            }
        }
    }
}
