using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class FranchiseSeasonStatisticCategoryExtensions
    {
        public static FranchiseSeasonStatisticCategory AsEntity(
            this EspnTeamSeasonStatisticsCategoryDto dto,
            Guid franchiseSeasonId)
        {
            return new FranchiseSeasonStatisticCategory
            {
                Id = Guid.NewGuid(),
                FranchiseSeasonId = franchiseSeasonId,
                Name = dto.Name,
                DisplayName = dto.DisplayName,
                ShortDisplayName = dto.ShortDisplayName,
                Abbreviation = dto.Abbreviation,
                Summary = dto.Summary,
                Stats = dto.Stats?.Select(stat => stat.AsEntity(Guid.Empty)).ToList() ?? new List<FranchiseSeasonStatistic>()
            };
        }

        public static bool HasChanges(this FranchiseSeasonStatisticCategory existing, EspnTeamSeasonStatisticsCategoryDto dto)
        {
            return
                existing.Name != dto.Name ||
                existing.DisplayName != dto.DisplayName ||
                existing.ShortDisplayName != dto.ShortDisplayName ||
                existing.Abbreviation != dto.Abbreviation ||
                existing.Summary != dto.Summary;
        }

        public static bool CategoriesMatch(
            this ICollection<FranchiseSeasonStatisticCategory> existing,
            List<EspnTeamSeasonStatisticsCategoryDto> incoming)
        {
            if (existing.Count != incoming.Count)
                return false;

            foreach (var dtoCategory in incoming)
            {
                var existingCategory = existing
                    .FirstOrDefault(c => c.Name == dtoCategory.Name);

                if (existingCategory == null)
                    return false;

                if (!StatsMatch(existingCategory.Stats, dtoCategory.Stats))
                    return false;
            }

            return true;
        }

        static bool StatsMatch(
            ICollection<FranchiseSeasonStatistic> existing,
            List<EspnTeamSeasonStatisticsCategoryStatDto> incoming)
        {
            if (existing.Count != incoming.Count)
                return false;

            foreach (var dtoStat in incoming)
            {
                var existingStat = existing.FirstOrDefault(s => s.Name == dtoStat.Name);
                if (existingStat == null)
                    return false;

                if (existingStat.HasChanges(dtoStat))
                    return false;
            }

            return true;
        }
    }
}
