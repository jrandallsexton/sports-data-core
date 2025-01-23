using MassTransit;

using Microsoft.EntityFrameworkCore;

using Newtonsoft.Json;

using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Football.Ncaa
{
    public class TeamBySeasonDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<TeamBySeasonDocumentProcessor> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IPublishEndpoint _bus;

        public TeamBySeasonDocumentProcessor(
            ILogger<TeamBySeasonDocumentProcessor> logger,
            AppDataContext dataContext,
            IPublishEndpoint bus)
        {
            _logger = logger;
            _dataContext = dataContext;
            _bus = bus;
        }

        public async Task ProcessAsync(ProcessDocumentCommand command)
        {
            // deserialize the DTO
            var espnDto = command.Document.FromJson<EspnTeamSeasonDto>(new JsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore
            });

            var franchiseExternalId = await _dataContext.FranchiseExternalIds
                .Include(x => x.Franchise)
                .ThenInclude(x => x.Seasons)
                .FirstOrDefaultAsync(x => x.Value == espnDto.Id.ToString() &&
                                          x.Provider == command.SourceDataProvider);

            if (franchiseExternalId == null)
            {
                // TODO: Revisit this pattern
                _logger.LogError("Could not find franchise with this external id");

                // TODO: Uncomment this throw after debugging
                //throw new ResourceNotFoundException("Could not find franchise with this external id");
                return;
            }

            if (command.Season is null)
            {
                _logger.LogError("SeasonId is required");
                return;
            }

            // does this season already exist?
            

            var franchiseBySeasonId = Guid.NewGuid();

            await _dataContext.FranchiseSeasons
                .AddAsync(espnDto.AsFranchiseSeasonEntity(
                    franchiseExternalId.Franchise.Id,
                    franchiseBySeasonId,
                    command.Season.Value,
                    command.CorrelationId));

            // any logos on the dto?
            var events = new List<ProcessImageRequest>();
            espnDto.Logos?.ForEach(logo =>
            {
                var imgId = Guid.NewGuid();
                events.Add(new ProcessImageRequest(
                    logo.Href.ToString(),
                    imgId,
                    franchiseBySeasonId,
                    $"{franchiseBySeasonId}.png",
                    command.Sport,
                    command.Season,
                    command.DocumentType,
                    command.SourceDataProvider,
                    0,
                    0,
                    null));
            });

            if (events.Count > 0)
                await _bus.PublishBatch(events);

            await _dataContext.SaveChangesAsync();
        }
    }
}
