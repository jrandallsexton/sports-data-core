namespace SportsData.Api.Infrastructure.Data.Canonical.Models
{
    public class MatchupResult
    {
        public Guid ContestId { get; set; }

        public Guid SeasonWeekId { get; set; }

        public Guid AwayFranchiseSeasonId { get; set; }

        public Guid HomeFranchiseSeasonId { get; set; }

        public int AwayScore { get; set; }

        public int HomeScore { get; set; }

        public Guid WinnerFranchiseSeasonId { get; set; }

        public Guid? SpreadWinnerFranchiseSeasonId { get; set; } // nullable if there was no spread

        public DateTime FinalizedUtc { get; set; }
    }
}
