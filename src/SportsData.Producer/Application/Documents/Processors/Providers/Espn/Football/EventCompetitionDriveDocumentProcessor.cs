using MassTransit;

using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionDrive)]
    public class EventCompetitionDriveDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : FootballDataContext
    {
        private readonly ILogger<EventCompetitionDriveDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IPublishEndpoint _publishEndpoint;

        public EventCompetitionDriveDocumentProcessor(
            ILogger<EventCompetitionDriveDocumentProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IPublishEndpoint publishEndpoint)
        {
            _logger = logger;
            _dataContext = dataContext;
            _publishEndpoint = publishEndpoint;
        }

        public async Task ProcessAsync(ProcessDocumentCommand command)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = command.CorrelationId
                   }))
            {
                _logger.LogInformation("Began with {@command}", command);

                await ProcessInternal(command);
            }
        }

        private async Task ProcessInternal(ProcessDocumentCommand command)
        {
            var externalProviderDto = command.Document.FromJson<EspnEventCompetitionDriveDto>();

            if (externalProviderDto is null)
            {
                _logger.LogError($"Error deserializing {command.DocumentType}");
                throw new InvalidOperationException($"Deserialization returned null for {nameof(EspnEventCompetitionDto)}");
            }

            // TODO: Implement logic to process the event document

            await Task.Delay(100); // Simulate processing delay
        }
    }
}
