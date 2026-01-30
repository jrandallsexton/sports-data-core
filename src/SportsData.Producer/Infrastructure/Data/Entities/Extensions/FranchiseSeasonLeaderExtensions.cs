using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class FranchiseSeasonLeaderExtensions
{
    public static FranchiseSeasonLeader AsEntity(
        this EspnLeadersCategoryDto dto,
        Guid franchiseSeasonId,
        int leaderCategoryId,
        Guid correlationId)
    {
        return new FranchiseSeasonLeader
        {
            Id = Guid.NewGuid(),
            FranchiseSeasonId = franchiseSeasonId,
            LeaderCategoryId = leaderCategoryId,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = correlationId,
            Stats = new List<FranchiseSeasonLeaderStat>() // to be populated manually in the processor
        };
    }
}
