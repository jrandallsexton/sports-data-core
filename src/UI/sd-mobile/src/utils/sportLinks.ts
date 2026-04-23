/**
 * Centralized link builders for sport-specific routes in the mobile app.
 * Mirrors the web app's `src/utils/sportLinks.js` so both clients use the
 * same URL segment convention.
 *
 * Backend serializes the Sport enum as PascalCase ("FootballNcaa",
 * "FootballNfl", "BaseballMlb"); route segments are lowercase tuples
 * (/sport/football/ncaa/..., etc.). resolveSportLeague() maps between
 * the two and returns null for missing/unknown enums so callers can
 * render an unsupported-sport state instead of silently routing to a
 * wrong-sport screen. This matches the web's stricter contract after
 * CodeRabbit review of PR #271.
 */

export type SportLeague = { sport: string; league: string };

const SPORT_ENUM_MAP: Record<string, SportLeague> = {
  FootballNcaa: { sport: 'football', league: 'ncaa' },
  FootballNfl: { sport: 'football', league: 'nfl' },
  BaseballMlb: { sport: 'baseball', league: 'mlb' },
};

/**
 * Map a backend Sport enum name to { sport, league } URL segments.
 * Returns null for null/undefined input and for enum names that aren't
 * in SPORT_ENUM_MAP — callers must handle that case (skip navigation,
 * render an unsupported state, etc.).
 */
export function resolveSportLeague(sportEnum: string | null | undefined): SportLeague | null {
  if (!sportEnum) return null;
  return SPORT_ENUM_MAP[sportEnum] ?? null;
}

/**
 * Route object for navigating to a team screen. Used as the `to` argument
 * of router.push: router.push(teamRoute({ sport, league, slug, ... })).
 */
export function teamRoute(args: {
  sport: string;
  league: string;
  slug: string;
  seasonYear?: number;
  backTitle?: string;
}): {
  pathname: '/sport/[sport]/[league]/team/[slug]';
  params: Record<string, string>;
} {
  const params: Record<string, string> = {
    sport: args.sport,
    league: args.league,
    slug: args.slug,
  };
  if (args.seasonYear !== undefined) params.season = String(args.seasonYear);
  if (args.backTitle) params.backTitle = args.backTitle;
  return {
    pathname: '/sport/[sport]/[league]/team/[slug]',
    params,
  };
}

/**
 * Route object for navigating to a contest/game overview screen.
 */
export function gameRoute(args: {
  sport: string;
  league: string;
  contestId: string;
  leagueId?: string;
  week?: number;
  backTitle?: string;
}): {
  pathname: '/sport/[sport]/[league]/game/[id]';
  params: Record<string, string>;
} {
  const params: Record<string, string> = {
    sport: args.sport,
    league: args.league,
    id: args.contestId,
  };
  if (args.leagueId) params.leagueId = args.leagueId;
  if (args.week !== undefined) params.week = String(args.week);
  if (args.backTitle) params.backTitle = args.backTitle;
  return {
    pathname: '/sport/[sport]/[league]/game/[id]',
    params,
  };
}
