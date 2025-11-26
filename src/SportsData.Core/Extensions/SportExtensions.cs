using SportsData.Core.Common;

using System;

namespace SportsData.Core.Extensions;

public static class SportExtensions
{
    public static string ToKebabCase(this Sport sport)
    {
        return sport switch
        {
            Sport.FootballNcaa => "football-ncaa",
            Sport.FootballNfl => "football-nfl",
            Sport.BaseballMlb => "baseball-mlb",
            Sport.BasketballNba => "basketball-nba",
            Sport.GolfPga => "golf-pga",
            _ => throw new ArgumentException()
        };
    }
}