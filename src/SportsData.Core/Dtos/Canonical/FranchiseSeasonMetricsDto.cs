using System.Collections.Generic;

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

    public class MetricLegendDto
    {
        public string Field { get; set; } = null!;
        public string Label { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string Group { get; set; } = null!; // Offense, Defense, Special Teams, Discipline
        public string Format { get; set; } = "decimal"; // percent, decimal, ratio
    }

    public static class FranchiseSeasonMetricLegend
    {
        public static readonly List<MetricLegendDto> All = new()
        {
            // Offense
            new() { Field = "ypp", Label = "Yards per Play", Group = "Offense", Format = "decimal", Description = "Average yards gained per offensive play." },
            new() { Field = "successRate", Label = "Success Rate", Group = "Offense", Format = "percent", Description = "Percent of plays that meet success criteria based on down and distance." },
            new() { Field = "explosiveRate", Label = "Explosive Rate", Group = "Offense", Format = "percent", Description = "Percent of plays gaining 10+ yards rushing or 15+ yards passing." },
            new() { Field = "pointsPerDrive", Label = "Points per Drive", Group = "Offense", Format = "decimal", Description = "Average points scored per offensive possession." },
            new() { Field = "thirdFourthRate", Label = "3rd/4th Down Conv. Rate", Group = "Offense", Format = "percent", Description = "Percent of third and fourth down attempts converted into first downs." },
            new() { Field = "rzTdRate", Label = "Red Zone TD Rate", Group = "Offense", Format = "percent", Description = "Touchdown percentage on red zone possessions." },
            new() { Field = "rzScoreRate", Label = "Red Zone Score Rate", Group = "Offense", Format = "percent", Description = "Score percentage (FG or TD) on red zone possessions." },
            new() { Field = "timePossRatio", Label = "Time of Possession Ratio", Group = "Offense", Format = "ratio", Description = "Ratio of possession time vs opponent (greater than 1 is favorable)." },

            // Defense
            new() { Field = "oppYpp", Label = "Opponent Yards per Play", Group = "Defense", Format = "decimal", Description = "Average yards allowed per defensive play." },
            new() { Field = "oppSuccessRate", Label = "Opponent Success Rate", Group = "Defense", Format = "percent", Description = "Success rate allowed by defense based on down and distance." },
            new() { Field = "oppExplosiveRate", Label = "Opponent Explosive Rate", Group = "Defense", Format = "percent", Description = "Explosive play rate allowed by the defense." },
            new() { Field = "oppPointsPerDrive", Label = "Opponent Points per Drive", Group = "Defense", Format = "decimal", Description = "Average points allowed per opponent possession." },
            new() { Field = "oppThirdFourthRate", Label = "Opponent 3rd/4th Down Rate", Group = "Defense", Format = "percent", Description = "Conversion rate allowed on 3rd and 4th downs." },
            new() { Field = "oppRzTdRate", Label = "Opponent RZ TD Rate", Group = "Defense", Format = "percent", Description = "Touchdown rate allowed on opponent red zone possessions." },
            new() { Field = "oppScoreTdRate", Label = "Opponent RZ Score Rate", Group = "Defense", Format = "percent", Description = "Score (TD or FG) rate allowed in the red zone." },

            // Special Teams & Discipline
            new() { Field = "netPunt", Label = "Net Punt", Group = "Special Teams", Format = "decimal", Description = "Average net punting distance in yards." },
            new() { Field = "fgPctShrunk", Label = "FG % (Adjusted)", Group = "Special Teams", Format = "percent", Description = "Field goal percentage, adjusted for distance and angle." },
            new() { Field = "fieldPosDiff", Label = "Field Position Diff", Group = "Special Teams", Format = "decimal", Description = "Average difference in starting field position per drive." },
            new() { Field = "turnoverMarginPerDrive", Label = "TO Margin per Drive", Group = "Discipline", Format = "decimal", Description = "Net turnovers gained per possession. Positive is favorable." },
            new() { Field = "penaltyYardsPerPlay", Label = "Penalty Yards per Play", Group = "Discipline", Format = "decimal", Description = "Average penalty yards assessed per play." },
        };
    }
}
