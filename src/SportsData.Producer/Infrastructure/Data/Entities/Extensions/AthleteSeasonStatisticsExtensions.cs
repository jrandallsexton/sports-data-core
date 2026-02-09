using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class AthleteSeasonStatisticsExtensions
{
    public static AthleteSeasonStatistic AsEntity(
        this EspnAthleteSeasonStatisticsDto dto,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid athleteSeasonId,
        Guid correlationId)
    {
        if (dto.Ref == null)
            throw new ArgumentException("AthleteSeasonStatistics DTO is missing its $ref property.");

        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        return new AthleteSeasonStatistic
        {
            Id = identity.CanonicalId,
            AthleteSeasonId = athleteSeasonId,
            SplitId = dto.Splits.Id,
            SplitName = dto.Splits.Name,
            SplitAbbreviation = dto.Splits.Abbreviation,
            SplitType = dto.Splits.Type ?? string.Empty, // ESPN can return null, default to empty string
            CreatedBy = correlationId,
            CreatedUtc = DateTime.UtcNow,
            Categories = dto.Splits.Categories?.Select(c => c.AsEntity()).ToList() ?? []
        };
    }

    public static AthleteSeasonStatisticCategory AsEntity(this EspnStatisticsCategoryDto dto)
    {
        return new AthleteSeasonStatisticCategory
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            DisplayName = dto.DisplayName,
            ShortDisplayName = dto.ShortDisplayName,
            Abbreviation = dto.Abbreviation,
            Summary = dto.Summary,
            CreatedUtc = DateTime.UtcNow,
            Stats = dto.Stats?.Select(s => s.AsEntity()).ToList() ?? []
        };
    }

    public static AthleteSeasonStatisticStat AsEntity(this EspnStatisticsStatDto dto)
    {
        return new AthleteSeasonStatisticStat
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
