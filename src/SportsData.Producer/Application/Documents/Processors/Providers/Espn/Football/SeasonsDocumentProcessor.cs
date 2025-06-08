using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Seasons)]
    public class SeasonsDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<SeasonsDocumentProcessor> _logger;
        private readonly FootballDataContext _dataContext;

        public SeasonsDocumentProcessor(
            ILogger<SeasonsDocumentProcessor> logger,
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
            // deserialize the DTO
            var espnDto = command.Document.FromJson<EspnFootballSeasonsDto>();

            foreach (var season in espnDto.Types.Items)
            {
                // external Id will be {seasonYear}{seasonId}
                var externalId = $"{season.Year}{season.Id}";

                var exists = await _dataContext.Seasons.AnyAsync(x =>
                    x.ExternalIds.Any(z => z.Value == externalId && z.Provider == command.SourceDataProvider));

                if (exists)
                {
                    // log something?
                    return;
                }

                // TODO: Determine if we really need to even persist this
            }

        }
    }
}
