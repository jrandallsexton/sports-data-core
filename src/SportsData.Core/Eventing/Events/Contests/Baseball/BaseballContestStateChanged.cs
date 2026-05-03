using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Contests.Baseball
{
    /// <summary>
    /// Baseball scoreboard tick. Carries the baseball shape (inning,
    /// half-inning, count, outs, base state, current at-bat). Published
    /// per pitch / at-bat once the MLB live-state pipeline is wired.
    ///
    /// Lifecycle transitions (Scheduled→InProgress→Final) are separately
    /// surfaced via <see cref="ContestStatusChanged"/>.
    /// </summary>
    public record BaseballContestStateChanged(
        Guid ContestId,
        int Inning,
        string HalfInning,
        int AwayScore,
        int HomeScore,
        int Balls,
        int Strikes,
        int Outs,
        bool RunnerOnFirst,
        bool RunnerOnSecond,
        bool RunnerOnThird,
        Guid? AtBatAthleteId,
        Guid? PitchingAthleteId,
        Uri? Ref,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
}
