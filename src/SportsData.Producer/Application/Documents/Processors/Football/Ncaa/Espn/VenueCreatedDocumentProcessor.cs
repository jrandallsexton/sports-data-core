using MassTransit;

using Microsoft.EntityFrameworkCore;

using Newtonsoft.Json;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Venues;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Football.Ncaa.Espn
{
    public class VenueCreatedDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<VenueCreatedDocumentProcessor> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IPublishEndpoint _publishEndpoint;

        public VenueCreatedDocumentProcessor(
            ILogger<VenueCreatedDocumentProcessor> logger,
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
            var espnDto = command.Document.FromJson<EspnVenueDto>(new JsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore
            });

            // Determine if this entity exists. Do NOT trust that it says it is a new document!
            var exists = await _dataContext.Venues.AnyAsync(x =>
                x.ExternalIds.Any(z => z.Value == espnDto.Id.ToString() && z.Provider == command.SourceDataProvider));

            if (exists)
            {
                _logger.LogWarning("Venue already exists.");
                return;
            }

            // 1. map to the entity and save it
            var newVenueEntity = espnDto.AsVenueEntity(Guid.NewGuid(), command.CorrelationId);
            await _dataContext.AddAsync(newVenueEntity);

            // 2. raise an event
            var evt = new VenueCreated()
            {
                Id = newVenueEntity.Id.ToString(),
                CorrelationId = command.CorrelationId,
                Canonical = newVenueEntity.ToCanonicalModel()
            };

            await _publishEndpoint.Publish(evt);

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("New {@type} event {@evt}", DocumentType.Venue, evt);
        }
    }
}
