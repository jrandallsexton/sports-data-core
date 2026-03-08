import { QueryClient } from '@tanstack/react-query';

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 1000 * 60 * 5,   // 5 min – safe for schedule data
      gcTime: 1000 * 60 * 30,     // 30 min garbage collect
      retry: (failureCount, error) => {
        // Never retry 401s — the token won't magically appear
        if ((error as { response?: { status?: number } })?.response?.status === 401) return false;
        return failureCount < 2;
      },
      retryDelay: (attempt) => Math.min(1000 * 2 ** attempt, 10_000),
      refetchOnWindowFocus: false, // mobile: don't refetch on app foreground by default
    },
    mutations: {
      retry: 0,
    },
  },
});
