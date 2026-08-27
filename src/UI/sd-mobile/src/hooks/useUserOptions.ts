import { useQuery, type QueryClient } from '@tanstack/react-query';

import { usersApi } from '@/src/services/api/usersApi';
import type { UserDto, UserOptions } from '@/src/types/models';
import { useAuthStore } from '@/src/stores/authStore';
import { standingsKeys } from '@/src/hooks/useStandings';

/**
 * The signed-in user's typed options (UserOptionsDto), DERIVED from the
 * /user/me payload — options ride the same response, so this shares the
 * me query's cache instead of making a second round-trip (the old
 * /user/me/options GET). Consumers treat `undefined` (loading / fetch
 * failure / pre-rollout payloads without the field) as all-defaults via
 * {@link shouldShowGambling}-style predicates.
 */
export function useUserOptions() {
  const { user, isInitialized } = useAuthStore();
  return useQuery<UserDto, Error, UserOptions | undefined>({
    queryKey: standingsKeys.me,
    queryFn: () => usersApi.getMe().then((r) => r.data),
    select: (me) => me.options,
    enabled: isInitialized && !!user,
    staleTime: 1000 * 60 * 5,
  });
}

/**
 * Cache helpers for the Profile toggle's optimistic write: options live
 * INSIDE the cached /user/me payload now, so the mutation must patch
 * that entry rather than a standalone options key.
 */
export function getUserOptionsCache(queryClient: QueryClient): UserOptions | undefined {
  return queryClient.getQueryData<UserDto>(standingsKeys.me)?.options;
}

export function setUserOptionsCache(queryClient: QueryClient, next: UserOptions | undefined): void {
  queryClient.setQueryData<UserDto>(standingsKeys.me, (me) =>
    me ? { ...me, options: next } : me
  );
}
