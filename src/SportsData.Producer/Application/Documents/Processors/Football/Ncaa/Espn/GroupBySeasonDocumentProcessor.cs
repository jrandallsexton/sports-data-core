﻿using MassTransit;

using Microsoft.EntityFrameworkCore;

using Newtonsoft.Json;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Conferences;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Extensions;

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Football.Ncaa.Espn
{
    public class GroupBySeasonDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<GroupBySeasonDocumentProcessor> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IPublishEndpoint _publishEndpoint;

        public GroupBySeasonDocumentProcessor(
            ILogger<GroupBySeasonDocumentProcessor> logger,
            AppDataContext dataContext,
            IPublishEndpoint publishEndpoint)
        {
            _logger = logger;
            _dataContext = dataContext;
            _publishEndpoint = publishEndpoint;
        }

        public async Task ProcessAsync(ProcessDocumentCommand command)
        {
            // deserialize the DTO
            var externalProviderDto = command.Document.FromJson<EspnGroupBySeasonDto>(new JsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore
            });

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
                _logger.LogWarning($"Group already exists for {command.SourceDataProvider}.");

                var groupTmp = await _dataContext.GroupExternalIds
                    .Include(x => x.Group)
                    .ThenInclude(x => x.Seasons)
                    .FirstOrDefaultAsync(x => x.Value == externalProviderDto.Id.ToString() &&
                                              x.Provider == command.SourceDataProvider);

                if (groupTmp is { Group: not null })
                {
                    // if the incoming season does not exist, add a new season to the existing group
                    if (!groupTmp.Group.Seasons.Any(s => s.Season == command.Season.Value))
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
                var newGroupEntity = externalProviderDto.AsGroupEntity(newGroupId, command.CorrelationId);
                var newGroupSeason = (externalProviderDto
                    .AsGroupSeasonEntity(
                        newGroupId,
                        Guid.NewGuid(),
                        command.Season.Value,
                        command.CorrelationId));

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
