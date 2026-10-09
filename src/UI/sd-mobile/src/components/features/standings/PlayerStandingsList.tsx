import React from 'react';
import { View, FlatList, ScrollView, StyleSheet, RefreshControl } from 'react-native';
import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { Colors, getTheme } from '@/constants/Colors';
import { LoadingSpinner } from '@/src/components/ui/LoadingSpinner';
import { EmptyState } from '@/src/components/ui/EmptyState';
import { Button } from '@/src/components/ui/Button';
import { usePlayerStandings } from '@/src/hooks/usePlayerPickemLeaderboard';

interface PlayerStandingsListProps {
  leagueId: string;
  seasonYear: number;
  /** API user id (not the Firebase uid): the DTO rows carry API ids. */
  currentUserId: string | undefined;
}

/**
 * Player Pick'em season standings: members by cumulative lineup points, with
 * weekly wins. The team-league counterpart is the Standings screen's
 * StandingRow list (web parity: PlayerStandingsTable).
 */
export function PlayerStandingsList({ leagueId, seasonYear, currentUserId }: PlayerStandingsListProps) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const { data, isLoading, isError, refetch, isRefetching } = usePlayerStandings(leagueId, seasonYear);

  if (isLoading) return <LoadingSpinner message="Loading standings…" />;

  if (isError) {
    return (
      <ScrollView
        contentContainerStyle={styles.errorContent}
        refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={theme.tint} />}
      >
        <Text style={[styles.errorTitle, { color: theme.text }]}>Couldn't load standings</Text>
        <Button title="Retry" variant="secondary" size="sm" onPress={() => refetch()} />
      </ScrollView>
    );
  }

  const rows = data?.rows ?? [];
  // Competition rank by total points: ties share a rank, the next skips.
  const rankOf = (points: number) => 1 + rows.filter((r) => r.totalPoints > points).length;

  return (
    <FlatList
      data={rows}
      keyExtractor={(r) => r.userId}
      contentContainerStyle={styles.list}
      refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={theme.tint} />}
      ItemSeparatorComponent={() => <View style={{ height: 6 }} />}
      ListHeaderComponent={
        <View style={styles.header}>
          <Text style={[styles.headerText, { color: theme.textMuted }]}>Rank</Text>
          <Text style={[styles.headerText, { color: theme.textMuted, flex: 1, marginLeft: 48 }]}>Member</Text>
          <Text style={[styles.headerText, { color: theme.textMuted }]}>Points</Text>
        </View>
      }
      ListEmptyComponent={<EmptyState title="No standings yet" subtitle="Scores appear once lineups are scored." />}
      renderItem={({ item }) => {
        const isMe = item.userId === currentUserId;
        return (
          <View
            style={[
              styles.row,
              { backgroundColor: isMe ? Colors.brand.navy : theme.card, borderColor: theme.border },
            ]}
          >
            <Text style={[styles.rank, { color: isMe ? '#fff' : theme.textMuted }]}>{rankOf(item.totalPoints)}</Text>
            <View style={styles.nameBox}>
              <Text style={[styles.name, { color: isMe ? '#fff' : theme.text }]} numberOfLines={1}>
                {item.displayName}
                {isMe ? '  (you)' : ''}
              </Text>
              <Text style={[styles.sub, { color: isMe ? 'rgba(255,255,255,0.7)' : theme.textMuted }]}>
                {item.weeklyWins > 0 ? `🏆 ${item.weeklyWins} · ` : ''}
                {item.weeks.length} {item.weeks.length === 1 ? 'week' : 'weeks'}
              </Text>
            </View>
            <Text style={[styles.points, { color: isMe ? '#fff' : theme.text }]}>
              {item.totalPoints.toFixed(1)}
            </Text>
          </View>
        );
      }}
    />
  );
}

const styles = StyleSheet.create({
  list: { padding: 14, paddingBottom: 24 },
  header: { flexDirection: 'row', paddingHorizontal: 16, paddingVertical: 8, alignItems: 'center' },
  headerText: { fontSize: 11, fontWeight: '700', textTransform: 'uppercase', letterSpacing: 0.5 },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    borderRadius: 12,
    borderWidth: StyleSheet.hairlineWidth,
    paddingHorizontal: 16,
    paddingVertical: 12,
  },
  rank: { width: 40, fontSize: 16, fontWeight: '700' },
  nameBox: { flex: 1, marginLeft: 8 },
  name: { fontSize: 15, fontWeight: '600' },
  sub: { fontSize: 12, marginTop: 2 },
  points: { fontSize: 16, fontWeight: '700' },
  errorContent: { flexGrow: 1, alignItems: 'center', justifyContent: 'center', padding: 40, gap: 10 },
  errorTitle: { fontSize: 16, fontWeight: '700' },
});
