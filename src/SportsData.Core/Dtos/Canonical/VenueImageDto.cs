using System;

namespace SportsData.Core.Dtos.Canonical
{
    public class VenueImageDto(
        Guid venueId,
        Uri url,
        int? height,
        int? width) : LogoDtoBase(url, height, width)
    {
        public Guid VenueId { get; init; } = venueId;
    }
}
