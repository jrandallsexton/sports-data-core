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
        // Ball position on the field, expressed as 0–100 yards from the
        // away (visitor) goal line. Matches ESPN's YardLine convention.
        // Null means unknown (e.g. pre-snap, halftime, post-game).
        int? BallOnYardLine,
        Uri? Ref,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
}
