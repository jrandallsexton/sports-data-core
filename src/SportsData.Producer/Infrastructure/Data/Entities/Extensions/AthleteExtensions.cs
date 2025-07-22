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
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid? franchiseId,
        Guid correlationId)
    {
        if (dto.Ref == null)
            throw new ArgumentException("Athlete DTO is missing its $ref property.");

        var athleteIdentity = externalRefIdentityGenerator.Generate(dto.Ref);

        var entity = new FootballAthlete
        {
            Id = athleteIdentity.CanonicalId,
            FranchiseId = franchiseId,
            CreatedBy = correlationId,
            CreatedUtc = DateTime.UtcNow
        };

        dto.MapAthleteProperties(entity, athleteIdentity);

        return entity;
    }

    private static void MapAthleteProperties(
        this EspnAthleteDto dto,
        Athlete entity,
        ExternalRefIdentity athleteIdentity)
    {
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
                Value = athleteIdentity.UrlHash,
                SourceUrlHash = athleteIdentity.UrlHash,
                SourceUrl = athleteIdentity.CleanUrl
            }
        ];
    }

    public static Athlete AsAthlete(
        this EspnAthleteDto dto,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid correlationId)
    {
        if (dto.Ref == null)
            throw new ArgumentException("Athlete DTO is missing its $ref property.");

        var athleteIdentity = externalRefIdentityGenerator.Generate(dto.Ref);

        return new Athlete
        {
            Id = athleteIdentity.CanonicalId,
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
                    Value = athleteIdentity.UrlHash,
                    SourceUrlHash = athleteIdentity.UrlHash,
                    SourceUrl = athleteIdentity.CleanUrl
                }
            ]
        };
    }

    public static AthleteDto ToCanonicalModel(this Athlete entity)
    {
        return new AthleteDto
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
            UpdatedUtc = entity.ModifiedUtc ?? default,
            //PositionId = entity.CurrentPosition,
            //PositionName = entity.Position.Name
        };
    }
}