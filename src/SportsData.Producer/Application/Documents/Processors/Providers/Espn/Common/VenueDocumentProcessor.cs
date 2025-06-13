using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Eventing.Events.Venues;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Venue)]
    public class VenueDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : BaseDataContext
    {
        private readonly ILogger<VenueDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IPublishEndpoint _publishEndpoint;

        public VenueDocumentProcessor(
            ILogger<VenueDocumentProcessor<TDataContext>> logger,
            TDataContext dataContext,
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
            // Deserialize the DTO
            var espnDto = command.Document.FromJson<EspnVenueDto>();

            if (espnDto is null)
            {
                _logger.LogError("Failed to deserialize document into EspnVenueDto. DocumentType: {DocumentType}, SourceDataProvider: {Provider}, CorrelationId: {CorrelationId}",
                    command.DocumentType, command.SourceDataProvider, command.CorrelationId);

                throw new InvalidOperationException($"Deserialization returned null for EspnVenueDto. CorrelationId: {command.CorrelationId}");
            }

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
            var events = EventFactory.CreateProcessImageRequests(
                dto.Images,
                newEntity.Id,
                command.Sport,
                command.Season,
                command.DocumentType,
                command.SourceDataProvider,
                command.CorrelationId,
                CausationId.Producer.VenueDocumentProcessor);

            if (events.Count > 0)
            {
                _logger.LogInformation("Requesting {Count} venue images.", events.Count);
                await _publishEndpoint.PublishBatch(events);
            }

            // 2. raise an integration event with the canonical model
            var evt = new VenueCreated(newEntity.AsCanonical(), command.CorrelationId,
                CausationId.Producer.VenueCreatedDocumentProcessor);

            await _publishEndpoint.Publish(evt, CancellationToken.None);

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("New {@type} event {@evt}", DocumentType.Venue, evt);
        }

        private async Task ProcessUpdate(ProcessDocumentCommand command, EspnVenueDto dto)
        {
            var venue = await _dataContext.Venues
                .Include(x => x.ExternalIds)
                .Include(x => x.Images)
                .FirstAsync(x => x.ExternalIds.Any(z => z.Value == dto.Id.ToString() && z.Provider == command.SourceDataProvider));

            var updated = false;

            if (venue.Name != dto.FullName)
            {
                _logger.LogInformation("Updating FullName from {Old} to {New}", venue.Name, dto.FullName);
                venue.Name = dto.FullName;
                updated = true;
            }

            if (venue.IsGrass != dto.Grass)
            {
                _logger.LogInformation("Updating Grass from {Old} to {New}", venue.IsGrass, dto.Grass);
                venue.IsGrass = dto.Grass;
                updated = true;
            }

            if (venue.IsIndoor != dto.Indoor)
            {
                _logger.LogInformation("Updating Indoor from {Old} to {New}", venue.IsIndoor, dto.Indoor);
                venue.IsIndoor = dto.Indoor;
                updated = true;
            }

            if (venue.Capacity != dto.Capacity)
            {
                _logger.LogInformation("Updating Capacity from {Old} to {New}", venue.Capacity, dto.Capacity);
                venue.Capacity = dto.Capacity;
                updated = true;
            }

            var address = dto.Address;

            if (venue.City != address.City ||
                 venue.State != address.State ||
                 venue.PostalCode != address.ZipCode.ToString() ||
                 venue.Country != address.Country)
            {
                _logger.LogInformation("Updating address");
                venue.City = address.City;
                venue.State = address.State;
                venue.PostalCode = address.ZipCode.ToString();
                venue.Country = address.Country;
                updated = true;
            }

            // === Detect new images
            var newImages = dto.Images?
                .Where(img => !venue.Images.Any(v => v.OriginalUrlHash == HashProvider.GenerateHashFromUri(img.Href)))
                .ToList();

            if (newImages?.Count > 0)
            {
                _logger.LogInformation("Found {Count} new images for venue", newImages.Count);

                var imageEvents = new List<ProcessImageRequest>();

                for (int i = 0; i < newImages.Count; i++)
                {
                    var img = newImages[i];
                    imageEvents.Add(new ProcessImageRequest(
                        img.Href,
                        Guid.NewGuid(),
                        venue.Id,
                        $"{venue.Id}-u{i}.png",
                        command.Sport,
                        command.Season,
                        command.DocumentType,
                        command.SourceDataProvider,
                        0,
                        0,
                        null,
                        command.CorrelationId,
                        CausationId.Producer.FranchiseDocumentProcessor));
                }

                await _publishEndpoint.PublishBatch(imageEvents, CancellationToken.None);
            }

            if (updated || newImages?.Count > 0)
            {
                await _dataContext.SaveChangesAsync();

                var evt = new VenueUpdated(venue.AsCanonical(), command.CorrelationId,
                    CausationId.Producer.VenueCreatedDocumentProcessor);

                await _publishEndpoint.Publish(evt, CancellationToken.None);

                _logger.LogInformation("Updated venue {@Venue}", evt);
            }
            else
            {
                _logger.LogInformation("No changes detected for venue {Id}", venue.Id);
            }
        }

    }
}
