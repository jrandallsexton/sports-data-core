using System.Collections.Generic;

namespace SportsData.Core.Models.Canonical
{
    public class VenueDto : DtoBase
    {
        public required string Name { get; set; }

        public required string ShortName { get; set; }

        public bool IsGrass { get; set; }

        public bool IsIndoor { get; set; }

        public required string Slug { get; set; }

        public List<VenueImageDto> Images { get; set; } = new List<VenueImageDto>();

        // TODO: Get a physical address on this thing
    }
}
