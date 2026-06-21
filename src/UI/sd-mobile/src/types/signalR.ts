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
  /**
   * Raw ESPN status type name for programmatic branching
   * (e.g. "STATUS_IN_PROGRESS", "STATUS_FINAL", "STATUS_RAIN_DELAY").
   */
  status: string;
  /** Human-readable status (e.g. "In Progress", "Final"). For display. */
  statusDescription: string;
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

/**
 * Fires AFTER ContestStatusChanged(STATUS_FINAL), once Producer's
 * ContestEnrichmentProcessor has written the canonical Contest row with
 * winner, spread winner, over/under result, and final scores. The status
 * event can't carry these because it fires the moment STATUS_FINAL is
 * detected (~30s before enrichment runs).
 *
 * Result fields are nullable mirroring the wire contract:
 *   - winnerFranchiseSeasonId is null on a tie (rare in football,
 *     impossible in MLB per the tie guards).
 *   - spreadWinnerFranchiseSeasonId is null on a true spread push
 *     (game landed exactly on the line).
 *   - overUnderResultRaw is null when no odds were enriched. Wire type
 *     is int? mapping to the Producer-side OverUnderResult enum
 *     (None=0, Over=1, Under=2, Push=3). FinalScoreResult / GameStatus
 *     consume the string form ("Over"/"Under"/"Push"), so the store
 *     handler translates before writing.
 *   - awayScore / homeScore / completedUtc are always populated when
 *     this event is published, but kept nullable so older Producer pods
 *     publishing the prior shape during a rolling deploy don't fail
 *     deserialization.
 */
export interface ContestFinalizedPayload {
  contestId: string;
  awayScore?: number | null;
  homeScore?: number | null;
  winnerFranchiseSeasonId?: string | null;
  spreadWinnerFranchiseSeasonId?: string | null;
  /** Producer-side OverUnderResult enum value: 0 None, 1 Over, 2 Under, 3 Push. */
  overUnderResultRaw?: number | null;
  completedUtc?: string | null;
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
