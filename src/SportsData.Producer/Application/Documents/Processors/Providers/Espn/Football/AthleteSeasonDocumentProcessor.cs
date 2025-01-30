using MassTransit;
using Microsoft.EntityFrameworkCore;

using Newtonsoft.Json;
using SportsData.Core.Extensions;

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    public class AthleteSeasonDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<AthleteSeasonDocumentProcessor> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IPublishEndpoint _publishEndpoint;

        public AthleteSeasonDocumentProcessor(
            ILogger<AthleteSeasonDocumentProcessor> logger,
            AppDataContext dataContext,
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
            throw new NotImplementedException();
            //// deserialize the DTO
            //var externalProviderDto = command.Document.FromJson<EspnAthleteSeasonDto>(new JsonSerializerSettings
            //{
            //    MetadataPropertyHandling = MetadataPropertyHandling.Ignore
            //});

            //// 1. Does this athlete exist? If not, we must create it prior to creating a season for it
            //var groupEntity = await _dataContext.Athletes
            //    .Include(g => g.Seasons)
            //    .Where(x =>
            //        x.ExternalIds.Any(z => z.Value == externalProviderDto.Id.ToString() &&
            //                               z.Provider == command.SourceDataProvider))
            //    .FirstOrDefaultAsync();
        }
    }
}
