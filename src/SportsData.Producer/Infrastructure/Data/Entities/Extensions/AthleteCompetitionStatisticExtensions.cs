using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class AthleteCompetitionStatisticExtensions
{
    public static AthleteCompetitionStatistic AsEntity(
        this EspnEventCompetitionAthleteStatisticsDto dto,
        Guid athleteSeasonId,
        Guid competitionId,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid correlationId)
    {
        if (dto.Ref == null)
            throw new ArgumentException("Athlete statistic DTO is missing its $ref property.");

        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        var entity = new AthleteCompetitionStatistic
        {
            Id = identity.CanonicalId,
            CreatedBy = correlationId,
            CreatedUtc = DateTime.UtcNow,
            AthleteSeasonId = athleteSeasonId,
            CompetitionId = competitionId,
        };

        if (dto.Splits?.Categories != null)
        {
            entity.Categories = dto.Splits.Categories.Select(category => new AthleteCompetitionStatisticCategory
            {
                Id = Guid.NewGuid(),
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,
                AthleteCompetitionStatisticId = entity.Id,
                Name = category.Name,
                DisplayName = category.DisplayName,
                ShortDisplayName = category.ShortDisplayName,
                Abbreviation = category.Abbreviation,
                Summary = category.Summary,
                Stats = Enumerable.Select(category.Stats, stat => new AthleteCompetitionStatisticStat
                {
                    Id = Guid.NewGuid(),
                    CreatedBy = correlationId,
                    CreatedUtc = DateTime.UtcNow,
                    Name = stat.Name,
                    DisplayName = stat.DisplayName,
                    ShortDisplayName = stat.ShortDisplayName,
                    Description = stat.Description,
                    Abbreviation = stat.Abbreviation,
                    Value = (decimal?)stat.Value,
                    DisplayValue = stat.DisplayValue
                }).ToList()
            }).ToList();
        }

        return entity;
    }
}
