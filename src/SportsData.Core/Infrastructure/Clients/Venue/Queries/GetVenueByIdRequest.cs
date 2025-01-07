using SportsData.Core.Infrastructure.Clients.Venue.DTOs;

namespace SportsData.Core.Infrastructure.Clients.Venue.Queries
{
    public record GetVenueByIdRequest
    {
        public int Id { get; set; }
    }

    public record GetVenueByIdResponse
    {
        public VenueDto Venue { get; set; }
    }
}
