using SportsData.Core.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamInformation)]
    public class TeamInformationDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<TeamInformationDocumentProcessor> _logger;

        public TeamInformationDocumentProcessor(ILogger<TeamInformationDocumentProcessor> logger)
        {
            _logger = logger;
        }

        public Task ProcessAsync(ProcessDocumentCommand command)
        {
            _logger.LogInformation("Began with {Command}", command);
            // TODO: Implement processing logic
            return Task.CompletedTask;
        }
    }
}
