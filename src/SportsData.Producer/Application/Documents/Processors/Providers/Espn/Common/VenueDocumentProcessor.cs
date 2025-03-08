using MassTransit;

using Microsoft.EntityFrameworkCore;

using Newtonsoft.Json;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Eventing.Events.Venues;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;

using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common
{
    public class VenueDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<VenueDocumentProcessor> _logger;
        private readonly BaseDataContext _dataContext;
        private readonly IPublishEndpoint _publishEndpoint;

        public VenueDocumentProcessor(
            ILogger<VenueDocumentProcessor> logger,
            BaseDataContext dataContext,
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
                await ProcessUpdate(command, espnDto);
            }
            else
            {
                await ProcessNewEntity(command, espnDto);
            }

        }

        private async Task ProcessNewEntity(ProcessDocumentCommand command, EspnVenueDto dto)
        {
            // 1. map to the entity and save it
            var newEntity = dto.AsEntity(Guid.NewGuid(), command.CorrelationId);
            _dataContext.Add(newEntity);

            // 2. Any images?
            var events = new List<ProcessImageRequest>();
            dto.Images?.ForEach(img =>
            {
                var imgId = Guid.NewGuid();
                events.Add(new ProcessImageRequest(
                    img.Href.AbsoluteUri,
                    imgId,
                    newEntity.Id,
                    $"{newEntity.Id}-{events.Count}.png",
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
            {
                _logger.LogInformation($"Requesting {events.Count} venue images.");
                await _publishEndpoint.PublishBatch(events, CancellationToken.None);
            }

            // 2. raise an integration event with the canonical model
            var evt = new VenueCreated(newEntity.ToCanonicalModel(), command.CorrelationId,
                CausationId.Producer.VenueCreatedDocumentProcessor);

            await _publishEndpoint.Publish(evt, CancellationToken.None);

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("New {@type} event {@evt}", DocumentType.Venue, evt);
        }

        private async Task ProcessUpdate(ProcessDocumentCommand command, EspnVenueDto dto)
        {
            // TODO: implement update
            await Task.CompletedTask;
        }
    }
}
