using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class CompetitionLeaderStatExtensions
{
    public static CompetitionLeaderStat AsEntity(
        this EspnEventCompetitionLeadersLeaderDto dto,
        Guid parentLeaderId,
        Guid athleteId,
        Guid franchiseSeasonId,
        Guid correlationId)
    {
        return new CompetitionLeaderStat
        {
            Id = Guid.NewGuid(),
            CompetitionLeaderId = parentLeaderId,
            AthleteId = athleteId,
            FranchiseSeasonId = franchiseSeasonId,
            DisplayValue = dto.DisplayValue,
            Value = dto.Value,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = correlationId
        };
    }

}