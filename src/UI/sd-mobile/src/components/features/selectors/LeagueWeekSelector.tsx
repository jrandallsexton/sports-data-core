import React, { useState } from 'react';
import {
  View,
  Text,
  ScrollView,
  TouchableOpacity,
  StyleSheet,
  LayoutAnimation,
  Platform,
  UIManager,
} from 'react-native';
import { useColorScheme } from 'react-native';
import { getTheme } from '@/constants/Colors';
import type { League } from '@/src/types/models';

// Required for LayoutAnimation on Android
if (Platform.OS === 'android') {
  UIManager.setLayoutAnimationEnabledExperimental?.(true);
}

interface LeagueWeekSelectorProps {
  leagues: League[];
  selectedLeagueId: string | null;
  onLeagueChange: (leagueId: string) => void;
  selectedWeek: number | null;
  maxWeek: number;
  onWeekChange: (week: number) => void;
}

export function LeagueWeekSelector({
  leagues,
  selectedLeagueId,
  onLeagueChange,
  selectedWeek,
  maxWeek,
  onWeekChange,
}: LeagueWeekSelectorProps) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const weeks = Array.from({ length: maxWeek }, (_, i) => i + 1);

  // Start collapsed when both a league and week are already selected (e.g. returning to the tab)
  const [collapsed, setCollapsed] = useState(
    () => selectedLeagueId != null && selectedWeek != null,
  );

  const animateCollapse = () => {
    LayoutAnimation.configureNext(LayoutAnimation.Presets.easeInEaseOut);
    setCollapsed(true);
  };

  const animateExpand = () => {
    LayoutAnimation.configureNext(LayoutAnimation.Presets.easeInEaseOut);
    setCollapsed(false);
  };

  const handleLeagueChange = (id: string) => {
    onLeagueChange(id);
    // Collapse after picking a league only if a week is already chosen —
    // otherwise keep open so the user can also pick their week.
    if (selectedWeek != null) {
      setTimeout(animateCollapse, 250);
    }
  };

  const handleWeekChange = (week: number) => {
    onWeekChange(week);
    // Week is the final selection — always collapse afterwards.
    setTimeout(animateCollapse, 250);
  };

  const selectedLeague = leagues.find((l) => l.id === selectedLeagueId);
  const leagueLabel =
    leagues.length > 1
      ? (selectedLeague?.name ?? 'Select League')
      : null; // hide league portion when user is in only one league
  const weekLabel = selectedWeek != null ? `Week ${selectedWeek}` : 'Select Week';
  const summaryLabel = leagueLabel ? `${leagueLabel} · ${weekLabel}` : weekLabel;

  return (
    <View style={[styles.container, { backgroundColor: theme.card, borderBottomColor: theme.border }]}>
      {/* Always-visible summary bar — tap to toggle */}
      <TouchableOpacity
        style={styles.summaryBar}
        onPress={collapsed ? animateExpand : animateCollapse}
        activeOpacity={0.7}
      >
        <Text style={[styles.summaryText, { color: theme.text }]} numberOfLines={1}>
          {summaryLabel}
        </Text>
        <Text style={[styles.chevron, { color: theme.textMuted }]}>
          {collapsed ? '▾' : '▴'}
        </Text>
      </TouchableOpacity>

      {/* Collapsible picker panels */}
      {!collapsed && (
        <View>
          {/* League selector — only shown when user belongs to 2+ leagues */}
          {leagues.length > 1 && (
            <ScrollView
              horizontal
              showsHorizontalScrollIndicator={false}
              contentContainerStyle={styles.row}
            >
              {leagues.map((league) => {
                const active = league.id === selectedLeagueId;
                return (
                  <TouchableOpacity
                    key={league.id}
                    style={[
                      styles.chip,
                      active && { backgroundColor: theme.tint },
                      !active && { borderColor: theme.border },
                    ]}
                    onPress={() => handleLeagueChange(league.id)}
                  >
                    <Text
                      style={[
                        styles.chipText,
                        { color: active ? '#fff' : theme.textMuted },
                      ]}
                      numberOfLines={1}
                    >
                      {league.name}
                    </Text>
                  </TouchableOpacity>
                );
              })}
            </ScrollView>
          )}

          {/* Week selector */}
          <View style={styles.weekRow}>
            {weeks.map((w) => {
              const active = w === selectedWeek;
              return (
                <TouchableOpacity
                  key={w}
                  style={[
                    styles.chip,
                    active && { backgroundColor: theme.tint },
                    !active && { borderColor: theme.border },
                  ]}
                  onPress={() => handleWeekChange(w)}
                >
                  <Text
                    style={[
                      styles.chipText,
                      { color: active ? '#fff' : theme.textMuted },
                    ]}
                  >
                    Wk {w}
                  </Text>
                </TouchableOpacity>
              );
            })}
          </View>
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  summaryBar: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingVertical: 10,
  },
  summaryText: {
    fontSize: 14,
    fontWeight: '600',
    flexShrink: 1,
    marginRight: 8,
  },
  chevron: {
    fontSize: 14,
  },
  row: {
    paddingHorizontal: 14,
    paddingVertical: 8,
    gap: 8,
  },
  weekRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    paddingHorizontal: 14,
    paddingVertical: 8,
    gap: 8,
  },
  chip: {
    paddingHorizontal: 14,
    paddingVertical: 6,
    borderRadius: 20,
    borderWidth: 1,
  },
  chipText: {
    fontSize: 13,
    fontWeight: '600',
  },
});
