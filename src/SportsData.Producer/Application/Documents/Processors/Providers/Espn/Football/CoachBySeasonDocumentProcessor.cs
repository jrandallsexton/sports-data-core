using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SportsData.Producer.Application.Documents.Processors.Commands;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    public class CoachBySeasonDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<CoachBySeasonDocumentProcessor> _logger;

        public CoachBySeasonDocumentProcessor(ILogger<CoachBySeasonDocumentProcessor> logger)
        {
            _logger = logger;
        }

        public Task ProcessAsync(ProcessDocumentCommand command)
        {
            _logger.LogInformation("Processing CoachBySeason document.");
            // TODO: Implement processing logic
            return Task.CompletedTask;
        }
    }
}