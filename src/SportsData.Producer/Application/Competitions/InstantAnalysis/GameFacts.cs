using SportsData.Core.Common;

namespace SportsData.Producer.Application.Competitions.InstantAnalysis;

/// <summary>
/// A play of the current game, as needed to count "as of this play".
/// <paramref name="OffenseFranchiseSeasonId"/> is the play's start team, which is
/// reliable for possession; an athlete's own AthleteSeason team is not (see the
/// task 05 notes in docs/features/instant-analysis/).
/// </summary>
public record GamePlay(
    Guid PlayId,
    long Sequence,
    string TypeId,
    int StatYardage,
    Guid? OffenseFranchiseSeasonId,
    IReadOnlyList<AnalysedPlayParticipant> Participants);

/// <summary>
/// One fact, before phrasing. <see cref="Value"/> is the number the sentence is
/// built on; <see cref="Provenance"/> records how it was counted, for the
/// PlayAnalysis.Facts audit column.
/// </summary>
public record PlayFact(
    string FactType,
    PlayKind Kind,
    Guid? AthleteSeasonId,
    Guid? FranchiseSeasonId,
    decimal Value,
    IReadOnlyDictionary<string, object?> Provenance)
{
    /// <summary>
    /// Set on season facts: when the season document they read was written,
    /// for the freshness guard in <see cref="FactSelector"/>.
    /// </summary>
    public DateTime? SeasonDocumentCreatedUtc { get; init; }
}

/// <summary>A fact that was considered and not produced, with why.</summary>
public record DroppedFact(string FactType, PlayKind Kind, Guid? AthleteSeasonId, string Reason);

public record FactResult(IReadOnlyList<PlayFact> Facts, IReadOnlyList<DroppedFact> Dropped);

public static class FactTypes
{
    /// <summary>The actor's Nth of this kind today ("his 2nd interception today").</summary>
    public const string GameCount = "GameCount";

    /// <summary>The victim's Nth today ("his 2nd interception thrown today", "sacked for the 3rd time").</summary>
    public const string GameCountAgainst = "GameCountAgainst";

    /// <summary>The defense's Nth takeaway or sack today.</summary>
    public const string TeamGameCount = "TeamGameCount";

    /// <summary>The longest run or pass of the game so far.</summary>
    public const string GameLongest = "GameLongest";

    /// <summary>The actor's season total as of this play ("his 2nd interception of the season").</summary>
    public const string SeasonCount = "SeasonCount";

    /// <summary>The victim's season total as of this play ("his 5th interception thrown this season").</summary>
    public const string SeasonCountAgainst = "SeasonCountAgainst";

    /// <summary>The actor's longest run, pass or catch of the season.</summary>
    public const string SeasonLongest = "SeasonLongest";
}

/// <summary>
/// Facts about the current game, computed from its own plays up to and
/// including the triggering play (instant-analysis task 05). Pure: the caller
/// loads the game's plays. Earlier plays are classified with
/// <see cref="PlayClassifier"/> so "an interception" means the same thing for
/// the play and for the count.
/// </summary>
public static class GameFacts
{
    public static FactResult Compute(
        AnalysedPlay play,
        IReadOnlyList<PlayEvent> events,
        IReadOnlyList<GamePlay> gamePlays,
        IReadOnlyDictionary<Guid, Guid> defenseByOffense)
    {
        var facts = new List<PlayFact>();
        var dropped = new List<DroppedFact>();

        if (!long.TryParse(play.SequenceNumber, out var sequence))
            return new FactResult(facts, dropped);

        // As of this play: everything up to and including it, never later plays
        // (a lab replay of a finished game has them all).
        var upToNow = gamePlays.Where(p => p.Sequence <= sequence).OrderBy(p => p.Sequence).ToList();
        var classified = upToNow
            .Select(p => (Play: p, Events: PlayClassifier.Classify(ToAnalysed(p, play))))
            .ToList();

        var offense = upToNow.FirstOrDefault(p => p.PlayId == play.PlayId)?.OffenseFranchiseSeasonId
                      ?? play.StartFranchiseSeasonId;

        foreach (var evt in events)
        {
            switch (evt.Kind)
            {
                case PlayKind.Interception:
                case PlayKind.Sack:
                case PlayKind.FumbleLost:
                    AddActorCounts(evt, classified, sequence, upToNow, offense, facts, dropped);
                    AddVictimCount(evt, classified, sequence, facts);
                    AddTeamCount(evt, classified, sequence, offense, defenseByOffense, facts, dropped);
                    break;

                case PlayKind.PassingTouchdown:
                case PlayKind.ReceivingTouchdown:
                case PlayKind.RushingTouchdown:
                    AddActorCounts(evt, classified, sequence, upToNow, offense, facts, dropped);
                    break;

                case PlayKind.ExplosiveRun:
                case PlayKind.ExplosivePass:
                    AddGameLongest(evt, play, upToNow, sequence, facts);
                    break;
            }
        }

        return new FactResult(facts, dropped);
    }

