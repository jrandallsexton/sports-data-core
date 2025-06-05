using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SportsData.Producer.Application.Documents.Processors.Commands;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
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