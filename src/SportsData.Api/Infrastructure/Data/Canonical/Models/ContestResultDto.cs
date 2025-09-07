using SportsData.Api.Application;

namespace SportsData.Api.Infrastructure.Data.Canonical.Models
{
    public class ContestResultDto
    {
        public DateTime StartDateUtc { get; set; }
        public Guid ContestId { get; set; }

        // Teams
        public string AwayShort { get; set; } = default!;
        public Guid AwayFranchiseSeasonId { get; set; }
        public string AwaySlug { get; set; } = default!;
        public int? AwayRank { get; set; }

        public string HomeShort { get; set; } = default!;
        public Guid HomeFranchiseSeasonId { get; set; }
        public string HomeSlug { get; set; } = default!;
        public int? HomeRank { get; set; }

        // Odds
        public decimal? AwaySpread { get; set; }
        public decimal? HomeSpread { get; set; }
        public decimal? OverUnder { get; set; }

        // Result
        public DateTime? FinalizedUtc { get; set; }
        public int? AwayScore { get; set; }
        public int? HomeScore { get; set; }
        public Guid? WinnerFranchiseSeasonId { get; set; }
        public Guid? SpreadWinnerFranchiseSeasonId { get; set; }
        public OverUnderPick? OverUnderResult { get; set; }
        public DateTime? CompletedUtc { get; set; }
    }
}
