using System.Globalization;

using SportsData.Api.Infrastructure.Data.Canonical.Models;

namespace SportsData.Api.Application.UI.TeamCard;

public interface IStatFormattingService
{
    void ApplyFriendlyLabelsAndFormatting(FranchiseSeasonStatisticDto dto, bool sortByLabel = true);
}

public sealed class StatFormattingService : IStatFormattingService
{
    // ---- category configs ---------------------------------------------------
    private sealed record CategoryConfig(
        Dictionary<string, string> FriendlyMap,
        HashSet<string> PercentKeys,
        HashSet<string> PreferPerGameForCounts,
        HashSet<string> LowerIsBetterKeys);

    // ===== Defensive ============================================================
    private static readonly CategoryConfig Defensive = new(
        FriendlyMap: new(StringComparer.OrdinalIgnoreCase)
        {
            // Tackling
            ["tackles"] = "Tackles",
            ["soloTackles"] = "Solo Tackles",
            ["assistTackles"] = "Assisted Tackles",
            ["tacklesForLoss"] = "TFL",
            ["tflYards"] = "TFL Yds",

            // Pass rush
            ["sacks"] = "Sacks",
            ["sackYards"] = "Sack Yds",
            ["qbHurries"] = "QB Hurries",

            // Coverage
            ["passesDefended"] = "Passes Defended",
            ["passesBrokenUp"] = "Passes Broken Up",
            ["interceptions"] = "INT",
            ["interceptionYards"] = "INT Yds",
            ["interceptionTouchdowns"] = "Pick 6",

            // Ball security
            ["fumblesForced"] = "Fumbles Forced",
            ["fumblesRecovered"] = "Fumbles Recovered",
            ["defensiveTouchdowns"] = "Def TD",
            ["safeties"] = "Safeties",

            // First downs / stops
            ["defensiveFirstDowns"] = "Def 1st Downs",
            ["thirdDownConvAllowedPct"] = "3rd Down Allowed %",
            ["redZoneAllowedPct"] = "Red Zone Allowed %",

            // Bookkeeping
            ["teamGamesPlayed"] = "Games",
            ["miscYards"] = "Misc Yds"
        },
        PercentKeys: new(StringComparer.OrdinalIgnoreCase)
        {
            "thirdDownConvAllowedPct","redZoneAllowedPct"
        },
        PreferPerGameForCounts: new(StringComparer.OrdinalIgnoreCase)
        {
            "tackles","soloTackles","assistTackles","tacklesForLoss","sacks","qbHurries",
            "passesDefended","passesBrokenUp","interceptions","fumblesForced","fumblesRecovered",
            "defensiveTouchdowns","safeties"
        },
        LowerIsBetterKeys: new(StringComparer.OrdinalIgnoreCase)
        {
            // Allowed/first downs: lower is better for defense
            "thirdDownConvAllowedPct","redZoneAllowedPct","defensiveFirstDowns"
            // Note: defensive interceptions are takeaways → higher is better, so omitted.
        }
    );

    // ===== General ==============================================================
    private static readonly CategoryConfig General = new(
        FriendlyMap: new(StringComparer.OrdinalIgnoreCase)
        {
            ["totalOffensivePlays"] = "Offensive Plays",
            ["totalYards"] = "Total Yards",
            ["yardsPerPlay"] = "Yards/Play",
            ["firstDowns"] = "First Downs",
            ["thirdDownPct"] = "3rd Down %",
            ["fourthDownPct"] = "4th Down %",
            ["redZonePct"] = "Red Zone %",
            ["timeOfPossession"] = "Time Of Possession",
            ["penalties"] = "Penalties",
            ["penaltyYards"] = "Penalty Yds",
            ["turnovers"] = "Turnovers",
            ["takeaways"] = "Takeaways",
            ["giveaways"] = "Giveaways",
            ["fumbles"] = "Fumbles",
            ["fumblesLost"] = "Fumbles Lost",
            ["teamGamesPlayed"] = "Games"
        },
        PercentKeys: new(StringComparer.OrdinalIgnoreCase)
        {
            "thirdDownPct","fourthDownPct","redZonePct"
        },
        PreferPerGameForCounts: new(StringComparer.OrdinalIgnoreCase)
        {
            "firstDowns","penalties","turnovers","takeaways","giveaways","fumbles","fumblesLost"
        },
        LowerIsBetterKeys: new(StringComparer.OrdinalIgnoreCase)
        {
            // Bad things for a team: fewer is better
            "penalties","penaltyYards","turnovers","giveaways","fumbles","fumblesLost","totalPenalties","totalPenaltyYards"
        }
    );

