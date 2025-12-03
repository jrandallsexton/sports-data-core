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
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = jobDefinition.ResourceIndexId
                   }))
                await ExecuteInternal(jobDefinition);
        }

        private async Task ProcessLeaf(
            DocumentJobDefinition jobDefinition,
            Guid id,
            Guid me)
        {
            try
            {
                // Treat the endpoint itself as the single item in this "index"
                var href = jobDefinition.Endpoint;
                var hrefHash = HashProvider.GenerateHashFromUri(href!.ToCleanUri());

                var cmd = new ProcessResourceIndexItemCommand(
                    jobDefinition.ResourceIndexId,            // correlation
                    id,                                       // parent RI job id (same as your loop)
                    hrefHash,                                 // UrlHash
                    href!,                                     // $ref
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
                        .SetProperty(x => x.IsQueued, _ => false)
                        .SetProperty(x => x.LastAccessedUtc, _ => DateTime.UtcNow)
                        .SetProperty(x => x.TotalPageCount, _ => 1)   // single "page"
                        .SetProperty(x => x.LastPageIndex, _ => 1)
                        .SetProperty(x => x.LastCompletedUtc, _ => DateTime.UtcNow));

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
                        .SetProperty(x => x.ProcessingInstanceId, _ => (Guid?)null)
                        .SetProperty(x => x.ProcessingStartedUtc, _ => (DateTime?)null));
            }

            return; // don't fall through to index traversal
        }

        private async Task ExecuteInternal(DocumentJobDefinition jobDefinition)
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
                await ProcessLeaf(jobDefinition, id, me);
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
                            jobDefinition.ResourceIndexId,
                            id,
                            HashProvider.GenerateHashFromUri(item.Ref.ToCleanUri()),
                            item.Ref,
                            jobDefinition.Sport,
                            jobDefinition.SourceDataProvider,
                            jobDefinition.DocumentType,
                            null,
                            jobDefinition.SeasonYear,
                            true,
                            jobDefinition.IncludeLinkedDocumentTypes);

                        _backgroundJobProvider.Enqueue<IProcessResourceIndexItems>(p => p.Process(cmd));
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

                _logger.LogInformation("Completed ResourceIndex {Id}.", id);
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
