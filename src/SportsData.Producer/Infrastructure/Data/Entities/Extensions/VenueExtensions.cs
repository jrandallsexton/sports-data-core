using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Slugs;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class VenueExtensions
{
    public static Venue AsEntity(
        this EspnVenueDto dto,
        SportsData.Core.Common.Hashing.IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid correlationId)
    {
        if (dto.Ref == null)
            throw new ArgumentException("Venue DTO is missing its $ref property.");

        var venueIdentity = externalRefIdentityGenerator.Generate(dto.Ref);

        return new Venue
        {
            Id = venueIdentity.CanonicalId,
            CreatedBy = correlationId,
            CreatedUtc = DateTime.UtcNow,
            ExternalIds = new List<VenueExternalId>
            {
                new VenueExternalId
                {
                    Id = Guid.NewGuid(),
                    Value = venueIdentity.UrlHash,
                    Provider = SourceDataProvider.Espn,
                    SourceUrlHash = venueIdentity.UrlHash,
                    SourceUrl = venueIdentity.CleanUrl
                }
            },
            IsGrass = dto.Grass,
            IsIndoor = dto.Indoor,
            Name = dto.FullName,
            ShortName = string.IsNullOrEmpty(dto.ShortName) ? dto.FullName : dto.ShortName,
            Slug = SlugGenerator.GenerateSlug(new[] { dto.ShortName, dto.FullName }),
            Capacity = dto.Capacity,
            City = dto.Address?.City ?? string.Empty,
            State = dto.Address?.State ?? string.Empty,
            PostalCode = dto.Address?.ZipCode.ToString() ?? string.Empty,
        };
    }

    public static VenueDto AsCanonical(this Venue entity)
    {
        return new VenueDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Slug = entity.Slug,
            CreatedUtc = entity.CreatedUtc,
            IsGrass = entity.IsGrass,
            IsIndoor = entity.IsIndoor,
            ShortName = entity.ShortName,
            Capacity = entity.Capacity,
            Address = null,
            Images = new List<VenueImageDto>(), // TODO: Add image metadata
            UpdatedUtc = entity.LastModified
        };
    }
}
