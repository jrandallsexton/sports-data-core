using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.SeasonType)]
    public class SeasonTypeDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<SeasonTypeDocumentProcessor> _logger;
        private readonly FootballDataContext _dataContext;

        public SeasonTypeDocumentProcessor(
            ILogger<SeasonTypeDocumentProcessor> logger,
            FootballDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
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
            await Task.Delay(100);

            // deserialize the DTO
            var espnDto = command.Document.FromJson<EspnFootballSeasonTypeDto>();

            // TODO: Convert this to a canonical entity

        }
    }
}
