using Hangfire;

using SportsData.Provider.Application.Jobs.Definitions;
using SportsData.Provider.Application.Processors;
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
        
        public DocumentProviderJob(
            TDocumentJobDefinition documentJobDefinition,
            ILogger<DocumentProviderJob<TDocumentJobDefinition>> logger,
            IProvideEspnApiData espnApi)
        {
            _logger = logger;
            _jobDefinition = documentJobDefinition;
            _espnApi = espnApi;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation($"Started {nameof(DocumentProviderJob<TDocumentJobDefinition>)}");

            // Get the resource index
            var resourceIndex = await _espnApi.GetResourceIndex(_jobDefinition.Endpoint, _jobDefinition.EndpointMask);

            foreach (var cmd in resourceIndex.items.Skip(415).Select(item => new ProcessResourceIndexItemCommand()
                     {
                         DocumentType = _jobDefinition.DocumentType,
                         Href = item.href,
                         Id = item.id,
                         SeasonYear = _jobDefinition.SeasonYear,
                         SourceDataProvider = _jobDefinition.SourceDataProvider
                     }))
            {
                // TODO: Put this in a wrapper with an interface for testing
                BackgroundJob.Enqueue<IProcessResourceIndexes>(p => p.Process(cmd));
                await Task.Delay(3_000); // do NOT beat on their API
            }

            _logger.LogInformation($"Completed {nameof(DocumentProviderJob<TDocumentJobDefinition>)} with {resourceIndex.items.Count} jobs spawned.");

        }
    }
}
