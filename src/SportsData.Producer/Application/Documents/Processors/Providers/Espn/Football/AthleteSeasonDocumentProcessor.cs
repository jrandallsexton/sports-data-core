using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Config;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

using SportsData.Core.Infrastructure.Refs;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.AthleteSeason)]
public class AthleteSeasonDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : FootballDataContext
{
    private readonly DocumentProcessingConfig _config;

    public AthleteSeasonDocumentProcessor(
        ILogger<AthleteSeasonDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs,
        DocumentProcessingConfig config)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
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
                var docCreated = command.ToDocumentCreated(command.AttemptCount + 1);
                _logger.LogWarning(retryEx, "Dependency not ready. Will retry later. {@evt}", docCreated);
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
        var dto = command.Document.FromJson<EspnAthleteSeasonDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize EspnAthleteSeasonDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnFootballAthleteSeasonDto Ref is null. {@Command}", command);
            return;
        }

        var athleteRef = EspnUriMapper.AthleteSeasonToAthleteRef(dto.Ref);
        var athleteIdentity = _externalRefIdentityGenerator.Generate(athleteRef);

        var athlete = await _dataContext.Athletes
            .Include(x => x.Seasons)
            .Where(x => x.Id == athleteIdentity.CanonicalId)
            .FirstOrDefaultAsync();

        if (athlete is null)
        {
            if (!_config.EnableDependencyRequests)
            {
                _logger.LogWarning(
                    "Missing dependency: {MissingDependencyType}. Processor: {ProcessorName}. Will retry. EnableDependencyRequests=false. Ref={Ref}",
                    DocumentType.Athlete,
                    nameof(AthleteSeasonDocumentProcessor<TDataContext>),
                    athleteIdentity.CleanUrl);
                throw new ExternalDocumentNotSourcedException(
                    $"Athlete not found for {athleteIdentity.CleanUrl}. Will retry when available.");
            }
            else
            {
                _logger.LogWarning(
                    "Athlete not found. Raising DocumentRequested (override mode). {@Identity} {cmdRef}",
                    athleteIdentity, dto.Ref);

                // Create a temp wrapper with the athlete ref (not the athlete season ref)
                var athleteLinkDto = new EspnLinkDto { Ref = athleteRef };
                await PublishChildDocumentRequest<string?>(
                    command,
                    athleteLinkDto,
                    parentId: null,
                    DocumentType.Athlete,
                    CausationId.Producer.AthleteSeasonDocumentProcessor);
                await _dataContext.SaveChangesAsync();

                throw new ExternalDocumentNotSourcedException(
                    $"Athlete not found for {dto.Ref} in command {command.CorrelationId}");
            }
        }

        var franchiseSeasonId = await TryResolveFranchiseSeasonIdAsync(dto, command);
        if (franchiseSeasonId == Guid.Empty)
        {
            _logger.LogError("Could not resolve FranchiseSeasonId for Team.Ref: {Ref}", dto.Team?.Ref?.ToString() ?? "null");
            return;
        }

        var positionId = await TryResolvePositionIdAsync(dto, command);
        if (positionId == Guid.Empty)
        {
            _logger.LogError("Could not resolve PositionId for Position.Ref: {Ref}", dto.Position?.Ref?.ToString() ?? "null");
            return;
        }

        var athleteSeasonIdentity = _externalRefIdentityGenerator.Generate(dto.Ref);

        var entity = await _dataContext.AthleteSeasons
            .FirstOrDefaultAsync(x => x.Id == athleteSeasonIdentity.CanonicalId);

        if (entity is not null)
        {
            await ProcessExisting(command, entity, dto, athlete.Id, franchiseSeasonId, positionId);
        }
        else
        {
            await ProcessNew(command, dto, franchiseSeasonId, athlete.Id, positionId);
        }
    }

    private async Task ProcessNew(
        ProcessDocumentCommand command,
        EspnAthleteSeasonDto dto,
        Guid franchiseSeasonId,
        Guid athleteId,
        Guid positionId)
    {
        var entity = dto.AsEntity(
            _externalRefIdentityGenerator,
            franchiseSeasonId,
            positionId,
            athleteId,
            command.CorrelationId);

        await _dataContext.AthleteSeasons.AddAsync(entity);

        await ProcessEventLog();

        await ProcessHeadshot(command, entity, dto);

        await ProcessStatistics(command, dto, entity.Id);

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Successfully created AthleteSeason {Id} for Athlete {AthleteId}", entity.Id, athleteId);
    }

    private async Task ProcessExisting(
        ProcessDocumentCommand command,
        AthleteSeason entity,
        EspnAthleteSeasonDto dto,
        Guid athleteId,
        Guid franchiseSeasonId,
        Guid positionId)
    {
        _logger.LogInformation("AthleteSeason already exists: {Id}. Processing updates.", entity.Id);

        var newEntity = dto.AsEntity(
            _externalRefIdentityGenerator,
            franchiseSeasonId,
            positionId,
            athleteId,
            command.CorrelationId);

        entity.DisplayName = newEntity.DisplayName;
        entity.ExperienceAbbreviation = newEntity.ExperienceAbbreviation;
        entity.ExperienceDisplayValue = newEntity.ExperienceDisplayValue;
        entity.ExperienceYears = newEntity.ExperienceYears;
        entity.FirstName = newEntity.FirstName;
        entity.FranchiseSeasonId = franchiseSeasonId;
        entity.HeightDisplay = newEntity.HeightDisplay;
        entity.HeightDisplay = newEntity.HeightDisplay;
        entity.HeightIn = newEntity.HeightIn;
        entity.IsActive = newEntity.IsActive;
        entity.Jersey = newEntity.Jersey;
        entity.LastName = newEntity.LastName;
        entity.ModifiedUtc = DateTime.UtcNow;
        entity.ModifiedBy = command.CorrelationId;
        entity.PositionId = positionId;
        entity.ShortName = newEntity.ShortName;
        entity.Slug = newEntity.Slug;
        entity.StatusId = newEntity.StatusId;
        entity.WeightDisplay = newEntity.WeightDisplay;
        entity.WeightLb = newEntity.WeightLb;

        await ProcessEventLog();

        await ProcessHeadshot(command, entity, dto);

        await ProcessStatistics(command, dto, entity.Id);

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Successfully processed existing AthleteSeason {Id}", entity.Id);
    }

    private async Task ProcessEventLog()
    {
        // TODO: Implement
        await Task.CompletedTask;
    }

    private async Task ProcessStatistics(
        ProcessDocumentCommand command,
        EspnAthleteSeasonDto dto,
        Guid athleteSeasonId)
    {
        // Use base class helper for child document request
        await PublishChildDocumentRequest(
            command,
            dto.Statistics,
            athleteSeasonId,
            DocumentType.AthleteSeasonStatistics,
            CausationId.Producer.AthleteSeasonDocumentProcessor);
    }

    private async Task ProcessHeadshot(
        ProcessDocumentCommand command,
        AthleteSeason entity,
        EspnAthleteSeasonDto dto)
    {
        if (dto.Headshot?.Href is null)
            return;

        var imgIdentity = _externalRefIdentityGenerator.Generate(dto.Headshot.Href);

        await _publishEndpoint.Publish(new Core.Eventing.Events.Images.ProcessImageRequest(
            dto.Headshot.Href,
            imgIdentity.CanonicalId,
            entity.Id,
            $"{entity.Id}-{imgIdentity.CanonicalId}.png",
            null,
            command.Sport,
            command.Season,
            command.DocumentType,
            command.SourceDataProvider,
            0, 0,
            null,
            command.CorrelationId,
            CausationId.Producer.AthleteSeasonDocumentProcessor));

        _logger.LogInformation("Published ProcessImageRequest for AthleteSeason {Id}, Image: {ImageId}", entity.Id, imgIdentity.CanonicalId);

    }

    private async Task<Guid> TryResolveFranchiseSeasonIdAsync(EspnAthleteSeasonDto dto, ProcessDocumentCommand command)
    {
        if (dto.Team?.Ref is null)
            return Guid.Empty;

        var franchiseSeasonIdentity = _externalRefIdentityGenerator.Generate(dto.Team.Ref);

        var franchiseSeason = await _dataContext.FranchiseSeasons
            .Where(x => x.Id == franchiseSeasonIdentity.CanonicalId)
            .FirstOrDefaultAsync();

        if (franchiseSeason is not null) 
            return franchiseSeason.Id;

        if (!_config.EnableDependencyRequests)
        {
            _logger.LogWarning(
                "Missing dependency: {MissingDependencyType}. Processor: {ProcessorName}. Will retry. EnableDependencyRequests=false. Ref={Ref}",
                DocumentType.TeamSeason,
                nameof(AthleteSeasonDocumentProcessor<TDataContext>),
                dto.Team.Ref);
            throw new ExternalDocumentNotSourcedException(
                $"Franchise season not found for {dto.Team.Ref}. Will retry when available.");
        }
        else
        {
            await PublishChildDocumentRequest<string?>(
                command,
                dto.Team,
                parentId: null,
                DocumentType.TeamSeason,
                CausationId.Producer.AthleteSeasonDocumentProcessor);
            await _dataContext.SaveChangesAsync();

            _logger.LogWarning(
                "FranchiseSeason not found. Requesting (override mode). {Ref} {@Identity}",
                dto.Team.Ref, franchiseSeasonIdentity);

            throw new ExternalDocumentNotSourcedException(
                $"Franchise season not found for {dto.Team.Ref} in command {command.CorrelationId}");
        }
    }

    private async Task<Guid> TryResolvePositionIdAsync(EspnAthleteSeasonDto dto, ProcessDocumentCommand command)
    {
        if (dto.Position?.Ref is null)
            return Guid.Empty;

        var positionIdentity = _externalRefIdentityGenerator.Generate(dto.Position.Ref);

        var positionId = await _dataContext.ResolveIdAsync<
            AthletePosition, AthletePositionExternalId>(
            dto.Position,
            command.SourceDataProvider,
            () => _dataContext.AthletePositions,
            externalIdsNav: "ExternalIds",
            key: p => p.Id);

        if (positionId.HasValue)
            return positionId.Value;

        if (!_config.EnableDependencyRequests)
        {
            _logger.LogWarning(
                "Missing dependency: {MissingDependencyType}. Processor: {ProcessorName}. Will retry. EnableDependencyRequests=false. Ref={Ref}",
                DocumentType.AthletePosition,
                nameof(AthleteSeasonDocumentProcessor<TDataContext>),
                dto.Position.Ref);
            throw new ExternalDocumentNotSourcedException(
                $"Position not found for {dto.Position.Ref}. Will retry when available.");
        }
        else
        {
            await PublishChildDocumentRequest<string?>(
                command,
                dto.Position,
                parentId: null,
                DocumentType.AthletePosition,
                CausationId.Producer.AthleteSeasonDocumentProcessor);
            await _dataContext.SaveChangesAsync();

            throw new ExternalDocumentNotSourcedException(
                $"Position not found for {dto.Position.Ref} in command {command.CorrelationId}");
        }
    }
}
