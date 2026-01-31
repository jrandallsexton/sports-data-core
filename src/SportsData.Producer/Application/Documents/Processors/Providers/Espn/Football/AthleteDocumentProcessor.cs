using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Athletes;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Config;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

// TODO: Rename to FootballAthleteDocumentProcessor
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Athlete)]
public class AthleteDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : FootballDataContext
{
    private readonly DocumentProcessingConfig _config;

    public AthleteDocumentProcessor(
        ILogger<AthleteDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refGenerator,
        DocumentProcessingConfig config)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refGenerator)
    {
        _config = config;
    }

    public override async Task ProcessAsync(ProcessDocumentCommand command)
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

        var athleteIdentity = _externalRefIdentityGenerator.Generate(dto.Ref);

        var entity = await _dataContext.Athletes
            .Include(x => x.ExternalIds)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ExternalIds.Any(z =>
                z.Value == athleteIdentity.UrlHash &&
                z.Provider == command.SourceDataProvider));

        if (entity != null)
        {
            await ProcessExisting(command, entity, dto);
        }
        else
        {
            await ProcessNew(command, dto);
        }

        await _dataContext.SaveChangesAsync();
    }

    private async Task ProcessNew(ProcessDocumentCommand command, EspnFootballAthleteDto dto)
    {
        var entity = dto.AsFootballAthlete(_externalRefIdentityGenerator, null, command.CorrelationId);

        await ProcessBirthplace(dto, entity);
        await ProcessAthleteStatus(dto, entity);
        await ProcessCurrentPosition(dto, entity, command);

        if (dto.Headshot?.Href is not null)
        {
            var imageId = _externalRefIdentityGenerator.Generate(dto.Headshot.Href).CanonicalId;
            
            await _publishEndpoint.Publish(new ProcessImageRequest(
                dto.Headshot.Href,
                imageId,
                entity.Id,
                $"{entity.Id}-headshot.png",
                null,
                command.Sport,
                command.Season,
                command.DocumentType,
                command.SourceDataProvider,
                0, 0,
                null,
                command.CorrelationId,
                CausationId.Producer.AthleteDocumentProcessor));
        }

        await _publishEndpoint.Publish(new AthleteCreated(
            entity.ToCanonicalModel(),
            null,
            command.Sport,
            command.Season,
            command.CorrelationId,
            CausationId.Producer.AthleteDocumentProcessor));

        await _dataContext.Athletes.AddAsync(entity);

        _logger.LogInformation("Created new athlete entity: {AthleteId}", entity.Id);
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

        // Check if it already exists
        var location = await _dataContext.Locations
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                (x.City ?? "").ToLower() == (cityLower ?? "") &&
                (x.State ?? "").ToLower() == (stateLower ?? "") &&
                (x.Country ?? "").ToLower() == (countryLower ?? ""));

        if (location is null)
        {
            // Create new location
            location = new Location
            {
                Id = Guid.NewGuid(),
                City = city,
                State = state,
                Country = country
            };

            await _dataContext.Locations.AddAsync(location);
            await _dataContext.SaveChangesAsync();

            // EF Core automatically assigns the ID after SaveChangesAsync, so location.Id is now populated
        }

        // Safe to assign FK
        newEntity.BirthLocationId = location.Id;
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

        // Look for existing
        var status = await _dataContext.AthleteStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                (x.Name ?? "").ToLower() == nameLower);

        if (status is null)
        {
            // Create new status
            status = new AthleteStatus
            {
                Id = Guid.NewGuid(),
                Name = name,
                Abbreviation = abbreviation,
                Type = type,
                ExternalId = externalId.ToString()
            };

            await _dataContext.AthleteStatuses.AddAsync(status);
            await _dataContext.SaveChangesAsync();

            // EF Core automatically assigns the ID after SaveChangesAsync, so status.Id is now populated
        }

        // Always safe assignment
        newEntity.StatusId = status.Id;
    }

    private async Task ProcessCurrentPosition(
        EspnFootballAthleteDto externalProviderDto,
        FootballAthlete newEntity,
        ProcessDocumentCommand command)
    {
        if (externalProviderDto.Position?.Ref is null)
            return;

        var positionIdentity = _externalRefIdentityGenerator.Generate(externalProviderDto.Position.Ref);

        var positionId = await _dataContext.AthletePositionExternalIds
            .Where(x => x.Provider == command.SourceDataProvider
                        && x.SourceUrlHash == positionIdentity.UrlHash)
            .Select(x => x.AthletePositionId)
            .FirstOrDefaultAsync();

        if (positionId == Guid.Empty)
        {
            if (!_config.EnableDependencyRequests)
            {
                _logger.LogWarning(
                    "Missing dependency: {MissingDependencyType}. Processor: {ProcessorName}. Will retry. EnableDependencyRequests=false. Ref={Ref}",
                    DocumentType.AthletePosition,
                    nameof(AthleteDocumentProcessor<TDataContext>),
                    positionIdentity.CleanUrl);
                throw new ExternalDocumentNotSourcedException(
                    $"AthletePosition {positionIdentity.CleanUrl} not found. Will retry when available.");
            }
            else
            {
                _logger.LogWarning(
                    "AthletePosition not found. Raising DocumentRequested (override mode). {@Identity}",
                    positionIdentity);

                await PublishChildDocumentRequest<string?>(
                    command,
                    externalProviderDto.Position,
                    parentId: null,
                    DocumentType.AthletePosition,
                    CausationId.Producer.AthleteDocumentProcessor);

                await _dataContext.SaveChangesAsync();

                throw new ExternalDocumentNotSourcedException(
                    $"No AthletePosition found for {externalProviderDto.Position.Ref}. " +
                    $"Please ensure the position document is processed before this athlete.");
            }
        }

        newEntity.PositionId = positionId;

        _logger.LogInformation("Resolved CurrentPositionId: {PositionId} for AthleteId: {AthleteId}", positionId, newEntity.Id);
    }

    private async Task ProcessExisting(
        ProcessDocumentCommand command,
        Athlete entity,
        EspnFootballAthleteDto dto)
    {
        if (ShouldSpawn(DocumentType.AthleteImage, command))
        {
            if (dto.Headshot?.Href is not null)
            {
                var imageId = _externalRefIdentityGenerator.Generate(dto.Headshot.Href).CanonicalId;

                await _publishEndpoint.Publish(new ProcessImageRequest(
                    dto.Headshot.Href,
                    imageId,
                    entity.Id,
                    $"{entity.Id}-headshot.png",
                    null,
                    command.Sport,
                    command.Season,
                    DocumentType.AthleteImage,
                    command.SourceDataProvider,
                    0, 0,
                    null,
                    command.CorrelationId,
                    CausationId.Producer.AthleteDocumentProcessor));
            }
        }
    }
}