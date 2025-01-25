using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Models.Canonical;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class GroupSeasonExtensions
    {
        public static GroupSeason AsGroupSeasonEntity(
            this EspnGroupBySeasonDto dto,
            Guid groupId,
            Guid groupSeasonId,
            int seasonYear,
            Guid correlationId)
        {
            return new GroupSeason()
            {
                Id = groupSeasonId,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = correlationId,
                Season = seasonYear,
                GroupId = groupId
            };
        }

        public static ConferenceSeasonCanonicalModel ToCanonicalModel(this GroupSeason entity)
        {
            return new ConferenceSeasonCanonicalModel()
            {
                // TODO: Implement
            };
        }
    }
}
