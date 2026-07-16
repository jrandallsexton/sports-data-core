import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { picksApi } from '@/src/services/api/picksApi';
import type { PickImportItem } from '@/src/services/api/picksApi';
import { pickKeys } from './useContest';
import { useAuthStore } from '@/src/stores/authStore';

export interface ImportSourceWithPicks {
  leagueId: string;
  name: string;
  toImport: PickImportItem[];
}

export const importKeys = {
  availability: (leagueId: string, week: number) =>
    ['import', 'availability', leagueId, week] as const,
};

/**
 * Which other (same-type) leagues the user can import picks into this one from.
 * Previews every candidate source and keeps only those with something to import.
 * A single import always draws from ONE source; this just populates the picker.
 *
 * Gate `enabled` on the picks + matchups queries having loaded so availability is
 * evaluated against confirmed current picks — React Query's enabled/queryKey give
 * us the staleness handling the web had to do manually.
 */
export function useImportAvailability(
  leagueId: string | null | undefined,
  week: number | null | undefined,
  enabled: boolean,
) {
  const { user, isInitialized } = useAuthStore();
  return useQuery<ImportSourceWithPicks[]>({
    queryKey: importKeys.availability(leagueId ?? '', week ?? 0),
    queryFn: async () => {
      const sourcesRes = await picksApi.getImportSources(leagueId!);
      const sources = sourcesRes.data ?? [];
      if (sources.length === 0) return [];

      const previews = await Promise.all(
        sources.map((s) =>
          picksApi
            .getImportPreview(leagueId!, s.leagueId)
            .then((r) => ({
              leagueId: s.leagueId,
              name: s.name,
              toImport: r.data?.toImport ?? [],
            }))
            .catch((err) => {
              // Degrade gracefully — one flaky source shouldn't hide the whole
              // import feature — but surface the failure (feeds a Sentry
              // breadcrumb) rather than swallowing it silently.
              console.warn('[import] preview failed for source', s.leagueId, err);
              return { leagueId: s.leagueId, name: s.name, toImport: [] };
            }),
        ),
      );
      return previews.filter((p) => p.toImport.length > 0);
    },
    enabled: isInitialized && !!user && !!leagueId && !!week && enabled,
    staleTime: 1000 * 30,
  });
}

/** Commits an import from one source league; refreshes picks + availability. */
export function useImportPicks() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (vars: {
      leagueId: string;
      week: number;
      sourceLeagueId: string;
      contestIds: string[];
    }) =>
      picksApi
        .executeImport(vars.leagueId, vars.sourceLeagueId, vars.contestIds)
        .then((r) => r.data),
    onSuccess: (_res, vars) => {
      qc.invalidateQueries({ queryKey: pickKeys.byLeagueWeek(vars.leagueId, vars.week) });
      qc.invalidateQueries({ queryKey: importKeys.availability(vars.leagueId, vars.week) });
    },
  });
}
