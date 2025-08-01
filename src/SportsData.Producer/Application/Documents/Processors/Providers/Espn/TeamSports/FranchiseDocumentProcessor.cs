﻿using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Eventing.Events.Franchise;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Franchise)]
public class FranchiseDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : TeamSportDataContext
{
    private readonly ILogger<FranchiseDocumentProcessor<TDataContext>> _logger;
    private readonly TDataContext _dataContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

    public FranchiseDocumentProcessor(
        ILogger<FranchiseDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IPublishEndpoint publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator)
    {
        _logger = logger;
        _dataContext = dataContext;
        _publishEndpoint = publishEndpoint;
        _externalRefIdentityGenerator = externalRefIdentityGenerator;
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
        var externalDto = command.Document.FromJson<EspnFranchiseDto>();

        if (externalDto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnFranchiseDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(externalDto.Ref?.ToString()))
        {
            _logger.LogError("EspnFranchiseDto Ref is null or empty. {@Command}", command);
            return;
        }

        // Determine if this entity exists. Do NOT trust that it says it is a new document!
        var entity = await _dataContext.Franchises
            .FirstOrDefaultAsync(x => x.ExternalIds.Any(z => z.Value == command.UrlHash &&
                                                             z.Provider == command.SourceDataProvider));

        if (entity is null)
        {
            await ProcessNewEntity(command, externalDto);
        }
        else
        {
            await ProcessUpdate(command, externalDto, entity);
        }

    }

