using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Conferences;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.GroupBySeason)]
    public class GroupBySeasonDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<GroupBySeasonDocumentProcessor> _logger;
        private readonly FootballDataContext _dataContext;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

        public GroupBySeasonDocumentProcessor(
            ILogger<GroupBySeasonDocumentProcessor> logger,
            FootballDataContext dataContext,
            IPublishEndpoint publishEndpoint,
            IGenerateExternalRefIdentities externalRefIdentityGenerator)
        {
            _logger = logger;
            _dataContext = dataContext;
            _publishEndpoint = publishEndpoint;
            _externalRefIdentityGenerator = externalRefIdentityGenerator;
        }

        public async Task ProcessAsync(ProcessDocumentCommand command)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = command.CorrelationId
            }))
            {
                await ProcessInternal(command);
            }
        }

        private async Task ProcessInternal(ProcessDocumentCommand command)
        {
            // deserialize the DTO
            var externalProviderDto = command.Document.FromJson<EspnGroupBySeasonDto>();

            if (externalProviderDto is null)
            {
                _logger.LogError($"Error deserializing {command.DocumentType}");
                throw new InvalidOperationException($"Deserialization returned null for EspnVenueDto. CorrelationId: {command.CorrelationId}");
            }

            if (!command.Season.HasValue)
            {
                _logger.LogError($"SeasonYear is required for {command.DocumentType}");
                throw new InvalidOperationException($"SeasonYear was not provided. CorrelationId: {command.CorrelationId}");
            }

            // 1. Does this group (conference) exist? If not, we must create it prior to creating a season for it
            var groupEntity = await _dataContext.Groups
                .Include(g => g.Seasons)
                .Where(x =>
                    x.ExternalIds.Any(z => z.Value == externalProviderDto.Id.ToString() &&
                                           z.Provider == command.SourceDataProvider))
                .FirstOrDefaultAsync();

            // Determine if this EspnGroupBySeasonDto exists. Do NOT trust that it says it is a new document!

            if (groupEntity != null)
            {
                _logger.LogWarning("Group already exists.");

                var groupTmp = await _dataContext.GroupExternalIds
                    .Include(x => x.Group)
                    .ThenInclude(x => x.Seasons)
                    .FirstOrDefaultAsync(x => x.Value == externalProviderDto.Id.ToString() &&
                                              x.Provider == command.SourceDataProvider);

                if (groupTmp is { Group: not null })
                {
                    // if the incoming season does not exist, add a new season to the existing group
                    if (groupTmp.Group.Seasons.All(s => s.Season != command.Season.Value))
                    {
                        groupTmp.Group.Seasons.Add(new GroupSeason()
                        {
                            Id = Guid.NewGuid(),
                            CreatedUtc = DateTime.UtcNow,
                            CreatedBy = command.CorrelationId,
                            Season = command.Season.Value,
                            GroupId = groupEntity.Id
                        });
                        await _dataContext.SaveChangesAsync();
                    }
                    else
                    {
                        // we already have this season for this group
                        // TODO: Update this later
                    }
                }
                else
                {
                    // raise an event ?
                }
            }
            else
            {
                var newGroupSeasonId = Guid.NewGuid();
                var newGroupId = Guid.NewGuid();

                var newGroupEntity = externalProviderDto.AsEntity(
                    _externalRefIdentityGenerator,
                    newGroupId,
                    command.CorrelationId);
                
                var newGroupSeason = externalProviderDto
                    .AsEntity(
                        newGroupId,
                        newGroupSeasonId,
                        command.Season.Value,
                        command.CorrelationId);

                _logger.LogInformation($"New GroupSeason with Id: {newGroupSeason.Id} created for GroupId: {newGroupId}");

                newGroupEntity.Seasons.Add(newGroupSeason);

                await _dataContext.Groups.AddAsync(newGroupEntity);

                // any logos on the dto?
                var events = new List<ProcessImageRequest>();
                externalProviderDto.Logos.ForEach(logo =>
                {
                    var imgId = Guid.NewGuid();
                    events.Add(new ProcessImageRequest(
                        logo.Href,
                        imgId,
                        newGroupSeasonId,
                        $"{newGroupSeasonId}.png",
                        command.Sport,
                        command.Season,
                        command.DocumentType,
                        command.SourceDataProvider,
                        0,
                        0,
                        null,
                        command.CorrelationId,
                        CausationId.Producer.GroupBySeasonDocumentProcessor));
                });

                if (events.Any())
                    await _publishEndpoint.PublishBatch(events);

                // raise an integration event for the group
                await _publishEndpoint.Publish(new ConferenceCreated(newGroupEntity.ToCanonicalModel(), command.CorrelationId,
                    CausationId.Producer.GroupBySeasonDocumentProcessor));

                // and the season
                await _publishEndpoint.Publish(new ConferenceSeasonCreated(newGroupSeason.ToCanonicalModel(), command.CorrelationId,
                    CausationId.Producer.GroupBySeasonDocumentProcessor));

                await _dataContext.SaveChangesAsync();
            }
        }
    }
}
