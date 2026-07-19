import React, { useState } from 'react';
import {
  View,
  TouchableOpacity,
  StyleSheet,
  LayoutAnimation,
  Platform,
  UIManager,
} from 'react-native';
import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import type { LeagueSummary } from '@/src/services/api/leaguesApi';

// Required for LayoutAnimation on Android.
if (Platform.OS === 'android') {
  UIManager.setLayoutAnimationEnabledExperimental?.(true);
}

interface StandingsControlsProps {
  /** All seasons the user has leagues in, newest-first. */
  seasons: number[];
  selectedSeason: number | null;
  onSeasonChange: (year: number) => void;
  /** Leagues within the selected season (already filtered by active/ended). */
  leagues: LeagueSummary[];
  selectedLeagueId: string | null;
  onLeagueChange: (id: string) => void;
  /** Whether the "Show ended" pill applies (current season with active leagues). */
  canFilterEnded: boolean;
  showEnded: boolean;
  onToggleEnded: () => void;
  showBots: boolean;
  onToggleBots: () => void;
}

/**
 * Standings header: a collapsible summary bar (matching the picks tab's
 * LeagueWeekSelector) that expands to compact season chips (multi-season only),
 * a wrapping row of league chips, and the Show ended / Show Bots pills — with
 * hairline rules between the groups. Purely presentational; selection state
 * lives in the screen.
 */
export function StandingsControls({
  seasons,
  selectedSeason,
  onSeasonChange,
  leagues,
  selectedLeagueId,
  onLeagueChange,
  canFilterEnded,
  showEnded,
  onToggleEnded,
  showBots,
  onToggleBots,
}: StandingsControlsProps) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  // Start collapsed if a league is already selected (e.g. returning to the tab);
  // otherwise open so the user can pick.
  const [collapsed, setCollapsed] = useState(() => selectedLeagueId != null);

  const toggle = () => {
    LayoutAnimation.configureNext(LayoutAnimation.Presets.easeInEaseOut);
    setCollapsed((c) => !c);
  };

  const selectedLeagueName =
    leagues.find((l) => l.id === selectedLeagueId)?.name ?? leagues[0]?.name ?? 'League';
  const summaryLabel =
    seasons.length > 1 && selectedSeason != null
      ? `${selectedSeason} · ${selectedLeagueName}`
      : selectedLeagueName;

  const chip = (
    key: string,
    label: string,
    active: boolean,
    onPress: () => void,
    extraStyle?: object,
  ) => (
    <TouchableOpacity
      key={key}
      style={[
        styles.chip,
        active
          ? { backgroundColor: theme.tint, borderColor: theme.tint }
          : { borderColor: theme.border },
        extraStyle,
      ]}
      onPress={onPress}
      activeOpacity={0.7}
      accessibilityRole="button"
      accessibilityState={{ selected: active }}
    >
      <Text
        style={[styles.chipText, { color: active ? theme.textOnAccent : theme.textMuted }]}
        numberOfLines={1}
      >
        {label}
      </Text>
    </TouchableOpacity>
  );

  // Only the sections that actually render, with a hairline rule between them.
  const sections = [
    seasons.length > 1 ? (
      <View style={styles.wrapRow}>
        {seasons.map((y) =>
          chip(String(y), String(y), y === selectedSeason, () => onSeasonChange(y)),
        )}
      </View>
    ) : null,
    leagues.length > 1 ? (
      <View style={styles.wrapRow}>
        {leagues.map((l) =>
          chip(l.id, l.name, l.id === selectedLeagueId, () => onLeagueChange(l.id), styles.leagueChip),
        )}
      </View>
    ) : null,
    <View style={styles.wrapRow}>
      {canFilterEnded && chip('__ended', 'Show ended', showEnded, onToggleEnded)}
      {chip('__bots', 'Show Bots', showBots, onToggleBots)}
    </View>,
  ].filter(Boolean);

  return (
    <View style={[styles.container, { backgroundColor: theme.card, borderBottomColor: theme.border }]}>
      {/* Always-visible summary — tap to expand/collapse. */}
      <TouchableOpacity style={styles.summaryBar} onPress={toggle} activeOpacity={0.7}>
        <Text style={[styles.summaryText, { color: theme.text }]} numberOfLines={1}>
          {summaryLabel}
        </Text>
        <Text style={[styles.chevron, { color: theme.textMuted }]}>{collapsed ? '▾' : '▴'}</Text>
      </TouchableOpacity>

      {!collapsed && (
        <View style={styles.panel}>
          {sections.map((section, i) => (
            <React.Fragment key={i}>
              {i > 0 && <View style={[styles.divider, { backgroundColor: theme.border }]} />}
              {section}
            </React.Fragment>
          ))}
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
  chevron: { fontSize: 14 },
  panel: {
    paddingBottom: 8,
    gap: 8,
  },
  wrapRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    paddingHorizontal: 14,
    gap: 8,
  },
  divider: {
    height: StyleSheet.hairlineWidth,
    marginHorizontal: 14,
  },
  chip: {
    paddingHorizontal: 12,
    paddingVertical: 5,
    borderRadius: 16,
    borderWidth: 1,
  },
  leagueChip: { maxWidth: '100%' },
  chipText: { fontSize: 13, fontWeight: '600' },
});
