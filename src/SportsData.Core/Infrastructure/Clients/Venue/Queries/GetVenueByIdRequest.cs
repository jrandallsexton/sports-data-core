using SportsData.Core.Infrastructure.Clients.Venue.DTOs;

namespace SportsData.Core.Infrastructure.Clients.Venue.Queries
{
    public record GetVenueByIdRequest(int Id);

    public record GetVenueByIdResponse(VenueDto? Venue);
}