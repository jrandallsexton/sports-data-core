import React, { useEffect, useMemo, useState } from 'react';
import { View, StyleSheet, ScrollView, TouchableOpacity } from 'react-native';

import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { formatPercent, meanAccuracy, seasonTotals } from '@/src/lib/pickAccuracy';
import type { PickAccuracyByWeek } from '@/src/types/models';

// Plot area height in px. Bars scale to 100% of this; labels sit above it.
const PLOT_HEIGHT = 120;

interface Props {
  /** Already filtered to active leagues with graded weeks (see activeAccuracyLeagues). */
  leagues: PickAccuracyByWeek[];
}

/**
 * Tier 1 home card, in season: the user's pick accuracy by week for one of
 * their leagues, with the season mean as a reference line. Mobile port of
 * sd-ui's `widgets/PickAccuracyWidget.jsx` (Recharts). Drawn with plain
 * Views on purpose: no charting package, so this ships OTA with no store
 * build and no new native dependency.
 *
 * Renders nothing when given no leagues; the caller (Home) decides whether
 * this or the off-season countdown owns the slot, so the two never flicker
 * against each other.
 *
 * Card recipe matches RankingsCard / JoinableLeaguesCard exactly (radius 14,
 * hairline border, 16 padding, 11pt eyebrow).
 */
