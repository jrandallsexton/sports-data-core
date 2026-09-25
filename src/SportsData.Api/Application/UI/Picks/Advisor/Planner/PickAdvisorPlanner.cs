namespace SportsData.Api.Application.UI.Picks.Advisor.Planner;

/// <summary>
/// Risk level the user chooses (or StatBot recommends). Numeric values are
/// the contract; the names are working names until the count is settled and
/// the football naming pass happens. See docs/features/statbot-advisor.md.
/// </summary>
public enum AdvisorLevel
{
    /// <summary>Model's side everywhere; points ranked by certainty. StatBot's own sheet.</summary>
    Prevent = 1,

    /// <summary>Flip the single closest coin flip; it gets a mid value.</summary>
    GoalLine = 2,

    /// <summary>Flip a few coin flips; they get upper-middle values.</summary>
    QbDraw = 3,

    /// <summary>Flip every coin flip; the flips get the top points.</summary>
    HailMary = 4
}

public enum AdvisedPickKind
{
    /// <summary>Model's side, above the coin-flip threshold, preview does not disagree.</summary>
    Lock,

    /// <summary>Model's side on a coin flip the level chose not to flip.</summary>
    Lean,

    /// <summary>The other side of a coin flip, taken for variance.</summary>
    Flip,

    /// <summary>Kicked off (or about to). Untouched; carries the user's existing pick.</summary>
    Locked,

    /// <summary>No model number for this game. Left blank, never guessed.</summary>
    NoPrediction
}

/// <summary>
/// Pure, storage-agnostic allocation core for the StatBot advisor. Given the
/// week's matchups with the model's number per game, the preview's side, and
/// the CALLER's existing picks, it produces a full sheet (side + confidence
/// value) for one risk level.
/// <para>
/// Rule zero, by construction: the input carries no other member's pick.
/// There is no parameter through which one could arrive.
/// </para>
/// </summary>
public interface IPickAdvisorPlanner
{
    PickAdvisorSheet BuildSheet(PickAdvisorPlanInput input);

    AdvisorLevel RecommendLevel(PickAdvisorStandings standings);
}

/// <summary>The model's call on one game: <paramref name="Probability"/> that <paramref name="FranchiseSeasonId"/> wins / covers.</summary>
public sealed record PickAdvisorSignal(Guid FranchiseSeasonId, double Probability);

/// <summary>The caller's own current pick on a game, if any.</summary>
public sealed record PickAdvisorExistingPick(Guid? FranchiseSeasonId, int? ConfidencePoints);

public sealed record PickAdvisorMatchup(
    Guid ContestId,
    string? Headline,
    DateTime StartDateUtc,
    bool IsLocked,
    Guid HomeFranchiseSeasonId,
    Guid AwayFranchiseSeasonId,
    PickAdvisorSignal? Model,
    Guid? PreviewFranchiseSeasonId,
    PickAdvisorExistingPick? Existing);

public sealed record PickAdvisorPlanInput(
    AdvisorLevel Level,
    bool UseConfidencePoints,
    IReadOnlyList<PickAdvisorMatchup> Matchups);

/// <summary>
/// Standings facts the recommendation reads. Performance numbers only —
/// never picks. Only THIS week's slate is a known quantity (future slates
/// aren't scheduled and can't be forecast for a ranked-teams league), so
/// the horizon is weeks, not games: the deficit is spread over the league
/// weeks left and compared with how much a week like this one typically
/// swings (per-game spread × this week's games).
/// <paramref name="PointsPerGameStdDev"/> is null when the league has too
/// few scored member-weeks to compute one. <paramref name="WeeksRemaining"/>
/// is null when the season calendar could not be read — distinct from
/// "last week", which is 1.
/// </summary>
public sealed record PickAdvisorStandings(
    int DeficitToLeader,
    int? WeeksRemaining,
    int GamesThisWeek,
    double? PointsPerGameStdDev,
    decimal LeaderPointsPerGame);

public sealed record AdvisedPick(
    Guid ContestId,
    string? Headline,
    AdvisedPickKind Kind,
    Guid? FranchiseSeasonId,
    int? ConfidencePoints,
    /// <summary>Model probability for the ADVISED side (so a flip reads below 0.5). Null without a model number.</summary>
    double? ModelProbability,
    /// <summary>Whether the preview named the model's side. Null when there is no preview or no model number.</summary>
    bool? PreviewAgrees,
    bool IsCoinFlip,
    /// <summary>True when applying this pick would change what the user has (no pick, other side, or other points).</summary>
    bool DiffersFromExisting);

public sealed class PickAdvisorSheet
{
    public required AdvisorLevel Level { get; init; }
    public List<AdvisedPick> Picks { get; } = [];
    public int FlipCount { get; set; }
    public int CoinFlipCount { get; set; }
    public int LockedCount { get; set; }
    public int NoPredictionCount { get; set; }
}

public class PickAdvisorPlanner : IPickAdvisorPlanner
{
    private readonly PickAdvisorOptions _options;

