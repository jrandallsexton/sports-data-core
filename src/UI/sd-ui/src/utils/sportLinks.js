/**
 * Centralized link builders for sport-specific routes.
 *
 * Backend serializes the Sport enum as PascalCase ("FootballNcaa",
 * "FootballNfl", "BaseballMlb"); URL segments are lowercase tuples
 * (/sport/football/ncaa, etc.). resolveSportLeague() maps between the
 * two. Callers that still pass nothing fall back to football/ncaa for
 * backward compatibility with legacy single-sport code paths.
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
 * Returns defaults (football/ncaa) for null/unknown values so callers
 * never render a broken route.
 */
export function resolveSportLeague(sportEnum) {
  if (!sportEnum) return { sport: DEFAULT_SPORT, league: DEFAULT_LEAGUE };
  return SPORT_ENUM_MAP[sportEnum] ?? { sport: DEFAULT_SPORT, league: DEFAULT_LEAGUE };
}

export function teamLink(slug, seasonYear, sport = DEFAULT_SPORT, league = DEFAULT_LEAGUE) {
  return `/app/sport/${sport}/${league}/team/${slug}${seasonYear ? `/${seasonYear}` : ''}`;
}

export function contestLink(contestId, sport = DEFAULT_SPORT, league = DEFAULT_LEAGUE) {
  return `/app/sport/${sport}/${league}/contest/${contestId}`;
}
