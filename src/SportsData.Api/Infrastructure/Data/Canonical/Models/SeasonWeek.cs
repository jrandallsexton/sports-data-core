namespace SportsData.Api.Infrastructure.Data.Canonical.Models
{
    public class SeasonWeek
    {
        public Guid Id { get; set; }

        public Guid SeasonId { get; set; }

        public int WeekNumber { get; set; }

        public string? SeasonPhase { get; set; }

        public int SeasonYear { get; set; }

        public bool IsNonStandardWeek { get; set; }

        public bool IsPostSeason => SeasonPhase?.ToLower() == "postseason";
    }
}
