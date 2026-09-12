using Microsoft.Extensions.Configuration;

using System;
using System.Globalization;
using System.Linq;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetContestPreviewHistory;

/// <summary>
/// Operator-tunable policy for the spread-context ATS bucket bands (owner
/// ask 2026-09-12: the bands are a judgment call, so they belong in
/// configuration, not code). Read once at startup from Azure App
/// Configuration under <c>SportsData.Producer:SpreadContext:*</c>; a
/// missing or malformed value falls back to the code default — never to a
/// broken ladder.
/// </summary>
/// <remarks>
/// Lives in this slice rather than a shared config namespace because the
/// policy is specific to this query (same reasoning as
/// <see cref="ContestPreviewHistoryCache"/>).
/// </remarks>
public class SpreadContextConfig
{
    /// <summary>
    /// The key-number rung ladder. ATS facts bucket on the BAND the live
    /// line sits in — [largest rung ≤ magnitude, next rung up) — rather
    /// than the exact line or an open-ended "10+": "as a 10–14 point
    /// favorite" reads naturally and accrues a meaningful sample, where
    /// "as a 12.5-point favorite" would almost always be n=0 and an
    /// unbounded "10+" pulls -49.5 FCS blowouts into a -12.5 question
    /// (owner calls 2026-09-02 and 2026-09-12: closeness beats cohort
    /// mass; a thin cohort self-discloses because the count is in the
    /// sentence, and n=0 renders "no games with a line in that range").
    /// Above the top rung the bucket stays open-ended ("49+").
    /// </summary>
    public double[] AtsKeyNumbers { get; init; } = DefaultAtsKeyNumbers;

    /// <summary>
    /// Safety net, rarely reached now that the ladder tops out at 49: the
    /// ATS pair renders only when the chosen lower rung sits within one
    /// touchdown of the line, so a stretched cohort can never masquerade
    /// as line-specific evidence. With rungs every 7 points from 35 up,
    /// every realistic football spread lands within the guard; this fires
    /// only for absurd (56+) lines.
    /// </summary>
    public double AtsBucketMaxDistancePoints { get; init; } = DefaultAtsBucketMaxDistancePoints;

    public static readonly double[] DefaultAtsKeyNumbers = [3, 7, 10, 14, 21, 28, 35, 42, 49];

    public const double DefaultAtsBucketMaxDistancePoints = 7;

    /// <summary>
    /// Builds the config from <c>SportsData.Producer:SpreadContext:*</c>.
    /// The ladder is ONE comma-separated string ("3,7,10,14,21,28,35,42,49")
    /// rather than an indexed array section: .NET array binding MERGES code
    /// defaults with partially-overridden indexes, while a single string
    /// replaces atomically — an operator can never end up with a spliced
    /// ladder. Parsed values are deduplicated and sorted; any non-positive
    /// or unparsable entry rejects the whole string in favor of the default.
    /// </summary>
    public static SpreadContextConfig FromConfiguration(IConfiguration config)
    {
        double[]? ladder = null;
        var ladderValue = config["SportsData.Producer:SpreadContext:AtsKeyNumbers"];
        if (!string.IsNullOrWhiteSpace(ladderValue))
        {
            var parsed = ladderValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d > 0
                    ? d
                    : (double?)null)
                .ToList();

            if (parsed.Count > 0 && parsed.All(x => x is not null))
                ladder = parsed.Select(x => x!.Value).Distinct().OrderBy(x => x).ToArray();
        }

        var guardValue = config["SportsData.Producer:SpreadContext:AtsBucketMaxDistancePoints"];
        var guard = double.TryParse(guardValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var g) && g > 0
            ? g
            : DefaultAtsBucketMaxDistancePoints;

        return new SpreadContextConfig
        {
            AtsKeyNumbers = ladder ?? DefaultAtsKeyNumbers,
            AtsBucketMaxDistancePoints = guard
        };
    }
}
