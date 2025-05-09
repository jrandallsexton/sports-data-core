using MassTransit;

using Microsoft.EntityFrameworkCore;

using Newtonsoft.Json;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Franchise;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports
{
    public class TeamSeasonDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<TeamSeasonDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IPublishEndpoint _publishEndpoint;

        public TeamSeasonDocumentProcessor(
            ILogger<TeamSeasonDocumentProcessor<TDataContext>> logger,
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

        public async Task ProcessInternal(ProcessDocumentCommand command)
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
                _logger.LogError("Could not find franchise {@Franchise} with this externalId: {@FranchiseExternalId}", espnDto.Name, espnDto.Id);

                // TODO: Uncomment this throw after debugging?
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
            var newFranchiseSeasonEntity = espnDto.AsEntity(
                franchiseExternalId.Franchise.Id,
                franchiseBySeasonId,
                command.Season.Value,
                command.CorrelationId);

            await _dataContext.FranchiseSeasons
                .AddAsync(newFranchiseSeasonEntity);

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
                    null,
                    command.CorrelationId,
                    CausationId.Producer.TeamSeasonDocumentProcessor));
            });

            if (events.Count > 0)
            {
                _logger.LogInformation($"Requesting {events.Count} images for {command.DocumentType} {command.Season}");
                await _publishEndpoint.PublishBatch(events);
            }

            await _publishEndpoint.Publish(
                new FranchiseSeasonCreated(
                    newFranchiseSeasonEntity.ToCanonicalModel(),
                    command.CorrelationId,
                    CausationId.Producer.TeamSeasonDocumentProcessor));

            await _dataContext.SaveChangesAsync();
        }
    }
}
