using SportsData.Core.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Standings)]
    public class StandingsDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<StandingsDocumentProcessor> _logger;

        public StandingsDocumentProcessor(ILogger<StandingsDocumentProcessor> logger)
        {
            _logger = logger;
        }

        public Task ProcessAsync(ProcessDocumentCommand command)
        {
            _logger.LogInformation("Processing Standings document.");
            // TODO: Implement processing logic
            return Task.CompletedTask;
        }
    }
}