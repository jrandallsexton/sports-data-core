using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;

using System.Threading.Tasks;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.CoachBySeason)]
    public class CoachBySeasonDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<CoachBySeasonDocumentProcessor> _logger;

        public CoachBySeasonDocumentProcessor(ILogger<CoachBySeasonDocumentProcessor> logger)
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