    private static void AddActorCounts(
        PlayEvent evt,
        List<(GamePlay Play, IReadOnlyList<PlayEvent> Events)> classified,
        long sequence,
        List<GamePlay> upToNow,
        Guid? offense,
        List<PlayFact> facts,
        List<DroppedFact> dropped)
    {
        foreach (var actor in evt.Actors)
        {
            // A plain interception (type 63) doesn't name the interceptor, so if
            // this defense already had one today, the actor's count is unknowable.
            if (evt.Kind == PlayKind.Interception &&
                upToNow.Any(p => p.Sequence < sequence && p.TypeId == "63" && p.OffenseFranchiseSeasonId == offense))
            {
                dropped.Add(new DroppedFact(FactTypes.GameCount, evt.Kind, actor,
                    "Defense has an earlier unattributed interception (type 63) in this game."));
                continue;
            }

            var count = classified.Count(c => c.Events.Any(e => e.Kind == evt.Kind && e.Actors.Contains(actor)));
            facts.Add(new PlayFact(FactTypes.GameCount, evt.Kind, actor, null, count,
                Provenance(sequence, "CompetitionPlay")));
        }
    }

    private static void AddVictimCount(
        PlayEvent evt,
        List<(GamePlay Play, IReadOnlyList<PlayEvent> Events)> classified,
        long sequence,
        List<PlayFact> facts)
    {
        if (evt.Victim is not { } victim)
            return;

        var count = classified.Count(c => c.Events.Any(e => e.Kind == evt.Kind && e.Victim == victim));
        facts.Add(new PlayFact(FactTypes.GameCountAgainst, evt.Kind, victim, null, count,
            Provenance(sequence, "CompetitionPlay")));
    }

    private static void AddTeamCount(
        PlayEvent evt,
        List<(GamePlay Play, IReadOnlyList<PlayEvent> Events)> classified,
        long sequence,
        Guid? offense,
        IReadOnlyDictionary<Guid, Guid> defenseByOffense,
        List<PlayFact> facts,
        List<DroppedFact> dropped)
    {
        if (offense is null || !defenseByOffense.TryGetValue(offense.Value, out var defense))
        {
            dropped.Add(new DroppedFact(FactTypes.TeamGameCount, evt.Kind, null, "Offense or defense unknown for this play."));
            return;
        }

        // Takeaways count interceptions and lost fumbles together; sacks count alone.
        var kinds = evt.Kind == PlayKind.Sack
            ? new[] { PlayKind.Sack }
            : new[] { PlayKind.Interception, PlayKind.FumbleLost };

        var count = classified.Count(c =>
            c.Play.OffenseFranchiseSeasonId == offense &&
            c.Events.Any(e => kinds.Contains(e.Kind)));

        facts.Add(new PlayFact(FactTypes.TeamGameCount, evt.Kind, null, defense, count,
            Provenance(sequence, "CompetitionPlay")));
    }

    private static void AddGameLongest(
        PlayEvent evt,
        AnalysedPlay play,
        List<GamePlay> upToNow,
        long sequence,
        List<PlayFact> facts)
    {
        var typeIds = evt.Kind == PlayKind.ExplosiveRun
            ? new[] { ((int)PlayType.Rush).ToString(), ((int)PlayType.RushingTouchdown).ToString() }
            : new[] { ((int)PlayType.PassReception).ToString(), ((int)PlayType.PassingTouchdown).ToString() };

        var previousBest = upToNow
            .Where(p => p.Sequence < sequence && typeIds.Contains(p.TypeId))
            .Select(p => (int?)p.StatYardage)
            .Max();

        // Strictly longer: a tie isn't "the longest".
        if (previousBest is not null && play.StatYardage <= previousBest)
            return;

        var actor = evt.Actors.Count > 0 ? evt.Actors[^1] : (Guid?)null;
        var provenance = new Dictionary<string, object?>(Provenance(sequence, "CompetitionPlay"))
        {
            ["previousLongest"] = previousBest
        };
        facts.Add(new PlayFact(FactTypes.GameLongest, evt.Kind, actor, null, play.StatYardage, provenance));
    }

    private static IReadOnlyDictionary<string, object?> Provenance(long sequence, string source) =>
        new Dictionary<string, object?>
        {
            ["source"] = source,
            ["countedThroughSequence"] = sequence
        };

    private static AnalysedPlay ToAnalysed(GamePlay p, AnalysedPlay current) =>
        new(p.PlayId, current.ContestId, current.CompetitionId, p.Sequence.ToString(), PlayType.Unknown,
            p.TypeId, string.Empty, p.StatYardage, p.OffenseFranchiseSeasonId, p.Participants);
}
