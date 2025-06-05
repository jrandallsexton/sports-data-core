using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SportsData.Producer.Application.Documents.Processors.Commands;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
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