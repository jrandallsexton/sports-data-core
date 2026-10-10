using SportsData.Core.Common;

namespace SportsData.Producer.Application.Competitions.InstantAnalysis;

public enum PlayKind
{
    Interception,
    Sack,
    FumbleLost,
    PassingTouchdown,
    ReceivingTouchdown,
    RushingTouchdown,
    ExplosiveRun,
    ExplosivePass
}

/// <summary>
/// One thing that happened on a play, and who did it. A play can produce
/// several (a 60-yard touchdown pass is a passing TD, a receiving TD and an
/// explosive pass).
/// </summary>
/// <param name="Actors">
/// The athletes credited, usually one. Two on a split sack. Empty when ESPN
/// doesn't name the actor (a plain interception, type 63, names only the
/// passer); facts about the actor are then impossible, but victim facts are not.
/// </param>
/// <param name="Victim">The athlete on the wrong end (the passer intercepted or sacked, the fumbler).</param>
public record PlayEvent(
    PlayKind Kind,
    IReadOnlyList<Guid> Actors,
    Guid? Victim);

/// <summary>
/// Classifies a play into <see cref="PlayEvent"/>s from its play type id, its
/// participants' roles and its yardage. Roles were verified against 2026 data
/// in the instant-analysis data spike (task 01): match on the role, never on
/// participant order, which varies.
///
/// Thresholds live here, in code, for the proof of concept; they move to the
/// database in Phase 2.
/// </summary>
public static class PlayClassifier
{
    public const int ExplosiveRunYards = 10;
    public const int ExplosivePassYards = 20;

    public static IReadOnlyList<PlayEvent> Classify(AnalysedPlay play)
    {
        // Classify on the stored ESPN type id rather than the mapped enum
        // column, so an id the enum mapping didn't recognise can't be
        // misread as a known type.
        if (!int.TryParse(play.TypeId, out var typeId))
            return [];

        var events = new List<PlayEvent>();

        switch ((PlayType)typeId)
        {
            case PlayType.PassInterceptionReturn:
            case PlayType.InterceptionReturnTouchdown:
            case PlayType.Interception:
                events.Add(new PlayEvent(PlayKind.Interception, Ids(play, "passDefender"), Id(play, "passer")));
                break;

            case PlayType.Sack:
                events.Add(new PlayEvent(PlayKind.Sack, Ids(play, "sackedBy"), Id(play, "passer")));
                break;

            case PlayType.FumbleLost:
                events.Add(new PlayEvent(PlayKind.FumbleLost, Ids(play, "recoverer"), Id(play, "fumbler")));
                break;

            case PlayType.PassingTouchdown:
                events.Add(new PlayEvent(PlayKind.PassingTouchdown, Ids(play, "passer"), null));
                events.Add(new PlayEvent(PlayKind.ReceivingTouchdown, Ids(play, "receiver"), null));
                if (play.StatYardage >= ExplosivePassYards)
                    events.Add(ExplosivePass(play));
                break;

            case PlayType.PassReception:
                if (play.StatYardage >= ExplosivePassYards)
                    events.Add(ExplosivePass(play));
                break;

            case PlayType.RushingTouchdown:
                events.Add(new PlayEvent(PlayKind.RushingTouchdown, Ids(play, "rusher"), null));
                if (play.StatYardage >= ExplosiveRunYards)
                    events.Add(new PlayEvent(PlayKind.ExplosiveRun, Ids(play, "rusher"), null));
                break;

            case PlayType.Rush:
                if (play.StatYardage >= ExplosiveRunYards)
                    events.Add(new PlayEvent(PlayKind.ExplosiveRun, Ids(play, "rusher"), null));
                break;
        }

        // A touchdown or explosive play nobody is credited with says nothing.
        // Takeaways and sacks stay even without a named actor: the victim and
        // team facts still apply.
        return events
            .Where(e => e.Actors.Count > 0 || e.Kind is PlayKind.Interception or PlayKind.Sack or PlayKind.FumbleLost)
            .ToList();
    }

    // Explosive pass credits both ends of the throw.
    private static PlayEvent ExplosivePass(AnalysedPlay play) =>
        new(PlayKind.ExplosivePass, [.. Ids(play, "passer"), .. Ids(play, "receiver")], null);

    private static IReadOnlyList<Guid> Ids(AnalysedPlay play, string role) =>
        play.Participants
            .Where(p => p.Type == role && p.AthleteSeasonId.HasValue)
            .Select(p => p.AthleteSeasonId!.Value)
            .Distinct()
            .ToList();

    private static Guid? Id(AnalysedPlay play, string role) =>
        play.Participants.FirstOrDefault(p => p.Type == role && p.AthleteSeasonId.HasValue)?.AthleteSeasonId;
}
