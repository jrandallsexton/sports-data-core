using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Athletes;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Baseball;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Baseball;

[DocumentProcessor(SourceDataProvider.Espn, Sport.BaseballMlb, DocumentType.Athlete)]
public class BaseballAthleteDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : BaseballDataContext
{
    public BaseballAthleteDocumentProcessor(
        ILogger<BaseballAthleteDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refGenerator)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refGenerator) { }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnBaseballAthleteDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnBaseballAthleteDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnBaseballAthleteDto Ref is null. {@Command}", command);
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

    private async Task ProcessNew(ProcessDocumentCommand command, EspnBaseballAthleteDto dto)
    {
        var entity = dto.AsBaseballAthlete(_externalRefIdentityGenerator, null, command.CorrelationId);

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
                command.SeasonYear,
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
            command.SeasonYear,
            command.CorrelationId,
            command.MessageId));

        await _dataContext.Athletes.AddAsync(entity);

        _logger.LogInformation("Created new baseball athlete entity: {AthleteId}", entity.Id);
    }

    private async Task ProcessBirthplace(EspnBaseballAthleteDto dto, Athlete newEntity)
    {
        if (dto.BirthPlace is null) return;

        var city = dto.BirthPlace.City?.Trim();
        var state = dto.BirthPlace.State?.Trim();
        var country = dto.BirthPlace.Country?.Trim();

        if (string.IsNullOrEmpty(city) && string.IsNullOrEmpty(state) && string.IsNullOrEmpty(country))
            return;

        var cityLower = city?.ToLower();
        var stateLower = state?.ToLower();
        var countryLower = country?.ToLower();

        var location = await _dataContext.Locations
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                (x.City ?? "").ToLower() == (cityLower ?? "") &&
                (x.State ?? "").ToLower() == (stateLower ?? "") &&
                (x.Country ?? "").ToLower() == (countryLower ?? ""));

        if (location is null)
        {
            location = new Location
            {
                Id = Guid.NewGuid(),
                City = city,
                State = state,
                Country = country
            };

            await _dataContext.Locations.AddAsync(location);
            await _dataContext.SaveChangesAsync();
        }

        newEntity.BirthLocationId = location.Id;
    }

    private async Task ProcessAthleteStatus(EspnBaseballAthleteDto dto, Athlete newEntity)
    {
        if (dto.Status is null) return;

        var name = dto.Status.Name?.Trim();
        if (string.IsNullOrEmpty(name)) return;

        var nameLower = name.ToLower();

        var status = await _dataContext.AthleteStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => (x.Name ?? "").ToLower() == nameLower);

        if (status is null)
        {
            status = new AthleteStatus
            {
                Id = Guid.NewGuid(),
                Name = name,
                Abbreviation = dto.Status.Abbreviation?.Trim(),
                Type = dto.Status.Type?.Trim(),
                ExternalId = dto.Status.Id.ToString()
            };

            await _dataContext.AthleteStatuses.AddAsync(status);
            await _dataContext.SaveChangesAsync();
        }

        newEntity.StatusId = status.Id;
    }

    private async Task ProcessCurrentPosition(
        EspnBaseballAthleteDto dto,
        BaseballAthlete newEntity,
        ProcessDocumentCommand command)
    {
        if (dto.Position?.Ref is null) return;

        var positionIdentity = _externalRefIdentityGenerator.Generate(dto.Position.Ref);

        var positionId = await _dataContext.AthletePositionExternalIds
            .Where(x => x.Provider == command.SourceDataProvider
                        && x.SourceUrlHash == positionIdentity.UrlHash)
            .Select(x => x.AthletePositionId)
            .FirstOrDefaultAsync();

        if (positionId == Guid.Empty)
        {
            await PublishDependencyRequest<string?>(
                command,
                dto.Position,
                parentId: null,
                DocumentType.AthletePosition);

            throw new ExternalDocumentNotSourcedException(
                $"No AthletePosition found for {dto.Position.Ref}. " +
                $"Please ensure the position document is processed before this athlete.");
        }

        newEntity.PositionId = positionId;
    }

    private async Task ProcessExisting(
        ProcessDocumentCommand command,
        Athlete entity,
        EspnBaseballAthleteDto dto)
    {
        _dataContext.Set<Athlete>().Attach(entity);

        entity.Age = dto.Age;
        entity.IsActive = dto.Active;
        entity.FirstName = dto.FirstName ?? string.Empty;
        entity.LastName = dto.LastName ?? string.Empty;
        entity.DisplayName = dto.DisplayName ?? string.Empty;
        entity.ShortName = dto.ShortName ?? string.Empty;
        entity.Slug = dto.Slug ?? string.Empty;
        entity.HeightIn = dto.Height;
        entity.HeightDisplay = dto.DisplayHeight ?? string.Empty;
        entity.WeightLb = dto.Weight;
        entity.WeightDisplay = dto.DisplayWeight ?? string.Empty;
        entity.DoB = !string.IsNullOrWhiteSpace(dto.DateOfBirth) && DateTime.TryParse(dto.DateOfBirth, out var dob)
            ? dob.ToUniversalTime()
            : null;
        entity.ExperienceYears = dto.Experience?.Years ?? 0;
        entity.ExperienceAbbreviation = dto.Experience?.Abbreviation;
        entity.ExperienceDisplayValue = dto.Experience?.DisplayValue;

        entity.DebutYear = dto.DebutYear;
        entity.CollegeAthleteRef = dto.CollegeAthlete?.Ref?.ToString();
        entity.Jersey = dto.Jersey;

        if (dto.Draft is not null)
        {
            entity.DraftDisplayText = dto.Draft.Display;
            entity.DraftRound = dto.Draft.Round;
            entity.DraftYear = dto.Draft.Year;
            entity.DraftSelection = dto.Draft.Selection;
            entity.DraftTeamRef = dto.Draft.Team?.Ref?.ToString();
        }

        if (entity is BaseballAthlete baseballEntity)
        {
            baseballEntity.BatsType = dto.Bats?.Type;
            baseballEntity.BatsAbbreviation = dto.Bats?.Abbreviation;
            baseballEntity.ThrowsType = dto.Throws?.Type;
            baseballEntity.ThrowsAbbreviation = dto.Throws?.Abbreviation;
        }

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
                    command.SeasonYear,
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
