using MassTransit;

using SportsData.Core.Eventing.Events.Documents;
using SportsData.Producer.Infrastructure.Data;

namespace SportsData.Producer.Application.Documents
{
    public class DocumentSourcingStartedHandler :
        IConsumer<DocumentSourcingStarted>
    {
        private readonly ILogger<DocumentSourcingStartedHandler> _logger;
        private readonly IDatabaseScaler _databaseScaler;

        public DocumentSourcingStartedHandler(
            ILogger<DocumentSourcingStartedHandler> logger,
            IDatabaseScaler databaseScaler)
        {
            _logger = logger;
            _databaseScaler = databaseScaler;
        }

        public async Task Consume(ConsumeContext<DocumentSourcingStarted> context)
        {
            _logger.LogInformation("Document Sourcing began. Scaling up database.");
            await _databaseScaler.ScaleUpAsync("doc sourcing began");
            _logger.LogInformation("Document Sourcing began. Scaling up database complete.");
        }
    }
}
