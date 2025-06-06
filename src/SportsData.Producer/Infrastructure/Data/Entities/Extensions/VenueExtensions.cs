﻿using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Producer.Application.Slugs;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class VenueExtensions
    {
        public static Venue AsEntity(
            this EspnVenueDto dto,
            Guid venueId,
            Guid correlationId,
            ISlugGenerator slugGenerator)
        {
            return new Venue()
            {
                Id = venueId,
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,
                ExternalIds = [new VenueExternalId() { Id = Guid.NewGuid(), Value = dto.Id.ToString(), Provider = SourceDataProvider.Espn }],
                IsGrass = dto.Grass,
                IsIndoor = dto.Indoor,
                Name = dto.FullName,
                ShortName = string.IsNullOrEmpty(dto.ShortName) ? dto.FullName : dto.ShortName,
                Slug = slugGenerator.GenerateSlug([dto.ShortName, dto.FullName])
            };
        }

        public static VenueDto ToCanonicalModel(this Venue entity)
        {
            return new VenueDto()
            {
                Id = entity.Id,
                CreatedUtc = entity.CreatedUtc,
                IsGrass = entity.IsGrass,
                IsIndoor = entity.IsIndoor,
                Name = entity.Name,
                ShortName = entity.ShortName,
                Slug = entity.Slug
            };
        }
    }
}
