namespace SportsData.Api.Infrastructure.Data.Canonical.Models
{
    public class MatchupForPreviewDto
    {
        public int SeasonYear { get; set; }
        public int WeekNumber { get; set; }

        public Guid ContestId { get; set; }
        public DateTime StartDateUtc { get; set; }

        public required string Venue { get; set; }
        public required string VenueCity { get; set; }
        public string? VenueState { get; set; }

        public Guid AwayFranchiseSeasonId { get; set; }
        public required string Away { get; set; }
        public required string AwaySlug { get; set; }
        public int? AwayRank { get; set; }
        public required string AwayConferenceSlug { get; set; }
        public int AwayWins { get; set; }
        public int AwayLosses { get; set; }
        public int AwayConferenceWins { get; set; }
        public int AwayConferenceLosses { get; set; }

        public Guid HomeFranchiseSeasonId { get; set; }
        public required string Home { get; set; }
        public required string HomeSlug { get; set; }
        public int? HomeRank { get; set; }
        public required string HomeConferenceSlug { get; set; }
        public int HomeWins { get; set; }
        public int HomeLosses { get; set; }
        public int HomeConferenceWins { get; set; }
        public int HomeConferenceLosses { get; set; }

        public string? Spread { get; set; }             // co."Details"
        public double? AwaySpread { get; set; }
        public double? HomeSpread { get; set; }
        public double? OverUnder { get; set; }
        public double? OverOdds { get; set; }
        public double? UnderOdds { get; set; }
    }

}
