import { useMutation, useQueryClient } from '@tanstack/react-query';

import { leaguesApi, leaguesKeys } from '@/src/services/api/leaguesApi';
import { standingsKeys } from '@/src/hooks/useStandings';

/**
 * The single "join a league" mutation, shared by the invite-preview screen and
 * the discovery confirm sheet so the two can't drift (they had: the invite
 * screen previously invalidated only standingsKeys.me, leaving the discover
 * list stale after a join).
 *
 * On success it invalidates every surface a new membership affects — the
 * public discovery list (the league leaves it), My Leagues, and /user/me
 * (which drives the picks league selector) — then hands the joined id to the
 * caller for navigation.
 */
export function useJoinLeagueMutation(onJoined?: (leagueId: string) => void) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (leagueId: string) => leaguesApi.joinLeague(leagueId),
    onSuccess: async (_data, leagueId) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: leaguesKeys.public }),
        queryClient.invalidateQueries({ queryKey: leaguesKeys.mine }),
        queryClient.invalidateQueries({ queryKey: standingsKeys.me }),
      ]);
      onJoined?.(leagueId);
    },
  });
}
