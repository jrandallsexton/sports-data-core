using SportsData.Core.Common;

namespace SportsData.Producer.Application.Competitions.InstantAnalysis;

/// <summary>
/// An athlete's season document (<c>AthleteSeasonStatistic</c>, split 0), reduced
/// to the stats instant analysis reads. <see cref="Values"/> is keyed
/// "category.stat", e.g. "defensiveInterceptions.interceptions".
/// </summary>
public record SeasonStatLine(
    Guid AthleteSeasonId,
    Guid StatisticId,
    DateTime CreatedUtc,
    IReadOnlyDictionary<string, decimal> Values);

/// <summary>
/// Facts about the season as of the current play (instant-analysis task 06):
/// season count = the athlete's season document + this game's plays up to and
/// including this one. Pure: the caller loads the documents.
///
/// The season document is rewritten in place (one row per ESPN ref, no
/// history), so "which document" is a rule on its CreatedUtc:
/// live uses the newest; a lab replay passes the game's kickoff as the cutoff
/// and uses only a document written before it, since any later one already
/// includes the replayed game. With no qualifying document the fact is dropped.
/// </summary>
public static class SeasonFacts
{
    // Actor's season count per kind. Fumble recoveries include own-team
    // recoveries in ESPN's stat; the in-game count is opponent recoveries only.
    private static readonly IReadOnlyDictionary<PlayKind, string> ActorStat = new Dictionary<PlayKind, string>
    {
        [PlayKind.Interception] = "defensiveInterceptions.interceptions",
        [PlayKind.Sack] = "defensive.sacks",
        [PlayKind.FumbleLost] = "general.fumblesRecovered",
        [PlayKind.PassingTouchdown] = "passing.passingTouchdowns",
        [PlayKind.ReceivingTouchdown] = "receiving.receivingTouchdowns",
        [PlayKind.RushingTouchdown] = "rushing.rushingTouchdowns"
    };

    // Victim's season count per kind (interceptions thrown, times sacked, fumbles lost).
    private static readonly IReadOnlyDictionary<PlayKind, string> VictimStat = new Dictionary<PlayKind, string>
    {
        [PlayKind.Interception] = "passing.interceptions",
        [PlayKind.Sack] = "passing.sacks",
        [PlayKind.FumbleLost] = "general.fumblesLost"
    };

    // Season longest per role on an explosive play.
    private static readonly IReadOnlyDictionary<string, string> LongestStatByRole = new Dictionary<string, string>
    {
        ["rusher"] = "rushing.longRushing",
        ["passer"] = "passing.longPassing",
        ["receiver"] = "receiving.longReception"
    };

    private static readonly string[] RunTypeIds = ["5", "68"];
    private static readonly string[] PassTypeIds = ["24", "67"];

    /// <summary>Every "category.stat" key read, for the loader to filter on.</summary>
    public static IReadOnlyCollection<string> StatKeys { get; } =
        ActorStat.Values.Concat(VictimStat.Values).Concat(LongestStatByRole.Values).Distinct().ToList();