    private async Task ProcessNewEntity(
        ProcessDocumentCommand command,
        EspnFranchiseDto dto)
    {
        // 1. map to the entity add it
        var newEntity = dto.AsEntity(
            _externalRefIdentityGenerator,
            command.Sport,
            command.CorrelationId);

        await _dataContext.AddAsync(newEntity);

        if (dto.Venue is not null && dto.Venue.Id > 0)
        {
            var venueId = await _dataContext.TryResolveFromDtoRefAsync(
                dto.Venue,
                command.SourceDataProvider,
                () => _dataContext.Venues.Include(x => x.ExternalIds).AsNoTracking(),
                _logger);

            if (venueId != null)
            {
                newEntity.VenueId = venueId.Value;
            }
            else
            {
                // TODO: What to do if the venue does not exist?
                // We have it on the Espn dto, but not in our db.
            }
        }

        // 2. any logos on the dto?
        var events = new List<ProcessImageRequest>();
        dto.Logos?.ForEach(logo =>
        {
            var imgId = Guid.NewGuid();
            events.Add(new ProcessImageRequest(
                logo.Href,
                imgId,
                newEntity.Id,
                $"{newEntity.Id}.png",
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
            _logger.LogInformation($"Requesting {events.Count} images for {command.DocumentType} {command.Season}");
            await _publishEndpoint.PublishBatch(events);
        }

        // 3. Child entities to be sourced
        if (dto.Team?.Ref is not null)
        {
            _logger.LogInformation("Requesting Team document: {Ref}", dto.Team.Ref);

            await _publishEndpoint.Publish(new DocumentRequested(
                dto.Team.Ref.Segments.Last().TrimEnd('/'),
                newEntity.Id.ToString(),
                dto.Team.Ref,
                command.Sport,
                command.Season,
                DocumentType.TeamSeason,
                command.SourceDataProvider,
                command.CorrelationId,
                CausationId.Producer.FranchiseDocumentProcessor));
        }

        if (dto.Awards?.Ref is not null)
        {
            _logger.LogInformation("Requesting Franchise Awards document: {Ref}", dto.Awards.Ref);

            await _publishEndpoint.Publish(new DocumentRequested(
                dto.Awards.Ref.Segments.Last().TrimEnd('/'),
                newEntity.Id.ToString(),
                dto.Awards.Ref,
                command.Sport,
                command.Season,
                DocumentType.Award,
                command.SourceDataProvider,
                command.CorrelationId,
                CausationId.Producer.FranchiseDocumentProcessor));
        }

        // 4. Raise the integration event
        await _publishEndpoint.Publish(
            new FranchiseCreated(
                newEntity.ToCanonicalModel(),
                command.CorrelationId,
                CausationId.Producer.FranchiseDocumentProcessor));

        await _dataContext.SaveChangesAsync();
    }

    private async Task ProcessUpdate(
        ProcessDocumentCommand command,
        EspnFranchiseDto dto,
        Franchise entity)
    {
        var franchise = await _dataContext.Franchises
            .Include(x => x.ExternalIds)
            .FirstAsync(x => x.ExternalIds.Any(z => z.Value == command.UrlHash &&
                                                    z.Provider == command.SourceDataProvider));

        if (dto.Venue is not null)
        {
            var venue = await _dataContext.Venues
                .Include(x => x.ExternalIds)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ExternalIds.Any(z =>
                    z.Provider == command.SourceDataProvider &&
                    z.Value == dto.Venue.Ref.ToString().UrlHash(true)));

            if (venue != null)
            {
                if (venue.Id != franchise.VenueId)
                {
                    _logger.LogInformation("Updating Venue from {Old} to {New}", franchise.VenueId, venue.Id);
                    franchise.VenueId = venue.Id;
                }
            }
        }

        var updated = false;

        if (franchise.Name != dto.Name)
        {
            _logger.LogInformation("Updating Name from {Old} to {New}", franchise.Name, dto.Name);
            franchise.Name = dto.Name;
            updated = true;
        }

        if (franchise.Location != dto.Location)
        {
            _logger.LogInformation("Updating Location from {Old} to {New}", franchise.Location, dto.Location);
            franchise.Location = dto.Location;
            updated = true;
        }

        if (franchise.Slug != dto.Slug)
        {
            _logger.LogInformation("Updating Slug from {Old} to {New}", franchise.Slug, dto.Slug);
            franchise.Slug = dto.Slug;
            updated = true;
        }

        if (franchise.Abbreviation != dto.Abbreviation)
        {
            _logger.LogInformation("Updating Abbreviation from {Old} to {New}", franchise.Abbreviation, dto.Abbreviation);
            franchise.Abbreviation = dto.Abbreviation;
            updated = true;
        }

        if (franchise.DisplayNameShort != dto.ShortDisplayName)
        {
            _logger.LogInformation("Updating ShortDisplayName from {Old} to {New}", franchise.DisplayNameShort, dto.ShortDisplayName);
            franchise.DisplayNameShort = dto.ShortDisplayName;
            updated = true;
        }

        if (franchise.DisplayName != dto.DisplayName)
        {
            _logger.LogInformation("Updating DisplayName from {Old} to {New}", franchise.DisplayName, dto.DisplayName);
            franchise.DisplayName = dto.DisplayName;
            updated = true;
        }

        if (franchise.ColorCodeHex != dto.Color)
        {
            _logger.LogInformation("Updating Color from {Old} to {New}", franchise.ColorCodeHex, dto.Color);
            franchise.ColorCodeHex = dto.Color;
            updated = true;
        }

        if (franchise.IsActive != dto.IsActive)
        {
            _logger.LogInformation("Updating IsActive from {Old} to {New}", franchise.IsActive, dto.IsActive);
            franchise.IsActive = dto.IsActive;
            updated = true;
        }

        if (updated)
        {
            await _dataContext.SaveChangesAsync();

            var evt = new FranchiseUpdated(
                franchise.ToCanonicalModel(),
                command.CorrelationId,
                CausationId.Producer.FranchiseDocumentProcessor);

            await _publishEndpoint.Publish(evt, CancellationToken.None);

            _logger.LogInformation("Published update for Franchise {Id}", franchise.Id);
        }
        else
        {
            _logger.LogInformation("No changes detected for Franchise {Id}", franchise.Id);
        }
    }

}