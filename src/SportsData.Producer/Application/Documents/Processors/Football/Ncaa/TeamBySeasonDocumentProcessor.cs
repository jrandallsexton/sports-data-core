using MassTransit;

using Microsoft.EntityFrameworkCore;

using Newtonsoft.Json;

using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Exceptions;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Football.Ncaa
{
    public class TeamBySeasonDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<TeamBySeasonDocumentProcessor> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IBus _bus;

        public TeamBySeasonDocumentProcessor(
            ILogger<TeamBySeasonDocumentProcessor> logger,
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

            var franchiseBySeasonId = Guid.NewGuid();
            franchiseExternalId.Franchise.Seasons.Add(new FranchiseSeason()
            {
                Abbreviation = espnDto.Abbreviation,
                ColorCodeAltHex = espnDto.AlternateColor,
                ColorCodeHex = espnDto.Color,
                CreatedBy = command.CorrelationId,
                CreatedUtc = DateTime.UtcNow,
                DisplayName = espnDto.DisplayName,
                DisplayNameShort = espnDto.ShortDisplayName,
                FranchiseId = franchiseExternalId.Franchise.Id,
                GlobalId = Guid.NewGuid(),
                Id = franchiseBySeasonId,
                IsActive = espnDto.IsActive,
                IsAllStar = espnDto.IsAllStar,
                Location = espnDto.Location,
                Logos = [],
                Losses = 0,
                Name = espnDto.Name,
                Season = command.Season.Value,
                Slug =espnDto.Slug,
                Ties = 0,
                Wins = 0
            });

            await _dataContext.SaveChangesAsync();

            // any logos on the dto?
            var events = new List<ProcessImageRequest>();
            espnDto.Logos.ForEach(logo =>
            {
                var imgId = Guid.NewGuid();
                events.Add(new ProcessImageRequest(
                    logo.Href.ToString(),
                    imgId,
                    franchiseExternalId.Franchise.Id,
                    $"{franchiseBySeasonId}.png",
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
