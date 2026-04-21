/**
 * Centralized link builders for sport-specific routes.
 *
 * Backend serializes the Sport enum as PascalCase ("FootballNcaa",
 * "FootballNfl", "BaseballMlb"); URL segments are lowercase tuples
 * (/sport/football/ncaa, etc.). resolveSportLeague() maps between the
 * two.
 *
 * Legacy single-sport call sites may still invoke teamLink / contestLink
 * without passing sport/league explicitly — those fall back to
 * DEFAULT_SPORT/DEFAULT_LEAGUE for backward compatibility. New multi-sport
 * call paths should go through resolveSportLeague so that unsupported or
 * missing enums surface as null instead of silently rendering a football/ncaa
 * route for an MLB / hockey / future-sport league.
 */

const DEFAULT_SPORT = 'football';
const DEFAULT_LEAGUE = 'ncaa';

const SPORT_ENUM_MAP = {
  FootballNcaa: { sport: 'football', league: 'ncaa' },
  FootballNfl: { sport: 'football', league: 'nfl' },
  BaseballMlb: { sport: 'baseball', league: 'mlb' },
};

/**
 * Map a backend Sport enum name to { sport, league } URL segments.
 * Returns null for null/undefined input and for enum names that aren't in
 * SPORT_ENUM_MAP — callers must handle that case (skip fetch, render an
 * unsupported state, etc.). The previous implementation defaulted unknowns
 * to football/ncaa, which masked bugs by producing valid-looking but wrong
 * routes for non-football leagues.
 *
 * @returns {{ sport: string, league: string } | null}
 */
export function resolveSportLeague(sportEnum) {
  if (!sportEnum) return null;
  return SPORT_ENUM_MAP[sportEnum] ?? null;
}

export function teamLink(slug, seasonYear, sport = DEFAULT_SPORT, league = DEFAULT_LEAGUE) {
  return `/app/sport/${sport}/${league}/team/${slug}${seasonYear ? `/${seasonYear}` : ''}`;
}

export function contestLink(contestId, sport = DEFAULT_SPORT, league = DEFAULT_LEAGUE) {
  return `/app/sport/${sport}/${league}/contest/${contestId}`;
}
