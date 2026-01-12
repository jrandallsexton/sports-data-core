using System.Collections.Generic;

namespace SportsData.Core.Dtos.Canonical
{
    public record VenueDto : DtoBase
    {
        public required string Name { get; set; }

        public string? ShortName { get; set; }

        public bool IsGrass { get; set; }

        public bool IsIndoor { get; set; }

        public required string Slug { get; set; }

        public int Capacity { get; set; } = 0;

        public List<VenueImageDto> Images { get; set; } = [];

        public AddressDto? Address { get; set; }

        public decimal? Latitude { get; set; }

        public decimal? Longitude { get; set; }

        // TODO: Get a physical address on this thing
    }
}