    // ===== Kicking ==============================================================
    private static readonly CategoryConfig Kicking = new(
        FriendlyMap: new(StringComparer.OrdinalIgnoreCase)
        {
            // Field goals
            ["fieldGoalsMade"] = "FG Made",
            ["fieldGoalsAttempted"] = "FG Att",
            ["fieldGoals"] = "FG Made",              // alias
            ["fieldGoalPct"] = "FG %",
            ["fieldGoalsLong"] = "Long FG (yds)",
            ["fieldGoalsMade_0_19"] = "FG 0–19",
            ["fieldGoalsMade_20_29"] = "FG 20–29",
            ["fieldGoalsMade_30_39"] = "FG 30–39",
            ["fieldGoalsMade_40_49"] = "FG 40–49",
            ["fieldGoalsMade_50Plus"] = "FG 50+",

            // Extra points
            ["extraPointsMade"] = "XP Made",
            ["extraPointsAttempted"] = "XP Att",
            ["extraPointPct"] = "XP %",
            ["extraPoints"] = "XP Made",              // alias
            ["extraPointsBlocked"] = "XP Blocked",

            // Kickoffs
            ["kickoffs"] = "Kickoffs",
            ["kickoffAverage"] = "Kick Avg",
            ["kickoffYards"] = "Kick Yards",
            ["kickoffTouchbacks"] = "Kick TB",
            ["kickoffOutOfBounds"] = "Kick OOB",
            ["onsideKickAttempts"] = "Onside Att",
            ["onsideKickRecoveries"] = "Onside Rec",

            // Misc / bookkeeping
            ["teamGamesPlayed"] = "Games",
            ["miscYards"] = "Misc Yds"
        },
        PercentKeys: new(StringComparer.OrdinalIgnoreCase)
        {
            "fieldGoalPct","extraPointPct"
        },
        PreferPerGameForCounts: new(StringComparer.OrdinalIgnoreCase)
        {
            "fieldGoalsMade","fieldGoalsAttempted",
            "extraPointsMade","extraPointsAttempted",
            "kickoffs","kickoffTouchbacks","kickoffOutOfBounds",
            "onsideKickAttempts","onsideKickRecoveries"
        },
        LowerIsBetterKeys: new(StringComparer.OrdinalIgnoreCase)
        {
            // Negative outcomes for kickers/kickoffs
            "extraPointsBlocked","kickoffOutOfBounds"
            // (Kickoff touchbacks are generally good → not included.)
        }
    );

    // ===== Miscellaneous ========================================================
    private static readonly CategoryConfig Miscellaneous = new(
        FriendlyMap: new(StringComparer.OrdinalIgnoreCase)
        {
            ["havocPlays"] = "Havoc Plays",
            ["havocRate"] = "Havoc %",
            ["explosivePlays"] = "Explosive Plays",
            ["explosivePlayRate"] = "Explosive %",
            ["pointsOffTurnovers"] = "Points Off TO",
            ["averageStartingFieldPos"] = "Avg Starting Field Pos",
            ["teamGamesPlayed"] = "Games",
            ["miscYards"] = "Misc Yds"
        },
        PercentKeys: new(StringComparer.OrdinalIgnoreCase)
        {
            "havocRate","explosivePlayRate"
        },
        PreferPerGameForCounts: new(StringComparer.OrdinalIgnoreCase)
        {
            "havocPlays","explosivePlays","pointsOffTurnovers"
        },
        LowerIsBetterKeys: new(StringComparer.OrdinalIgnoreCase)
        {
            // None by default; these are generally positive when higher.
        }
    );

