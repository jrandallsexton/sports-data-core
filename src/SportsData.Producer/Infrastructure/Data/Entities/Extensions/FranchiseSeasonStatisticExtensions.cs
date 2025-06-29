using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class FranchiseSeasonStatisticExtensions
    {
        public static FranchiseSeasonStatistic AsEntity(
            this EspnTeamSeasonStatisticsCategoryStatDto dto,
            Guid categoryId)
        {
            return new FranchiseSeasonStatistic
            {
                Id = Guid.NewGuid(),
                FranchiseSeasonStatisticCategoryId = categoryId,
                Name = dto.Name,
                DisplayName = dto.DisplayName,
                ShortDisplayName = dto.ShortDisplayName,
                Description = dto.Description,
                Abbreviation = dto.Abbreviation,
                Value = dto.Value,
                DisplayValue = dto.DisplayValue,
                Rank = dto.Rank,
                RankDisplayValue = dto.RankDisplayValue,
                PerGameValue = dto.PerGameValue,
                PerGameDisplayValue = dto.PerGameDisplayValue
            };
        }

        public static bool HasChanges(
            this FranchiseSeasonStatistic existing,
            EspnTeamSeasonStatisticsCategoryStatDto dto)
        {
            return
                existing.Name != dto.Name ||
                existing.DisplayName != dto.DisplayName ||
                existing.ShortDisplayName != dto.ShortDisplayName ||
                existing.Description != dto.Description ||
                existing.Abbreviation != dto.Abbreviation ||
                existing.Value != dto.Value ||
                existing.DisplayValue != dto.DisplayValue ||
                existing.Rank != dto.Rank ||
                existing.RankDisplayValue != dto.RankDisplayValue ||
                existing.PerGameValue != dto.PerGameValue ||
                existing.PerGameDisplayValue != dto.PerGameDisplayValue;
        }
    }
}
