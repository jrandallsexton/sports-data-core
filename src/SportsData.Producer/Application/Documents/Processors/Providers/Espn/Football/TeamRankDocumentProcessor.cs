using SportsData.Core.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamRank)]
    public class TeamRankDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<TeamRankDocumentProcessor> _logger;

        public TeamRankDocumentProcessor(ILogger<TeamRankDocumentProcessor> logger)
        {
            _logger = logger;
        }

        public Task ProcessAsync(ProcessDocumentCommand command)
        {
            _logger.LogInformation("Processing TeamRank document.");
            // TODO: Implement processing logic
            return Task.CompletedTask;
        }
    }
}