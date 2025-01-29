
using Microsoft.EntityFrameworkCore;

using MongoDB.Driver;

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
        private readonly DocumentService _documentService;
        private readonly IDecodeDocumentProvidersAndTypes _decoder;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public ResourceIndexJob(
            ILogger<ResourceIndexJob> logger,
            AppDataContext dataContext,
            IProvideEspnApiData espnApi,
            DocumentService documentService,
            IDecodeDocumentProvidersAndTypes decoder,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _espnApi = espnApi;
            _documentService = documentService;
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
            // Get the resource index
            var resourceIndex = await _espnApi.GetResourceIndex(jobDefinition.Endpoint, jobDefinition.EndpointMask);

            _logger.LogInformation("Obtained Resource Index Definition {@resourceIndex}", resourceIndex);

            // Log this access to AppDataContext
            var resourceIndexEntity = await _dataContext.Resources
                .Where(x => x.Id == jobDefinition.ResourceIndexId)
                .FirstOrDefaultAsync();

            if (resourceIndexEntity == null)
            {
                // log an error, but do not stop processing
                _logger.LogError($"ResourceIndex could not be loaded from the database for item: {jobDefinition.ResourceIndexId}");
                // TODO: Throw and retry?
                return;
            }
            else
            {
                _logger.LogInformation("Updated access to ResourceIndex in the database");
                resourceIndexEntity.LastAccessed = DateTime.UtcNow;
                await _dataContext.SaveChangesAsync();
            }

            // TODO: Remove this code after testing.
            // For now, I do not want to load each resource if I already have them in Mongo
            // Otherwise ESPN might blacklist my IP.  Not sure.
            var collectionName = _decoder.GetCollectionName(jobDefinition.SourceDataProvider, jobDefinition.Sport, jobDefinition.DocumentType, jobDefinition.SeasonYear);

            _logger.LogInformation("Getting collection {@CollectionName}", collectionName);

            try
            {
                var dbObjects = _documentService.Database.GetCollection<DocumentBase>(collectionName);
                var filter = Builders<DocumentBase>.Filter.Empty;
                var dbCursor = await dbObjects.FindAsync(filter);
                var dbDocuments = await dbCursor.ToListAsync();

                _logger.LogInformation("Obtained {@CollectionObjectCount}", dbDocuments.Count);

                if (dbDocuments.Count == resourceIndex.count)
                {
                    _logger.LogInformation(
                        $"Number of counts matched for {jobDefinition.SourceDataProvider}.{jobDefinition.Sport}.{jobDefinition.DocumentType}");
                    return;
                }

                foreach (var cmd in resourceIndex.items.Select(item =>
                             new ProcessResourceIndexItemCommand(
                                 item.id,
                                 item.href,
                                 jobDefinition.Sport,
                                 jobDefinition.SourceDataProvider,
                                 jobDefinition.DocumentType,
                                 jobDefinition.SeasonYear)))
                {
                    _logger.LogInformation($"Generating background job for resourceIndexId: {cmd.Id}");
                    _backgroundJobProvider.Enqueue<IProcessResourceIndexItems>(p => p.Process(cmd));
                    await Task.Delay(500); // do NOT beat on their API
                }

                _logger.LogInformation($"Completed {nameof(jobDefinition)} with {resourceIndex.items.Count} jobs spawned.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "o shit");

            }

        }
    }
}
