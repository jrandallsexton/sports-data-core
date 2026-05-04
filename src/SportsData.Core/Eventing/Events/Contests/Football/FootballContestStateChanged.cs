using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Contests.Football
{
    /// <summary>
    /// Football scoreboard tick. Published per play during a live game and
    /// per-play during ContestReplayService replays. Carries the football
    /// shape (period, game clock, possession, scoring-play flag).
    ///
    /// Lifecycle transitions (Scheduled→InProgress→Final) are separately
    /// surfaced via <see cref="ContestStatusChanged"/>.
    /// </summary>
    public record FootballContestStateChanged(
        Guid ContestId,
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
