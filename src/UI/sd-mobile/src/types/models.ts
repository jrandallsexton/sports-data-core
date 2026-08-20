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
  /**
   * Backend Sport enum (PascalCase). Drives the default sport-icon glyph
   * shown next to the league name on YourLeaguesCard until commissioner-
   * uploaded league icons land. Optional — older API builds (pre the
   * GetMeQueryHandler Sport projection) don't include it.
   */
  sport?: 'FootballNcaa' | 'FootballNfl' | 'BaseballMlb';
  /**
   * Ascending list of week numbers that exist in this league.
   * Custom-window leagues may contain a subset (e.g. [4]) rather than 1..N.
   */
  seasonWeeks?: number[];
  /**
   * The league's CURRENT week (server-computed from game dates). The picks
   * page lands here by default — web parity: PicksPage prefers this over
   * the latest week. Optional: older payloads and past-league summaries
   * don't carry it, in which case latest-week is the fallback.
   */
  currentSeasonWeek?: number | null;
}

// ─── Probable Pitcher (MLB only) ─────────────────────────────────────────────

/** Matches ProbablePitcherDto from the canonical layer. */
export interface ProbablePitcher {
  displayName: string;
  headshotUrl?: string | null;
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
  /** Dark-theme variant. Falls back to awayLogoUri when null. */
  awayLogoUriDark?: string | null;
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
  /** Dark-theme variant. Falls back to homeLogoUri when null. */
  homeLogoUriDark?: string | null;
  homeColor?: string | null;
  homeRank?: number | null;
  homeWins?: number;
  homeLosses?: number;
  homeConferenceWins?: number;
  homeConferenceLosses?: number;

  // Status — raw ESPN status type name for programmatic branching
  // (e.g. "STATUS_IN_PROGRESS", "STATUS_FINAL", "STATUS_RAIN_DELAY"). Pair
  // with `statusDescription` for display.
  status: string;
  /** Human-readable status (e.g. "In Progress", "Final"). For display. */
  statusDescription?: string;
  isComplete?: boolean;

  // Live game state — football (populated when status = InProgress / Halftime)
  period?: string | null;
  clock?: string | null;
  possessionFranchiseSeasonId?: string | null;
  isScoringPlay?: boolean | null;
  /** Canonical scoring-type NAME slug ('touchdown', 'field-goal', 'safety',
   *  'defensive-two-point-conversion') or null - NOT a display name. */
  scoringPlayType?: string | null;
  /** 0–100 yards from the away (visitor) goal line. Null pre-snap / halftime / post-game. */
  ballOnYardLine?: number | null;

  // Live game state — baseball (populated by BaseballPlayCompleted via contestUpdatesStore)
  inning?: number | null;
  /** "Top" or "Bottom" (case-insensitive consumers welcome). */
  halfInning?: string | null;
  balls?: number | null;
  strikes?: number | null;
  outs?: number | null;
  runnerOnFirst?: boolean | null;
  runnerOnSecond?: boolean | null;
  runnerOnThird?: boolean | null;
  /** Season-scoped — resolves to AthleteSeason.Id, not AthleteBase.Id. */
  atBatAthleteSeasonId?: string | null;
  atBatShortName?: string | null;
  atBatPositionAbbreviation?: string | null;
  atBatHeadshotUrl?: string | null;
  pitchingAthleteSeasonId?: string | null;
  pitchingShortName?: string | null;
  pitchingPositionAbbreviation?: string | null;
  pitchingHeadshotUrl?: string | null;

  // Sport-neutral last-play info (written by either *PlayCompleted handler)
  lastPlayId?: string | null;
  lastPlayDescription?: string | null;

  // MLB only — null on non-MLB matchups.
  homeProbablePitcher?: ProbablePitcher | null;
  awayProbablePitcher?: ProbablePitcher | null;

  // Scores
  awayScore?: number | null;
  homeScore?: number | null;
  winnerFranchiseSeasonId?: string | null;
  spreadWinnerFranchiseSeasonId?: string | null;
  /**
   * Post-enrichment O/U verdict. Wire shape is messy: API DTO type is
   * OverUnderPick ("None"/"Over"/"Under") but Producer's Contest.OverUnder
   * uses OverUnderResult which adds Push=3 — the mapper casts an int
   * through, so a real push can arrive as "Push", 3, or "3" depending on
   * serializer behavior on the unnamed enum value. Consumers should match
   * all three (see FinalScoreResult).
   */
  overUnderResult?: string | number | null;
  completedUtc?: string | null;

