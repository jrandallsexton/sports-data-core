using SportsData.Core.Infrastructure.Clients.Venue.DTOs;

using System.Collections.Generic;

namespace SportsData.Core.Infrastructure.Clients.Venue.Queries
{
    public class GetVenuesRequest
    {
    }

    public class GetVenuesResponse
    {
        public List<VenueDto> Venues { get; set; } = new List<VenueDto>();
    }
}
