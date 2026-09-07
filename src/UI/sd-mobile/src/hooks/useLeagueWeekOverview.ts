import { useQuery } from '@tanstack/react-query';
import { leaguesApi, leaguesKeys } from '@/src/services/api/leaguesApi';
import type { LeagueWeekOverview } from '@/src/types/models';

/**
 * The By Week pane's payload: contests + member roster (with readiness
 * counts) + revealed picks. Reveal enforcement is server-side (#736): the
 * payload already excludes other members' picks on un-locked contests, so
 * the client renders what it gets and never filters for secrecy itself.
 */
export function useLeagueWeekOverview(
  leagueId: string | null | undefined,
  week: number | null | undefined,
) {
  return useQuery<LeagueWeekOverview>({
    queryKey: leaguesKeys.weekOverview(leagueId ?? '', week ?? 0),
    queryFn: () => leaguesApi.getLeagueWeekOverview(leagueId!, week!).then((r) => r.data),
    enabled: !!leagueId && week != null,
    // Live games move; keep the pane reasonably fresh without hammering.
    staleTime: 1000 * 30,
  });
}
