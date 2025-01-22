using MassTransit;

using Microsoft.EntityFrameworkCore;

using Newtonsoft.Json;

using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Extensions;

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Football.Ncaa
{
    public class GroupBySeasonDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<GroupBySeasonDocumentProcessor> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IBus _bus;

        public GroupBySeasonDocumentProcessor(
            ILogger<GroupBySeasonDocumentProcessor> logger,
            AppDataContext dataContext,
            IBus bus)
        {
            _logger = logger;
            _dataContext = dataContext;
            _bus = bus;
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
                var newSeasonId = Guid.NewGuid();

                // create the new group ...
                var group = new Group()
                {
                    Id = Guid.NewGuid(),
                    Abbreviation = externalProviderDto.Abbreviation,
                    CreatedBy = command.CorrelationId,
                    CreatedUtc = DateTime.UtcNow,
                    ExternalIds = [new GroupExternalId() { Id = Guid.NewGuid(), Value = externalProviderDto.Id.ToString(), Provider = command.SourceDataProvider }],
                    IsConference = externalProviderDto.IsConference,
                    MidsizeName = externalProviderDto.MidsizeName,
                    Name = externalProviderDto.Name,
                    //ParentGroupId = espnDto.Parent. // TODO: Determine how to set/get this
                    ShortName = externalProviderDto.ShortName,
                    Seasons =
                    [
                        // ... and add the season to it
                        new GroupSeason()
                        {
                            Id = newSeasonId,
                            CreatedUtc = DateTime.UtcNow,
                            CreatedBy = command.CorrelationId,
                            Season = command.Season.Value
                        }
                    ]
                };

                await _dataContext.Groups.AddAsync(group);
                await _dataContext.SaveChangesAsync();

                // any logos on the dto?
                var events = new List<ProcessImageRequest>();
                externalProviderDto.Logos.ForEach(logo =>
                {
                    var imgId = Guid.NewGuid();
                    events.Add(new ProcessImageRequest(
                        logo.Href,
                        imgId,
                        newSeasonId,
                        $"{newSeasonId}.png",
                        command.Sport,
                        command.Season,
                        command.DocumentType,
                        command.SourceDataProvider,
                        0,
                        0,
                        null));
                });

                if (events.Any())
                    await _bus.PublishBatch(events);
            }
        }
    }
}
