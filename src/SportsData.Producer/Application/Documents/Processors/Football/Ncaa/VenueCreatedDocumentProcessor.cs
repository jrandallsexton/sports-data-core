using MassTransit;

using Microsoft.EntityFrameworkCore;

using Newtonsoft.Json;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Venues;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Models.Canonical;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Football.Ncaa
{
    public class VenueCreatedDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<VenueCreatedDocumentProcessor> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IBus _bus;

        public VenueCreatedDocumentProcessor(
            ILogger<VenueCreatedDocumentProcessor> logger,
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
            var espnDto = command.Document.FromJson<EspnVenueDto>(new JsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore
            });

            // Determine if this entity exists. Do NOT trust that it says it is a new document!
            var exists = await _dataContext.Venues.AnyAsync(x =>
                x.ExternalIds.Any(z => z.Value == espnDto.Id.ToString() && z.Provider == command.SourceDataProvider));

            if (exists)
            {
                _logger.LogWarning($"Venue already exists for {SourceDataProvider.Espn}.");
                return;
            }

            // 1. map to the entity and save it
            // TODO: Move to extension method?
            var venueEntity = new Venue()
            {
                Id = Guid.NewGuid(),
                Name = espnDto.FullName,
                ShortName = espnDto.ShortName,
                IsIndoor = espnDto.Indoor,
                IsGrass = espnDto.Grass,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = command.CorrelationId,
                ExternalIds = [new VenueExternalId() { Id = Guid.NewGuid(), Value = espnDto.Id.ToString(), Provider = SourceDataProvider.Espn }]
            };
            await _dataContext.AddAsync(venueEntity);
            await _dataContext.SaveChangesAsync();

            // 2. raise an event
            // TODO: Determine if I want to publish all data in the event instead of this chatty stuff
            var evt = new VenueCreated()
            {
                Id = venueEntity.Id.ToString(),
                CorrelationId = command.CorrelationId,
                Canonical = new VenueCanonicalModel()
                {
                    CreatedUtc = venueEntity.CreatedUtc,
                    Id = venueEntity.Id,
                    IsGrass = venueEntity.IsGrass,
                    IsIndoor = venueEntity.IsIndoor,
                    Name = venueEntity.Name,
                    ShortName = venueEntity.ShortName
                }
            };
            await _bus.Publish(evt);
            _logger.LogInformation("New {@type} event {@evt}", DocumentType.Venue, evt);
        }
    }
}
