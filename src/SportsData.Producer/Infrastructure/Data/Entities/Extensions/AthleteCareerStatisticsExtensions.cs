using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class AthleteCareerStatisticsExtensions
{
    public static AthleteCareerStatistic AsEntity(
        this EspnAthleteCareerStatisticsDto dto,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid athleteId,
        Guid correlationId)
    {
        if (dto.Ref == null)
            throw new ArgumentException("AthleteCareerStatistics DTO is missing its $ref property.");

        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        return new AthleteCareerStatistic
        {
            Id = identity.CanonicalId,
            AthleteId = athleteId,
            SplitId = dto.Splits.Id,
            SplitName = dto.Splits.Name,
            SplitAbbreviation = dto.Splits.Abbreviation,
            CreatedBy = correlationId,
            CreatedUtc = DateTime.UtcNow,
            Categories = dto.Splits.Categories?.Select(c => c.AsCareerEntity()).ToList() ?? []
        };
    }

    public static AthleteCareerStatisticCategory AsCareerEntity(this EspnStatisticsCategoryDto dto)
    {
        return new AthleteCareerStatisticCategory
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            DisplayName = dto.DisplayName,
            ShortDisplayName = dto.ShortDisplayName,
            Abbreviation = dto.Abbreviation,
            Summary = dto.Summary,
            CreatedUtc = DateTime.UtcNow,
            Stats = dto.Stats?.Select(s => s.AsCareerEntity()).ToList() ?? []
        };
    }

    public static AthleteCareerStatisticStat AsCareerEntity(this EspnStatisticsStatDto dto)
    {
        return new AthleteCareerStatisticStat
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            DisplayName = dto.DisplayName,
            ShortDisplayName = dto.ShortDisplayName,
            Description = dto.Description,
            Abbreviation = dto.Abbreviation,
            Value = (decimal)dto.Value,
            DisplayValue = dto.DisplayValue,
            PerGameValue = dto.PerGameValue.HasValue ? (decimal)dto.PerGameValue.Value : null,
            PerGameDisplayValue = dto.PerGameDisplayValue,
            CreatedUtc = DateTime.UtcNow
        };
    }
}
