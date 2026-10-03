import { useQuery } from '@tanstack/react-query';
import { picksApi } from '@/src/services/api/picksApi';
import { useAuthStore } from '@/src/stores/authStore';
import type { PickAccuracyByWeek } from '@/src/types/models';

// ─── Query key factory ────────────────────────────────────────────────────────
export const pickAccuracyKeys = {
  chart: ['picks', 'accuracy-chart'] as const,
};

/**
 * The current user's pick accuracy by week, every league they have ever
 * belonged to. Raw wire shape; filter with `activeAccuracyLeagues` before
 * rendering. Same auth gating and staleTime as useCurrentUser so Home's two
 * boot queries dedupe and refresh together; pull-to-refresh invalidates
 * `pickAccuracyKeys.chart` explicitly.
 */
export function usePickAccuracy() {
  const { user, isInitialized } = useAuthStore();

  return useQuery<PickAccuracyByWeek[]>({
    queryKey: pickAccuracyKeys.chart,
    queryFn: () => picksApi.getAccuracyChart().then((r) => r.data),
    staleTime: 1000 * 60 * 5,
    enabled: isInitialized && user !== null,
  });
}
