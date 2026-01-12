using SportsData.Core.Dtos.Canonical;

using System.Collections.Generic;

namespace SportsData.Core.Infrastructure.Clients.Venue.Queries
{
    public class GetVenuesRequest
    {
    }

    /// <summary>
    /// Internal client response for venues list.
    /// Contains pagination metadata but NO HATEOAS links (internal API).
    /// Matches Producer's GetAllVenuesResponse structure (PaginatedResponse<VenueDto>).
    /// </summary>
    public class GetVenuesResponse
    {
        public List<VenueDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }
    }
}
