namespace SportsData.Api.Infrastructure.Data.Canonical.Models
{
    public class Matchup
    {
        public Guid SeasonWeekId { get; set; }

        public Guid ContestId { get; set; }

        public DateTime StartDateUtc { get; set; }

        public required string AwaySlug { get; set; }

        public int? AwayRank { get; set; }

        public int AwayWins { get; set; }

        public int AwayLosses { get; set; }

        public int AwayConferenceWins { get; set; }

        public int AwayConferenceLosses { get; set; }

        public string? AwayConferenceSlug { get; set; }

        public required string HomeSlug { get; set; }

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
