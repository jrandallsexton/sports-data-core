import { createContext, useContext } from 'react';
import { useParams } from 'react-router-dom';

/**
 * Sport context provides the current sport and league from route params
 * or defaults to football/ncaa. Components use useSport() to get the
 * current sport context for building links and API calls.
 */

const SportContext = createContext({
  sport: 'football',
  league: 'ncaa',
  sportPath: 'sport/football/ncaa',
});

/**
 * Supported sport/league combinations.
 * Used for validation and display names.
 */
export const SPORTS = {
  'football/ncaa': { sport: 'football', league: 'ncaa', label: 'NCAA Football' },
  'football/nfl': { sport: 'football', league: 'nfl', label: 'NFL' },
  'baseball/mlb': { sport: 'baseball', league: 'mlb', label: 'MLB' },
};

export const DEFAULT_SPORT = 'football';
export const DEFAULT_LEAGUE = 'ncaa';

/**
 * Provider that reads sport/league from route params or uses defaults.
 * Wrap routes that need sport context with this provider.
 */
export function SportProvider({ children }) {
  const { sport, league } = useParams();

  const currentSport = sport || DEFAULT_SPORT;
  const currentLeague = league || DEFAULT_LEAGUE;
  const sportPath = `sport/${currentSport}/${currentLeague}`;

  return (
    <SportContext.Provider value={{ sport: currentSport, league: currentLeague, sportPath }}>
      {children}
    </SportContext.Provider>
  );
}

/**
 * Hook to access the current sport context.
 * Returns { sport, league, sportPath } where sportPath is "sport/football/ncaa" etc.
 */
export function useSport() {
  return useContext(SportContext);
}

/**
 * Helper to build a team link for the current sport context.
 */
export function teamPath(sport, league, slug, seasonYear) {
  return `/app/sport/${sport}/${league}/team/${slug}${seasonYear ? `/${seasonYear}` : ''}`;
}

/**
 * Helper to build a contest link for the current sport context.
 */
export function contestPath(sport, league, contestId) {
  return `/app/sport/${sport}/${league}/contest/${contestId}`;
}