    /// <summary>
    /// Picks each athlete's document: the newest, or with <paramref name="cutoffUtc"/>
    /// the newest written strictly before it.
    /// </summary>
    public static IReadOnlyDictionary<Guid, SeasonStatLine> NewestPerAthlete(
        IEnumerable<SeasonStatLine> lines,
        DateTime? cutoffUtc) =>
        lines
            .Where(l => cutoffUtc is null || l.CreatedUtc < cutoffUtc)
            .GroupBy(l => l.AthleteSeasonId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.CreatedUtc).First());

    public static FactResult Compute(
        AnalysedPlay play,
        IReadOnlyList<PlayEvent> events,
        IReadOnlyList<GamePlay> gamePlays,
        IReadOnlyDictionary<Guid, SeasonStatLine> seasonStats,
        DateTime kickoffUtc,
        DateTime? documentCutoffUtc)
    {
        var facts = new List<PlayFact>();
        var dropped = new List<DroppedFact>();

        if (!long.TryParse(play.SequenceNumber, out var sequence))
            return new FactResult(facts, dropped);

        var context = new Context(play, sequence, gamePlays, seasonStats, kickoffUtc, documentCutoffUtc, facts, dropped);

        foreach (var evt in events)
        {
            if (ActorStat.TryGetValue(evt.Kind, out var actorStat))
                AddActorCounts(evt, actorStat, context);

            if (VictimStat.TryGetValue(evt.Kind, out var victimStat))
                AddVictimCount(evt, victimStat, context);

            if (evt.Kind is PlayKind.ExplosiveRun or PlayKind.ExplosivePass)
                AddSeasonLongest(evt, context);
        }

        return new FactResult(facts, dropped);
    }

    private sealed class Context
    {
        public Context(
            AnalysedPlay play,
            long sequence,
            IReadOnlyList<GamePlay> gamePlays,
            IReadOnlyDictionary<Guid, SeasonStatLine> seasonStats,
            DateTime kickoffUtc,
            DateTime? documentCutoffUtc,
            List<PlayFact> facts,
            List<DroppedFact> dropped)
        {
            Play = play;
            Sequence = sequence;
            SeasonStats = seasonStats;
            KickoffUtc = kickoffUtc;
            DocumentCutoffUtc = documentCutoffUtc;
            Facts = facts;
            Dropped = dropped;

            // As of this play: never later plays (a lab replay has them all).
            UpToNow = gamePlays.Where(p => p.Sequence <= sequence).OrderBy(p => p.Sequence).ToList();
            Classified = UpToNow
                .Select(p => (Play: p, Events: PlayClassifier.Classify(ToAnalysed(p, play))))
                .ToList();
            Offense = UpToNow.FirstOrDefault(p => p.PlayId == play.PlayId)?.OffenseFranchiseSeasonId
                      ?? play.StartFranchiseSeasonId;
        }

        public AnalysedPlay Play { get; }
        public long Sequence { get; }
        public IReadOnlyDictionary<Guid, SeasonStatLine> SeasonStats { get; }
        public DateTime KickoffUtc { get; }
        public DateTime? DocumentCutoffUtc { get; }
        public List<PlayFact> Facts { get; }
        public List<DroppedFact> Dropped { get; }
        public List<GamePlay> UpToNow { get; }
        public List<(GamePlay Play, IReadOnlyList<PlayEvent> Events)> Classified { get; }
        public Guid? Offense { get; }
    }

    private static void AddActorCounts(PlayEvent evt, string stat, Context ctx)
    {
        foreach (var actor in evt.Actors)
        {
            // Same guard as the game count: an earlier type 63 interception
            // against this offense hides who made it, so the in-game part is unknowable.
            if (evt.Kind == PlayKind.Interception &&
                ctx.UpToNow.Any(p => p.Sequence < ctx.Sequence && p.TypeId == "63" && p.OffenseFranchiseSeasonId == ctx.Offense))
            {
                ctx.Dropped.Add(new DroppedFact(FactTypes.SeasonCount, evt.Kind, actor,
                    "Defense has an earlier unattributed interception (type 63) in this game."));
                continue;
            }

            if (!TryGetSeasonValue(actor, stat, FactTypes.SeasonCount, evt.Kind, ctx, out var line, out var seasonValue))
                continue;

            // ESPN credits a split sack as a half to each sacker; match it.
            var gameValue = ctx.Classified
                .SelectMany(c => c.Events)
                .Where(e => e.Kind == evt.Kind && e.Actors.Contains(actor))
                .Sum(e => evt.Kind == PlayKind.Sack ? 1m / e.Actors.Count : 1m);

            ctx.Facts.Add(new PlayFact(FactTypes.SeasonCount, evt.Kind, actor, null, seasonValue + gameValue,
                Provenance(ctx, line, stat, seasonValue, gameValue)) { SeasonDocumentCreatedUtc = line.CreatedUtc });
        }
    }

    private static void AddVictimCount(PlayEvent evt, string stat, Context ctx)
    {
        if (evt.Victim is not { } victim)
            return;

        if (!TryGetSeasonValue(victim, stat, FactTypes.SeasonCountAgainst, evt.Kind, ctx, out var line, out var seasonValue))
            return;

        decimal gameValue = ctx.Classified
            .Count(c => c.Events.Any(e => e.Kind == evt.Kind && e.Victim == victim));

        ctx.Facts.Add(new PlayFact(FactTypes.SeasonCountAgainst, evt.Kind, victim, null, seasonValue + gameValue,
            Provenance(ctx, line, stat, seasonValue, gameValue)) { SeasonDocumentCreatedUtc = line.CreatedUtc });
    }

    private static void AddSeasonLongest(PlayEvent evt, Context ctx)
    {
        var typeIds = evt.Kind == PlayKind.ExplosiveRun ? RunTypeIds : PassTypeIds;

        foreach (var actor in evt.Actors)
        {
            var role = ctx.Play.Participants
                .FirstOrDefault(p => p.AthleteSeasonId == actor && LongestStatByRole.ContainsKey(p.Type))?.Type;

            if (role is null)
                continue;

            var stat = LongestStatByRole[role];

            if (!TryGetSeasonValue(actor, stat, FactTypes.SeasonLongest, evt.Kind, ctx, out var line, out var seasonLongest))
                continue;

            var gameLongest = ctx.UpToNow
                .Where(p => p.Sequence < ctx.Sequence && typeIds.Contains(p.TypeId) &&
                            p.Participants.Any(x => x.AthleteSeasonId == actor && x.Type == role))
                .Select(p => (int?)p.StatYardage)
                .Max();

            var previous = Math.Max(seasonLongest, gameLongest ?? 0);

            // Strictly longer: a tie isn't "his longest".
            if (ctx.Play.StatYardage <= previous)
                continue;

            var provenance = new Dictionary<string, object?>(Provenance(ctx, line, stat, seasonLongest, null))
            {
                ["previousGameLongest"] = gameLongest
            };
            ctx.Facts.Add(new PlayFact(FactTypes.SeasonLongest, evt.Kind, actor, null, ctx.Play.StatYardage, provenance)
            {
                SeasonDocumentCreatedUtc = line.CreatedUtc
            });
        }
    }

    private static bool TryGetSeasonValue(
        Guid athlete,
        string stat,
        string factType,
        PlayKind kind,
        Context ctx,
        out SeasonStatLine line,
        out decimal value)
    {
        value = 0;

        if (!ctx.SeasonStats.TryGetValue(athlete, out line!))
        {
            ctx.Dropped.Add(new DroppedFact(factType, kind, athlete, ctx.DocumentCutoffUtc is null
                ? "No season document."
                : "No season document written before kickoff."));
            return false;
        }

        if (!line.Values.TryGetValue(stat, out value))
        {
            ctx.Dropped.Add(new DroppedFact(factType, kind, athlete, $"Season document has no {stat}."));
            return false;
        }

        return true;
    }

    private static IReadOnlyDictionary<string, object?> Provenance(
        Context ctx,
        SeasonStatLine line,
        string stat,
        decimal seasonValue,
        decimal? gameValue) =>
        new Dictionary<string, object?>
        {
            ["source"] = "AthleteSeasonStatistic+CompetitionPlay",
            ["seasonStatisticId"] = line.StatisticId,
            ["seasonStatisticCreatedUtc"] = line.CreatedUtc,
            ["documentRule"] = ctx.DocumentCutoffUtc is null ? "newest" : "beforeKickoff",
            // A live document written after kickoff may already include part of
            // this game (2.4% in the task 01 spike); the post-game audit checks these.
            ["writtenAfterKickoff"] = line.CreatedUtc >= ctx.KickoffUtc,
            ["stat"] = stat,
            ["seasonValue"] = seasonValue,
            ["gameValue"] = gameValue,
            ["countedThroughSequence"] = ctx.Sequence
        };

    private static AnalysedPlay ToAnalysed(GamePlay p, AnalysedPlay current) =>
        new(p.PlayId, current.ContestId, current.CompetitionId, p.Sequence.ToString(), PlayType.Unknown,
            p.TypeId, string.Empty, p.StatYardage, p.OffenseFranchiseSeasonId, p.Participants);
}
