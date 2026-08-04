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
 * public discovery list (the league leaves it), My Leagues, /user/me (which
 * drives the picks league selector), and pending invitations (joining a
 * league you were invited to fulfills the invite; the BE's already-member
 * filter drops it) — then hands the joined id to the caller for navigation.
 */
export function useJoinLeagueMutation(onJoined?: (leagueId: string) => void) {
  const invalidate = useMembershipInvalidation();

  return useMutation({
    mutationFn: (leagueId: string) => leaguesApi.joinLeague(leagueId),
    onSuccess: async (_data, leagueId) => {
      await invalidate();
      onJoined?.(leagueId);
    },
  });
}

/**
 * Accepting an invitation = joining via the invitation endpoint (the BE
 * delegates to the same join path, so every join-policy gate applies).
 * Identical invalidation set to useJoinLeagueMutation; resolves the joined
 * league id from the response body.
 */
export function useAcceptInvitationMutation(onJoined?: (leagueId: string) => void) {
  const invalidate = useMembershipInvalidation();

  return useMutation({
    mutationFn: (invitationId: string) => leaguesApi.acceptInvitation(invitationId),
    onSuccess: async (response) => {
      await invalidate();
      onJoined?.(response.data);
    },
  });
}

/** Shared invalidation for "this user's memberships just changed". */
function useMembershipInvalidation() {
  const queryClient = useQueryClient();
  return async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: leaguesKeys.public }),
      queryClient.invalidateQueries({ queryKey: leaguesKeys.mine }),
      queryClient.invalidateQueries({ queryKey: leaguesKeys.invitations }),
      queryClient.invalidateQueries({ queryKey: standingsKeys.me }),
    ]);
  };
}