export function PickAccuracyCard({ leagues }: Props) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const [selectedId, setSelectedId] = useState<string | null>(leagues[0]?.leagueId ?? null);
  // If the selected league disappears (left it, refetch reordered), fall back
  // to the first rather than rendering an empty chart.
  useEffect(() => {
    if (!leagues.some((l) => l.leagueId === selectedId)) {
      setSelectedId(leagues[0]?.leagueId ?? null);
    }
  }, [leagues, selectedId]);

  const league = leagues.find((l) => l.leagueId === selectedId) ?? leagues[0];

  const { weeks, mean, totals } = useMemo(() => {
    const w = league?.weeklyAccuracy ?? [];
    return { weeks: w, mean: meanAccuracy(w), totals: seasonTotals(w) };
  }, [league]);

  if (!league || weeks.length === 0) return null;

  const multiLeague = leagues.length > 1;
  const seasonPct = league.overallAccuracyPercent;

  return (
    <View style={[styles.card, { backgroundColor: theme.card, borderColor: theme.border }]}>
      <View style={styles.headerRow}>
        <Text style={[styles.eyebrow, { color: theme.tint }]}>PICK ACCURACY</Text>
        {/* One league: name in the header, no picker. */}
        {!multiLeague && (
          <Text style={[styles.leagueName, { color: theme.textSecondary }]} numberOfLines={1}>
            {league.leagueName}
          </Text>
        )}
      </View>

      <View style={styles.summaryRow}>
        <Text style={[styles.summaryValue, { color: theme.text }]} testID="season-percent">
          {formatPercent(seasonPct)}
        </Text>
        <Text style={[styles.summaryLabel, { color: theme.textMuted }]}>
          season · {totals.correct}/{totals.total} correct
        </Text>
      </View>

      {/* Same recipe as ByWeekPane's week chips: plain horizontal
          ScrollView, TouchableOpacity with the button role. */}
      {multiLeague && (
        <ScrollView
          horizontal
          showsHorizontalScrollIndicator={false}
          contentContainerStyle={styles.chips}
        >
          {leagues.map((l) => {
            const active = l.leagueId === league.leagueId;
            return (
              <TouchableOpacity
                key={l.leagueId}
                onPress={() => setSelectedId(l.leagueId)}
                accessibilityRole="button"
                accessibilityLabel={`Show ${l.leagueName}`}
                accessibilityState={{ selected: active }}
                activeOpacity={0.75}
                style={[
                  styles.chip,
                  {
                    backgroundColor: active ? theme.tint : theme.background,
                    borderColor: active ? theme.tint : theme.border,
                  },
                ]}
              >
                <Text
                  style={[styles.chipLabel, { color: active ? theme.textOnAccent : theme.text }]}
                  numberOfLines={1}
                >
                  {l.leagueName}
                </Text>
              </TouchableOpacity>
            );
          })}
        </ScrollView>
      )}

      {/* Chart: one column per graded week. Column = label above, bar
          bottom-anchored inside the fixed-height plot, week label below.
          The mean line is absolute over the plot and shares its scale. */}
      <View style={styles.chart}>
        <View style={styles.plot}>
          <View
            pointerEvents="none"
            style={[
              styles.meanLine,
              { bottom: (mean / 100) * PLOT_HEIGHT, borderColor: theme.textMuted },
            ]}
            testID="mean-line"
          />
          {weeks.map((w) => {
            const pct = Math.max(0, Math.min(100, w.accuracyPercent));
            return (
              <View
                key={w.week}
                style={styles.column}
                accessibilityLabel={`Week ${w.week}, ${w.correctPicks} of ${w.totalPicks} correct, ${formatPercent(w.accuracyPercent)}`}
              >
                <View style={styles.barSlot}>
                  <Text
                    style={[styles.barLabel, { color: theme.textSecondary }]}
                    numberOfLines={1}
                  >
                    {Math.round(pct)}%
                  </Text>
                  <View
                    testID={`bar-week-${w.week}`}
                    style={[
                      styles.bar,
                      {
                        height: Math.max(2, (pct / 100) * PLOT_HEIGHT),
                        backgroundColor: pct >= mean ? theme.tint : theme.accentMuted,
                      },
                    ]}
                  />
                </View>
              </View>
            );
          })}
        </View>
        <View style={styles.axis}>
          {weeks.map((w) => (
            <Text
              key={w.week}
              style={[styles.axisLabel, { color: theme.textMuted }]}
              numberOfLines={1}
            >
              {w.week}
            </Text>
          ))}
        </View>
      </View>

      <Text style={[styles.footer, { color: theme.textMuted }]}>
        Weeks · dashed line = mean {formatPercent(Math.round(mean * 10) / 10)}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    borderRadius: 14,
    borderWidth: StyleSheet.hairlineWidth,
    padding: 16,
  },
  headerRow: {
    flexDirection: 'row',
    alignItems: 'baseline',
    justifyContent: 'space-between',
    gap: 12,
    marginBottom: 4,
  },
  eyebrow: {
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 1.5,
  },
  leagueName: {
    flexShrink: 1,
    fontSize: 12,
    fontWeight: '600',
  },
  summaryRow: {
    flexDirection: 'row',
    alignItems: 'baseline',
    gap: 8,
    marginBottom: 10,
  },
  summaryValue: {
    fontSize: 28,
    fontWeight: '700',
    fontVariant: ['tabular-nums'],
  },
  summaryLabel: {
    fontSize: 13,
  },
  chips: {
    gap: 6,
    paddingBottom: 10,
  },
  chip: {
    paddingVertical: 6,
    paddingHorizontal: 12,
    borderRadius: 999,
    borderWidth: StyleSheet.hairlineWidth,
    maxWidth: 180,
  },
  chipLabel: {
    fontSize: 13,
    fontWeight: '600',
  },
  chart: {
    marginTop: 4,
  },
  // Plot is the bar area only; bar labels overflow above it via barSlot's
  // extra headroom, so the tallest bar's label never clips.
  plot: {
    height: PLOT_HEIGHT + 18,
    flexDirection: 'row',
    alignItems: 'flex-end',
    gap: 4,
  },
  meanLine: {
    position: 'absolute',
    left: 0,
    right: 0,
    borderTopWidth: 1,
    borderStyle: 'dashed',
    opacity: 0.8,
  },
  column: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'flex-end',
  },
  barSlot: {
    width: '100%',
    alignItems: 'center',
    justifyContent: 'flex-end',
  },
  barLabel: {
    fontSize: 10,
    fontVariant: ['tabular-nums'],
    marginBottom: 2,
  },
  bar: {
    width: '100%',
    maxWidth: 28,
    borderTopLeftRadius: 4,
    borderTopRightRadius: 4,
  },
  axis: {
    flexDirection: 'row',
    gap: 4,
    marginTop: 4,
  },
  axisLabel: {
    flex: 1,
    textAlign: 'center',
    fontSize: 10,
    fontVariant: ['tabular-nums'],
  },
  footer: {
    marginTop: 8,
    fontSize: 11,
  },
});
