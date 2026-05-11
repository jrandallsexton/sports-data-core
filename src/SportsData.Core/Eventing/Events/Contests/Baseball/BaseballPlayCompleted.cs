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
    /// The athlete fields are season-scoped: ESPN's play
    /// `participant.athlete.$ref` points at AthleteSeason (URL path is
    /// `/seasons/{year}/athletes/{id}`), so the resolved canonical ID is
    /// AthleteSeason.Id, not AthleteBase.Id. The display fields
    /// (ShortName, PositionAbbreviation, HeadshotUrl) are hydrated on the
    /// publish path so consumers can render the live at-bat header
    /// without round-tripping back to the API for athlete data.
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
        Guid? AtBatAthleteSeasonId,
        string? AtBatShortName,
        string? AtBatPositionAbbreviation,
        string? AtBatHeadshotUrl,
        Guid? PitchingAthleteSeasonId,
        string? PitchingShortName,
        string? PitchingPositionAbbreviation,
        string? PitchingHeadshotUrl,
        Uri? Ref,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
}
