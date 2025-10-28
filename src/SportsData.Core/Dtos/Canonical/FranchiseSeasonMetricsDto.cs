namespace SportsData.Core.Dtos.Canonical
{
    public class FranchiseSeasonMetricsDto
    {
        public string FranchiseName { get; set; } = null!;
        public string FranchiseSlug { get; set; } = null!;

        public string? Conference { get; set; }

        public int SeasonYear { get; set; }
        public int GamesPlayed { get; set; }

        // Offense
        public decimal Ypp { get; set; }
        public decimal SuccessRate { get; set; }
        public decimal ExplosiveRate { get; set; }
        public decimal PointsPerDrive { get; set; }
        public decimal ThirdFourthRate { get; set; }
        public decimal? RzTdRate { get; set; }
        public decimal? RzScoreRate { get; set; }
        public decimal TimePossRatio { get; set; }

        // Defense (opponent metrics)
        public decimal OppYpp { get; set; }
        public decimal OppSuccessRate { get; set; }
        public decimal OppExplosiveRate { get; set; }
        public decimal OppPointsPerDrive { get; set; }
        public decimal OppThirdFourthRate { get; set; }
        public decimal? OppRzTdRate { get; set; }
        public decimal? OppScoreTdRate { get; set; }

        // ST / Discipline
        public decimal NetPunt { get; set; }
        public decimal FgPctShrunk { get; set; }
        public decimal FieldPosDiff { get; set; }
        public decimal TurnoverMarginPerDrive { get; set; }
        public decimal PenaltyYardsPerPlay { get; set; }
    }
}
