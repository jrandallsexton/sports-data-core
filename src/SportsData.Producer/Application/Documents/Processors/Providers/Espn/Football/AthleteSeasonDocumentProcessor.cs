using MassTransit;

using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    public class AthleteSeasonDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<AthleteSeasonDocumentProcessor> _logger;
        private readonly FootballDataContext _dataContext;
        private readonly IPublishEndpoint _publishEndpoint;

        public AthleteSeasonDocumentProcessor(
            ILogger<AthleteSeasonDocumentProcessor> logger,
            FootballDataContext dataContext,
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
            _logger.LogInformation("Began with {Command}", command);
            // TODO: Implement processing logic
            await Task.Delay(100);
            //// deserialize the DTO
            //var externalProviderDto = command.Document.FromJson<EspnAthleteSeasonDto>(new JsonSerializerSettings
            //{
            //    MetadataPropertyHandling = MetadataPropertyHandling.Ignore
            //});

            //// 1. Does this AthleteDto exist? If not, we must create it prior to creating a season for it
            //var groupEntity = await _dataContext.Athletes
            //    .Include(g => g.Seasons)
            //    .Where(x =>
            //        x.ExternalIds.Any(z => z.Value == externalProviderDto.Id.ToString() &&
            //                               z.Provider == command.SourceDataProvider))
            //    .FirstOrDefaultAsync();
        }
    }
}
