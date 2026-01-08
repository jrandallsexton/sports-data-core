using System;

namespace SportsData.Core.Dtos.Canonical
{
    public class VenueImageDto : LogoDtoBase
    {
        public Guid VenueId { get; init; }
        
        public VenueImageDto() : base()
        {
        }
        
        public VenueImageDto(Guid venueId, Uri url, int? height, int? width) 
            : base(url, height, width)
        {
            VenueId = venueId;
        }
    }
}