    public PickAdvisorPlanner(PickAdvisorOptions options)
    {
        _options = options;
    }

    public AdvisorLevel RecommendLevel(PickAdvisorStandings standings)
    {
        if (standings.DeficitToLeader <= 0)
            return AdvisorLevel.Prevent;

        // Unit: how much a week like this one typically swings — the league's
        // per-game spread scaled by this week's slate. Fall back to a fraction
        // of the leader's per-game rate when the league is too young for a
        // spread; floor it so the ratio stays finite.
        var perGame = standings.PointsPerGameStdDev is > 0
            ? standings.PointsPerGameStdDev.Value
            : Math.Max(_options.MinUnit, (double)standings.LeaderPointsPerGame * _options.FallbackUnitFraction);
        var unit = perGame * Math.Max(1, standings.GamesThisWeek);

        // Unknown horizon: stay neutral. Judge the deficit against ONE week's
        // swing and never go past the middle — treating "unknown" as "last
        // week" would max out the risk whenever the calendar was unreachable.
        if (standings.WeeksRemaining is null)
        {
            return standings.DeficitToLeader / unit < _options.GoalLineMaxUnits
                ? AdvisorLevel.GoalLine
                : AdvisorLevel.QbDraw;
        }

        var weeks = Math.Max(1, standings.WeeksRemaining.Value);
        var ratio = (double)standings.DeficitToLeader / weeks / unit;

        if (ratio < _options.GoalLineMaxUnits) return AdvisorLevel.GoalLine;
        if (ratio < _options.QbDrawMaxUnits) return AdvisorLevel.QbDraw;
        return AdvisorLevel.HailMary;
    }

    public PickAdvisorSheet BuildSheet(PickAdvisorPlanInput input)
    {
        var sheet = new PickAdvisorSheet { Level = input.Level };

        var locked = new List<AdvisedPick>();
        var blank = new List<AdvisedPick>();
        var candidates = new List<Candidate>();

        foreach (var m in input.Matchups)
        {
            if (m.IsLocked)
            {
                locked.Add(new AdvisedPick(
                    m.ContestId, m.Headline, AdvisedPickKind.Locked,
                    m.Existing?.FranchiseSeasonId,
                    input.UseConfidencePoints ? m.Existing?.ConfidencePoints : null,
                    ModelProbability: null, PreviewAgrees: null, IsCoinFlip: false,
                    DiffersFromExisting: false));
                continue;
            }

            var signal = Normalize(m);
            if (signal is null)
            {
                // Carries the user's existing pick (if any) so the sheet shows
                // it rather than a blank the user might think was cleared.
                blank.Add(new AdvisedPick(
                    m.ContestId, m.Headline, AdvisedPickKind.NoPrediction,
                    m.Existing?.FranchiseSeasonId,
                    input.UseConfidencePoints ? m.Existing?.ConfidencePoints : null,
                    ModelProbability: null, PreviewAgrees: null, IsCoinFlip: false,
                    DiffersFromExisting: m.Existing?.FranchiseSeasonId is null));
                continue;
            }

            var (favored, other, pFavored) = signal.Value;
            bool? previewAgrees = m.PreviewFranchiseSeasonId is Guid pv ? pv == favored : null;
            var certainty = pFavored - 0.5;
            // Disagreement between the model and the preview IS uncertainty:
            // the game becomes a coin flip and sorts as the least certain.
            var effectiveCertainty = previewAgrees == false ? 0.0 : certainty;
            var isCoinFlip = pFavored < _options.CoinFlipThreshold || previewAgrees == false;

            candidates.Add(new Candidate(m, favored, other, pFavored, certainty, effectiveCertainty, previewAgrees, isCoinFlip));
        }

        sheet.LockedCount = locked.Count;
        sheet.NoPredictionCount = blank.Count;
        sheet.CoinFlipCount = candidates.Count(c => c.IsCoinFlip);

        // Least certain first: that is where flipping is cheapest.
        var flipOrder = candidates
            .Where(c => c.IsCoinFlip)
            .OrderBy(c => c.EffectiveCertainty)
            .ThenBy(c => c.Certainty)
            .ThenBy(c => c.Matchup.StartDateUtc)
            .ThenBy(c => c.Matchup.ContestId)
            .ToList();

        var flipCount = input.Level switch
        {
            AdvisorLevel.Prevent => 0,
            AdvisorLevel.GoalLine => Math.Min(1, flipOrder.Count),
            AdvisorLevel.QbDraw => Math.Min(Math.Max(0, _options.QbDrawFlipCount), flipOrder.Count),
            AdvisorLevel.HailMary => flipOrder.Count,
            _ => 0
        };

        var flips = flipOrder.Take(flipCount).ToList();
        var flipped = flips.Select(c => c.Matchup.ContestId).ToHashSet();
        sheet.FlipCount = flips.Count;

        // Base order for points: most certain first, flips excluded.
        var baseOrder = candidates
            .Where(c => !flipped.Contains(c.Matchup.ContestId))
            .OrderByDescending(c => c.EffectiveCertainty)
            .ThenByDescending(c => c.Certainty)
            .ThenBy(c => c.Matchup.StartDateUtc)
            .ThenBy(c => c.Matchup.ContestId)
            .ToList();

        var pointOrder = OrderForPoints(input.Level, baseOrder, flips);

        // Points run 1..N over the WHOLE week (pick sheet contract). Values
        // the sheet will not rewrite are reserved: those held by locked
        // picks, and any existing pick on a game the advisor leaves blank —
        // the client never applies a blank row, so its value stays put and
        // must not be handed out twice (CodeRabbit, PR #791). The rest go
        // top-down so blanks the user fills by hand get the lowest values.
        var blankIds = blank.Select(b => b.ContestId).ToHashSet();
        var reserved = locked
            .Where(p => p.ConfidencePoints.HasValue)
            .Select(p => p.ConfidencePoints!.Value)
            .Concat(input.Matchups
                .Where(m => blankIds.Contains(m.ContestId) && m.Existing?.ConfidencePoints is not null)
                .Select(m => m.Existing!.ConfidencePoints!.Value))
            .ToHashSet();
        var available = Enumerable.Range(1, input.Matchups.Count)
            .Where(v => !reserved.Contains(v))
            .OrderByDescending(v => v)
            .ToList();

        var advised = new List<AdvisedPick>(pointOrder.Count);
        for (var i = 0; i < pointOrder.Count; i++)
        {
            var c = pointOrder[i];
            var isFlip = flipped.Contains(c.Matchup.ContestId);
            var side = isFlip ? c.Other : c.Favored;
            var probability = isFlip ? 1.0 - c.PFavored : c.PFavored;
            int? points = input.UseConfidencePoints && i < available.Count ? available[i] : null;
            var kind = isFlip ? AdvisedPickKind.Flip
                : c.IsCoinFlip ? AdvisedPickKind.Lean
                : AdvisedPickKind.Lock;

            var existing = c.Matchup.Existing;
            var differs = existing is null
                          || existing.FranchiseSeasonId != side
                          || (input.UseConfidencePoints && existing.ConfidencePoints != points);

            advised.Add(new AdvisedPick(
                c.Matchup.ContestId, c.Matchup.Headline, kind, side, points,
                Math.Round(probability, 4), c.PreviewAgrees, c.IsCoinFlip, differs));
        }

        // Present in slate order so the sheet reads like the picks page.
        var byContest = advised.Concat(locked).Concat(blank).ToDictionary(p => p.ContestId);
        foreach (var m in input.Matchups)
        {
            if (byContest.TryGetValue(m.ContestId, out var p))
                sheet.Picks.Add(p);
        }

        return sheet;
    }

