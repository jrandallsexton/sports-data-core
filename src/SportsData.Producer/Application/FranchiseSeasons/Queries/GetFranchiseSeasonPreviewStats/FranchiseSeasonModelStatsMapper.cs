using System;
using System.Collections.Generic;
using System.Linq;

using SportsData.Core.Dtos.Canonical;

namespace SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonPreviewStats;

/// <summary>
/// Maps the stored ESPN team-season statistics onto the compact block the
/// matchup-preview prompt receives.
/// </summary>
/// <remarks>
/// Reads <c>Value</c>, never <c>PerGameValue</c>. ESPN populates the
/// per-game column for ~15 of ~285 statistics (kicking and kickoff-return
/// rates only), and none of the ones mapped here. The previous mapper read
/// PerGameValue for everything and, because the raw row typed it as a
/// non-nullable double, every null became 0: every preview since April
/// carried all-zero stats for both teams, and the 0.0 in RushingYardsPerGame
/// counted as "has stats", so the with-stats prompt was selected on empty
/// numbers (found 2026-09-24 on Northwestern @ Indiana, week 4).
/// <para>
/// Keys are category-qualified where ESPN reuses a name with different
/// meanings: <c>sacks</c> is 7 under <c>defensive</c> (sacks made) and 4
/// under <c>passing</c> (sacks taken); <c>interceptions</c> is 3 under
/// <c>defensiveInterceptions</c> and 0 under <c>passing</c> (thrown). The
/// old GroupBy-first picked by alphabetical category order, by luck.
/// </para>
/// <para>
/// Per-game figures come from ESPN's own per-game keys where they exist
/// (<c>passingYardsPerGame</c>, <c>rushingYardsPerGame</c>, <c>yardsPerGame</c>,
/// <c>totalPointsPerGame</c>); the rest are derived from season totals and
/// <c>teamGamesPlayed</c>. A missing key stays null so the caller's
/// both-teams-have-stats check means what it says.
/// </para>
/// </remarks>
internal static class FranchiseSeasonModelStatsMapper
{
    public static FranchiseSeasonModelStatsDto Map(IReadOnlyCollection<FranchiseSeasonRawStat> stats)
    {
        // (category, statistic) -> value, first row wins within an exact pair.
        var byPair = new Dictionary<(string Category, string Statistic), double?>();
        // statistic -> value, for keys whose meaning does not depend on category
        // (ESPN repeats them verbatim under passing/rushing/receiving/scoring).
        var byName = new Dictionary<string, double?>(StringComparer.Ordinal);

        foreach (var s in stats)
        {
            if (s.Statistic is null) continue;
            var pair = (s.Category ?? string.Empty, s.Statistic);
            if (!byPair.ContainsKey(pair)) byPair[pair] = s.Value;
            if (!byName.ContainsKey(s.Statistic)) byName[s.Statistic] = s.Value;
        }

        double? Any(string statistic) => byName.TryGetValue(statistic, out var v) ? v : null;
        double? In(string category, string statistic) =>
            byPair.TryGetValue((category, statistic), out var v) ? v : null;
        double? Div(double? a, double? b) => a.HasValue && b.HasValue && b.Value != 0 ? a / b : null;
        int? ToInt(double? v) => v.HasValue ? (int?)Convert.ToInt32(v.Value) : null;

        var gamesPlayed = Any("teamGamesPlayed");

        return new FranchiseSeasonModelStatsDto
        {
            PointsPerGame = In("scoring", "totalPointsPerGame") ?? Any("totalPointsPerGame"),
            YardsPerGame = Any("yardsPerGame") ?? Div(Any("totalYardsFromScrimmage"), gamesPlayed),
            PassingYardsPerGame = In("passing", "passingYardsPerGame") ?? Div(In("passing", "passingYards"), gamesPlayed),
            RushingYardsPerGame = In("rushing", "rushingYardsPerGame") ?? Div(In("rushing", "rushingYards"), gamesPlayed),
            ThirdDownConvPct = In("miscellaneous", "thirdDownConvPct") ?? Any("thirdDownConvPct"),
            RedZoneScoringPct = In("miscellaneous", "redzoneScoringPct") ?? Any("redzoneScoringPct"),
            TurnoverDifferential = In("miscellaneous", "turnOverDifferential") ?? Any("turnOverDifferential"),

            PenaltiesPerGame = Div(Any("totalPenalties"), gamesPlayed),
            PenaltyYardsPerGame = Div(Any("totalPenaltyYards"), gamesPlayed),
            AvgYardsPerPlay = Div(Any("totalYardsFromScrimmage"), Any("totalOffensivePlays")),

            // Defensive meanings, explicitly: sacks MADE, interceptions MADE.
            Sacks = ToInt(In("defensive", "sacks")),
            Interceptions = ToInt(In("defensiveInterceptions", "interceptions")),
            FumblesLost = ToInt(In("general", "fumblesLost") ?? Any("fumblesLost")),
            Takeaways = ToInt(In("miscellaneous", "totalTakeaways") ?? Any("totalTakeaways"))
        };
    }
}
