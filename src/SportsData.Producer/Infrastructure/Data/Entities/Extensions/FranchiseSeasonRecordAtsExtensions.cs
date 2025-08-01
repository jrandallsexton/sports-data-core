using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class FranchiseSeasonRecordAtsExtensions
    {
        public static FranchiseSeasonRecordAts AsEntity(
            this EspnTeamSeasonRecordAtsItemDto dto,
            Guid franchiseSeasonId,
            int categoryId,
            Guid createdBy)
        {
            return new FranchiseSeasonRecordAts
            {
                Id = Guid.NewGuid(),
                FranchiseSeasonId = franchiseSeasonId,
                CategoryId = categoryId,
                Wins = dto.Wins,
                Losses = dto.Losses,
                Pushes = dto.Pushes,
                CreatedBy = createdBy
            };
        }
    }
}