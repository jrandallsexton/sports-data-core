/**
 * TypeScript shapes for SignalR event payloads received from
 * `/hubs/notifications`. Each payload mirrors the corresponding C# event
 * record under `SportsData.Core.Eventing.Events.Contests.*` — JSON
 * serialization camel-cases the property names by default.
 *
 * Only fields the mobile app actually consumes are typed. The wire carries
 * additional fields (Ref, Sport, SeasonYear, CorrelationId, CausationId)
 * that mobile ignores today; if a future feature needs them, extend these
 * interfaces rather than reaching at untyped properties.
 */

export interface ContestStatusChangedPayload {
  contestId: string;
  /** Sport-neutral lifecycle state, e.g. "Scheduled", "InProgress", "Final". */
  status: string;
}

export interface FootballPlayCompletedPayload {
  contestId: string;
  competitionId: string;
  playId: string;
  playDescription: string;
  period: string;
  clock: string;
  awayScore: number;
  homeScore: number;
  possessionFranchiseSeasonId: string | null;
  isScoringPlay: boolean;
  /**
   * 0–100 yards from the away (visitor) goal line. Null at pre-snap,
   * halftime, or post-game — match ESPN's YardLine convention.
   */
  ballOnYardLine: number | null;
}

export interface BaseballPlayCompletedPayload {
  contestId: string;
  competitionId: string;
  playId: string;
  playDescription: string;
  inning: number;
  /** "Top" or "Bottom". */
  halfInning: string;
  awayScore: number;
  homeScore: number;
  balls: number;
  strikes: number;
  outs: number;
  runnerOnFirst: boolean;
  runnerOnSecond: boolean;
  runnerOnThird: boolean;
  /**
   * Athlete IDs are season-scoped — they resolve to AthleteSeason.Id, not
   * AthleteBase.Id (ESPN's play `participant.athlete.$ref` points at
   * AthleteSeason).
   */
  atBatAthleteSeasonId: string | null;
  atBatShortName: string | null;
  atBatPositionAbbreviation: string | null;
  atBatHeadshotUrl: string | null;
  pitchingAthleteSeasonId: string | null;
  pitchingShortName: string | null;
  pitchingPositionAbbreviation: string | null;
  pitchingHeadshotUrl: string | null;
}
