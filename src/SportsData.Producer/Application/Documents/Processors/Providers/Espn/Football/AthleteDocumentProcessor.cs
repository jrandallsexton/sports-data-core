﻿using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Athletes;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

// TODO: Rename to FootballAthleteDocumentProcessor
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Athlete)]
public class AthleteDocumentProcessor : IProcessDocuments
{
    private readonly ILogger<AthleteDocumentProcessor> _logger;
    private readonly FootballDataContext _dataContext;
    private readonly IBus _publishEndpoint;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

    public AthleteDocumentProcessor(
        ILogger<AthleteDocumentProcessor> logger,
        FootballDataContext dataContext,
        IBus publishEndpoint,
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
        var externalProviderDto = command.Document.FromJson<EspnFootballAthleteDto>();

        if (externalProviderDto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnFootballAthleteDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(externalProviderDto.Ref?.ToString()))
        {
            _logger.LogError("EspnFootballAthleteDto Ref is null. {@Command}", command);
            return;
        }

        // Determine if this entity exists. Do NOT trust that it says it is a new document!
        var exists = await _dataContext.Athletes
            .Include(x => x.ExternalIds)
            .AsNoTracking()
            .AnyAsync(x => x.ExternalIds.Any(z => z.Value == command.UrlHash &&
                                                  z.Provider == command.SourceDataProvider));

        if (exists)
        {
            // TODO: Eventually we need to handle updates to existing entities.
            _logger.LogWarning($"Athlete already exists for {command.SourceDataProvider}.");
            return;
        }

        // 1. map to the entity add it
        // TODO: Get the current franchise Id from the athleteDto?
        var newEntity = externalProviderDto.AsFootballAthlete(_externalRefIdentityGenerator, null, command.CorrelationId);

        // 2. any headshot (image) for the AthleteDto?
        if (externalProviderDto.Headshot is not null)
        {
            var newImgId = Guid.NewGuid();
            var imgEvt = new ProcessImageRequest(
                externalProviderDto.Headshot.Href,
                newImgId,
                newEntity.Id,
                $"{newEntity.Id}-{newImgId}.png",
                command.Sport,
                command.Season,
                command.DocumentType,
                command.SourceDataProvider,
                0,
                0,
                null,
                command.CorrelationId,
                CausationId.Producer.AthleteDocumentProcessor);
            await _publishEndpoint.Publish(imgEvt);
        }

        // birthplace
        await ProcessBirthplace(externalProviderDto, newEntity);

        // athlete status
        await ProcessAthleteStatus(externalProviderDto, newEntity);

        // current position
        await ProcessCurrentPosition(
            externalProviderDto,
            newEntity,
            command);

        // 3. Raise the integration event
        await _publishEndpoint.Publish(
            new AthleteCreated(
                newEntity.ToCanonicalModel(),
                command.CorrelationId,
                CausationId.Producer.AthleteDocumentProcessor));

        await _dataContext.AddAsync(newEntity);
        await _dataContext.SaveChangesAsync();
    }

    private async Task ProcessCurrentPosition(
        EspnFootballAthleteDto externalProviderDto,
        FootballAthlete newEntity,
        ProcessDocumentCommand command)
    {
        var positionIdentity = _externalRefIdentityGenerator.Generate(externalProviderDto.Position.Ref);

        var positionId = await _dataContext.AthletePositionExternalIds
            .Where(x => x.Provider == command.SourceDataProvider
                        && x.SourceUrlHash == positionIdentity.UrlHash)
            .Select(x => x.AthletePositionId)
            .FirstOrDefaultAsync();

        if (positionId == Guid.Empty)
        {
            await _publishEndpoint.Publish(new DocumentRequested(
                Id: positionIdentity.CanonicalId.ToString(),
                ParentId: null,
                Uri: externalProviderDto.Position.Ref,
                Sport: Sport.FootballNcaa,
                SeasonYear: command.Season,
                DocumentType: DocumentType.AthletePosition,
                SourceDataProvider: SourceDataProvider.Espn,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.AthleteDocumentProcessor
            ));

            _logger.LogWarning("No AthletePosition found. {@Identity}", positionIdentity);
            throw new InvalidOperationException($"No AthletePosition found for {externalProviderDto.Position.Ref}. " +
                                                $"Please ensure the position document is processed before this athlete.");

        }

        newEntity.PositionId = positionId;

        _logger.LogInformation("Resolved CurrentPositionId: {PositionId} for AthleteId: {AthleteId}", positionId, newEntity.Id);
    }


