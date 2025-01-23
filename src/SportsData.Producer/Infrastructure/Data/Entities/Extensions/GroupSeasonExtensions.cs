﻿using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;

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
                GroupId = groupId,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = correlationId,
                Season = seasonYear
            };
        }
    }
}
