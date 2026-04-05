/**
 * Centralized link builders for sport-specific routes.
 * Currently defaults to football/ncaa. When multi-sport UI is added,
 * these will accept sport/league from the data or context.
 */

const DEFAULT_SPORT = 'football';
const DEFAULT_LEAGUE = 'ncaa';

export function teamLink(slug, seasonYear, sport = DEFAULT_SPORT, league = DEFAULT_LEAGUE) {
  return `/app/sport/${sport}/${league}/team/${slug}${seasonYear ? `/${seasonYear}` : ''}`;
}

export function contestLink(contestId, sport = DEFAULT_SPORT, league = DEFAULT_LEAGUE) {
  return `/app/sport/${sport}/${league}/contest/${contestId}`;
}
