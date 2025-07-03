using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using System;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class FranchiseSeasonProjectionExtensions
    {
        public static FranchiseSeasonProjection AsEntity(
            this EspnTeamSeasonProjectionDto dto,
            Guid franchiseSeasonId,
            Guid franchiseId,
            int seasonYear,
            Guid correlationId)
        {
            return new FranchiseSeasonProjection
            {
                Id = Guid.NewGuid(),
                FranchiseSeasonId = franchiseSeasonId,
                FranchiseId = franchiseId,
                SeasonYear = seasonYear,
                ChanceToWinDivision = dto.ChanceToWinDivision,
                ChanceToWinConference = dto.ChanceToWinConference,
                ProjectedWins = dto.ProjectedWins,
                ProjectedLosses = dto.ProjectedLosses,
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow
            };
        }
    }
}
