using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class AthleteExtensions
{
    public static FootballAthlete AsFootballAthlete(
        this EspnAthleteDto dto,
        Guid athleteId,
        Guid? franchiseId,
        Guid correlationId)
    {
        var entity = new FootballAthlete
        {
            Id = athleteId,
            FranchiseId = franchiseId
        };

        dto.MapAthleteProperties(entity, correlationId);

        return entity;
    }

    private static void MapAthleteProperties(
        this EspnAthleteDto dto,
        Athlete entity,
        Guid correlationId)
    {
        var sourceUrlHash = HashProvider.GenerateHashFromUri(dto.Ref);

        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        entity.CreatedBy = correlationId;
        entity.CreatedUtc = DateTime.UtcNow;

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

        entity.DoB = !string.IsNullOrWhiteSpace(dto.DateOfBirth)
            ? DateTime.Parse(dto.DateOfBirth).ToUniversalTime()
            : null;

        entity.ExperienceYears = dto.Experience?.Years ?? 0;
        entity.ExperienceAbbreviation = dto.Experience?.Abbreviation;
        entity.ExperienceDisplayValue = dto.Experience?.DisplayValue;

        entity.ExternalIds =
        [
            new AthleteExternalId()
            {
                Id = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                Value = sourceUrlHash,
                SourceUrlHash = sourceUrlHash,
                SourceUrl = dto.Ref.ToCleanUrl()
            }
        ];
    }

    public static Athlete AsAthlete(
        this EspnAthleteDto dto,
        Guid athleteId,
        Guid correlationId)
    {
        var sourceUrlHash = HashProvider.GenerateHashFromUri(dto.Ref);

        return new Athlete()
        {
            Id = athleteId,
            CreatedBy = correlationId,
            CreatedUtc = DateTime.UtcNow,

            Age = dto.Age,
            IsActive = dto.Active,

            FirstName = dto.FirstName ?? string.Empty,
            LastName = dto.LastName ?? string.Empty,
            DisplayName = dto.DisplayName ?? string.Empty,
            ShortName = dto.ShortName ?? string.Empty,

            HeightIn = dto.Height,
            HeightDisplay = dto.DisplayHeight ?? string.Empty,

            WeightLb = dto.Weight,
            WeightDisplay = dto.DisplayWeight ?? string.Empty,

            DoB = !string.IsNullOrWhiteSpace(dto.DateOfBirth)
                ? DateTime.Parse(dto.DateOfBirth).ToUniversalTime()
                : null,

            ExperienceYears = dto.Experience?.Years ?? 0,
            ExperienceAbbreviation = dto.Experience?.Abbreviation,
            ExperienceDisplayValue = dto.Experience?.DisplayValue,

            ExternalIds =
            [
                new AthleteExternalId()
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    Value = sourceUrlHash,
                    SourceUrlHash = sourceUrlHash,
                    SourceUrl = dto.Ref.ToCleanUrl()
                }
            ]
        };
    }

    public static AthleteDto ToCanonicalModel(this Athlete entity)
    {
        return new AthleteDto()
        {
            Id = entity.Id,
            CreatedUtc = entity.CreatedUtc,
            ShortName = entity.ShortName,
            DisplayName = entity.DisplayName,
            IsActive = entity.IsActive,
            FirstName = entity.FirstName ?? string.Empty,
            WeightLb = entity.WeightLb,
            WeightDisplay = entity.WeightDisplay,
            Age = entity.Age,
            CurrentExperience = entity.ExperienceYears,
            DoB = entity.DoB,
            HeightDisplay = entity.HeightDisplay,
            HeightIn = entity.HeightIn,
            LastName = entity.LastName ?? string.Empty,
            UpdatedUtc = entity.ModifiedUtc,
            //PositionId = entity.CurrentPosition,
            //PositionName = entity.Position.Name
        };
    }
}