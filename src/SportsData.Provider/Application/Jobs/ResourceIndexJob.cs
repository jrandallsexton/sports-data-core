
using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Jobs.Definitions;
using SportsData.Provider.Application.Processors;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Providers.Espn;

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

        private const int PageSize = 500;

        public ResourceIndexJob(
            ILogger<ResourceIndexJob> logger,
            AppDataContext dataContext,
            IProvideEspnApiData espnApi,
            IDocumentStore documentStore,
            IDecodeDocumentProvidersAndTypes decoder,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _espnApi = espnApi;
            _documentStore = documentStore;
            _decoder = decoder;
            _backgroundJobProvider = backgroundJobProvider;
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

            var resourceIndexDto = await _espnApi.GetResourceIndex(url, jobDefinition.EndpointMask);

            _logger.LogInformation("Obtained Resource Index Definition {@resourceIndex}", resourceIndexDto);

            // Log this access to AppDataContext
            var resourceIndexEntity = await _dataContext.Resources
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
            resourceIndexEntity.LastAccessed = DateTime.UtcNow;
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
                    _logger.LogInformation(
                        $"Number of counts matched for {jobDefinition.SourceDataProvider}.{jobDefinition.Sport}.{jobDefinition.DocumentType}");
                    return;
                }

                while (resourceIndexDto.PageIndex <= resourceIndexDto.PageCount)
                {
                    _logger.LogInformation("Processing {@CurrentPage} of {@TotalPages} for {@DocumentType}",
                        resourceIndexDto.PageIndex, resourceIndexDto.PageCount, jobDefinition.DocumentType)
                        ;

                    foreach (var cmd in resourceIndexDto.Items.Select(item =>
                                 new ProcessResourceIndexItemCommand(
                                     resourceIndexEntity.Id,
                                     0,
                                     item.Id,
                                     item.Href,
                                     jobDefinition.Sport,
                                     jobDefinition.SourceDataProvider,
                                     jobDefinition.DocumentType,
                                     jobDefinition.SeasonYear)))
                    {
                        _logger.LogInformation($"Generating background job for resourceIndexId: {cmd.Id}");

                        _backgroundJobProvider.Enqueue<IProcessResourceIndexItems>(p => p.Process(cmd));

                        await Task.Delay(500); // do NOT beat on their API

                        resourceIndexEntity.LastPageIndex = resourceIndexDto.PageIndex;
                        await _dataContext.SaveChangesAsync();
                    }

                    // TODO: I think I can remove this
                    if (resourceIndexDto.PageIndex == resourceIndexDto.PageCount)
                    {
                        break;
                    }

                    url = $"{jobDefinition.Endpoint}?limit={PageSize}&page={resourceIndexDto.PageIndex + 1}";
                    resourceIndexDto = await _espnApi.GetResourceIndex(url, jobDefinition.EndpointMask);
                }

                _logger.LogInformation($"Completed {nameof(jobDefinition)} with {resourceIndexDto.Items.Count} jobs spawned.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "o shit");
            }
        }
    }
}
