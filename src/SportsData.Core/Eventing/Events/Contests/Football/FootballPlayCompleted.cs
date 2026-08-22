using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Contests.Football
{
    /// <summary>
    /// Football per-play update. Published by the FB
    /// EventCompetitionPlayDocumentProcessor for every new play during a
    /// live game and per-play during FootballContestReplayService replays.
    /// Carries the play description AND the football scoreboard tick in
    /// one event so consumers don't have to reassemble them.
    ///
    /// Lifecycle transitions (Scheduled→InProgress→Final) remain on
    /// <see cref="ContestStatusChanged"/>.
    /// </summary>
    public record FootballPlayCompleted(
        Guid ContestId,
        Guid CompetitionId,
        Guid PlayId,
        string PlayDescription,
        string Period,
        string Clock,
        int AwayScore,
        int HomeScore,
        Guid? PossessionFranchiseSeasonId,
        bool IsScoringPlay,
        // ESPN scoringType NAME slug from
        // FootballCompetitionPlay.ScoringTypeName. Real vocabulary (verified
        // against canon 2026-08-05): touchdown | field-goal | safety |
        // defensive-two-point-conversion. Null when the play isn't a score or
        // the type wasn't captured (all pre-capture historical rows — so
        // REPLAYS are mostly null): clients then sniff PlayDescription and
        // finally fall back to a neutral "SCORE!" label (issue #45).
        // Nullable keeps old in-flight messages deserializable, so deploy
        // order doesn't matter.
        string? ScoringPlayType,
        // Ball position as an ABSOLUTE field coordinate, 0–100 measured
        // from the HOME team's goal line (home goal = 0, away goal = 100).
        // Matches ESPN's YardLine convention. So the home team's own yard
        // numbers read directly (HOME 35 → 35) and the away team's invert
        // (AWAY 25 → 75) — verified against stored play text across
        // several games and both home/away orientations. A drive by the
        // home team increases this value; an away drive decreases it.
        // Null means unknown (e.g. pre-snap, halftime, post-game).
        int? BallOnYardLine,
        // Down and yards-to-go for the NEXT snap (the play's End* state,
        // falling back to Start*). Drives the live card's situation line
        // ("2nd & 7"). Nullable for the same reason as ScoringPlayType:
        // pre-capture rows and old in-flight messages must stay
        // deserializable, so deploy order doesn't matter. Down 0 / null
        // means no snap state (kickoff, extra point, end of period).
        int? Down,
        int? Distance,
        Uri? Ref,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
}
