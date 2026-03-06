// ─── Enums / unions ──────────────────────────────────────────────────────────

// Pick type as used by the backend
export type PickType = 'StraightUp' | 'AgainstTheSpread' | 'OverUnder';

// UI-side pick choice (which side the user picked in the card)
export type PickChoice = 'home' | 'away';

// ─── Pick'em League (group/pool) ─────────────────────────────────────────────

/** Matches UserLeagueMembership from /user/me */
export interface League {
  id: string;
  name: string;
  maxSeasonWeek?: number | null;
}

// ─── Matchup ─────────────────────────────────────────────────────────────────

/** Matches MatchupForPickDto from GET /ui/leagues/{id}/matchups/{week} */
export interface Matchup {
  contestId: string;
  headLine?: string | null;
  startDateUtc: string;         // ISO 8601

  // Away team
  away: string;                 // full name
  awayShort: string;            // abbreviation
  awaySlug: string;
  awayFranchiseSeasonId: string;
  awayLogoUri?: string | null;
  awayColor?: string | null;
  awayRank?: number | null;
  awayWins?: number;
  awayLosses?: number;
  awayConferenceWins?: number;
  awayConferenceLosses?: number;

  // Home team
  home: string;                 // full name
  homeShort: string;            // abbreviation
  homeSlug: string;
  homeFranchiseSeasonId: string;
  homeLogoUri?: string | null;
  homeColor?: string | null;
  homeRank?: number | null;
  homeWins?: number;
  homeLosses?: number;
  homeConferenceWins?: number;
  homeConferenceLosses?: number;

  // Status
  status: string;               // "Scheduled" | "InProgress" | "Halftime" | "Final" | etc.
  isComplete?: boolean;

  // Scores
  awayScore?: number | null;
  homeScore?: number | null;
  winnerFranchiseSeasonId?: string | null;
  spreadWinnerFranchiseSeasonId?: string | null;
  completedUtc?: string | null;

  // Odds
  spreadCurrent?: number | null;
  spreadOpen?: number | null;
  spreadCurrentDetails?: string | null;
  overUnderCurrent?: number | null;
  overUnderOpen?: number | null;

  // Venue (flat strings, not nested object)
  venue?: string | null;
  venueCity?: string | null;
  venueState?: string | null;

  // Broadcast
  broadcasts?: string | null;

  // AI / Preview
  aiWinnerFranchiseSeasonId?: string | null;
  isPreviewAvailable?: boolean;
  isPreviewReviewed?: boolean;

  [key: string]: unknown;
}

// Response shape from GET /ui/leagues/{id}/matchups/{week}
export interface LeagueMatchupsResponse {
  seasonYear: number;
  weekNumber: number;
  matchups: Matchup[];
  pickType: PickType;
  useConfidencePoints: boolean;
}

// ─── Picks ───────────────────────────────────────────────────────────────────

/** Matches UserPickDto from GET /ui/picks/{groupId}/week/{week} */
export interface UserPick {
  id: string;
  userId: string;
  contestId: string;
  franchiseId: string;
  pickType: PickType;
  confidencePoints?: number | null;
  tiebreakerGuessTotal?: number | null;
  isCorrect?: boolean | null;
  pointsAwarded?: number | null;
  isSynthetic?: boolean;
  [key: string]: unknown;
}

// ─── Standings ───────────────────────────────────────────────────────────────

/** Matches LeaderboardUserDto from GET /ui/leaderboard/{groupId} */
export interface Standing {
  leagueId: string;
  leagueName: string;
  userId: string;
  name: string;
  isSynthetic?: boolean;
  totalPicks: number;
  totalCorrect: number;
  pickAccuracy: number;
  totalPoints: number;
  currentWeekPoints: number;
  weeklyAverage: number;
  rank: number;
  lastWeekRank?: number | null;
  [key: string]: unknown;
}

// ─── Pick widget ────────────────────────────────────────────────────────────

/** One row in the pick-record widget — per league season totals */
export interface PickWidgetItem {
  leagueId: string;
  leagueName: string;
  correct: number;
  incorrect: number;
  pushes: number;
  accuracy: number; // decimal 0–1
}

/** Response from GET /ui/picks/{year}/widget */
export interface PickWidgetResponse {
  asOfWeek: number;
  items: PickWidgetItem[];
}

// ─── AI Preview ─────────────────────────────────────────────────────────────

/** Response from GET /ui/matchup/{contestId}/preview */
export interface PreviewResponse {
  id: string;
  overview?: string | null;
  analysis?: string | null;
  prediction?: string | null;
  straightUpWinner?: string | null;
  atsWinner?: string | null;
  awayScore?: number | null;
  homeScore?: number | null;
  vegasImpliedScore?: string | null;
  generatedUtc?: string | null;
}

// ─── Team Stats / Comparison ─────────────────────────────────────────────────

/** A single stat entry within a category */
export interface TeamStatEntry {
  displayValue: string;
  label?: string | null;
  [key: string]: unknown;
}

/** Shape of stats?.data?.statistics from team card API */
export type TeamStatistics = Record<string, TeamStatEntry[]>;

/** Shape of metrics?.data from team card API */
export type TeamMetrics = Record<string, unknown>;

/** Assembled comparison object used by StatsComparisonModal */
export interface TeamComparisonData {
  teamA: {
    name: string;
    logoUri?: string | null;
    stats?: { data?: { statistics?: TeamStatistics } } | null;
    metrics?: { data?: TeamMetrics } | null;
  };
  teamB: {
    name: string;
    logoUri?: string | null;
    stats?: { data?: { statistics?: TeamStatistics } } | null;
    metrics?: { data?: TeamMetrics } | null;
  };
}

// ─── User ────────────────────────────────────────────────────────────────────

/** Matches UserDto from GET /user/me */
export interface UserDto {
  id: string;
  firebaseUid?: string | null;
  email: string;
  displayName?: string | null;
  photoUrl?: string | null;
  timezone?: string | null;
  lastLoginUtc: string;
  leagues: League[];
  isAdmin?: boolean;
  isReadOnly?: boolean;
}
