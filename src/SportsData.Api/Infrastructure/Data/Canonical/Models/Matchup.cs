namespace SportsData.Api.Infrastructure.Data.Canonical.Models
{
    public class Matchup
    {
        public Guid SeasonWeekId { get; set; }

        public Guid ContestId { get; set; }

        public DateTime StartDateUtc { get; set; }

        public string Status { get; set; } = null!;

        // Venue Info
        public string? VenueName { get; set; }

        public string? VenueCity { get; set; }

        public string? VenueState { get; set; }

        public decimal? VenueLatitude { get; set; }

        public decimal? VenueLongitude { get; set; }

        // Away Team Info

        public required string AwaySlug { get; set; }

        public string? AwayColor { get; set; }

        public string AwayAbbreviation { get; set; } = null!;

        public int? AwayRank { get; set; }

        public int AwayWins { get; set; }

        public int AwayLosses { get; set; }

        public int AwayConferenceWins { get; set; }

        public int AwayConferenceLosses { get; set; }

        public string? AwayConferenceSlug { get; set; }

        // Home Team Info

        public required string HomeSlug { get; set; }

        public string? HomeColor { get; set; }

        public string HomeAbbreviation { get; set; } = null!;

        public int? HomeRank { get; set; }

        public int HomeWins { get; set; }

        public int HomeLosses { get; set; }

        public int HomeConferenceWins { get; set; }

        public int HomeConferenceLosses { get; set; }

        public string? HomeConferenceSlug { get; set; }

        public string? Spread { get; set; }

        public double? AwaySpread { get; set; }

        public double? HomeSpread { get; set; }

        public double? OverUnder { get; set; }

        public double? OverOdds { get; set; }

        public double? UnderOdds { get; set; }
    }
}
