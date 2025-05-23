﻿using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class FranchiseSeasonExtensions
    {
        public static FranchiseSeason AsEntity(
            this EspnTeamSeasonDto dto,
            Guid franchiseId,
            Guid franchiseSeasonId,
            int seasonYear,
            Guid correlationId)
        {
            return new FranchiseSeason()
            {
                Abbreviation = dto.Abbreviation,
                ColorCodeAltHex = dto.AlternateColor,
                ColorCodeHex = dto.Color ?? string.Empty,
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,
                DisplayName = dto.DisplayName,
                DisplayNameShort = dto.ShortDisplayName,
                FranchiseId = franchiseId,
                Id = franchiseSeasonId,
                IsActive = dto.IsActive,
                IsAllStar = dto.IsAllStar,
                Location = dto.Location,
                Logos = [],
                Losses = 0,
                Name = dto.Name,
                Season = seasonYear,
                Slug = dto.Slug,
                Ties = 0,
                Wins = 0
            };
        }

        public static FranchiseSeasonDto ToCanonicalModel(this FranchiseSeason entity)
        {
            return new FranchiseSeasonDto()
            {
                // TODO: Implement
            };
        }
    }
}
