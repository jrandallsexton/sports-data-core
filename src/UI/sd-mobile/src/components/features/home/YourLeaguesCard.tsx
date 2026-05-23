import React from 'react';
import { View, StyleSheet, TouchableOpacity } from 'react-native';
import { useRouter } from 'expo-router';
import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import type { League } from '@/src/types/models';

// Default sport-icon glyphs. Mirrors sd-ui's YourLeaguesCard SPORT_ICON map.
// Stand-in until commissioner-uploaded league icons land — at that point the
// per-league icon overrides this map and unknown-sport rows skip the glyph.
const SPORT_ICON: Record<NonNullable<League['sport']>, string> = {
  FootballNcaa: '🏈',
  FootballNfl: '🏈',
  BaseballMlb: '⚾',
};

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

      <View style={styles.pillGrid}>
        {leagues.map((league) => {
          const icon = league.sport ? SPORT_ICON[league.sport] : undefined;
          return (
            <TouchableOpacity
              key={league.id}
              onPress={() => openLeague(league.id)}
              activeOpacity={0.6}
              accessibilityRole="button"
              accessibilityLabel={`Open ${league.name}`}
              style={[
                styles.pill,
                { backgroundColor: theme.background, borderColor: theme.border },
              ]}
            >
              {icon && (
                <Text
                  style={styles.pillIcon}
                  accessibilityElementsHidden
                  importantForAccessibility="no-hide-descendants"
                >
                  {icon}
                </Text>
              )}
              <Text
                style={[styles.pillName, { color: theme.text }]}
                numberOfLines={1}
              >
                {league.name}
              </Text>
            </TouchableOpacity>
          );
        })}
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
  pillGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  pill: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderRadius: 999,
    borderWidth: 1,
    gap: 6,
    // Hard cap so a single very-long league name can't push the pill past
    // the card width; long names truncate via numberOfLines={1} + flexShrink
    // on the name Text.
    maxWidth: '100%',
  },
  pillIcon: {
    fontSize: 14,
  },
  pillName: {
    fontSize: 14,
    fontWeight: '600',
    flexShrink: 1,
  },
});
