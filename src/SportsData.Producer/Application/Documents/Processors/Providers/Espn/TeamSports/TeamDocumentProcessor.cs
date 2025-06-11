using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports
{
    public class TeamDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<TeamDocumentProcessor> _logger;

        public TeamDocumentProcessor(ILogger<TeamDocumentProcessor> logger)
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
