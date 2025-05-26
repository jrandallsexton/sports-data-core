using System;

namespace SportsData.Core.Common.Mapping
{
    public static class ModeMapper
    {
        public static Sport ResolveMode(string sport, string league)
        {
            // Normalize and map
            return (sport.ToLower(), league.ToLower()) switch
            {
                ("football", "ncaa") => Sport.FootballNcaa,
                ("football", "nfl") => Sport.FootballNfl,
                ("baseball", "mlb") => Sport.BaseballMlb,
                ("basketball", "nba") => Sport.BasketballNba,
                ("golf", "pga") => Sport.GolfPga,
                _ => throw new NotSupportedException($"Unsupported sport/league combo: {sport}/{league}")
            };
        }
    }
}