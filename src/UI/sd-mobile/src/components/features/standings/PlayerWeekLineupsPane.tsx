import React from 'react';
import { View, FlatList, ScrollView, TouchableOpacity, StyleSheet, RefreshControl } from 'react-native';
import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { LoadingSpinner } from '@/src/components/ui/LoadingSpinner';
import { EmptyState } from '@/src/components/ui/EmptyState';
import { Button } from '@/src/components/ui/Button';
import { useLeagueWeekLineups } from '@/src/hooks/usePlayerPickemLeaderboard';

// Lineup slot order (rosterLogic's SLOT_DEFS, minus the disabled DEF).
const SLOT_ORDER = ['QB', 'RB1', 'RB2', 'WR1', 'WR2', 'TE', 'FLEX', 'K'];
const SLOT_LABEL: Record<string, string> = { RB1: 'RB', RB2: 'RB', WR1: 'WR', WR2: 'WR' };

interface PlayerWeekLineupsPaneProps {
  leagueId: string;
  seasonYear: number;
  week: number | null;
  seasonWeeks: number[];
  onWeekChange: (week: number) => void;
  showBots: boolean;
  /** API user id (not the Firebase uid): the DTO members carry API ids. */
  currentUserId: string | undefined;
}

/**
 * Player Pick'em "By Week": every member's lineup for one week, players,
 * points and the stats behind them instead of the team pane's games and
 * picks (web parity: PlayerWeekLineupsTable). One card per member; another
 * member's slots stay hidden until their game locks.
 */
export function PlayerWeekLineupsPane({
  leagueId,
  seasonYear,
  week,
  seasonWeeks,
  onWeekChange,
  showBots,
  currentUserId,
}: PlayerWeekLineupsPaneProps) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const { data, isLoading, isError, refetch, isRefetching } = useLeagueWeekLineups(leagueId, seasonYear, week);

  const weekChips = (
    <View style={styles.weekRow}>
      <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.weekRowContent}>
        {seasonWeeks.map((w) => {
          const active = w === week;
          return (
            <TouchableOpacity
              key={w}
              style={[
                styles.weekChip,
                active ? { backgroundColor: theme.tint, borderColor: theme.tint } : { borderColor: theme.border },
              ]}
              onPress={() => onWeekChange(w)}
              accessibilityRole="button"
              accessibilityState={{ selected: active }}
              activeOpacity={0.7}
            >
              <Text style={[styles.weekChipText, { color: active ? theme.textOnAccent : theme.textMuted }]}>
                Wk {w}
              </Text>
            </TouchableOpacity>
          );
        })}
      </ScrollView>
    </View>
  );

  if (week == null) {
    return <EmptyState icon="🗓️" title="No weeks yet" subtitle="This league's weeks haven't been set up." />;
  }

  const members = (data?.members ?? []).filter((m) => showBots || !m.isSynthetic);

  return (
    <View style={styles.container}>
      {weekChips}
      {isLoading ? (
        <LoadingSpinner message="Loading lineups…" />
      ) : isError ? (
        <View style={styles.errorBox}>
          <Text style={[styles.errorTitle, { color: theme.text }]}>Couldn't load lineups</Text>
          <Button title="Retry" variant="secondary" size="sm" onPress={() => refetch()} />
        </View>
      ) : (
        <FlatList
          data={members}
          keyExtractor={(m) => m.userId}
          contentContainerStyle={styles.list}
          refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={theme.tint} />}
          ItemSeparatorComponent={() => <View style={{ height: 10 }} />}
          ListEmptyComponent={<EmptyState title="No lineups" subtitle="No one has a lineup this week." />}
          renderItem={({ item: m }) => {
            const isMe = m.userId === currentUserId;
            return (
              <View
                style={[
                  styles.card,
                  { backgroundColor: theme.card, borderColor: isMe ? theme.tint : theme.border },
                  isMe && { borderWidth: 2 },
                ]}
              >
                <View style={styles.cardHeader}>
                  <Text style={[styles.cardName, { color: theme.text }]} numberOfLines={1}>
                    {m.isSynthetic ? '🤖 ' : ''}
                    {m.displayName}
                    {isMe ? '  (you)' : ''}
                  </Text>
                  <Text style={[styles.cardTotal, { color: theme.tint }]}>{m.totalPoints.toFixed(1)}</Text>
                </View>
                {m.hiddenSlotCount > 0 ? (
                  <Text style={[styles.hidden, { color: theme.textMuted }]}>
                    🔒 {m.hiddenSlotCount} hidden until kickoff
                  </Text>
                ) : null}
                {SLOT_ORDER.map((slotId) => {
                  const slot = m.slots.find((s) => s.slotId === slotId);
                  if (!slot) return null;
                  return (
                    <View key={slotId} style={[styles.slotRow, { borderTopColor: theme.border }]}>
                      <Text style={[styles.slotLabel, { color: theme.textMuted }]}>{SLOT_LABEL[slotId] ?? slotId}</Text>
                      <View style={styles.slotBody}>
                        <Text style={[styles.slotName, { color: theme.text }]} numberOfLines={1}>
                          {slot.firstName.charAt(0)}. {slot.lastName}
                          <Text style={{ color: theme.textMuted }}>{`  ${slot.teamName}`}</Text>
                        </Text>
                        {/* The stats behind the points, as scored. */}
                        {slot.statLine ? (
                          <Text style={[styles.statLine, { color: theme.textMuted }]} numberOfLines={2}>
                            {slot.statLine}
                          </Text>
                        ) : null}
                      </View>
                      <Text style={[styles.slotPoints, { color: theme.text }]}>
                        {slot.points == null ? '–' : slot.points.toFixed(1)}
                      </Text>
                    </View>
                  );
                })}
                {m.slots.length === 0 && m.hiddenSlotCount === 0 ? (
                  <Text style={[styles.hidden, { color: theme.textMuted }]}>No lineup this week.</Text>
                ) : null}
              </View>
            );
          }}
        />
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1 },
  weekRow: { paddingTop: 8 },
  weekRowContent: { paddingHorizontal: 14, gap: 6 },
  weekChip: { borderWidth: 1, borderRadius: 999, paddingHorizontal: 12, paddingVertical: 6 },
  weekChipText: { fontSize: 12, fontWeight: '600' },
  list: { padding: 14, paddingBottom: 24 },
  card: { borderWidth: StyleSheet.hairlineWidth, borderRadius: 12, padding: 12 },
  cardHeader: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  cardName: { fontSize: 15, fontWeight: '700', flexShrink: 1 },
  cardTotal: { fontSize: 16, fontWeight: '800', marginLeft: 8 },
  hidden: { fontSize: 12, marginTop: 6 },
  slotRow: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    borderTopWidth: StyleSheet.hairlineWidth,
    marginTop: 8,
    paddingTop: 8,
  },
  slotLabel: { width: 40, fontSize: 11, fontWeight: '700', letterSpacing: 0.6, paddingTop: 2 },
  slotBody: { flex: 1, paddingRight: 8 },
  slotName: { fontSize: 14, fontWeight: '600' },
  statLine: { fontSize: 12, marginTop: 2 },
  slotPoints: { fontSize: 14, fontWeight: '700', paddingTop: 1 },
  errorBox: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: 10, padding: 30 },
  errorTitle: { fontSize: 16, fontWeight: '700' },
});
