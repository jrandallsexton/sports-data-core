using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Slugs;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class VenueExtensions
    {
        public static Venue AsEntity(
            this EspnVenueDto dto,
            Guid venueId,
            Guid correlationId)
        {
            var sourceUrlHash = HashProvider.GenerateHashFromUri(dto.Ref);
            return new Venue()
            {
                Id = venueId,
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,
                ExternalIds = [ new VenueExternalId()
                {
                    Id = Guid.NewGuid(),
                    Value = sourceUrlHash,
                    Provider = SourceDataProvider.Espn,
                    SourceUrlHash = sourceUrlHash
                }],
                IsGrass = dto.Grass,
                IsIndoor = dto.Indoor,
                Name = dto.FullName,
                ShortName = string.IsNullOrEmpty(dto.ShortName) ? dto.FullName : dto.ShortName,
                Slug = SlugGenerator.GenerateSlug([dto.ShortName, dto.FullName]),
                Capacity = dto.Capacity,
                City = dto.Address?.City ?? string.Empty,
                State = dto.Address?.State ?? string.Empty,
                PostalCode = dto.Address?.ZipCode.ToString() ?? string.Empty,
            };
        }

        public static VenueDto AsCanonical(this Venue entity)
        {
            // TODO: Address and Images
            return new VenueDto()
            {
                Id = entity.Id,
                CreatedUtc = entity.CreatedUtc,
                IsGrass = entity.IsGrass,
                IsIndoor = entity.IsIndoor,
                Name = entity.Name,
                ShortName = entity.ShortName,
                Slug = entity.Slug,
                Capacity = entity.Capacity,
                Address = null,
                Images = [], // TODO: Add image metadata
                UpdatedUtc = entity.LastModified
            };
        }
    }
}
