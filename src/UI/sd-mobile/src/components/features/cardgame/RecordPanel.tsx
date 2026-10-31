import React from 'react';
import { StyleSheet, View } from 'react-native';
import { Text } from '@/src/components/ui/AppText';
import type { StatsSummary } from '@/src/utils/minigames/stats';
import type { MiniGameStats } from '@/src/utils/minigames/types';
import { CG, FONT_MONO } from './palette';

// The player's record on the start screen (docs/features/mini-game-results.md):
// W-L, average margins, streaks, and the last 10 results as chips, newest
// first. Private to the player.

const margin = (n: number | null, sign: '+' | '-') => (n === null ? '—' : `${sign}${n.toFixed(1)}`);

export function RecordPanel({ stats, summary }: { stats: MiniGameStats; summary: StatsSummary }) {
  if (!summary.games) {
    return (
      <View style={styles.panel} testID="cardgame-record">
        <Text style={styles.label}>YOUR RECORD</Text>
        <Text style={styles.empty}>No games yet. Your record starts with the first final whistle.</Text>
      </View>
    );
  }

  return (
    <View style={styles.panel} testID="cardgame-record">
      <Text style={styles.label}>YOUR RECORD</Text>
      <Text style={styles.record}>{summary.record}</Text>
      <View style={styles.row}>
        <Stat label="Avg win" value={margin(summary.avgWinMargin, '+')} />
        <Stat label="Avg loss" value={margin(summary.avgLossMargin, '-')} />
        <Stat label="Streak" value={summary.streak ?? '—'} />
        <Stat label="Best" value={stats.bestWinStreak ? `W${stats.bestWinStreak}` : '—'} />
      </View>
      <View style={styles.chips} accessibilityLabel={`Last ${stats.recent.length} games`}>
        {stats.recent.map((r) => (
          <View
            key={r.id}
            style={[styles.chip, r.outcome === 'Win' ? styles.win : r.outcome === 'Loss' ? styles.loss : styles.tie]}
          >
            <Text style={styles.chipText}>{r.outcome[0]}</Text>
          </View>
        ))}
      </View>
    </View>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.stat}>
      <Text style={styles.statValue}>{value}</Text>
      <Text style={styles.statLabel}>{label}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  panel: {
    borderWidth: 2,
    borderColor: CG.ink,
    borderRadius: 8,
    backgroundColor: CG.paper,
    padding: 12,
    gap: 8,
  },
  label: { fontFamily: FONT_MONO, fontSize: 11, letterSpacing: 1, color: CG.inkSoft },
  empty: { fontSize: 14, lineHeight: 20, color: CG.ink },
  record: { fontSize: 28, fontWeight: '800', color: CG.ink },
  row: { flexDirection: 'row', justifyContent: 'space-between' },
  stat: { alignItems: 'flex-start' },
  statValue: { fontSize: 16, fontWeight: '700', color: CG.ink },
  statLabel: { fontSize: 11, color: CG.inkSoft },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: 4 },
  chip: {
    width: 22,
    height: 22,
    borderRadius: 4,
    alignItems: 'center',
    justifyContent: 'center',
  },
  win: { backgroundColor: CG.turf },
  loss: { backgroundColor: CG.warn },
  tie: { backgroundColor: CG.inkSoft },
  chipText: { fontSize: 12, fontWeight: '700', color: CG.chalk },
});
