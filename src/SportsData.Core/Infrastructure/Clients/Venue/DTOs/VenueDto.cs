﻿using SportsData.Core.Dtos.Canonical;

using System.Collections.Generic;

namespace SportsData.Core.Infrastructure.Clients.Venue.DTOs
{
    public record VenueDto
    {
        public required string Name { get; set; }

        public required string ShortName { get; set; }

        public bool IsGrass { get; set; }

        public bool IsIndoor { get; set; }

        public required string Slug { get; set; }

        public int Capacity { get; set; } = 0;

        public List<VenueImageDto> Images { get; set; } = new List<VenueImageDto>();

        public AddressDto Address { get; set; }
    }

    public class AddressDto
    {
        public string City { get; set; }

        public string State { get; set; }
    }
}
