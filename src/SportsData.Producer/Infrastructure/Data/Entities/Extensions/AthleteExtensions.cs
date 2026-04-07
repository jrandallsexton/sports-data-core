using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
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

        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        var entity = new FootballAthlete
        {
            Id = identity.CanonicalId,
            FranchiseId = franchiseId,
            CreatedBy = correlationId,
            CreatedUtc = DateTime.UtcNow
        };

        dto.MapAthleteProperties(entity, identity);

        if (dto is EspnFootballAthleteDto footballDto)
        {
            entity.Jersey = footballDto.Jersey;
            if (footballDto.Draft is not null)
            {
                entity.DraftDisplayText = footballDto.Draft.Display;
                entity.DraftRound = footballDto.Draft.Round;
                entity.DraftYear = footballDto.Draft.Year;
                entity.DraftSelection = footballDto.Draft.Selection;
                entity.DraftTeamRef = footballDto.Draft.Team?.Ref?.ToString();
            }
        }

        return entity;
    }

    private static void MapAthleteProperties(
        this EspnAthleteDto dto,
        Athlete entity,
        ExternalRefIdentity identity)
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

        entity.DoB = !string.IsNullOrWhiteSpace(dto.DateOfBirth) && DateTime.TryParse(dto.DateOfBirth, out var dob)
            ? dob.ToUniversalTime()
            : null;

        entity.ExperienceYears = dto.Experience?.Years ?? 0;
        entity.ExperienceAbbreviation = dto.Experience?.Abbreviation;
        entity.ExperienceDisplayValue = dto.Experience?.DisplayValue;

        entity.DebutYear = dto.DebutYear;
        entity.CollegeAthleteRef = dto.CollegeAthlete?.Ref?.ToString();

        entity.ExternalIds =
        [
            new AthleteExternalId()
            {
                Id = identity.CanonicalId,
                Provider = SourceDataProvider.Espn,
                Value = identity.UrlHash,
                SourceUrlHash = identity.UrlHash,
                SourceUrl = identity.CleanUrl
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

        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        return new Athlete
        {
            Id = identity.CanonicalId,
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

            DoB = !string.IsNullOrWhiteSpace(dto.DateOfBirth) && DateTime.TryParse(dto.DateOfBirth, out var dob2)
                ? dob2.ToUniversalTime()
                : null,

            ExperienceYears = dto.Experience?.Years ?? 0,
            ExperienceAbbreviation = dto.Experience?.Abbreviation,
            ExperienceDisplayValue = dto.Experience?.DisplayValue,

            DebutYear = dto.DebutYear,
            CollegeAthleteRef = dto.CollegeAthlete?.Ref?.ToString(),

            ExternalIds =
            [
                new AthleteExternalId()
                {
                    Id = identity.CanonicalId,
                    Provider = SourceDataProvider.Espn,
                    Value = identity.UrlHash,
                    SourceUrlHash = identity.UrlHash,
                    SourceUrl = identity.CleanUrl
                }
            ]
        };
    }

    public static BaseballAthlete AsBaseballAthlete(
        this EspnAthleteDto dto,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid? franchiseId,
        Guid correlationId)
    {
        if (dto.Ref == null)
            throw new ArgumentException("Athlete DTO is missing its $ref property.");

        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        var entity = new BaseballAthlete
        {
            Id = identity.CanonicalId,
            FranchiseId = franchiseId,
            CreatedBy = correlationId,
            CreatedUtc = DateTime.UtcNow
        };

        dto.MapAthleteProperties(entity, identity);

        if (dto is EspnBaseballAthleteDto baseballDto)
        {
            entity.Jersey = baseballDto.Jersey;
            entity.BatsType = baseballDto.Bats?.Type;
            entity.BatsAbbreviation = baseballDto.Bats?.Abbreviation;
            entity.ThrowsType = baseballDto.Throws?.Type;
            entity.ThrowsAbbreviation = baseballDto.Throws?.Abbreviation;

            if (baseballDto.Draft is not null)
            {
                entity.DraftDisplayText = baseballDto.Draft.Display;
                entity.DraftRound = baseballDto.Draft.Round;
                entity.DraftYear = baseballDto.Draft.Year;
                entity.DraftSelection = baseballDto.Draft.Selection;
                entity.DraftTeamRef = baseballDto.Draft.Team?.Ref?.ToString();
            }
        }

        return entity;
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