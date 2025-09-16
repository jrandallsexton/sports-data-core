using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class CompetitionCompetitorStatisticExtensions
{
    public static CompetitionCompetitorStatistic AsEntity(
        this EspnEventCompetitionCompetitorStatisticsDto dto,
        Guid franchiseSeasonId,
        Guid competitionId,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid correlationId)
    {
        if (dto.Ref == null)
            throw new ArgumentException("Competitor statistics DTO is missing its $ref property.");

        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        var entity = new CompetitionCompetitorStatistic
        {
            Id = identity.CanonicalId,
            CreatedBy = correlationId,
            CreatedUtc = DateTime.UtcNow,
            FranchiseSeasonId = franchiseSeasonId,
            CompetitionId = competitionId,
        };

        if (dto.Splits?.Categories is { Count: > 0 })
        {
            entity.Categories = dto.Splits.Categories.Select(category => new CompetitionCompetitorStatisticCategory
            {
                Id = Guid.NewGuid(),
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,
                CompetitionCompetitorStatisticId = entity.Id,
                Name = category.Name,
                DisplayName = category.DisplayName,
                ShortDisplayName = category.ShortDisplayName,
                Abbreviation = category.Abbreviation,
                Summary = category.Summary,
                Stats = category.Stats?.Select(stat => new CompetitionCompetitorStatisticStat
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
                }).ToList() ?? new List<CompetitionCompetitorStatisticStat>()
            }).ToList();
        }

        return entity;
    }
}
