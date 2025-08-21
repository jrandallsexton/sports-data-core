namespace SportsData.Api.Infrastructure.Data.Canonical.Models
{
    public class SeasonWeek
    {
        public Guid Id { get; set; }

        public Guid SeasonId { get; set; }

        public int WeekNumber { get; set; }

        public int SeasonYear { get; set; }
    }
}