  // Odds
  spreadCurrent?: number | null;
  spreadOpen?: number | null;
  spreadCurrentDetails?: string | null;
  overUnderCurrent?: number | null;
  overUnderOpen?: number | null;
  /**
   * Sportsbook display name whose spread / O/U populated the values
   * above (e.g. "ESPN BET", "DraftKings"). Selected per-competition by
   * Producer's preferred-then-fallback provider ordering. Null when no
   * qualifying odds row exists for this contest.
   */
  providerName?: string | null;

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
  /**
   * ISO 8601 inclusive cutoff = SeasonWeek.EndDate of the displayed week.
   * Passed to /ui/teamcard/.../schedule?asOfDate=… by the MiniSchedule fetch
   * so historical pick reviews don't leak future-game results. Null when the
   * week has no canonical matchups to derive it from.
   * See docs/team-schedule-endpoint.md.
   */
  asOfDate?: string | null;
  matchups: Matchup[];
  pickType: PickType;
  useConfidencePoints: boolean;
  /**
   * Backend Sport enum name ("FootballNcaa" | "FootballNfl" | "BaseballMlb").
   * Drives sport-aware team/game route segments. Resolve to URL-friendly
   * {sport, league} tuple via utils/sportLinks.resolveSportLeague().
   */
  sport: string;
}

// ─── User options ────────────────────────────────────────────────────────────

/**
 * Matches UserOptionsDto from GET /user/me/options — the typed projection of
 * the per-user key/value UserOption rows. Absent rows yield defaults, so this
 * is always a full set. See docs/features/user-options.md.
 */
export interface UserOptions {
  /**
   * Default false: gambling content (spreads, totals, odds) renders only
   * where the league's pick type requires it (ATS/OU) until the user opts in.
   */
  showGamblingContent: boolean;
}

// ─── Picks ───────────────────────────────────────────────────────────────────

/**
 * Matches UserPicksResultDto from GET /ui/picks/{groupId}/week/{week}.
 * Envelope over the user's picks plus server-computed result counts for the
 * ended-league header glance (X|Y|Z). X (no scored pick) is derived:
 * totalMatchups - correctCount - incorrectCount — the server sends the minimal
 * orthogonal set so a redundant fourth counter can't drift out of agreement.
 * See docs/features/league-ended-headers.md.
 */
export interface UserPicksResult {
  picks: UserPick[];
  /** All matchups in the group-week, picked or not. */
  totalMatchups: number;
  /** Picks with isCorrect === true. */
  correctCount: number;
  /** Picks with isCorrect === false. */
  incorrectCount: number;
  /**
   * Matchups whose outcome for THIS user is still open: unpicked games that
   * haven't started, plus picked games not yet scored. Zero means the user's
   * results are final for the week (the full glance can render) — no waiting
   * for league deactivation, which lags the end date by ~7 days.
   */
  pendingCount: number;
}

/** Matches UserPickDto from GET /ui/picks/{groupId}/week/{week} */
export interface UserPick {
  id: string;
  userId: string;
  contestId: string;
  franchiseSeasonId: string;
  pickType: PickType;
  confidencePoints?: number | null;
  tiebreakerGuessTotal?: number | null;
  isCorrect?: boolean | null;
  pointsAwarded?: number | null;
  isSynthetic?: boolean;
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
  /** Percentage-scaled by the API (70.59, not 0.7059) — render raw, never * 100. */
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

/**
 * One historical game from GET /api/{sport}/{league}/contests/{id}/history.
 * Team fields are Franchise.DisplayName strings — the same source as
 * Matchup.away/home, so exact string matching identifies "our" side.
 */
export interface ContestHistoryGame {
  gameDate: string;
  seasonYear: number;
  phase?: string | null;
  note?: string | null;
  homeTeam: string;
  awayTeam: string;
  homeScore?: number | null;
  awayScore?: number | null;
  winner?: string | null;
  spreadWinner?: string | null;
  spread?: string | null;
  overUnder?: number | null;
  overUnderResult?: string | null;
}

/** A team's prior season in summary (record; conference record when published). */
export interface ContestPriorSeasonSummary {
  seasonYear: number;
  wins: number;
  losses: number;
  conferenceWins?: number | null;
  conferenceLosses?: number | null;
}

/**
 * "When is the last time this team won/lost by ≥ X?" — score-margin tier.
 * A null lastGame means it has not happened within the searched window
 * (searchFloorSeason onward), which is itself the headline fact.
 */
export interface ContestMarginFact {
  lastGame?: ContestHistoryGame | null;
  opponentSeasonRecord?: string | null;
  opponentPriorSeasonRecord?: string | null;
  countLastFiveSeasons: number;
  searchFloorSeason: number;
}

/** ATS record conditioned on spread size ("as a 35+ underdog") — market tier (~2022+). */
export interface ContestAtsBucketFact {
  threshold: number;
  games: number;
  covers: number;
  dataFloorSeason: number;
}

/**
 * Facts conditioned on the target contest's CURRENT spread. Spread-derived —
 * display must be gated behind the gambling-content preference.
 */
export interface ContestSpreadContext {
  favoriteTeam: string;
  underdogTeam: string;
  magnitude: number;
  spreadDetails?: string | null;
  favoriteWonByMargin: ContestMarginFact;
  underdogLostByMargin: ContestMarginFact;
  favoriteAtsAsBigFavorite?: ContestAtsBucketFact | null;
  underdogAtsAsBigUnderdog?: ContestAtsBucketFact | null;
}

/**
 * Historical context for a matchup: recent head-to-head meetings plus each
 * team's late-prior-season form — the same blocks the preview/insight models
 * consume. Away/Home here refer to the LIVE matchup's sides.
 */
export interface ContestHistory {
  headToHead: ContestHistoryGame[];
  awayPriorSeasonGames: ContestHistoryGame[];
  homePriorSeasonGames: ContestHistoryGame[];
  awayPriorSeason?: ContestPriorSeasonSummary | null;
  homePriorSeason?: ContestPriorSeasonSummary | null;
  /** Null when the contest has no line from the preferred providers. */
  spreadContext?: ContestSpreadContext | null;
}

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
  /** Matchup history; null when the fetch failed or nothing exists. */
  history?: ContestHistory | null;
}

