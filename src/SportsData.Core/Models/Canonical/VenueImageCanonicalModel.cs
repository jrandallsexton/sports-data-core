using System;

namespace SportsData.Core.Models.Canonical
{
    public class VenueImageCanonicalModel(
        Guid venueId,
        string url,
        int? height,
        int? width) : CanonicalLogoModelBase(url, height, width)
    {
        public Guid VenueId { get; init; } = venueId;
    }
}