    private async Task ProcessAthleteStatus(
        EspnFootballAthleteDto externalProviderDto,
        Athlete newEntity)
    {
        if (externalProviderDto.Status is null)
        {
            return;
        }

        var name = externalProviderDto.Status.Name?.Trim();
        var abbreviation = externalProviderDto.Status.Abbreviation?.Trim();
        var type = externalProviderDto.Status.Type?.Trim();
        var externalId = externalProviderDto.Status.Id;

        if (string.IsNullOrEmpty(name))
        {
            _logger.LogInformation("AthleteStatus is empty for athlete {AthleteId}. Skipping status creation.", newEntity.Id);
            return;
        }

        var nameLower = name.ToLower();

        // 1️⃣ Look for existing
        var status = await _dataContext.AthleteStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                (x.Name ?? "").ToLower() == nameLower);

        if (status is null)
        {
            // 2️⃣ Create new
            var newStatus = new AthleteStatus
            {
                Id = Guid.NewGuid(),
                Name = name,
                Abbreviation = abbreviation,
                Type = type,
                ExternalId = externalId.ToString()
            };

            await _dataContext.AthleteStatuses.AddAsync(newStatus);
            await _dataContext.SaveChangesAsync();

            // 3️⃣ Confirm it's persisted
            status = await _dataContext.AthleteStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == newStatus.Id);

            if (status is null)
            {
                throw new InvalidOperationException($"Failed to persist AthleteStatus for athlete {newEntity.Id}. FK assignment aborted.");
            }
        }

        // 4️⃣ Always safe assignment
        newEntity.StatusId = status.Id;
    }

    private async Task ProcessBirthplace(
        EspnFootballAthleteDto externalProviderDto,
        Athlete newEntity)
    {
        if (externalProviderDto.BirthPlace is null)
        {
            return;
        }

        var city = externalProviderDto.BirthPlace.City?.Trim();
        var state = externalProviderDto.BirthPlace.State?.Trim();
        var country = externalProviderDto.BirthPlace.Country?.Trim();

        if (string.IsNullOrEmpty(city) && string.IsNullOrEmpty(state) && string.IsNullOrEmpty(country))
        {
            _logger.LogInformation("BirthPlace is empty for athlete {AthleteId}. Skipping location creation.", newEntity.Id);
            return;
        }

        var cityLower = city?.ToLower();
        var stateLower = state?.ToLower();
        var countryLower = country?.ToLower();

        // 1️⃣ Check if it already exists
        var location = await _dataContext.Locations
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                (x.City ?? "").ToLower() == (cityLower ?? "") &&
                (x.State ?? "").ToLower() == (stateLower ?? "") &&
                (x.Country ?? "").ToLower() == (countryLower ?? ""));

        if (location is null)
        {
            // 2️⃣ Create it
            var newLocation = new Location
            {
                Id = Guid.NewGuid(),
                City = city,
                State = state,
                Country = country
            };

            await _dataContext.Locations.AddAsync(newLocation);
            await _dataContext.SaveChangesAsync();

            // 3️⃣ Re-fetch to confirm it's actually persisted
            location = await _dataContext.Locations
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == newLocation.Id);

            if (location is null)
            {
                throw new InvalidOperationException($"Failed to persist Location for athlete {newEntity.Id}. FK assignment aborted.");
            }
        }

        // 4️⃣ Safe to assign FK
        newEntity.BirthLocationId = location.Id;
    }
}