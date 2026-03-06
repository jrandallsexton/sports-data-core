import React, { useState } from 'react';
import {
  Modal,
  View,
  Text,
  ScrollView,
  TouchableOpacity,
  Image,
  StyleSheet,
  useColorScheme,
  ActivityIndicator,
} from 'react-native';
import { Colors, getTheme } from '@/constants/Colors';
import type { Matchup, TeamComparisonData, TeamStatEntry } from '@/src/types/models';

// ─── Props ────────────────────────────────────────────────────────────────────

interface StatsComparisonModalProps {
  visible: boolean;
  onClose: () => void;
  matchup: Matchup;
  comparison: TeamComparisonData | null;
  isLoading: boolean;
}

// ─── Team header ──────────────────────────────────────────────────────────────

function TeamHeader({
  name,
  logoUri,
  color,
  align,
}: {
  name: string;
  logoUri?: string | null;
  color?: string | null;
  align: 'left' | 'right';
}) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const isRight = align === 'right';
  return (
    <View style={[styles.teamHeader, isRight && styles.teamHeaderRight]}>
      {!isRight && (
        logoUri ? (
          <Image source={{ uri: logoUri }} style={styles.teamLogo} />
        ) : (
          <View style={[styles.teamLogoPlaceholder, { backgroundColor: color ?? Colors.brand.navy }]}>
            <Text style={styles.teamLogoInitial}>{name?.[0] ?? '?'}</Text>
          </View>
        )
      )}
      <Text
        numberOfLines={1}
        style={[styles.teamHeaderName, { color: theme.text }, isRight && { textAlign: 'right' }]}
      >
        {name}
      </Text>
      {isRight && (
        logoUri ? (
          <Image source={{ uri: logoUri }} style={styles.teamLogo} />
        ) : (
          <View style={[styles.teamLogoPlaceholder, { backgroundColor: color ?? Colors.brand.navy }]}>
            <Text style={styles.teamLogoInitial}>{name?.[0] ?? '?'}</Text>
          </View>
        )
      )}
    </View>
  );
}

// ─── Category tab ─────────────────────────────────────────────────────────────

function CategoryTab({
  label,
  active,
  onPress,
}: {
  label: string;
  active: boolean;
  onPress: () => void;
}) {
  return (
    <TouchableOpacity
      onPress={onPress}
      style={[styles.tab, active && { backgroundColor: Colors.brand.navy, borderColor: Colors.brand.navy }]}
    >
      <Text style={[styles.tabText, active && { color: '#fff' }]}>{label}</Text>
    </TouchableOpacity>
  );
}

// ─── Parse a displayValue string into a number for bar sizing ─────────────────

function parseNumeric(displayValue: string): number | null {
  const match = displayValue.match(/[-\d.]+/);
  if (!match) return null;
  const n = parseFloat(match[0]);
  return isNaN(n) ? null : n;
}

// ─── One stat comparison row ──────────────────────────────────────────────────

function StatRow({
  label,
  awayEntry,
  homeEntry,
}: {
  label: string;
  awayEntry: TeamStatEntry;
  homeEntry: TeamStatEntry;
}) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const awayNum = parseNumeric(awayEntry.displayValue ?? '');
  const homeNum = parseNumeric(homeEntry.displayValue ?? '');
  const max = awayNum != null && homeNum != null ? Math.max(Math.abs(awayNum), Math.abs(homeNum)) : null;

  const awayPct = max && max > 0 ? Math.abs(awayNum!) / max : 0;
  const homePct = max && max > 0 ? Math.abs(homeNum!) / max : 0;

  return (
    <View style={[styles.statRow, { borderBottomColor: theme.border }]}>
      {/* Away value */}
      <View style={[styles.statValue, styles.statValueLeft]}>
        <Text style={[styles.statValueText, { color: theme.text }]}>{awayEntry.displayValue}</Text>
        {max != null && (
          <View style={styles.barTrack}>
            <View
              style={[
                styles.bar,
                styles.barRight,
                { width: `${awayPct * 100}%`, backgroundColor: Colors.brand.navy },
              ]}
            />
          </View>
        )}
      </View>

      {/* Label */}
      <View style={styles.statLabelBox}>
        <Text numberOfLines={2} style={[styles.statLabel, { color: theme.textMuted }]}>{label}</Text>
      </View>

      {/* Home value */}
      <View style={[styles.statValue, styles.statValueRight]}>
        <Text style={[styles.statValueText, styles.statValueTextRight, { color: theme.text }]}>
          {homeEntry.displayValue}
        </Text>
        {max != null && (
          <View style={styles.barTrack}>
            <View
              style={[
                styles.bar,
                styles.barLeft,
                { width: `${homePct * 100}%`, backgroundColor: Colors.brand.navy },
              ]}
            />
          </View>
        )}
      </View>
    </View>
  );
}