    /// <summary>
    /// Where the flips sit in the points order, per level. Goal-line parks
    /// its one flip mid-sheet; QB Draw starts its flips a quarter of the way
    /// down; Hail Mary puts them on top — variance lives in c²·p(1−p), so the
    /// high-point coin flips are what swing a week.
    /// </summary>
    private static List<Candidate> OrderForPoints(AdvisorLevel level, List<Candidate> baseOrder, List<Candidate> flips)
    {
        if (flips.Count == 0) return baseOrder;

        var ordered = new List<Candidate>(baseOrder);
        var total = baseOrder.Count + flips.Count;

        var insertAt = level switch
        {
            AdvisorLevel.GoalLine => total / 2,
            AdvisorLevel.QbDraw => total / 4,
            AdvisorLevel.HailMary => 0,
            _ => ordered.Count
        };

        insertAt = Math.Clamp(insertAt, 0, ordered.Count);
        ordered.InsertRange(insertAt, flips);
        return ordered;
    }

    /// <summary>
    /// Expresses the model's number as (favored side, other side, p ≥ 0.5).
    /// A signal naming neither team is treated as no prediction.
    /// </summary>
    private static (Guid Favored, Guid Other, double PFavored)? Normalize(PickAdvisorMatchup m)
    {
        if (m.Model is null) return null;

        Guid other;
        if (m.Model.FranchiseSeasonId == m.HomeFranchiseSeasonId) other = m.AwayFranchiseSeasonId;
        else if (m.Model.FranchiseSeasonId == m.AwayFranchiseSeasonId) other = m.HomeFranchiseSeasonId;
        else return null;

        var p = Math.Clamp(m.Model.Probability, 0.0, 1.0);
        return p >= 0.5
            ? (m.Model.FranchiseSeasonId, other, p)
            : (other, m.Model.FranchiseSeasonId, 1.0 - p);
    }

    private sealed record Candidate(
        PickAdvisorMatchup Matchup,
        Guid Favored,
        Guid Other,
        double PFavored,
        double Certainty,
        double EffectiveCertainty,
        bool? PreviewAgrees,
        bool IsCoinFlip);
}