// ─── User ────────────────────────────────────────────────────────────────────

/** Matches UserDto from GET /user/me */
export interface UserDto {
  id: string;
  firebaseUid?: string | null;
  email: string;
  displayName?: string | null;
  username?: string | null;
  photoUrl?: string | null;
  timezone?: string | null;
  lastLoginUtc: string;
  leagues: League[];
  isAdmin?: boolean;
  isReadOnly?: boolean;
}

/**
 * Per-category push-notification opt-in flags. Mirrors the API's
 * NotificationPreferencesDto — every field defaults to `true` (a user with no
 * saved row is treated as fully opted-in). The settings screen holds the whole
 * set in state and PATCHes it back as a full replacement.
 */
export interface NotificationPreferences {
  pickResultEnabled: boolean;
  pickDeadlineReminderEnabled: boolean;
  contestStartReminderEnabled: boolean;
  leagueInviteEnabled: boolean;
  membershipEnabled: boolean;
  matchupPreviewEnabled: boolean;
  scheduleChangeEnabled: boolean;
  oddsChangedEnabled: boolean;
  /**
   * Wire name of the pick-result copy voice — 'Standard' | 'Smack' today
   * (server-validated allow-list; typed string for forward-compat). MUST be
   * carried on every PATCH: the endpoint is full-replacement, so omitting it
   * silently resets an opted-in user to Standard.
   */
  pickResultVoice: string;
}

// ─── Contest Overview ─────────────────────────────────────────────────────────

export interface ContestOverviewTeam {
  displayName: string;
  logoUrl?: string | null;
  /** Explicit dark-theme variant. Falls back to logoUrl when null. */
  logoUrlDark?: string | null;
  /** Explicit light-theme variant. Falls back to logoUrl when null. */
  logoUrlLight?: string | null;
  slug?: string | null;
}

export interface QuarterScore {
  quarter: string;
  awayScore: number;
  homeScore: number;
}

export interface ContestOverviewLeader {
  playerName: string;
  playerHeadshotUrl?: string | null;
  statLine?: string | null;
}

export interface ContestOverviewLeaderSide {
  leaders: ContestOverviewLeader[];
}

export interface ContestOverviewLeaderCategory {
  categoryId?: string | null;
  categoryName: string;
  away: ContestOverviewLeaderSide;
  home: ContestOverviewLeaderSide;
}

export interface ContestOverviewInfo {
  venue?: string | null;
  venueCity?: string | null;
  venueState?: string | null;
  venueImageUrl?: string | null;
  startDateUtc?: string | null;
  attendance?: number | null;
  broadcast?: string | null;
}

export interface ContestOverviewDto {
  header: {
    homeTeam: ContestOverviewTeam;
    awayTeam: ContestOverviewTeam;
    quarterScores: QuarterScore[];
  };
  leaders?: {
    categories: ContestOverviewLeaderCategory[];
  } | null;
  info?: ContestOverviewInfo | null;
  homeMetrics?: Record<string, unknown> | null;
  awayMetrics?: Record<string, unknown> | null;
}

// ─── Team Card ────────────────────────────────────────────────────────────────

export interface TeamCardScheduleGame {
  date: string;
  /**
   * Season phase name from the backend ("Preseason" | "Regular Season" |
   * "Postseason"). Drives the phase divider rows in the TeamCard schedule
   * (parity with web's TeamSchedule). Nullable defensively for legacy rows.
   */
  seasonPhase?: string | null;
  opponent: string;
  opponentSlug?: string | null;
  location?: string | null;
  contestId?: string | null;
  finalizedUtc?: string | null;
  awayScore?: number | null;
  homeScore?: number | null;
  wasWinner?: boolean | null;
}

export interface TeamCardDto {
  name: string;
  logoUrl?: string | null;
  /** Explicit dark-theme variant. Falls back to logoUrl when null. */
  logoUrlDark?: string | null;
  /** Explicit light-theme variant. Falls back to logoUrl when null. */
  logoUrlLight?: string | null;
  colorPrimary?: string | null;
  colorSecondary?: string | null;
  conferenceName?: string | null;
  conferenceShortName?: string | null;
  overallRecord?: string | null;
  conferenceRecord?: string | null;
  stadiumName?: string | null;
  stadiumCapacity?: number | null;
  seasonYears?: number[] | null;
  franchiseSeasonId?: string | null;
  schedule?: TeamCardScheduleGame[] | null;
}
