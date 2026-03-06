import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { picksApi } from '@/src/services/api/picksApi';
import type { UserPick, PickWidgetResponse } from '@/src/types/models';
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
  return useQuery<UserPick[]>({
    queryKey: pickKeys.byLeagueWeek(leagueId ?? '', week ?? 0),
    queryFn: () =>
      picksApi.getByLeagueAndWeek(leagueId!, week!).then((r) => r.data),
    enabled: !!leagueId && !!week,
  });
}

/** Fetches the season-to-date pick record widget for the current user. */
export function usePickWidget(year = 2025) {
  return useQuery<PickWidgetResponse>({
    queryKey: pickKeys.widget(year),
    queryFn: () => picksApi.getWidget(year).then((r) => r.data),
    staleTime: 1000 * 60 * 5,
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
