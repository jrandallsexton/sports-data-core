using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class FranchiseSeasonLeaderStatExtensions
{
    public static FranchiseSeasonLeaderStat AsEntity(
        this EspnLeadersLeaderDto dto,
        Guid parentLeaderId,
        Guid athleteSeasonId,
        Guid correlationId)
    {
        return new FranchiseSeasonLeaderStat
        {
            Id = Guid.NewGuid(),
            FranchiseSeasonLeaderId = parentLeaderId,
            AthleteSeasonId = athleteSeasonId,
            DisplayValue = dto.DisplayValue ?? "N/A",
            Value = (decimal)dto.Value,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = correlationId
        };
    }
}
