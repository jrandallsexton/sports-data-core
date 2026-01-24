using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Eventing.Events.Venues;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Venue)]
public class VenueDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : BaseDataContext
{
    private readonly ILogger<VenueDocumentProcessor<TDataContext>> _logger;
    private readonly TDataContext _dataContext;
    private readonly IEventBus _publishEndpoint;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
    private readonly IGenerateResourceRefs _resourceRefGenerator;

    public VenueDocumentProcessor(
        ILogger<VenueDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs resourceRefGenerator
        )
    {
        _logger = logger;
        _dataContext = dataContext;
        _publishEndpoint = publishEndpoint;
        _externalRefIdentityGenerator = externalRefIdentityGenerator;
        _resourceRefGenerator = resourceRefGenerator;
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
        var espnDto = command.Document.FromJson<EspnVenueDto>();

        if (espnDto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnVenueDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(espnDto.Ref?.ToString()))
        {
            _logger.LogError("EspnVenueDto Ref is null for venue. {@Command}", command);
            return;
        }

        // Determine if this entity exists. Do NOT trust that it says it is a new document!
        var exists = await _dataContext.Venues
            .AnyAsync(x => x.ExternalIds.Any(z => z.Value == command.UrlHash &&
                                                  z.Provider == command.SourceDataProvider));

        if (exists)
        {
            //_logger.LogInformation("Update detected; not implemented");
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
        var newEntity = dto.AsEntity(
            _externalRefIdentityGenerator,
            command.CorrelationId);

        _dataContext.Add(newEntity);

        // 2. Any images?
        var events = EventFactory.CreateProcessImageRequests(
            _externalRefIdentityGenerator,
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
        var evt = new VenueCreated(
            newEntity.AsCanonical(),
            _resourceRefGenerator.ForVenue(newEntity.Id),
            command.Sport,
            command.Season,
            command.CorrelationId,
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
            .AsSplitQuery()
            .FirstAsync(x => x.ExternalIds.Any(z => z.Value == command.UrlHash &&
                                                    z.Provider == command.SourceDataProvider));

        // Map DTO to a temporary entity with all properties populated
        var updatedEntity = dto.AsEntity(
            _externalRefIdentityGenerator,
            command.CorrelationId);

        // Preserve immutable fields before SetValues overwrites them
        var originalId = venue.Id;
        var originalCreatedBy = venue.CreatedBy;
        var originalCreatedUtc = venue.CreatedUtc;

        // Use EF Core's SetValues to update all scalar properties automatically
        // This compares every property and marks only changed ones as Modified
        var entry = _dataContext.Entry(venue);
        entry.CurrentValues.SetValues(updatedEntity);

        // Restore immutable fields - SetValues overwrites in-memory values
        venue.Id = originalId;
        venue.CreatedBy = originalCreatedBy;
        venue.CreatedUtc = originalCreatedUtc;

        // Check if EF detected any changes to scalar properties
        var scalarPropertiesChanged = entry.State == EntityState.Modified;

        // Log which properties changed (helpful for debugging)
        if (scalarPropertiesChanged)
        {
            var changedProperties = entry
                .Properties
                .Where(p => p.IsModified)
                .Select(p => $"{p.Metadata.Name}: {p.OriginalValue} -> {p.CurrentValue}")
                .ToList();

            _logger.LogInformation(
                "Updating Venue. VenueId={VenueId}, ChangedProperties={Changes}",
                venue.Id,
                string.Join(", ", changedProperties));
        }

        // Detect new images
        var newImages = dto.Images?
            .Where(img => !venue.Images.Any(v => v.OriginalUrlHash == HashProvider.GenerateHashFromUri(img.Href)))
            .ToList();

        if (newImages?.Count > 0)
        {
            _logger.LogInformation("Found {Count} new images for venue", newImages.Count);

            var imageEvents = EventFactory.CreateProcessImageRequests(
                _externalRefIdentityGenerator,
                newImages,
                venue.Id,
                command.Sport,
                command.Season,
                command.DocumentType,
                command.SourceDataProvider,
                command.CorrelationId,
                CausationId.Producer.VenueDocumentProcessor);

            if (imageEvents.Count > 0)
            {
                await _publishEndpoint.PublishBatch(imageEvents, CancellationToken.None);
            }
        }

        // Save if there were any changes (scalar properties or new images)
        if (scalarPropertiesChanged || newImages?.Count > 0)
        {
            // Update audit fields
            venue.ModifiedUtc = DateTime.UtcNow;
            venue.ModifiedBy = command.CorrelationId;

            await _dataContext.SaveChangesAsync();

            var evt = new VenueUpdated(
                venue.AsCanonical(),
                null,
                command.Sport,
                command.Season,
                command.CorrelationId,
                CausationId.Producer.VenueDocumentProcessor);

            await _publishEndpoint.Publish(evt, CancellationToken.None);

            _logger.LogInformation(
                "Venue updated. VenueId={VenueId}, ScalarChanges={ScalarChanges}, NewImages={NewImages}",
                venue.Id,
                scalarPropertiesChanged,
                newImages?.Count ?? 0);
        }
        else
        {
            _logger.LogInformation("No changes detected for venue {Id}", venue.Id);
        }
    }

}