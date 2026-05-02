import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { picksApi } from '@/src/services/api/picksApi';
import { contestOverviewApi } from '@/src/services/api/contestOverviewApi';
import { useAuthStore } from '@/src/stores/authStore';
import type { UserPick, PickWidgetResponse, ContestOverviewDto } from '@/src/types/models';
import type { SubmitPickPayload } from '@/src/services/api/picksApi';

// ─── Query key factory ────────────────────────────────────────────────────────
export const pickKeys = {
  byLeagueWeek: (leagueId: string, week: number) =>
    ['picks', leagueId, week] as const,
  widget: (year: number) => ['picks', 'widget', year] as const,
};

// ─── Hooks ────────────────────────────────────────────────────────────────────

/** Fetches the current user's picks for a given league + week. */
export function usePicks(
  leagueId: string | null | undefined,
  week: number | null | undefined,
) {
  const { user, isInitialized } = useAuthStore();
  return useQuery<UserPick[]>({
    queryKey: pickKeys.byLeagueWeek(leagueId ?? '', week ?? 0),
    queryFn: () =>
      picksApi.getByLeagueAndWeek(leagueId!, week!).then((r) => r.data),
    enabled: isInitialized && !!user && !!leagueId && !!week,
  });
}

/** Fetches the season-to-date pick record widget for the current user. */
export function usePickWidget(year = 2025) {
  const { user, isInitialized } = useAuthStore();
  return useQuery<PickWidgetResponse>({
    queryKey: pickKeys.widget(year),
    queryFn: () => picksApi.getWidget(year).then((r) => r.data),
    staleTime: 1000 * 60 * 5,
    enabled: isInitialized && !!user,
  });
}

/** Mutation: submit or update a pick. Invalidates the picks cache for that league+week. */
export function useSubmitPick() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: SubmitPickPayload) => picksApi.submitPick(payload),
    onSuccess: (_, payload) => {
      qc.invalidateQueries({
        queryKey: pickKeys.byLeagueWeek(payload.pickemGroupId, payload.week),
      });
    },
  });
}

// ─── Contest overview ─────────────────────────────────────────────────────────

export const contestKeys = {
  // sport/league are part of the cache key because the API routes by them —
  // the same contestId in two sports would collide otherwise.
  overview: (contestId: string, sport: string, league: string) =>
    ['contest', 'overview', sport, league, contestId] as const,
};

/** Fetches the full contest overview for a completed or in-progress game. */
export function useContestOverview(
  contestId: string | null | undefined,
  sport: string | null | undefined,
  league: string | null | undefined,
) {
  return useQuery<ContestOverviewDto>({
    queryKey: contestKeys.overview(contestId ?? '', sport ?? '', league ?? ''),
    queryFn: () =>
      contestOverviewApi.getOverview(contestId!, sport!, league!).then((r) => {
        // API may wrap in { data: ContestOverviewDto } or return the DTO directly
        const body = r.data as unknown;
        const wrapped = body as { data?: ContestOverviewDto };
        return wrapped.data ?? (body as ContestOverviewDto);
      }),
    enabled: !!contestId && !!sport && !!league,
    staleTime: 1000 * 60 * 2, // 2 min — box score data refreshes moderately
    refetchInterval: 30_000, // poll every 30 s while mounted for live contest updates
  });
}
