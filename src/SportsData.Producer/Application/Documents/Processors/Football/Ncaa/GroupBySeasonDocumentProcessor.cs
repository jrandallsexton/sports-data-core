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
            var group = await _dataContext.Groups.FirstOrDefaultAsync(x =>
                x.ExternalIds.Where(z => z.Value == espnDto.Id.ToString() && z.Provider == SourceDataProvider.Espn))
                .ToListAsync();
        }
    }
}
