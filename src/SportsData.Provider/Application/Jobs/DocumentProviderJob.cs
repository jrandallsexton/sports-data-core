using Hangfire;

using MongoDB.Driver;

using SportsData.Provider.Application.Documents;
using SportsData.Provider.Application.Jobs.Definitions;
using SportsData.Provider.Application.Processors;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Providers.Espn;

namespace SportsData.Provider.Application.Jobs
{
    public interface IProvideDocuments : IAmARecurringJob { }

    public class DocumentProviderJob<TDocumentJobDefinition> :
        IProvideDocuments where TDocumentJobDefinition : DocumentJobDefinition
    {
        private readonly ILogger<DocumentProviderJob<TDocumentJobDefinition>> _logger;
        private readonly DocumentJobDefinition _jobDefinition;
        private readonly IProvideEspnApiData _espnApi;
        private readonly DocumentService _documentService;
        private readonly IDecodeDocumentProvidersAndTypes _decoder;

        public DocumentProviderJob(          
            TDocumentJobDefinition documentJobDefinition,
            ILogger<DocumentProviderJob<TDocumentJobDefinition>> logger,
            IProvideEspnApiData espnApi,
            DocumentService documentService,
            IDecodeDocumentProvidersAndTypes decoder)
        {
            _logger = logger;
            _jobDefinition = documentJobDefinition;
            _espnApi = espnApi;
            _documentService = documentService;
            _decoder = decoder;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation($"Started {nameof(DocumentProviderJob<TDocumentJobDefinition>)}");

            // Get the resource index
            var resourceIndex = await _espnApi.GetResourceIndex(_jobDefinition.Endpoint, _jobDefinition.EndpointMask);

            // TODO: Log this access to AppDataContext

            // TODO: Remove this code after testing.
            // For now, I do not want to load each resource if I already have them in Mongo
            // Otherwise ESPN might blacklist my IP.  Not sure.
            var type = _decoder.GetTypeAndName(_jobDefinition.SourceDataProvider, _jobDefinition.Sport,  _jobDefinition.DocumentType, _jobDefinition.SeasonYear);
            var dbObjects = _documentService.Database.GetCollection<DocumentBase>(type.Name);
            var filter = Builders<DocumentBase>.Filter.Empty;
            var dbCursor = await dbObjects.FindAsync(filter);
            var dbDocuments = await dbCursor.ToListAsync();

            if (dbDocuments.Count == resourceIndex.count)
            {
                _logger.LogInformation(
                    $"Number of counts matched for {_jobDefinition.SourceDataProvider}.{_jobDefinition.Sport}.{_jobDefinition.DocumentType}");
                return;
            }

            foreach (var cmd in resourceIndex.items.Select(item =>
                         new ProcessResourceIndexItemCommand(
                             item.id,
                             item.href,
                             _jobDefinition.Sport,
                             _jobDefinition.SourceDataProvider,
                             _jobDefinition.DocumentType,
                             _jobDefinition.SeasonYear)))
            {
                // TODO: Put this in a wrapper with an interface for testing
                BackgroundJob.Enqueue<IProcessResourceIndexItems>(p => p.Process(cmd));
                await Task.Delay(1_000); // do NOT beat on their API
            }

            _logger.LogInformation($"Completed {nameof(DocumentProviderJob<TDocumentJobDefinition>)} with {resourceIndex.items.Count} jobs spawned.");

        }
    }
}
