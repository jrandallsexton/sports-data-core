import React from 'react';
import {
  View,
  Text,
  FlatList,
  StyleSheet,
  RefreshControl,
} from 'react-native';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { Colors, getTheme } from '@/constants/Colors';
import { LoadingSpinner } from '@/src/components/ui/LoadingSpinner';
import { EmptyState } from '@/src/components/ui/EmptyState';
import { useStandings, useCurrentUser } from '@/src/hooks/useStandings';
import { useAuthStore } from '@/src/stores/authStore';
import type { Standing } from '@/src/types/models';

// ─── Row ──────────────────────────────────────────────────────────────────────

function StandingRow({
  standing,
  isMe,
}: {
  standing: Standing;
  isMe: boolean;
}) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  return (
    <View
      style={[
        styles.row,
        { backgroundColor: isMe ? Colors.brand.navy : theme.card, borderColor: theme.border },
      ]}
    >
      {/* Rank */}
      <View style={styles.rankBox}>
        <Text style={[styles.rank, { color: isMe ? '#fff' : theme.textMuted }]}>
          {standing.rank}
        </Text>
      </View>

      {/* Name */}
      <View style={styles.nameBox}>
        <Text
          style={[styles.name, { color: isMe ? '#fff' : theme.text }]}
          numberOfLines={1}
        >
          {standing.name}
          {isMe ? '  (you)' : ''}
        </Text>
        <Text style={[styles.accuracy, { color: isMe ? 'rgba(255,255,255,0.7)' : theme.textMuted }]}>
          {standing.totalCorrect}/{standing.totalPicks} ({(standing.pickAccuracy * 100).toFixed(0)}%)
        </Text>
      </View>

      {/* Points */}
      <View style={styles.recordBox}>
        <Text style={[styles.record, { color: isMe ? '#fff' : theme.text }]}>
          {standing.totalPoints} pts
        </Text>
        <Text style={[styles.points, { color: isMe ? 'rgba(255,255,255,0.7)' : theme.textMuted }]}>
          Wk {standing.currentWeekPoints}
        </Text>
      </View>
    </View>
  );
}

// ─── Screen ───────────────────────────────────────────────────────────────────

export default function StandingsScreen() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const { user } = useAuthStore();
  const { data: me } = useCurrentUser();
  const firstLeagueId = me?.leagues?.[0]?.id;

  const {
    data: standings = [],
    isLoading,
    refetch,
    isRefetching,
    isError,
  } = useStandings(firstLeagueId);

  if (isLoading) {
    return <LoadingSpinner message="Loading standings…" fullScreen />;
  }

  if (isError || !firstLeagueId) {
    return (
      <EmptyState
        icon="🏆"
        title="No standings yet"
        subtitle="Standings appear once the season begins."
      />
    );
  }

  return (
    <View style={[styles.container, { backgroundColor: theme.background }]}>
      <FlatList
        data={standings}
        keyExtractor={(item) => item.userId}
        renderItem={({ item }) => (
          <StandingRow
            standing={item}
            isMe={item.userId === user?.uid}
          />
        )}
        contentContainerStyle={styles.list}
        showsVerticalScrollIndicator={false}
        refreshControl={
          <RefreshControl
            refreshing={isRefetching}
            onRefresh={refetch}
            tintColor={theme.tint}
          />
        }
        ListHeaderComponent={
          <View style={styles.listHeader}>
            <Text style={[styles.listHeaderText, { color: theme.textMuted }]}>Rank</Text>
            <Text style={[styles.listHeaderText, { color: theme.textMuted, flex: 1, marginLeft: 48 }]}>Player</Text>
            <Text style={[styles.listHeaderText, { color: theme.textMuted }]}>Points</Text>
          </View>
        }
        ItemSeparatorComponent={() => <View style={{ height: 6 }} />}
        ListEmptyComponent={
          <EmptyState title="No standings yet" subtitle="Be the first to make your picks!" />
        }
      />
    </View>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  container: { flex: 1 },
  list: { padding: 14, paddingBottom: 24 },
  listHeader: {
    flexDirection: 'row',
    paddingHorizontal: 16,
    paddingVertical: 8,
    alignItems: 'center',
  },
  listHeaderText: { fontSize: 11, fontWeight: '700', textTransform: 'uppercase', letterSpacing: 0.5 },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    borderRadius: 12,
    borderWidth: StyleSheet.hairlineWidth,
    paddingHorizontal: 16,
    paddingVertical: 14,
    gap: 10,
  },
  rankBox: { width: 28, alignItems: 'center' },
  rank: { fontSize: 16, fontWeight: '800' },
  nameBox: { flex: 1, gap: 2 },
  name: { fontSize: 15, fontWeight: '600' },
  accuracy: { fontSize: 12 },
  recordBox: { alignItems: 'flex-end', gap: 2 },
  record: { fontSize: 15, fontWeight: '700' },
  points: { fontSize: 11 },
});
