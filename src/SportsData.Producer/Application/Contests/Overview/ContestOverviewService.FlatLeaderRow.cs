namespace SportsData.Producer.Application.Contests.Overview;

public partial class ContestOverviewService
{
    /// <summary>
    /// Strongly-typed projection row used for grouping/selection (EF-safe).
    /// </summary>
    private sealed class FlatLeaderRow
    {
        // Category metadata
        public string CategoryId { get; set; } = null!;

        public string CategoryName { get; set; } = null!;

        public string? Abbr { get; set; }

        public string? Unit { get; set; }

        public int DisplayOrder { get; set; }

        // Player/leader data
        public Guid FranchiseSeasonId { get; set; }

        public Guid AthleteSeasonId { get; set; } // For fetching headshots

        public string PlayerName { get; set; } = null!;

        public string? PlayerHeadshotUrl { get; set; }

        public string? StatLine { get; set; }

        public decimal? Numeric { get; set; }

        public int Rank { get; set; } = 1;
    }
}