import React from 'react';
import { View, Text, StyleSheet, TouchableOpacity } from 'react-native';
import { useRouter } from 'expo-router';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import type { League } from '@/src/types/models';

interface Props {
  leagues: League[];
}

/**
 * Tier 2 — "Your Leagues" card. Lists the user's active leagues below the
 * Tier 1 primary slot. Tapping a row deep-links into the Picks tab with
 * that league preselected (/(tabs)/picks?leagueId=<id>). Gut-call
 * destination for v1; a future revision may route admins to a league
 * overview / standings screen once those exist on mobile.
 *
 * Hidden by the parent when leagues is empty — the new-user primary slot
 * already handles the zero-state branch.
 */
export function YourLeaguesCard({ leagues }: Props) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const router = useRouter();

  if (leagues.length === 0) return null;

  const openLeague = (leagueId: string) => {
    router.push({
      pathname: '/(tabs)/picks',
      params: { leagueId },
    } as never);
  };

  return (
    <View
      style={[
        styles.card,
        { backgroundColor: theme.card, borderColor: theme.border },
      ]}
    >
      <Text style={[styles.eyebrow, { color: theme.tint }]}>YOUR LEAGUES</Text>

      <View style={styles.list}>
        {leagues.map((league, i) => (
          <TouchableOpacity
            key={league.id}
            onPress={() => openLeague(league.id)}
            activeOpacity={0.6}
            accessibilityRole="button"
            accessibilityLabel={`Open ${league.name}`}
            style={[
              styles.row,
              i > 0 && {
                borderTopColor: theme.separator,
                borderTopWidth: StyleSheet.hairlineWidth,
              },
            ]}
          >
            <Text
              style={[styles.name, { color: theme.text }]}
              numberOfLines={1}
            >
              {league.name}
            </Text>
            <Text
              style={[styles.chevron, { color: theme.textMuted }]}
              // accessibilityElementsHidden is iOS-only; importantForAccessibility
              // covers Android TalkBack. Pass both so the "›" glyph never reaches
              // a screen reader alongside the row's accessibilityLabel.
              accessibilityElementsHidden
              importantForAccessibility="no-hide-descendants"
            >
              ›
            </Text>
          </TouchableOpacity>
        ))}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    borderRadius: 14,
    borderWidth: StyleSheet.hairlineWidth,
    padding: 16,
  },
  eyebrow: {
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 1.5,
    marginBottom: 10,
  },
  list: {
    gap: 0,
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingVertical: 12,
  },
  name: {
    fontSize: 15,
    fontWeight: '600',
    flex: 1,
  },
  chevron: {
    fontSize: 22,
    fontWeight: '400',
    marginLeft: 8,
  },
});
