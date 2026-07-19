import React, { useMemo, useState } from 'react';
import {
  View,
  FlatList,
  StyleSheet,
  RefreshControl,
} from 'react-native';
import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { Colors, getTheme } from '@/constants/Colors';
import { LoadingSpinner } from '@/src/components/ui/LoadingSpinner';
import { EmptyState } from '@/src/components/ui/EmptyState';
import { StandingsControls } from '@/src/components/features/selectors/StandingsControls';
import { useStandings, useUserLeagues } from '@/src/hooks/useStandings';
import { useSeasonLeagueSelection } from '@/src/hooks/useSeasonLeagueSelection';
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

  // Source the league list from getUserLeagues (includes deactivated) so past-
  // season and recently-ended leagues are reachable — /user/me is active-only.
  const { data: allLeagues = [], isLoading: leaguesLoading } = useUserLeagues();

  // Season/league selection state machine (derivation + reconciliation).
  const {
    seasons,
    selectedSeason,
    setSelectedSeason,
    seasonLeagues,
    selectedLeagueId,
    setSelectedLeagueId,
    canFilterEnded,
    showEnded,
    setShowEnded,
  } = useSeasonLeagueSelection(allLeagues);

  const [showBots, setShowBots] = useState(true);

  const {
    data: standings = [],
    isLoading,
    refetch,
    isRefetching,
    isError,
  } = useStandings(selectedLeagueId);

  const visibleStandings = useMemo(
    () => (showBots ? standings : standings.filter((s) => !s.isSynthetic)),
    [standings, showBots],
  );

  if (leaguesLoading) {
    return <LoadingSpinner message="Loading standings…" fullScreen />;
  }

  if (allLeagues.length === 0) {
    return (
      <EmptyState
        icon="🏆"
        title="No leagues yet"
        subtitle="Join or create a league to see standings."
      />
    );
  }

  const standingsLoading = isLoading || !selectedLeagueId;

  return (
    <View style={[styles.container, { backgroundColor: theme.background }]}>
      <StandingsControls
        seasons={seasons}
        selectedSeason={selectedSeason}
        onSeasonChange={setSelectedSeason}
        leagues={seasonLeagues}
        selectedLeagueId={selectedLeagueId}
        onLeagueChange={setSelectedLeagueId}
        canFilterEnded={canFilterEnded}
        showEnded={showEnded}
        onToggleEnded={() => setShowEnded((v) => !v)}
        showBots={showBots}
        onToggleBots={() => setShowBots((v) => !v)}
      />

      {standingsLoading ? (
        <LoadingSpinner message="Loading standings…" />
      ) : isError ? (
        <EmptyState
          icon="🏆"
          title="No standings yet"
          subtitle="Standings appear once the season begins."
        />
      ) : (
        <FlatList
          data={visibleStandings}
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
      )}
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
