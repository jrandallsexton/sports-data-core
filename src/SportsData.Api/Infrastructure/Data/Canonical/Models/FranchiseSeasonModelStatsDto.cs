namespace SportsData.Api.Infrastructure.Data.Canonical.Models
{
    public class FranchiseSeasonModelStatsDto
    {
        public double? PointsPerGame { get; set; }
        public double? YardsPerGame { get; set; }
        public double? PassingYardsPerGame { get; set; }
        public double? RushingYardsPerGame { get; set; }

        public double? ThirdDownConvPct { get; set; }
        public double? RedZoneScoringPct { get; set; }
        public double? TurnoverDifferential { get; set; }

        public double? PenaltiesPerGame { get; set; }
        public double? PenaltyYardsPerGame { get; set; }

        public double? AvgYardsPerPlay { get; set; }

        public int? Sacks { get; set; }
        public int? Interceptions { get; set; }
        public int? FumblesLost { get; set; }
        public int? Takeaways { get; set; }
    }

}
