import React, { useMemo } from 'react';
import {
  View,
  Text,
  ScrollView,
  TouchableOpacity,
  StyleSheet,
  RefreshControl,
} from 'react-native';
import { useRouter } from 'expo-router';
import { useColorScheme } from 'react-native';
import { getTheme } from '@/constants/Colors';
import { LoadingSpinner } from '@/src/components/ui/LoadingSpinner';
import { EmptyState } from '@/src/components/ui/EmptyState';
import { useCurrentUser, useStandings } from '@/src/hooks/useStandings';
import { usePickWidget } from '@/src/hooks/useContest';
import { getLeagues } from '@/src/lib/leagues';

// ─── Screen ───────────────────────────────────────────────────────────────────

export default function HomeScreen() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const router = useRouter();

  const { data: me, isLoading: meLoading, refetch: refetchMe } = useCurrentUser();
  const leagues = useMemo(() => getLeagues(me), [me]);
  const primaryLeague = leagues[0] ?? null;

  const {
    data: pickWidget,
    isLoading: widgetLoading,
    refetch: refetchWidget,
  } = usePickWidget();

  const {
    data: standings = [],
    isLoading: standingsLoading,
    refetch: refetchStandings,
  } = useStandings(primaryLeague?.id);

  const isRefreshing = false;
  const onRefresh = () => { refetchMe(); refetchWidget(); refetchStandings(); };

  if (meLoading) {
    return <LoadingSpinner message="Loading…" fullScreen />;
  }

  if (leagues.length === 0) {
    return (
      <EmptyState
        icon="🏈"
        title="No leagues yet"
        subtitle="Join a pick'em group to get started."
      />
    );
  }

  const userName = me?.displayName || me?.email?.split('@')[0] || 'there';
  const items = pickWidget?.items ?? [];
  const overall = items.reduce(
    (acc, item) => ({ correct: acc.correct + item.correct, incorrect: acc.incorrect + item.incorrect }),
    { correct: 0, incorrect: 0 },
  );
  const accuracy =
    overall.correct + overall.incorrect > 0
      ? ((overall.correct / (overall.correct + overall.incorrect)) * 100).toFixed(1)
      : '—';

  const topStandings = standings.slice(0, 5);

  return (
    <ScrollView
      style={{ backgroundColor: theme.background }}
      contentContainerStyle={styles.container}
      showsVerticalScrollIndicator={false}
      refreshControl={
        <RefreshControl refreshing={isRefreshing} onRefresh={onRefresh} tintColor={theme.tint} />
      }
    >
      {/* Greeting */}
      <Text style={[styles.greeting, { color: theme.text }]}>Hey, {userName} 👋</Text>

      {/* ── Pick Record ────────────────────────────────────────── */}
      <View style={[styles.card, { backgroundColor: theme.card, borderColor: theme.border }]}>
        <Text style={[styles.cardTitle, { color: theme.text }]}>Your Pick Record</Text>

        {widgetLoading ? (
          <LoadingSpinner />
        ) : items.length === 0 ? (
          <Text style={[styles.empty, { color: theme.textMuted }]}>No picks recorded yet.</Text>
        ) : (
          <>
            <View style={styles.tableRow}>
              <Text style={[styles.colLeague, styles.headerCell, { color: theme.textMuted }]}>League</Text>
              <Text style={[styles.colStat, styles.headerCell, { color: theme.textMuted }]}>W</Text>
              <Text style={[styles.colStat, styles.headerCell, { color: theme.textMuted }]}>L</Text>
              <Text style={[styles.colStat, styles.headerCell, { color: theme.textMuted }]}>%</Text>
            </View>

            {items.map((item) => (
              <View key={item.leagueId} style={[styles.tableRow, { borderTopColor: theme.border, borderTopWidth: StyleSheet.hairlineWidth }]}>
                <Text style={[styles.colLeague, { color: theme.text }]} numberOfLines={1}>{item.leagueName}</Text>
                <Text style={[styles.colStat, { color: theme.success }]}>{item.correct}</Text>
                <Text style={[styles.colStat, { color: theme.error }]}>{item.incorrect}</Text>
                <Text style={[styles.colStat, { color: theme.text }]}>{(item.accuracy * 100).toFixed(1)}%</Text>
              </View>
            ))}

            {items.length > 1 && (
              <View style={[styles.tableRow, styles.totalRow, { borderTopColor: theme.border }]}>
                <Text style={[styles.colLeague, styles.bold, { color: theme.text }]}>Overall</Text>
                <Text style={[styles.colStat, styles.bold, { color: theme.success }]}>{overall.correct}</Text>
                <Text style={[styles.colStat, styles.bold, { color: theme.error }]}>{overall.incorrect}</Text>
                <Text style={[styles.colStat, styles.bold, { color: theme.text }]}>{accuracy}%</Text>
              </View>
            )}
          </>
        )}
      </View>

      {/* ── Standings preview ──────────────────────────────────── */}
      {primaryLeague && (
        <View style={[styles.card, { backgroundColor: theme.card, borderColor: theme.border }]}>
          <View style={styles.cardTitleRow}>
            <Text style={[styles.cardTitle, { color: theme.text }]}>{primaryLeague.name}</Text>
            <TouchableOpacity onPress={() => router.push('/(tabs)/standings')}>
              <Text style={[styles.seeAll, { color: theme.tint }]}>See all →</Text>
            </TouchableOpacity>
          </View>

          {standingsLoading ? (
            <LoadingSpinner />
          ) : topStandings.length === 0 ? (
            <Text style={[styles.empty, { color: theme.textMuted }]}>No standings yet.</Text>
          ) : (
            topStandings.map((s, i) => (
              <View
                key={s.userId}
                style={[
                  styles.standingRow,
                  i > 0 && { borderTopColor: theme.border, borderTopWidth: StyleSheet.hairlineWidth },
                ]}
              >
                <Text style={[styles.rank, { color: theme.textMuted }]}>{s.rank}</Text>
                <Text style={[styles.standingName, { color: theme.text }]} numberOfLines={1}>{s.name}</Text>
                <Text style={[styles.standingRecord, { color: theme.textMuted }]}>{s.totalCorrect}–{s.totalPicks - s.totalCorrect}</Text>
              </View>
            ))
          )}
        </View>
      )}

      {/* ── CTA ───────────────────────────────────────────────── */}
      <TouchableOpacity
        style={[styles.ctaButton, { backgroundColor: theme.tint }]}
        onPress={() => router.push('/(tabs)/picks')}
        activeOpacity={0.8}
      >
        <Text style={styles.ctaText}>Make Your Picks 🏈</Text>
      </TouchableOpacity>
    </ScrollView>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  container: {
    padding: 16,
    paddingBottom: 32,
    gap: 16,
  },
  greeting: {
    fontSize: 22,
    fontWeight: '700',
    marginBottom: 4,
  },
  card: {
    borderRadius: 12,
    borderWidth: StyleSheet.hairlineWidth,
    padding: 16,
    gap: 2,
  },
  cardTitle: {
    fontSize: 16,
    fontWeight: '700',
    marginBottom: 10,
  },
  cardTitleRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 10,
  },
  seeAll: {
    fontSize: 13,
    fontWeight: '600',
  },
  tableRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 6,
  },
  headerCell: {
    fontSize: 11,
    fontWeight: '700',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  colLeague: {
    flex: 2,
    fontSize: 14,
  },
  colStat: {
    width: 40,
    textAlign: 'center',
    fontSize: 14,
    fontWeight: '600',
  },
  totalRow: {
    borderTopWidth: StyleSheet.hairlineWidth,
    marginTop: 4,
    paddingTop: 8,
  },
  bold: {
    fontWeight: '700',
  },
  standingRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 8,
  },
  rank: {
    width: 28,
    fontSize: 13,
    fontWeight: '700',
  },
  standingName: {
    flex: 1,
    fontSize: 14,
  },
  standingRecord: {
    fontSize: 13,
    fontWeight: '600',
  },
  empty: {
    fontSize: 13,
    fontStyle: 'italic',
    paddingVertical: 8,
  },
  ctaButton: {
    borderRadius: 12,
    paddingVertical: 14,
    alignItems: 'center',
  },
  ctaText: {
    color: '#fff',
    fontSize: 16,
    fontWeight: '700',
  },
});
