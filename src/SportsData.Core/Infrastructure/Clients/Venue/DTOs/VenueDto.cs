namespace SportsData.Core.Infrastructure.Clients.Venue.DTOs
{
    public class VenueDto
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string ShortName { get; set; }

        public bool IsGrass { get; set; }

        public bool IsIndoor { get; set; }
    }
}
