using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;

using System.Threading.Tasks;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Group)]
    public class GroupDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<GroupDocumentProcessor> _logger;

        public GroupDocumentProcessor(ILogger<GroupDocumentProcessor> logger)
        {
            _logger = logger;
        }

        public Task ProcessAsync(ProcessDocumentCommand command)
        {
            _logger.LogInformation("Processing Group document.");
            // TODO: Implement processing logic
            return Task.CompletedTask;
        }
    }
}