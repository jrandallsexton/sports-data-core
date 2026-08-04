import React, { useEffect } from 'react';
import { Modal, View, StyleSheet, ScrollView, Pressable } from 'react-native';

import { Text } from '@/src/components/ui/AppText';
import { Button } from '@/src/components/ui/Button';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { type PublicLeague } from '@/src/services/api/leaguesApi';
import {
  useAcceptInvitationMutation,
  useJoinLeagueMutation,
} from '@/src/hooks/useJoinLeagueMutation';
import { JoinClosesLabel } from './JoinClosesLabel';
import { useLiveJoinState } from './useLiveJoinState';
import {
  SPORT_LABEL,
  PICK_TYPE_LABEL,
  TIEBREAKER_LABEL,
  windowLabel,
} from './joinDisplay';

interface Props {
  /** The league to confirm joining; null closes the sheet. */
  league: PublicLeague | null;
  /**
   * When set, confirming ACCEPTS this invitation (the invitation endpoint —
   * the BE delegates to the same join path) instead of a public join. Set by
   * the Pending Invitations card; discovery surfaces leave it unset.
   */
  invitationId?: string | null;
  onCancel: () => void;
  /** Called after a successful join (parent navigates / refetches). */
  onJoined: (league: PublicLeague) => void;
}

/**
 * Confirmation sheet shown before joining a public league — joining has no
 * self-serve undo, so the details render on the join path itself (parity with
 * sd-ui's JoinLeagueConfirmDialog). Owns the join mutation + cache
 * invalidation so both the home rail and the browse screen stay thin. Also
 * serves invitation accepts (see invitationId) so every join surface shows
 * identical league details before committing.
 */
export function JoinLeagueConfirmSheet({ league, invitationId, onCancel, onJoined }: Props) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const onJoinedCb = () => {
    if (league) onJoined(league);
  };
  const publicJoin = useJoinLeagueMutation(onJoinedCb);
  const acceptInvite = useAcceptInvitationMutation(onJoinedCb);
  // Both hooks always mount (rules of hooks); invitationId picks the active one.
  const joinMutation = invitationId ? acceptInvite : publicJoin;

  // Live join state (ticks past the countdown/close instant) — a league can
  // close while the sheet is open, so Join disables rather than submitting a
  // join the BE would reject.
  const joinState = useLiveJoinState(league?.closesAtUtc ?? null, league?.isJoinable ?? true);
  const closed = joinState.kind === 'closed';

  // The sheet is a single persistent component reused across leagues; only the
  // `league` prop changes. Reset both mutations when the target changes so a
  // prior failure's error doesn't bleed into the next league's sheet.
  useEffect(() => {
    publicJoin.reset();
    acceptInvite.reset();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [league?.id]);

  const Row = ({ label, value }: { label: string; value: string }) => (
    <View style={styles.row}>
      <Text style={[styles.rowLabel, { color: theme.textMuted }]}>{label}</Text>
      <Text style={[styles.rowValue, { color: theme.text }]}>{value}</Text>
    </View>
  );

  return (
    <Modal
      visible={league != null}
      transparent
      animationType="fade"
      onRequestClose={onCancel}
    >
      <Pressable style={styles.backdrop} onPress={onCancel}>
        {/* Inner press swallows taps so tapping the card doesn't dismiss. */}
        <Pressable
          style={[styles.sheet, { backgroundColor: theme.card, borderColor: theme.border }]}
          onPress={() => {}}
        >
          {league && (
            <>
              <Text style={[styles.title, { color: theme.text }]}>Join {league.name}?</Text>
              {league.description ? (
                <Text style={[styles.desc, { color: theme.textMuted }]}>{league.description}</Text>
              ) : null}

              <ScrollView style={styles.rows} contentContainerStyle={{ gap: 2 }}>
                <Row
                  label="Sport"
                  value={`${SPORT_LABEL[league.sport] ?? league.sport} ${league.seasonYear}`}
                />
                <Row
                  label="Pick Type"
                  value={`${PICK_TYPE_LABEL[league.pickType] ?? '—'}${
                    league.useConfidencePoints ? ' with Confidence Points' : ''
                  }`}
                />
                <Row
                  label="Tiebreaker"
                  value={TIEBREAKER_LABEL[league.tiebreakerType] ?? league.tiebreakerType}
                />
                <Row
                  label="Drop Low Weeks"
                  value={league.dropLowWeeksCount > 0 ? String(league.dropLowWeeksCount) : 'None — all weeks count'}
                />
                <Row label="League Window" value={windowLabel(league.startsOn, league.endsOn)} />
                <Row label="Members" value={String(league.memberCount)} />
                <Row label="Commissioner" value={league.commissioner} />
                <View style={styles.row}>
                  <Text style={[styles.rowLabel, { color: theme.textMuted }]}>Joining</Text>
                  <JoinClosesLabel closesAtUtc={league.closesAtUtc} isJoinable={league.isJoinable} />
                </View>
              </ScrollView>

              {joinMutation.isError ? (
                <Text style={[styles.error, { color: theme.error }]}>
                  Couldn&apos;t join the league. Please try again.
                </Text>
              ) : null}

              <View style={styles.actions}>
                <Button title="Cancel" variant="ghost" onPress={onCancel} />
                <Button
                  title={closed ? 'Closed' : joinMutation.isPending ? 'Joining…' : 'Join'}
                  onPress={() => joinMutation.mutate(invitationId ?? league.id)}
                  loading={joinMutation.isPending}
                  disabled={closed}
                />
              </View>
            </>
          )}
        </Pressable>
      </Pressable>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.55)',
    justifyContent: 'center',
    padding: 20,
  },
  sheet: {
    borderRadius: 14,
    borderWidth: StyleSheet.hairlineWidth,
    padding: 20,
    maxHeight: '85%',
  },
  title: { fontSize: 18, fontWeight: '700', marginBottom: 4 },
  desc: { fontSize: 14, marginBottom: 10 },
  rows: { marginBottom: 12 },
  row: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 6,
    gap: 12,
  },
  rowLabel: { fontSize: 13, fontWeight: '600' },
  rowValue: { fontSize: 13, flexShrink: 1, textAlign: 'right' },
  error: { fontSize: 13, marginBottom: 8 },
  actions: { flexDirection: 'row', justifyContent: 'flex-end', gap: 10 },
});