    // ===== Passing ==============================================================
    private static readonly CategoryConfig Passing = new(
        FriendlyMap: new(StringComparer.OrdinalIgnoreCase)
        {
            ["ESPNQBRating"] = "Team QBR (ESPN)",
            ["QBRating"] = "Passer Rating",
            ["avgGain"] = "Avg Gain",
            ["yardsPerPassAttempt"] = "Yards/Attempt",
            ["netYardsPerPassAttempt"] = "Net Yards/Att",
            ["yardsPerCompletion"] = "Yards/Completion",
            ["completionPct"] = "Completion %",
            ["interceptionPct"] = "INT Rate",
            ["passingTouchdownPct"] = "Pass TD Rate",
            ["passingAttempts"] = "Attempts",
            ["completions"] = "Completions",
            ["passingTouchdowns"] = "Pass TD",
            ["interceptions"] = "INT",
            ["longPassing"] = "Long (yds)",
            ["passingYards"] = "Pass Yards",
            ["netPassingYards"] = "Net Pass Yards",
            ["passingYardsPerGame"] = "Pass Yds/G",
            ["netPassingYardsPerGame"] = "Net Pass Yds/G",
            ["passingYardsAfterCatch"] = "YAC (team)",
            ["passingYardsAtCatch"] = "Air Yds (team)",
            ["sacks"] = "Sacks Taken",
            ["sackYardsLost"] = "Sack Yds Lost",
            ["totalPoints"] = "Points",
            ["totalPointsPerGame"] = "Points/G",
            ["totalTouchdowns"] = "TD (all)",
            ["totalYards"] = "Total Yards",
            ["yardsPerGame"] = "Yards/G",
            ["passingFirstDowns"] = "Pass 1st Downs",
            ["twoPointPassConvs"] = "2-Pt Pass (Made)",
            ["twoPtPass"] = "2-Pt Pass (Made)",
            ["twoPtPassAttempts"] = "2-Pt Pass Att",
            ["miscYards"] = "Misc Yds",
            ["teamGamesPlayed"] = "Games"
        },
        PercentKeys: new(StringComparer.OrdinalIgnoreCase)
        {
            "completionPct","interceptionPct","passingTouchdownPct"
        },
        PreferPerGameForCounts: new(StringComparer.OrdinalIgnoreCase)
        {
            "completions","passingAttempts","interceptions","sacks",
            "passingTouchdowns","totalPoints","totalTouchdowns"
        },
        LowerIsBetterKeys: new(StringComparer.OrdinalIgnoreCase)
        {
            // Offensive negatives
            "interceptions","interceptionPct","sacks","sackYardsLost","drops","dropPct"
        }
    );

    // ===== Punting ==============================================================
    private static readonly CategoryConfig Punting = new(
        FriendlyMap: new(StringComparer.OrdinalIgnoreCase)
        {
            // Volume
            ["punts"] = "Punts",
            ["puntAttempts"] = "Punts",                 // alias

            // Distance / averages
            ["puntAverage"] = "Punt Avg",
            ["grossPuntAverage"] = "Punt Avg",          // alias
            ["netPuntAverage"] = "Net Punt Avg",
            ["avgPuntDistance"] = "Punt Avg",           // alias

            // Yardage
            ["puntYards"] = "Punt Yards",
            ["grossPuntYards"] = "Punt Yards",          // alias
            ["netPuntYards"] = "Net Punt Yards",

            // Long / hang time
            ["longPunt"] = "Long Punt (yds)",
            ["averageHangTime"] = "Avg Hang Time",

            // Placement
            ["puntsInside20"] = "Inside 20",
            ["puntsInsideTwenty"] = "Inside 20",        // alias
            ["puntsDowned"] = "Downed",
            ["fairCatches"] = "Fair Catches",
            ["puntsFairCaught"] = "Fair Catches",       // alias

            // Outcomes
            ["touchbacks"] = "Touchbacks",
            ["puntTouchbacks"] = "Touchbacks",          // alias
            ["blockedPunts"] = "Blocked Punts",

            // Misc
            ["teamGamesPlayed"] = "Games",
            ["miscYards"] = "Misc Yds"
        },
        PercentKeys: new(StringComparer.OrdinalIgnoreCase) /* none for punting */,
        PreferPerGameForCounts: new(StringComparer.OrdinalIgnoreCase)
        {
            "punts","puntAttempts","puntsInside20","puntsInsideTwenty","puntsDowned",
            "fairCatches","puntsFairCaught","touchbacks","puntTouchbacks","blockedPunts"
        },
        LowerIsBetterKeys: new(StringComparer.OrdinalIgnoreCase)
        {
            // Negative punting outcomes
            "touchbacks","puntTouchbacks","blockedPunts"
        }
    );

