using System;

namespace SportsData.Core.Dtos.Canonical
{
    public class VenueImageDto(
        Guid venueId,
        string url,
        int? height,
        int? width) : LogoDtoBase(url, height, width)
    {
        public Guid VenueId { get; init; } = venueId;
    }
}
