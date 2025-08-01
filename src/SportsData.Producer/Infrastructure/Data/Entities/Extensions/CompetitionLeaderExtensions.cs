using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class CompetitionLeaderExtensions
{
    public static CompetitionLeader AsEntity(
        this EspnEventCompetitionLeadersCategoryDto dto,
        Guid competitionId,
        int leaderCategoryId,
        Guid correlationId)
    {
        return new CompetitionLeader
        {
            Id = Guid.NewGuid(),
            CompetitionId = competitionId,
            LeaderCategoryId = leaderCategoryId,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = correlationId,
            Stats = new List<CompetitionLeaderStat>() // to be populated manually in the processor
        };
    }
}