    // ===== Receiving ============================================================
    private static readonly CategoryConfig Receiving = new(
        FriendlyMap: new(StringComparer.OrdinalIgnoreCase)
        {
            // Volume
            ["targets"] = "Targets",
            ["receptions"] = "Receptions",

            // Yardage
            ["receivingYards"] = "Rec Yards",
            ["receivingYardsPerGame"] = "Rec Yds/G",
            ["yardsPerReception"] = "Yards/Reception",
            ["yardsPerCatch"] = "Yards/Reception",   // alias

            // YAC / Air
            ["receivingYardsAfterCatch"] = "YAC (team)",
            ["yardsAfterCatch"] = "YAC (team)",      // alias
            ["receivingYardsAtCatch"] = "Air Yds (team)",
            ["yardsAtCatch"] = "Air Yds (team)",     // alias

            // Explosives / long
            ["longReception"] = "Long (yds)",
            ["receivingBigPlays"] = "Explosive Receptions",

            // Scoring / conversions
            ["receivingTouchdowns"] = "Rec TD",
            ["receivingFirstDowns"] = "Rec 1st Downs",

            // Efficiency / hands
            ["catchPct"] = "Catch %",
            ["receptionPct"] = "Catch %",            // alias
            ["dropPct"] = "Drop %",
            ["drops"] = "Drops",

            // Misc
            ["miscYards"] = "Misc Yds",
            ["teamGamesPlayed"] = "Games"
        },
        PercentKeys: new(StringComparer.OrdinalIgnoreCase)
        {
            "catchPct","receptionPct","dropPct"
        },
        PreferPerGameForCounts: new(StringComparer.OrdinalIgnoreCase)
        {
            "receptions","targets","receivingTouchdowns"
        },
        LowerIsBetterKeys: new(StringComparer.OrdinalIgnoreCase)
        {
            "drops","dropPct"
        }
    );

    // ===== Returning ============================================================
    private static readonly CategoryConfig Returning = new(
        FriendlyMap: new(StringComparer.OrdinalIgnoreCase)
        {
            // Kick returns
            ["kickReturns"] = "Kick Returns",
            ["kickReturnYards"] = "Kick Return Yds",
            ["kickReturnAverage"] = "Kick Return Avg",
            ["longKickReturn"] = "Long KR (yds)",
            ["kickReturnTouchdowns"] = "Kick Return TD",

            // Punt returns
            ["puntReturns"] = "Punt Returns",
            ["puntReturnYards"] = "Punt Return Yds",
            ["puntReturnAverage"] = "Punt Return Avg",
            ["longPuntReturn"] = "Long PR (yds)",
            ["puntReturnTouchdowns"] = "Punt Return TD",

            // Muffs/fair catches
            ["puntReturnFairCatches"] = "Fair Catches",
            ["returnFumbles"] = "Return Fumbles",
            ["returnFumblesLost"] = "Return Fumbles Lost",

            // Bookkeeping
            ["teamGamesPlayed"] = "Games",
            ["miscYards"] = "Misc Yds"
        },
        PercentKeys: new(StringComparer.OrdinalIgnoreCase),
        PreferPerGameForCounts: new(StringComparer.OrdinalIgnoreCase)
        {
            "kickReturns","kickReturnTouchdowns","puntReturns","puntReturnTouchdowns",
            "puntReturnFairCatches","returnFumbles","returnFumblesLost"
        },
        LowerIsBetterKeys: new(StringComparer.OrdinalIgnoreCase)
        {
            "returnFumbles","returnFumblesLost"
        }
    );

