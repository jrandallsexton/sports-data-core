using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Contests.Baseball
{
    /// <summary>
    /// Baseball per-play update. Published by the MLB
    /// BaseballEventCompetitionPlayDocumentProcessor for every new play
    /// during a live game and per-play during BaseballContestReplayService
    /// replays. Carries the play description AND the baseball scoreboard
    /// tick in one event so consumers don't have to reassemble them.
    ///
    /// Lifecycle transitions (Scheduled→InProgress→Final) remain on
    /// <see cref="ContestStatusChanged"/>.
    ///
    /// Some scoreboard fields (Outs, runners, AtBat/Pitching athlete
    /// IDs) are not yet populated from the BaseballCompetitionPlay
    /// entity — those will fill in once the AtBat / runner sourcing
    /// pipeline lands. The wire shape is preserved so the existing UI
    /// diamond renderer keeps working when those fields go non-null.
    /// </summary>
    public record BaseballPlayCompleted(
        Guid ContestId,
        Guid CompetitionId,
        Guid PlayId,
        string PlayDescription,
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
