using SportsData.Core.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Models.Canonical;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class VenueExtensions
    {
        public static Venue AsVenueEntity(
            this EspnVenueDto dto,
            Guid venueId,
            Guid correlationId)
        {
            return new Venue()
            {
                Id = Guid.NewGuid(),
                Name = dto.FullName,
                ShortName = dto.ShortName,
                IsIndoor = dto.Indoor,
                IsGrass = dto.Grass,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = correlationId,
                ExternalIds = [new VenueExternalId() { Id = Guid.NewGuid(), Value = dto.Id.ToString(), Provider = SourceDataProvider.Espn }]
            };
        }

        public static VenueCanonicalModel ToCanonicalModel(this Venue entity)
        {
            return new VenueCanonicalModel()
            {
                Id = entity.Id,
                Name = entity.Name,
                CreatedUtc = entity.CreatedUtc,
                IsIndoor = entity.IsIndoor,
                IsGrass = entity.IsGrass,
                ShortName = entity.ShortName
            };
        }
    }
}
