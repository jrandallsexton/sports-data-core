import React, { useState } from 'react';
import { View, StyleSheet, TouchableOpacity } from 'react-native';
import { useRouter } from 'expo-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import {
  leaguesApi,
  leaguesKeys,
  type PendingInvitation,
} from '@/src/services/api/leaguesApi';
import { JoinLeagueConfirmSheet } from '@/src/components/features/leagues/JoinLeagueConfirmSheet';
import { JoinClosesLabel } from '@/src/components/features/leagues/JoinClosesLabel';
import { SPORT_ICON, SPORT_LABEL } from '@/src/components/features/leagues/joinDisplay';

/**
 * "Pending Invitations" home card — league invites awaiting the user's
 * answer. Closes the gap where a push notification (and its launcher badge)
 * implied something was waiting in the app but no in-app surface showed it.
 *
 * Accept opens the SAME JoinLeagueConfirmSheet used by public-league
 * discovery (each invitation embeds the league's full parameters), so every
 * join surface shows identical details before committing. Decline is inline.
 * Mirrors web's PendingInvitesCard. Renders nothing when there are none.
 */
export function PendingInvitesCard() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const router = useRouter();
  const queryClient = useQueryClient();
  // The invitation whose league-parameters sheet is open.
  const [confirming, setConfirming] = useState<PendingInvitation | null>(null);
  // invitationId currently failing decline, for a retryable inline error.
  const [errorId, setErrorId] = useState<string | null>(null);

  const { data } = useQuery({
    queryKey: leaguesKeys.invitations,
    queryFn: async () => (await leaguesApi.getPendingInvitations()).data,
  });

  const decline = useMutation({
    mutationFn: (invitationId: string) => leaguesApi.declineInvitation(invitationId),
    onSuccess: async () => {
      setErrorId(null);
      // Declining only affects the invitations list.
      await queryClient.invalidateQueries({ queryKey: leaguesKeys.invitations });
    },
    onError: (_err, invitationId) => setErrorId(invitationId),
  });

  const invites = data ?? [];
  if (invites.length === 0) return null;

  return (
    <View style={[styles.card, { backgroundColor: theme.card, borderColor: theme.border }]}>
      <Text style={[styles.eyebrow, { color: theme.tint }]}>PENDING INVITATIONS</Text>

      <View>
        {invites.map((invite: PendingInvitation) => {
          const league = invite.league;
          const declining = decline.isPending && decline.variables === invite.invitationId;
          return (
            <View
              key={invite.invitationId}
              style={[styles.row, { borderTopColor: theme.border }]}
            >
              <View style={styles.info}>
                <Text style={[styles.name, { color: theme.text }]} numberOfLines={1}>
                  {league.name}
                </Text>
                {/* Wrapping row so the JoinClosesLabel (with its live
                    countdown) can flow after the static meta — same shape
                    as JoinableLeaguesCard's metaRow. */}
                <View style={styles.metaRow}>
                  <Text style={[styles.meta, { color: theme.textMuted }]}>
                    {SPORT_ICON[league.sport] ?? '🏆'} {SPORT_LABEL[league.sport] ?? league.sport}{' '}
                    {league.seasonYear} · Invited by {invite.invitedBy} ·{' '}
                  </Text>
                  <JoinClosesLabel
                    closesAtUtc={league.closesAtUtc}
                    isJoinable={league.isJoinable}
                    verb="Expires"
                    style={styles.meta}
                  />
                </View>
                {errorId === invite.invitationId ? (
                  <Text style={[styles.errorText, { color: theme.error }]}>
                    Failed — try again
                  </Text>
                ) : null}
              </View>

              <View style={styles.actions}>
                <TouchableOpacity
                  onPress={() => decline.mutate(invite.invitationId)}
                  disabled={declining}
                  style={[styles.declineBtn, { borderColor: theme.border }]}
                  accessibilityRole="button"
                  accessibilityLabel={`Decline invitation to ${league.name}`}
                >
                  <Text style={[styles.declineText, { color: theme.textMuted }]}>
                    {declining ? '…' : 'Decline'}
                  </Text>
                </TouchableOpacity>
                <TouchableOpacity
                  onPress={() => setConfirming(invite)}
                  disabled={declining}
                  style={[styles.acceptBtn, { backgroundColor: theme.tint }]}
                  accessibilityRole="button"
                  accessibilityLabel={`Accept invitation to ${league.name}`}
                >
                  <Text style={[styles.acceptText, { color: theme.textOnAccent }]}>Accept</Text>
                </TouchableOpacity>
              </View>
            </View>
          );
        })}
      </View>

      {/* Same sheet as discovery — league parameters shown before joining.
          invitationId routes the confirm through the accept endpoint. */}
      <JoinLeagueConfirmSheet
        league={confirming?.league ?? null}
        invitationId={confirming?.invitationId ?? null}
        onCancel={() => setConfirming(null)}
        onJoined={(joined) => {
          setConfirming(null);
          router.push({ pathname: '/(tabs)/picks', params: { leagueId: joined.id } } as never);
        }}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    borderRadius: 14,
    borderWidth: StyleSheet.hairlineWidth,
    padding: 16,
  },
  eyebrow: {
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 1.5,
    marginBottom: 6,
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 12,
    paddingVertical: 10,
    borderTopWidth: StyleSheet.hairlineWidth,
  },
  info: { flex: 1, minWidth: 0 },
  name: { fontSize: 15, fontWeight: '600' },
  metaRow: { flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', marginTop: 2 },
  meta: { fontSize: 12 },
  errorText: { fontSize: 12, marginTop: 2, fontWeight: '600' },
  actions: { flexDirection: 'row', gap: 8 },
  declineBtn: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 8,
    borderWidth: 1,
  },
  declineText: { fontSize: 13, fontWeight: '600' },
  acceptBtn: {
    paddingHorizontal: 14,
    paddingVertical: 6,
    borderRadius: 8,
  },
  acceptText: { fontSize: 13, fontWeight: '700' },
});
