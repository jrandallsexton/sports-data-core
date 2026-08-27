using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.UI.PlayerLineups.Scoring;

/// <summary>
/// Applies a scoring matrix to a flattened statline. The engine owns the
/// STRUCTURAL pieces — derived keys (missed kicks, FG distance buckets)
/// and the per-unit math — while every point VALUE comes from the
/// dynamically-loaded rule set. See docs/features/player-pickem/scoring.md.
/// </summary>
public static class PlayerPickemScoringEngine
{
    /// <summary>One rule's contribution — powers the per-slot breakdown display.</summary>
    public readonly record struct StatContribution(string StatKey, decimal StatValue, decimal Points);

    public readonly record struct SlotScore(decimal Points, IReadOnlyList<StatContribution> Contributions);

    public static SlotScore Score(
        IReadOnlyCollection<PlayerScoringRule> rules,
        IReadOnlyDictionary<string, decimal> stats)
    {
        var enriched = WithDerived(stats);
        var contributions = new List<StatContribution>();
        decimal total = 0m;

        foreach (var rule in rules)
        {
            if (!enriched.TryGetValue(rule.StatKey, out var value) || value == 0m)
                continue;
            if (rule.PerUnits == 0m)
                continue; // malformed rule; never divide by zero

            var points = Math.Round(value * rule.Points / rule.PerUnits, 2, MidpointRounding.AwayFromZero);
            if (points == 0m)
                continue;

            total += points;
            contributions.Add(new StatContribution(rule.StatKey, value, points));
        }

        return new SlotScore(Math.Round(total, 2, MidpointRounding.AwayFromZero), contributions);
    }

    /// <summary>
    /// Derived keys are structural facts (a missed kick IS attempts minus
    /// made; the chart's 17-39 tier IS the union of ESPN's 1-19/20-29/
    /// 30-39 buckets), so they live in code — the matrix only prices them.
    /// </summary>
    private static Dictionary<string, decimal> WithDerived(IReadOnlyDictionary<string, decimal> stats)
    {
        var result = new Dictionary<string, decimal>(stats, StringComparer.Ordinal);
        decimal Get(string key) => stats.TryGetValue(key, out var v) ? v : 0m;

        var made1739 = Get("kicking.fieldGoalsMade1_19") + Get("kicking.fieldGoalsMade20_29") + Get("kicking.fieldGoalsMade30_39");
        var att1739 = Get("kicking.fieldGoalAttempts1_19") + Get("kicking.fieldGoalAttempts20_29") + Get("kicking.fieldGoalAttempts30_39");
        var made4049 = Get("kicking.fieldGoalsMade40_49");
        var att4049 = Get("kicking.fieldGoalAttempts40_49");

        result["derived.missedExtraPoints"] = Get("kicking.extraPointAttempts") - Get("kicking.extraPointsMade");
        result["derived.fieldGoalsMade17_39"] = made1739;
        result["derived.fieldGoalsMissed17_39"] = att1739 - made1739;
        result["derived.fieldGoalsMade40_49"] = made4049;
        result["derived.fieldGoalsMissed40_49"] = att4049 - made4049;
        result["derived.fieldGoalsMade50_59"] = Get("kicking.fieldGoalsMade50_59");
        result["derived.fieldGoalsMade60Plus"] = Get("kicking.fieldGoalsMade60_99");

        return result;
    }

    /// <summary>
    /// Compact display line for the roster UI ("187 PaYd · 2 PaTD"),
    /// built from the SCORED contributions so it always matches the
    /// points shown. Unknown keys fall back to the raw stat name.
    /// </summary>
    public static string BuildStatLine(IReadOnlyList<StatContribution> contributions)
    {
        if (contributions.Count == 0) return string.Empty;
        return string.Join(" · ", contributions.Select(c =>
        {
            var label = StatLabels.TryGetValue(c.StatKey, out var l)
                ? l
                : c.StatKey[(c.StatKey.IndexOf('.') + 1)..];
            var value = c.StatValue == Math.Truncate(c.StatValue)
                ? ((int)c.StatValue).ToString()
                : c.StatValue.ToString("0.##");
            return $"{value} {label}";
        }));
    }

    private static readonly Dictionary<string, string> StatLabels = new(StringComparer.Ordinal)
    {
        ["passing.passingYards"] = "PaYd",
        ["passing.passingTouchdowns"] = "PaTD",
        ["passing.interceptions"] = "INT",
        ["passing.twoPtPass"] = "2PT",
        ["rushing.rushingYards"] = "RuYd",
        ["rushing.rushingTouchdowns"] = "RuTD",
        ["rushing.twoPtRush"] = "2PT",
        ["receiving.receivingYards"] = "ReYd",
        ["receiving.receivingTouchdowns"] = "ReTD",
        ["receiving.receptions"] = "Rec",
        ["receiving.twoPtReception"] = "2PT",
        ["fumbles.fumblesLost"] = "FumL",
        ["kicking.extraPointsMade"] = "XP",
        ["derived.missedExtraPoints"] = "XP Miss",
        ["derived.fieldGoalsMade17_39"] = "FG17-39",
        ["derived.fieldGoalsMissed17_39"] = "FG Miss",
        ["derived.fieldGoalsMade40_49"] = "FG40-49",
        ["derived.fieldGoalsMissed40_49"] = "FG Miss",
        ["derived.fieldGoalsMade50_59"] = "FG50-59",
        ["derived.fieldGoalsMade60Plus"] = "FG60+",
    };
}