// ─── StatsComparisonModal ─────────────────────────────────────────────────────

export function StatsComparisonModal({
  visible,
  onClose,
  matchup,
  comparison,
  isLoading,
}: StatsComparisonModalProps) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const [activeCategory, setActiveCategory] = useState<string | null>(null);

  // Collect all category names from teamA stats
  const awayStats = comparison?.teamA?.stats?.data?.statistics ?? {};
  const homeStats = comparison?.teamB?.stats?.data?.statistics ?? {};
  const categories = Object.keys(awayStats).length > 0
    ? Object.keys(awayStats)
    : Object.keys(homeStats);

  const currentCategory = activeCategory ?? categories[0] ?? null;

  const awayRows: TeamStatEntry[] = currentCategory ? (awayStats[currentCategory] ?? []) : [];
  const homeRows: TeamStatEntry[] = currentCategory ? (homeStats[currentCategory] ?? []) : [];
  const rowCount = Math.max(awayRows.length, homeRows.length);

  return (
    <Modal
      visible={visible}
      animationType="slide"
      presentationStyle="pageSheet"
      onRequestClose={onClose}
    >
      <View style={[styles.container, { backgroundColor: theme.background }]}>
        {/* Header */}
        <View style={[styles.header, { borderBottomColor: theme.border }]}>
          <View style={styles.headerLeft} />
          <Text style={[styles.headerTitle, { color: theme.text }]}>Team Comparison</Text>
          <TouchableOpacity onPress={onClose} style={styles.closeBtn} hitSlop={12}>
            <Text style={[styles.closeText, { color: theme.textMuted }]}>✕</Text>
          </TouchableOpacity>
        </View>

        {isLoading ? (
          <View style={styles.loadingContainer}>
            <ActivityIndicator size="large" color={Colors.brand.navy} />
            <Text style={[styles.loadingText, { color: theme.textMuted }]}>
              Loading stats…
            </Text>
          </View>
        ) : comparison == null || categories.length === 0 ? (
          <View style={styles.loadingContainer}>
            <Text style={[styles.emptyText, { color: theme.textMuted }]}>
              Stats not available.
            </Text>
          </View>
        ) : (
          <View style={styles.body}>
            {/* Team headers */}
            <View style={[styles.teamsRow, { borderBottomColor: theme.border }]}>
              <TeamHeader
                name={comparison.teamA.name}
                logoUri={comparison.teamA.logoUri}
                color={matchup.awayColor}
                align="left"
              />
              <TeamHeader
                name={comparison.teamB.name}
                logoUri={comparison.teamB.logoUri}
                color={matchup.homeColor}
                align="right"
              />
            </View>

            {/* Category tabs */}
            <ScrollView
              horizontal
              showsHorizontalScrollIndicator={false}
              style={[styles.tabScroll, { borderBottomColor: theme.border }]}
              contentContainerStyle={styles.tabScrollContent}
            >
              {categories.map((cat) => (
                <CategoryTab
                  key={cat}
                  label={cat}
                  active={currentCategory === cat}
                  onPress={() => setActiveCategory(cat)}
                />
              ))}
            </ScrollView>

            {/* Stat rows */}
            <ScrollView showsVerticalScrollIndicator={false}>
              {rowCount === 0 ? (
                <Text style={[styles.emptyText, { color: theme.textMuted, padding: 24 }]}>
                  No {currentCategory} stats available.
                </Text>
              ) : (
                Array.from({ length: rowCount }, (_, i) => {
                  const away = awayRows[i];
                  const home = homeRows[i];
                  // Use label from entry if available, else stat index
                  const label =
                    (away as any)?.label ??
                    (away as any)?.name ??
                    (home as any)?.label ??
                    (home as any)?.name ??
                    `Stat ${i + 1}`;

                  if (!away && !home) return null;

                  return (
                    <StatRow
                      key={i}
                      label={label}
                      awayEntry={away ?? { displayValue: '—' }}
                      homeEntry={home ?? { displayValue: '—' }}
                    />
                  );
                })
              )}
            </ScrollView>
          </View>
        )}
      </View>
    </Modal>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 14,
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  headerLeft: {
    width: 32,
  },
  headerTitle: {
    fontSize: 17,
    fontWeight: '700',
  },
  closeBtn: {
    width: 32,
    alignItems: 'flex-end',
  },
  closeText: {
    fontSize: 17,
  },
  loadingContainer: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 12,
  },
  loadingText: {
    fontSize: 15,
  },
  emptyText: {
    fontSize: 15,
  },
  body: {
    flex: 1,
  },

  // Teams row
  teamsRow: {
    flexDirection: 'row',
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderBottomWidth: StyleSheet.hairlineWidth,
    gap: 8,
  },
  teamHeader: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  teamHeaderRight: {
    justifyContent: 'flex-end',
  },
  teamLogo: {
    width: 32,
    height: 32,
    resizeMode: 'contain',
  },
  teamLogoPlaceholder: {
    width: 32,
    height: 32,
    borderRadius: 16,
    alignItems: 'center',
    justifyContent: 'center',
  },
  teamLogoInitial: {
    color: '#fff',
    fontSize: 14,
    fontWeight: '700',
  },
  teamHeaderName: {
    flex: 1,
    fontSize: 13,
    fontWeight: '700',
  },

  // Category tabs
  tabScroll: {
    borderBottomWidth: StyleSheet.hairlineWidth,
    flexGrow: 0,
  },
  tabScrollContent: {
    paddingHorizontal: 12,
    paddingVertical: 8,
    gap: 8,
  },
  tab: {
    paddingHorizontal: 14,
    paddingVertical: 6,
    borderRadius: 20,
    borderWidth: 1.5,
    borderColor: '#CBD5E1',
  },
  tabText: {
    fontSize: 13,
    fontWeight: '600',
    color: '#64748B',
  },

  // Stat rows
  statRow: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderBottomWidth: StyleSheet.hairlineWidth,
    gap: 8,
  },
  statValue: {
    flex: 1,
    gap: 4,
  },
  statValueLeft: {
    alignItems: 'flex-start',
  },
  statValueRight: {
    alignItems: 'flex-end',
  },
  statValueText: {
    fontSize: 14,
    fontWeight: '700',
  },
  statValueTextRight: {
    textAlign: 'right',
  },
  statLabelBox: {
    width: 90,
    alignItems: 'center',
  },
  statLabel: {
    fontSize: 11,
    fontWeight: '500',
    textAlign: 'center',
    lineHeight: 14,
  },
  barTrack: {
    width: '100%',
    height: 4,
    backgroundColor: '#E2E8F0',
    borderRadius: 2,
    overflow: 'hidden',
  },
  bar: {
    height: 4,
    borderRadius: 2,
    maxWidth: '100%',
  },
  barRight: {
    alignSelf: 'flex-start',
  },
  barLeft: {
    alignSelf: 'flex-end',
  },
});
