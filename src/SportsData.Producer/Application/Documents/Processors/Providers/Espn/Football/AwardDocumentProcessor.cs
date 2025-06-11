using SportsData.Core.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Award)]
    public class AwardDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<AwardDocumentProcessor> _logger;

        public AwardDocumentProcessor(ILogger<AwardDocumentProcessor> logger)
        {
            _logger = logger;
        }

        public async Task ProcessAsync(ProcessDocumentCommand command)
        {
            _logger.LogInformation("Began with {Command}", command);
            // TODO: Implement processing logic
            await Task.Delay(100);
        }
    }
}
