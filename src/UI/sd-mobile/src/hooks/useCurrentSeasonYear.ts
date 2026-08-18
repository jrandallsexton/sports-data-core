import { useQuery } from '@tanstack/react-query';
import { seasonApi } from '@/src/services/api/seasonApi';

/**
 * Resolves the current (or upcoming) season year for a sport from
 * /api/{sport}/{league}/seasons/current — the same source the home
 * countdown uses. Exists so rankings surfaces never hardcode a season
 * year: sd-ui's 2025-era literals froze its widget to a finished season
 * at rollover, and this is the mobile twin of the hook that fixed it.
 *
 * seasonYear is null while loading and when no season is sourced (a
 * valid state for an unsupported sport, not an error).
 */
export function useCurrentSeasonYear(sport = 'football', league = 'ncaa') {
  const { data, isLoading } = useQuery({
    queryKey: ['currentSeason', sport, league],
    queryFn: async () => (await seasonApi.getCurrentSeason(sport, league)).data,
    staleTime: 60 * 60 * 1000,
  });

  return { seasonYear: data?.seasonYear ?? null, loading: isLoading };
}
