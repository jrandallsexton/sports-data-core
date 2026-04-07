using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Baseball;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Baseball;

[DocumentProcessor(SourceDataProvider.Espn, Sport.BaseballMlb, DocumentType.AthleteSeason)]
public class BaseballAthleteSeasonDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : BaseballDataContext
{
    private readonly IDateTimeProvider _dateTimeProvider;

    public BaseballAthleteSeasonDocumentProcessor(
        ILogger<BaseballAthleteSeasonDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs,
        IDateTimeProvider dateTimeProvider)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnBaseballAthleteSeasonDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize EspnBaseballAthleteSeasonDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnBaseballAthleteSeasonDto Ref is null. {@Command}", command);
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
            var athleteLinkDto = new EspnLinkDto { Ref = athleteRef };

            await PublishDependencyRequest<string?>(
                command,
                athleteLinkDto,
                parentId: null,
                DocumentType.Athlete);

            throw new ExternalDocumentNotSourcedException(
                $"Athlete not found for {dto.Ref}");
        }

        var franchiseSeasonId = await TryResolveFranchiseSeasonIdAsync(dto, command);
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
        EspnBaseballAthleteSeasonDto dto,
        Guid? franchiseSeasonId,
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

        await ProcessHeadshot(command, entity, dto, athleteId);
        await ProcessStatistics(command, dto, entity.Id);
        await ProcessNotes(command, dto, entity.Id);
        await ProcessHotZones(dto, entity.Id, command.CorrelationId);

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Created baseball AthleteSeason {Id} for Athlete {AthleteId}", entity.Id, athleteId);
    }

    private async Task ProcessExisting(
        ProcessDocumentCommand command,
        AthleteSeason entity,
        EspnBaseballAthleteSeasonDto dto,
        Guid athleteId,
        Guid? franchiseSeasonId,
        Guid positionId)
    {
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
        entity.HeightIn = newEntity.HeightIn;
        entity.IsActive = newEntity.IsActive;
        entity.Jersey = newEntity.Jersey;
        entity.LastName = newEntity.LastName;
        entity.PositionId = positionId;
        entity.ShortName = newEntity.ShortName;
        entity.Slug = newEntity.Slug;
        entity.StatusId = newEntity.StatusId;
        entity.WeightDisplay = newEntity.WeightDisplay;
        entity.WeightLb = newEntity.WeightLb;
        entity.ModifiedBy = command.CorrelationId;
        entity.ModifiedUtc = _dateTimeProvider.UtcNow();

        if (ShouldSpawn(DocumentType.AthleteImage, command))
            await ProcessHeadshot(command, entity, dto, athleteId);

        if (ShouldSpawn(DocumentType.AthleteSeasonStatistics, command))
            await ProcessStatistics(command, dto, entity.Id);

        if (ShouldSpawn(DocumentType.AthleteSeasonNote, command))
            await ProcessNotes(command, dto, entity.Id);

        // Always update hot zones on re-process — delete and replace
        await ProcessHotZones(dto, entity.Id, command.CorrelationId);

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Updated baseball AthleteSeason {Id}", entity.Id);
    }

    private async Task ProcessHotZones(
        EspnBaseballAthleteSeasonDto dto,
        Guid athleteSeasonId,
        Guid correlationId)
    {
        if (dto.HotZones is null || dto.HotZones.Count == 0)
            return;

        // Delete existing hot zones for this athlete season (delete-and-replace)
        var existing = await _dataContext.AthleteSeasonHotZones
            .Where(hz => hz.AthleteSeasonId == athleteSeasonId)
            .ToListAsync();

        if (existing.Count > 0)
            _dataContext.AthleteSeasonHotZones.RemoveRange(existing);

        foreach (var hotZoneDto in dto.HotZones)
        {
            var hotZone = new AthleteSeasonHotZone
            {
                Id = Guid.NewGuid(),
                AthleteSeasonId = athleteSeasonId,
                ConfigurationId = hotZoneDto.ConfigurationId,
                Name = hotZoneDto.Name,
                Active = hotZoneDto.Active,
                SplitTypeId = hotZoneDto.SplitTypeId,
                Season = hotZoneDto.Season,
                SeasonType = hotZoneDto.SeasonType,
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow
            };

            if (hotZoneDto.Zones is not null)
            {
                foreach (var zoneDto in hotZoneDto.Zones)
                {
                    hotZone.Entries.Add(new AthleteSeasonHotZoneEntry
                    {
                        Id = Guid.NewGuid(),
                        ZoneId = zoneDto.ZoneId,
                        XMin = zoneDto.XMin,
                        XMax = zoneDto.XMax,
                        YMin = zoneDto.YMin,
                        YMax = zoneDto.YMax,
                        AtBats = zoneDto.AtBats,
                        Hits = zoneDto.Hits,
                        BattingAvg = zoneDto.BattingAvg,
                        BattingAvgScore = zoneDto.BattingAvgScore,
                        Slugging = zoneDto.Slugging,
                        SluggingScore = zoneDto.SluggingScore,
                        CreatedBy = correlationId,
                        CreatedUtc = DateTime.UtcNow
                    });
                }
            }

            await _dataContext.AthleteSeasonHotZones.AddAsync(hotZone);
        }

        _logger.LogInformation(
            "Processed {Count} hot zone(s) for AthleteSeason {AthleteSeasonId}",
            dto.HotZones.Count, athleteSeasonId);
    }

    private async Task ProcessStatistics(
        ProcessDocumentCommand command,
        EspnBaseballAthleteSeasonDto dto,
        Guid athleteSeasonId)
    {
        await PublishChildDocumentRequest(
            command,
            dto.Statistics,
            athleteSeasonId,
            DocumentType.AthleteSeasonStatistics);
    }

    private async Task ProcessNotes(
        ProcessDocumentCommand command,
        EspnBaseballAthleteSeasonDto dto,
        Guid athleteSeasonId)
    {
        await PublishChildDocumentRequest(
            command,
            dto.Notes,
            athleteSeasonId,
            DocumentType.AthleteSeasonNote);
    }

    private async Task ProcessHeadshot(
        ProcessDocumentCommand command,
        AthleteSeason entity,
        EspnBaseballAthleteSeasonDto dto,
        Guid athleteId)
    {
        if (dto.Headshot?.Href is null)
            return;

        var imgIdentity = _externalRefIdentityGenerator.Generate(dto.Headshot.Href);

        await _publishEndpoint.Publish(new Core.Eventing.Events.Images.ProcessImageRequest(
            dto.Headshot.Href,
            imgIdentity.CanonicalId,
            athleteId,
            $"{athleteId}-{imgIdentity.CanonicalId}.png",
            null,
            command.Sport,
            command.SeasonYear,
            DocumentType.AthleteImage,
            command.SourceDataProvider,
            0, 0,
            null,
            command.CorrelationId,
            CausationId.Producer.AthleteSeasonDocumentProcessor));
    }

    private async Task<Guid?> TryResolveFranchiseSeasonIdAsync(
        EspnBaseballAthleteSeasonDto dto,
        ProcessDocumentCommand command)
    {
        if (dto.Team?.Ref is null)
            return null;

        var franchiseSeasonIdentity = _externalRefIdentityGenerator.Generate(dto.Team.Ref);

        var franchiseSeason = await _dataContext.FranchiseSeasons
            .Where(x => x.Id == franchiseSeasonIdentity.CanonicalId)
            .FirstOrDefaultAsync();

        if (franchiseSeason is not null)
            return franchiseSeason.Id;

        await PublishDependencyRequest<string?>(
            command,
            dto.Team,
            parentId: null,
            DocumentType.TeamSeason);

        throw new ExternalDocumentNotSourcedException(
            $"Franchise season not found for {dto.Team.Ref}");
    }

    private async Task<Guid> TryResolvePositionIdAsync(
        EspnBaseballAthleteSeasonDto dto,
        ProcessDocumentCommand command)
    {
        if (dto.Position?.Ref is null)
            return Guid.Empty;

        var positionId = await _dataContext.ResolveIdAsync<
            AthletePosition, AthletePositionExternalId>(
            dto.Position,
            command.SourceDataProvider,
            () => _dataContext.AthletePositions,
            externalIdsNav: "ExternalIds",
            key: p => p.Id);

        if (positionId.HasValue)
            return positionId.Value;

        await PublishDependencyRequest<string?>(
            command,
            dto.Position,
            parentId: null,
            DocumentType.AthletePosition);

        throw new ExternalDocumentNotSourcedException(
            $"Position not found for {dto.Position.Ref}");
    }
}
