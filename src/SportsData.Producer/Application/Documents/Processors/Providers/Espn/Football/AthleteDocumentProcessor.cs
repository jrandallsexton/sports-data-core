using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Athletes;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
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
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

    public AthleteDocumentProcessor(
        ILogger<AthleteDocumentProcessor> logger,
        FootballDataContext dataContext,
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
            _logger.LogInformation("Processing EventDocument with {@Command}", command);
            try
            {
                await ProcessInternal(command);
            }
            catch (ExternalDocumentNotSourcedException retryEx)
            {
                _logger.LogWarning(retryEx, "Dependency not ready. Will retry later.");
                var docCreated = command.ToDocumentCreated(command.AttemptCount + 1);
                await _publishEndpoint.Publish(docCreated);
                await _dataContext.OutboxPings.AddAsync(new OutboxPing());
                await _dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing. {@Command}", command);
                throw;
            }
        }
    }

    private async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnFootballAthleteDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnFootballAthleteDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnFootballAthleteDto Ref is null. {@Command}", command);
            return;
        }

        var exists = await _dataContext.Athletes
            .Include(x => x.ExternalIds)
            .AsNoTracking()
            .AnyAsync(x => x.ExternalIds.Any(z =>
                z.Value == command.UrlHash &&
                z.Provider == command.SourceDataProvider));

        if (exists)
        {
            await ProcessExisting(command, dto);
        }
        else
        {
            await ProcessNew(command, dto);
        }
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

            await _dataContext.OutboxPings.AddAsync(new OutboxPing());
            await _dataContext.SaveChangesAsync();

            _logger.LogWarning("No AthletePosition found. {@Identity}", positionIdentity);
            throw new ExternalDocumentNotSourcedException($"No AthletePosition found for {externalProviderDto.Position.Ref}. " +
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

    private async Task ProcessNew(ProcessDocumentCommand command, EspnFootballAthleteDto dto)
    {
        var entity = dto.AsFootballAthlete(_externalRefIdentityGenerator, null, command.CorrelationId);

        if (dto.Headshot?.Href is not null)
        {
            var imgId = Guid.NewGuid();
            await _publishEndpoint.Publish(new ProcessImageRequest(
                dto.Headshot.Href,
                imgId,
                entity.Id,
                $"{entity.Id}-{imgId}.png",
                command.Sport,
                command.Season,
                command.DocumentType,
                command.SourceDataProvider,
                0, 0,
                null,
                command.CorrelationId,
                CausationId.Producer.AthleteDocumentProcessor));
        }

        await ProcessBirthplace(dto, entity);
        await ProcessAthleteStatus(dto, entity);
        await ProcessCurrentPosition(dto, entity, command);

        await _publishEndpoint.Publish(new AthleteCreated(
            entity.ToCanonicalModel(),
            command.CorrelationId,
            CausationId.Producer.AthleteDocumentProcessor));

        await _dataContext.Athletes.AddAsync(entity);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Created new athlete entity: {AthleteId}", entity.Id);
    }


    private Task ProcessExisting(ProcessDocumentCommand command, EspnFootballAthleteDto dto)
    {
        _logger.LogWarning("Athlete already exists for {Provider}. Skipping for now.", command.SourceDataProvider);
        return Task.CompletedTask;
    }

}