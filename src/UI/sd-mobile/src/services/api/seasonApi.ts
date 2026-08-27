import { apiClient } from './client';

// Matches SportsData.Core.Dtos.Canonical.SeasonPhaseDto.
// TypeCode: 1 = Preseason, 2 = Regular Season, 3 = Postseason, 4 = Off Season.
export interface SeasonPhase {
  typeCode: number;
  name: string;
  startDate: string;
  endDate: string;
}

// Matches SportsData.Core.Dtos.Canonical.CurrentSeasonDto.
export interface CurrentSeason {
  seasonYear: number;
  name: string;
  startDate: string;
  endDate: string;
  phases: SeasonPhase[];
}

export const REGULAR_SEASON_TYPE_CODE = 2;

/**
 * Canonical react-query keys for current-season lookups. Every consumer
 * MUST use these — the off-season countdown and useCurrentSeasonYear
 * once used different ad-hoc keys for the same endpoint, so react-query
 * couldn't dedupe and the home screen fetched NCAA's season twice.
 */
export const currentSeasonKeys = {
  current: (sport: string, league: string) =>
    ['season', 'current', sport, league] as const,
};

/**
 * Retry policy for current-season observers (use on EVERY observer of a
 * currentSeasonKeys query — mixed policies on one key are confusing).
 * 404 is the VALID "no season sourced" state — never retry it. Anything
 * else (timeouts, connection failures, 5xx) gets the bounded retry a
 * blank seasonYear deserves, since a resolved failure disables
 * dependent queries (rankings) until the next mount.
 */
export const currentSeasonRetry = (failureCount: number, error: unknown): boolean => {
  const status = (error as { response?: { status?: number } })?.response?.status;
  if (status === 404) return false;
  return failureCount < 2;
};

export const seasonApi = {
  // GET /api/{sport}/{league}/seasons/current — current-or-upcoming season with
  // its phases. Raw phase data; the caller interprets it (e.g. the off-season
  // countdown reads the Regular Season phase's startDate). `sport`/`league` are
  // route segments, e.g. ('football','ncaa').
  getCurrentSeason: (sport: string, league: string) =>
    apiClient.get<CurrentSeason>(`/api/${sport}/${league}/seasons/current`),
};