    // ===== Rushing ==============================================================
    private static readonly CategoryConfig Rushing = new(
        FriendlyMap: new(StringComparer.OrdinalIgnoreCase)
        {
            ["ESPNRBRating"] = "Team RBR (ESPN)",
            ["avgGain"] = "Yards/Carry",
            ["yardsPerRushAttempt"] = "Yards/Carry",
            ["rushingAttempts"] = "Attempts",
            ["rushingYards"] = "Rush Yards",
            ["rushingYardsPerGame"] = "Rush Yds/G",
            ["rushingTouchdowns"] = "Rush TD",
            ["rushingBigPlays"] = "Explosive Runs",
            ["rushingFirstDowns"] = "Rush 1st Downs",
            ["longRushing"] = "Long (yds)",
            ["rushingFumbles"] = "Fumbles",
            ["rushingFumblesLost"] = "Fumbles Lost",
            ["stuffs"] = "Runs Stuffed",
            ["stuffYardsLost"] = "Stuff Yds Lost",
            ["miscYards"] = "Misc Yds",
            ["netTotalYards"] = "Net Total Yds",
            ["netYardsPerGame"] = "Net Yds/G",
            ["totalOffensivePlays"] = "Offensive Plays",
            ["totalPoints"] = "Points",
            ["totalPointsPerGame"] = "Points/G",
            ["totalTouchdowns"] = "TD (all)",
            ["totalYards"] = "Total Yards",
            ["totalYardsFromScrimmage"] = "Yards from Scrimmage",
            ["yardsPerGame"] = "Yards/G",
            ["yardsFromScrimmagePerGame"] = "Scrimmage Yds/G",
            ["twoPointRushConvs"] = "2-Pt Rush (Made)",
            ["twoPtRush"] = "2-Pt Rush (Made)",
            ["twoPtRushAttempts"] = "2-Pt Rush Att",
            ["teamGamesPlayed"] = "Games"
        },
        PercentKeys: new(StringComparer.OrdinalIgnoreCase),
        PreferPerGameForCounts: new(StringComparer.OrdinalIgnoreCase)
        {
            "rushingAttempts","rushingTouchdowns","totalPoints","totalTouchdowns"
        },
        LowerIsBetterKeys: new(StringComparer.OrdinalIgnoreCase)
        {
            "rushingFumbles","rushingFumblesLost","stuffs","stuffYardsLost"
        }
    );

    // ===== Scoring ==============================================================
    private static readonly CategoryConfig Scoring = new(
        FriendlyMap: new(StringComparer.OrdinalIgnoreCase)
        {
            ["totalPoints"] = "Points",
            ["totalPointsPerGame"] = "Points/G",
            ["totalTouchdowns"] = "Total TD",
            ["passingTouchdowns"] = "Pass TD",
            ["rushingTouchdowns"] = "Rush TD",
            ["receivingTouchdowns"] = "Rec TD",
            ["defensiveTouchdowns"] = "Def TD",
            ["specialTeamsTouchdowns"] = "ST TD",
            ["twoPointConvs"] = "2-Pt Conversions",
            ["safeties"] = "Safeties",
            ["teamGamesPlayed"] = "Games"
        },
        PercentKeys: new(StringComparer.OrdinalIgnoreCase),
        PreferPerGameForCounts: new(StringComparer.OrdinalIgnoreCase)
        {
            "totalTouchdowns","passingTouchdowns","rushingTouchdowns",
            "receivingTouchdowns","defensiveTouchdowns","specialTeamsTouchdowns","twoPointConvs"
        },
        LowerIsBetterKeys: new(StringComparer.OrdinalIgnoreCase)
        {
            // None: these are all generally positive when higher.
        }
    );

