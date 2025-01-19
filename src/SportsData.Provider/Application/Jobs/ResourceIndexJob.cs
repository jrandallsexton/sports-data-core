using Hangfire;
using MongoDB.Driver;

using SportsData.Provider.Application.Documents;
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
        private readonly IProvideEspnApiData _espnApi;
        private readonly DocumentService _documentService;
        private readonly IDecodeDocumentProvidersAndTypes _decoder;

        public ResourceIndexJob(
            ILogger<ResourceIndexJob> logger,
            IProvideEspnApiData espnApi,
            DocumentService documentService,
            IDecodeDocumentProvidersAndTypes decoder)
        {
            _logger = logger;
            _espnApi = espnApi;
            _documentService = documentService;
            _decoder = decoder;
        }

        public async Task ExecuteAsync(DocumentJobDefinition jobDefinition)
        {
            _logger.LogInformation($"Started {nameof(ResourceIndexJob)}");

            // Get the resource index
            var resourceIndex = await _espnApi.GetResourceIndex(jobDefinition.Endpoint, jobDefinition.EndpointMask);

            // TODO: Log this access to AppDataContext

            // TODO: Remove this code after testing.
            // For now, I do not want to load each resource if I already have them in Mongo
            // Otherwise ESPN might blacklist my IP.  Not sure.
            var type = _decoder.GetType(jobDefinition.SourceDataProvider, jobDefinition.DocumentType);
            var dbObjects = _documentService.Database.GetCollection<DocumentBase>(type.Name);
            var filter = Builders<DocumentBase>.Filter.Empty;
            var dbCursor = await dbObjects.FindAsync(filter);
            var dbDocuments = await dbCursor.ToListAsync();

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
                // TODO: Put this in a wrapper with an interface for testing
                BackgroundJob.Enqueue<IProcessResourceIndexItems>(p => p.Process(cmd));
                await Task.Delay(1_000); // do NOT beat on their API
            }

            _logger.LogInformation($"Completed {nameof(jobDefinition)} with {resourceIndex.items.Count} jobs spawned.");
        }
    }
}
