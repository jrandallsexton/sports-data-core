import { useQuery } from '@tanstack/react-query';

import { usersApi } from '@/src/services/api/usersApi';
import type { UserOptions } from '@/src/types/models';
import { useAuthStore } from '@/src/stores/authStore';

export const userOptionsKeys = {
  me: ['user', 'me', 'options'] as const,
};

/**
 * The signed-in user's typed options (UserOptionsDto). Consumers treat
 * `undefined` (loading / fetch failure) as all-defaults via
 * {@link shouldShowGambling}-style predicates, so a failed fetch degrades to
 * the safe defaults rather than blocking render. Long staleTime: options only
 * change from the Profile toggle, which writes the cache directly.
 */
export function useUserOptions() {
  const { user, isInitialized } = useAuthStore();
  return useQuery<UserOptions>({
    queryKey: userOptionsKeys.me,
    queryFn: () => usersApi.getUserOptions().then((r) => r.data),
    enabled: isInitialized && !!user,
    staleTime: 1000 * 60 * 60,
  });
}
