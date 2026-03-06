import { useQuery } from '@tanstack/react-query';
import { standingsApi } from '@/src/services/api/standingsApi';
import { useAuthStore } from '@/src/stores/authStore';
import type { Standing, UserDto } from '@/src/types/models';

// ─── Query key factory ────────────────────────────────────────────────────────
export const standingsKeys = {
  byLeague: (leagueId: string) => ['standings', leagueId] as const,
  me: ['user', 'me'] as const,
};

// ─── Hooks ────────────────────────────────────────────────────────────────────

/** Fetches the leaderboard for a given league. */
export function useStandings(leagueId: string | null | undefined) {
  return useQuery<Standing[]>({
    queryKey: standingsKeys.byLeague(leagueId ?? ''),
    queryFn: () => standingsApi.getByLeague(leagueId!).then((r) => r.data),
    enabled: !!leagueId,
  });
}

/**
 * Fetches the current user's profile from /user/me.
 * Returns UserDto which includes the user's leagues dict.
 *
 * Waits for Firebase auth to initialise so the request carries a valid JWT.
 */
export function useCurrentUser() {
  const { user, isInitialized } = useAuthStore();

  return useQuery<UserDto>({
    queryKey: standingsKeys.me,
    queryFn: () => standingsApi.getMe().then((r) => r.data),
    staleTime: 1000 * 60 * 5,
    enabled: isInitialized && user !== null,
  });
}
