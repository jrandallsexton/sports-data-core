import { useQuery } from '@tanstack/react-query';
import { matchupsApi } from '@/src/services/api/matchupsApi';
import type { LeagueMatchupsResponse } from '@/src/types/models';

// ─── Query key factory ────────────────────────────────────────────────────────
export const matchupKeys = {
  byLeagueWeek: (leagueId: string, week: number) =>
    ['matchups', leagueId, week] as const,
};

// ─── Hooks ────────────────────────────────────────────────────────────────────

/**
 * Fetches all matchups (and pick metadata) for a given league + week.
 * Returns the full LeagueMatchupsResponse so callers can read
 * `pickType` and `useConfidencePoints` alongside the matchups list.
 */
export function useMatchups(
  leagueId: string | null | undefined,
  week: number | null | undefined,
) {
  return useQuery<LeagueMatchupsResponse>({
    queryKey: matchupKeys.byLeagueWeek(leagueId ?? '', week ?? 0),
    queryFn: () =>
      matchupsApi.getByLeagueAndWeek(leagueId!, week!).then((r) => r.data),
    enabled: !!leagueId && !!week,
    staleTime: 1000 * 30, // refresh every 30 s while games are live
  });
}