    // Registry
    private static readonly Dictionary<string, CategoryConfig> Categories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["passing"] = Passing,
        ["rushing"] = Rushing,
        ["receiving"] = Receiving,
        ["punting"] = Punting,
        ["kicking"] = Kicking,
        ["defensive"] = Defensive,
        ["returning"] = Returning,
        ["general"] = General,
        ["miscellaneous"] = Miscellaneous,
        ["scoring"] = Scoring
    };

    // ---- public API ---------------------------------------------------------
    public void ApplyFriendlyLabelsAndFormatting(FranchiseSeasonStatisticDto dto, bool sortByLabel = true)
    {
        foreach (var (category, list) in dto.Statistics.ToArray())
        {
            if (!Categories.TryGetValue(category, out var cfg)) continue;

            foreach (var e in list)
            {
                var rawKey = !string.IsNullOrWhiteSpace(e.StatisticKey)
                    ? e.StatisticKey
                    : (e.GetType().GetProperty("Statistic")?.GetValue(e) as string) ?? string.Empty;

                if (string.IsNullOrWhiteSpace(rawKey)) continue;

                e.StatisticKey = rawKey;
                e.StatisticValue = cfg.FriendlyMap.TryGetValue(rawKey, out var label) ? label : HumanizeKey(rawKey);

                // NEW: set polarity for TeamComparison logic
                e.IsNegativeAttribute = cfg.LowerIsBetterKeys.Contains(rawKey);

                e.DisplayValue = FormatValue(
                    displayValue: e.DisplayValue,
                    perGameDisplayValue: e.PerGameDisplayValue,
                    rawKey: rawKey,
                    percentKeys: cfg.PercentKeys,
                    preferPerGame: cfg.PreferPerGameForCounts);
            }

            if (sortByLabel)
            {
                dto.Statistics[category] = list
                    .OrderBy(x => x.StatisticValue ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.StatisticKey, StringComparer.OrdinalIgnoreCase) // stable tiebreaker
                    .ToList();
            }
        }
    }

    // ---- helpers ------------------------------------------------------------
    private static string FormatValue(
        string? displayValue,
        string? perGameDisplayValue,
        string rawKey,
        ISet<string> percentKeys,
        ISet<string> preferPerGame)
    {
        var chosen = (preferPerGame.Contains(rawKey) && !string.IsNullOrWhiteSpace(perGameDisplayValue))
            ? perGameDisplayValue
            : displayValue;

        if (string.IsNullOrWhiteSpace(chosen)) return "—";

        // If percent key, handle as percent (supports both 0–1 and 0–100 inputs)
        bool isPercent = percentKeys.Contains(rawKey);

        if (decimal.TryParse(chosen.Trim(), NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out var d))
        {
            if (isPercent)
            {
                var pct = d <= 1m ? d * 100m : d;
                return pct.ToString("0.#", CultureInfo.InvariantCulture) + "%"; // up to 1 decimal
            }

            var isIntLike = decimal.Remainder(d, 1m) == 0m;
            return isIntLike
                ? d.ToString("0", CultureInfo.InvariantCulture)     // no thousands separators
                : d.ToString("0.#", CultureInfo.InvariantCulture);  // up to 1 decimal
        }

        // Fallback: treat as text; if percent, just append %
        return isPercent ? chosen.Trim() + "%" : chosen.Trim();
    }

    private static string AppendPercent(string s)
    {
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return d.ToString("0.0", CultureInfo.InvariantCulture) + "%";
        return s.EndsWith("%", StringComparison.Ordinal) ? s : s + "%";
    }

    private static string HumanizeKey(string key)
    {
        var words = System.Text.RegularExpressions.Regex.Replace(key, "(?<=[a-z])([A-Z])", " $1");
        words = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(words);
        words = words.Replace("Pct", " %")
                     .Replace("Yds", " Yds")
                     .Replace("Td", " TD")
                     .Replace("Touchdowns", " TDs")
                     .Replace("Touchdown", " TD")
                     .Replace("Int", " INT");
        return words;
    }
}
