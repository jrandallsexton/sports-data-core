using SportsData.Core.Dtos.Canonical;

namespace SportsData.Core.Infrastructure.Clients.Venue.Queries
{
    public record GetVenueByIdRequest(int Id);

    public record GetVenueByIdResponse(VenueDto? Venue);
}