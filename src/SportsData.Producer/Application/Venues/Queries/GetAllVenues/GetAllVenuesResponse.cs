using SportsData.Core.Dtos.Canonical;

namespace SportsData.Producer.Application.Venues.Queries.GetAllVenues
{
    public class GetAllVenuesResponse
    {
        public int Count { get; set; }

        public List<VenueDto> Items { get; set; } = [];
    }
}
