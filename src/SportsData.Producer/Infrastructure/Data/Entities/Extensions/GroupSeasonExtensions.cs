using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class GroupSeasonExtensions
    {
        public static GroupSeason AsEntity(
            this EspnGroupBySeasonDto dto,
            Guid groupId,
            Guid groupSeasonId,
            int seasonYear,
            Guid correlationId)
        {
            // TODO: Use Identity
            return new GroupSeason
            {
                Id = groupSeasonId,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = correlationId,
                Season = seasonYear,
                GroupId = groupId,
                Name = dto.Name,
                Abbreviation = dto.Abbreviation,
                ShortName = dto.ShortName,
                MidsizeName = dto.MidsizeName,
                Slug = dto.Slug
            };
        }


        public static ConferenceSeasonDto ToCanonicalModel(this GroupSeason entity)
        {
            return new ConferenceSeasonDto()
            {
                // TODO: Implement
            };
        }
    }
}
