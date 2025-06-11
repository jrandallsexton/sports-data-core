using SportsData.Core.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Contest)]
    public class ContestDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<ContestDocumentProcessor> _logger;

        public ContestDocumentProcessor(ILogger<ContestDocumentProcessor> logger)
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
