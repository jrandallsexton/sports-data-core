import { useQuery } from '@tanstack/react-query';
import { leaguesApi, leaguesKeys } from '@/src/services/api/leaguesApi';
import type { LeagueWeekOverview } from '@/src/types/models';

/**
 * The By Week pane's payload: contests + member roster (with readiness
 * counts) + revealed picks. Reveal enforcement is server-side (#736): the
 * payload already excludes other members' picks on un-locked contests, so
 * the client renders what it gets and never filters for secrecy itself.
 *
 * When `poll` is true (the pane is FOCUSED — tab screens stay mounted, so
 * callers must gate on focus or every backgrounded tab would poll), the
 * query refetches on an interval WHILE the week still has unresolved games:
 * the reveal moment, LOCKED→LIVE→FINAL flips, scores, and readiness counts
 * all arrive without a manual pull-to-refresh. A fully-final week stops
 * polling entirely — nothing left to change.
 */
export function useLeagueWeekOverview(
  leagueId: string | null | undefined,
  week: number | null | undefined,
  poll = false,
) {
  return useQuery<LeagueWeekOverview>({
    queryKey: leaguesKeys.weekOverview(leagueId ?? '', week ?? 0),
    queryFn: () => leaguesApi.getLeagueWeekOverview(leagueId!, week!).then((r) => r.data),
    enabled: !!leagueId && week != null,
    // Live games move; keep the pane reasonably fresh without hammering.
    staleTime: 1000 * 30,
    refetchInterval: (query) => {
      if (!poll) return false;
      const data = query.state.data;
      if (!data || data.contests.length === 0) return false;
      const unresolved = data.contests.some((c) => !c.completedUtc && !c.finalizedUtc);
      return unresolved ? 30_000 : false;
    },
  });
}
