using MassTransit;

using Microsoft.EntityFrameworkCore;

using Newtonsoft.Json;

using SportsData.Core.Common;
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
            var espnDto = command.Document.FromJson<EspnGroupBySeasonDto>(new JsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore
            });

            // Determine if this entity exists. Do NOT trust that it says it is a new document!
            var exists = await _dataContext.Venues.AnyAsync(x =>
                x.ExternalIds.Any(z => z.Value == espnDto.Id.ToString() && z.Provider == SourceDataProvider.Espn));

            if (exists)
            {
                _logger.LogWarning($"Venue already exists for {SourceDataProvider.Espn}.");
                return;
            }

            // 1. Does this group (conference) exist? If not, we must create it prior to creating a season for it
            var groupTmp = await _dataContext.GroupExternalIds
                .Include(x => x.Group)
                .ThenInclude(x => x.Seasons)
                .FirstOrDefaultAsync(x => x.Value == espnDto.Id.ToString() && x.Provider == SourceDataProvider.Espn);

            if (groupTmp is { Group: not null })
            {
                // if the incoming season does not exist, add a new season to the existing group
                if (!groupTmp.Group.Seasons.Any(s => s.Season == command.Season.Value))
                {

                }
                else
                {
                    // we already have this season for this group
                    // TODO: Update this later
                }
            }
            else
            {
                // create the new group ...
                var group = new Group()
                {
                    Id = Guid.NewGuid(),
                    Abbreviation = espnDto.Abbreviation,
                    CreatedBy = command.CorrelationId,
                    CreatedUtc = DateTime.UtcNow,
                    ExternalIds =
                        [new GroupExternalId() { Value = espnDto.Id.ToString(), Provider = SourceDataProvider.Espn }],
                    GlobalId = Guid.NewGuid(),
                    IsConference = espnDto.IsConference,
                    MidsizeName = espnDto.MidsizeName,
                    Name = espnDto.Name,
                    //ParentGroupId = espnDto.Parent. // TODO: Determine how to set/get this
                    ShortName = espnDto.ShortName,
                    Seasons =
                    [
                        // ... and add the season to it
                        new GroupSeason()
                        {
                            Id = Guid.NewGuid(),
                            CreatedUtc = DateTime.UtcNow,
                            CreatedBy = command.CorrelationId,
                            GlobalId = Guid.NewGuid(),
                            Season = command.Season.Value
                        }
                    ]
                };
                await _dataContext.Groups.AddAsync(group);
                await _dataContext.SaveChangesAsync();

                // raise an event
            }
        }
    }
}
