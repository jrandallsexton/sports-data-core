
using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Jobs.Definitions;
using SportsData.Provider.Application.Processors;
using SportsData.Provider.Config;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Providers.Espn;

using System;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;

namespace SportsData.Provider.Application.Jobs
{
    public interface IProcessResourceIndexes
    {
        Task ExecuteAsync(DocumentJobDefinition jobDefinition);
    }

    public class ResourceIndexJob : IProcessResourceIndexes
    {
        private readonly ILogger<ResourceIndexJob> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IProvideEspnApiData _espnApi;
        private readonly IDocumentStore _documentStore;
        private readonly IDecodeDocumentProvidersAndTypes _decoder;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IProviderAppConfig _appConfig;
        private readonly EspnApiClientConfig _espnApiClientConfig;

        private const int PageSize = 500;

        public ResourceIndexJob(
            ILogger<ResourceIndexJob> logger,
            AppDataContext dataContext,
            IProvideEspnApiData espnApi,
            IDocumentStore documentStore,
            IDecodeDocumentProvidersAndTypes decoder,
            IProvideBackgroundJobs backgroundJobProvider,
            IProviderAppConfig appConfig,
            EspnApiClientConfig espnApiClientConfig)
        {
            _logger = logger;
            _dataContext = dataContext;
            _espnApi = espnApi;
            _documentStore = documentStore;
            _decoder = decoder;
            _backgroundJobProvider = backgroundJobProvider;
            _appConfig = appConfig;
            _espnApiClientConfig = espnApiClientConfig;
        }

        public async Task ExecuteAsync(DocumentJobDefinition jobDefinition)
        {
            using (_logger.BeginScope(nameof(ResourceIndexJob)))
                await ExecuteInternal(jobDefinition);
        }

        private async Task ExecuteInternal(DocumentJobDefinition jobDefinition)
        {
            _logger.LogInformation("Begin processing {@JobDefinition}", jobDefinition);

            // Get the resource index
            var url = jobDefinition.StartPage.HasValue
                ? $"{jobDefinition.Endpoint}?limit={PageSize}&page={jobDefinition.StartPage.Value}"
                : $"{jobDefinition.Endpoint}?limit={PageSize}";

            if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
            {
                _logger.LogError("Could not created URI from {@Url}", url);
                throw new InvalidOperationException($"Invalid URI: {url}");
            }

            var resourceIndexDto = await _espnApi.GetResourceIndex(uri, jobDefinition.EndpointMask);

            _logger.LogInformation("Obtained Resource Index Definition {@resourceIndex}", resourceIndexDto);

            // Log this access to AppDataContext
            var resourceIndexEntity = await _dataContext.ResourceIndexJobs
                .Include(x => x.Items)
                .Where(x => x.Id == jobDefinition.ResourceIndexId)
                .FirstOrDefaultAsync();

            if (resourceIndexEntity == null)
            {
                // log an error, but do not stop processing
                _logger.LogError($"ResourceIndex could not be loaded from the database for item: {jobDefinition.ResourceIndexId}");
                // TODO: Throw and retry?
                return;
            }

            _logger.LogInformation("Updating access to ResourceIndex in the database");
            resourceIndexEntity.LastAccessedUtc = DateTime.UtcNow;
            resourceIndexEntity.IsQueued = true;
            resourceIndexEntity.TotalPageCount = resourceIndexDto.PageCount;

            await _dataContext.SaveChangesAsync();

            // TODO: Remove this code after testing.
            // For now, I do not want to load each resource if I already have them in Mongo
            // Otherwise ESPN might blacklist my IP.  Not sure.
            var collectionName = _decoder.GetCollectionName(jobDefinition.SourceDataProvider, jobDefinition.Sport, jobDefinition.DocumentType, jobDefinition.SeasonYear);

            _logger.LogInformation("Getting collection {@CollectionName}", collectionName);

            try
            {
                var dbDocuments = await _documentStore.GetAllDocumentsAsync<DocumentBase>(collectionName);

                _logger.LogInformation("Obtained {@CollectionObjectCount}", dbDocuments.Count);

                if (dbDocuments.Count == resourceIndexDto.Count)
                {
                    _logger.LogInformation($"Number of counts matched for {jobDefinition.SourceDataProvider}.{jobDefinition.Sport}.{jobDefinition.DocumentType}");
                    return;
                }

                // TODO: raise an event that document sourcing is about to start

                var itemsProcessed = 0;

                while (true)
                {
                    if (resourceIndexDto.PageIndex > resourceIndexDto.PageCount)
                    {
                        _logger.LogInformation("Completed all pages for {DocumentType}", jobDefinition.DocumentType);
                        break;
                    }

                    _logger.LogInformation("Processing page {CurrentPage} of {TotalPages} for {DocumentType}",
                        resourceIndexDto.PageIndex, resourceIndexDto.PageCount, jobDefinition.DocumentType);

                    foreach (var item in resourceIndexDto.Items)
                    {
                        if (_appConfig.MaxResourceIndexItemsToProcess.HasValue &&
                            itemsProcessed >= _appConfig.MaxResourceIndexItemsToProcess.Value)
                        {
                            _logger.LogInformation("Reached processing cap of {Max}. Exiting early.",
                                _appConfig.MaxResourceIndexItemsToProcess.Value);
                            resourceIndexEntity.IsQueued = false;
                            resourceIndexEntity.LastCompletedUtc = DateTime.UtcNow;
                            await _dataContext.SaveChangesAsync();
                            return;
                        }

                        var cmd = new ProcessResourceIndexItemCommand(
                            resourceIndexEntity.Id,
                            HashProvider.GenerateHashFromUri(item.Ref.ToCleanUri()),
                            item.Ref,
                            jobDefinition.Sport,
                            jobDefinition.SourceDataProvider,
                            jobDefinition.DocumentType,
                            null,
                            jobDefinition.SeasonYear);

                        _logger.LogInformation("Preparing job for resourceIndexItem: {Id}", cmd.Id);

                        if (_appConfig.IsDryRun)
                        {
                            _logger.LogInformation("DryRun enabled: skipping enqueue for {Ref}", item.Ref);
                        }
                        else
                        {
                            _backgroundJobProvider.Enqueue<IProcessResourceIndexItems>(p => p.Process(cmd));
                        }

                        itemsProcessed++;

                        resourceIndexEntity.LastPageIndex = resourceIndexDto.PageIndex;
                        await _dataContext.SaveChangesAsync();
                    }

                    if (resourceIndexDto.PageIndex == resourceIndexDto.PageCount)
                    {
                        _logger.LogInformation("Final page {PageIndex} reached. Job complete.", resourceIndexDto.PageIndex);
                        break;
                    }

                    var nextPage = resourceIndexDto.PageIndex + 1;
                    url = $"{jobDefinition.Endpoint}?limit={PageSize}&page={nextPage}";
                    resourceIndexDto = await _espnApi.GetResourceIndex(new Uri(url), jobDefinition.EndpointMask);
                }

                resourceIndexEntity.IsQueued = false;
                resourceIndexEntity.LastCompletedUtc = DateTime.UtcNow;
                await _dataContext.SaveChangesAsync();

                _logger.LogInformation($"Completed {nameof(jobDefinition)} with {resourceIndexDto.Items.Count} jobs spawned.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception during ResourceIndexJob execution for {JobId}", jobDefinition.ResourceIndexId);
                throw; // ensure Hangfire retries
            }

        }
    }
}
