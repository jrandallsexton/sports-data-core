using MassTransit;

using Microsoft.EntityFrameworkCore;

using Newtonsoft.Json;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Images;
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
            using (_logger.BeginScope(new Dictionary<string, Guid>()
                   {
                       { "CorrelationId", command.CorrelationId }
                   }))
            {
                _logger.LogInformation("Began with {@command}", command);

                await ProcessInternal(command);
            }
        }

        private async Task ProcessInternal(ProcessDocumentCommand command)
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
                // TODO: Determine what to do here.  Publish the correct event? Pass it directly to the correct handler?
                _logger.LogWarning("Venue already exists.");
                return;
            }

            // 1. map to the entity and save it
            var newVenueEntity = espnDto.AsVenueEntity(Guid.NewGuid(), command.CorrelationId);
            _dataContext.Add(newVenueEntity);

            // 2. Any images?
            var events = new List<ProcessImageRequest>();
            espnDto.Images?.ForEach(img =>
            {
                var imgId = Guid.NewGuid();
                events.Add(new ProcessImageRequest(
                    img.Href.AbsoluteUri,
                    imgId,
                    newVenueEntity.Id,
                    $"{newVenueEntity.Id}.png",
                    command.Sport,
                    command.Season,
                    command.DocumentType,
                    command.SourceDataProvider,
                    0,
                    0,
                    null,
                    command.CorrelationId,
                    CausationId.Producer.FranchiseDocumentProcessor));
            });

            if (events.Count > 0)
                await _publishEndpoint.PublishBatch(events);

            // 2. raise an integration event with the canonical model
            var evt = new VenueCreated(newVenueEntity.ToCanonicalModel(), command.CorrelationId, CausationId.Producer.VenueCreatedDocumentProcessor);

            await _publishEndpoint.Publish(evt);

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("New {@type} event {@evt}", DocumentType.Venue, evt);
        }
    }
}
