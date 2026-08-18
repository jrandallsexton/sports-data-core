import React from 'react';
import { View, StyleSheet, TouchableOpacity, Image } from 'react-native';
import { useRouter } from 'expo-router';
import { useQuery } from '@tanstack/react-query';

import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { rankingsApi, rankingsKeys, type RankingsPoll } from '@/src/services/api/rankingsApi';
import { useCurrentSeasonYear } from '@/src/hooks/useCurrentSeasonYear';

const TOP_N = 5;

/**
 * Tier 2 home card — Top 5 from the current AP poll, linking to the full
 * rankings screen for all polls and all 25 entries. Web sibling of sd-ui's
 * RankingsCard (which shows 10 — mobile gets 5, operator call: home real
 * estate is tighter here).
 *
 * AP only here; the fallback (AP absent) must never surface CFP either —
 * it's hidden app-wide until those rankings publish (operator call, the
 * bracket surface needs its own pass first). Renders nothing while loading,
 * on error, and when no poll exists for the current season, so Home never
 * shows a broken card.
 */
export function RankingsCard() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const router = useRouter();
  const { seasonYear } = useCurrentSeasonYear();

  const { data } = useQuery({
    queryKey: rankingsKeys.season('football', 'ncaa', seasonYear ?? 0),
    queryFn: async () => (await rankingsApi.getSeasonRankings(seasonYear!)).data,
    enabled: seasonYear !== null,
  });

  const polls: RankingsPoll[] = data ?? [];
  const eligible = polls.filter((p) => p.pollId !== 'cfp');
  const poll = eligible.find((p) => p.pollId === 'ap') ?? eligible[0];

  if (!poll?.entries?.length || seasonYear === null) return null;

  const topEntries = poll.entries.slice(0, TOP_N);

  const openFullRankings = () => {
    router.push('/sport/football/ncaa/rankings' as never);
  };

  const openTeam = (slug: string) => {
    router.push({
      pathname: '/sport/[sport]/[league]/team/[slug]',
      params: { sport: 'football', league: 'ncaa', slug, season: String(seasonYear) },
    } as never);
  };

  return (
    <View style={[styles.card, { backgroundColor: theme.card, borderColor: theme.border }]}>
      <View style={styles.headerRow}>
        <Text style={[styles.eyebrow, { color: theme.tint }]}>
          {(poll.pollName || 'RANKINGS').toUpperCase()}
        </Text>
        <TouchableOpacity
          onPress={openFullRankings}
          hitSlop={8}
          accessibilityRole="button"
          accessibilityLabel="Open full rankings"
        >
          <Text style={[styles.full, { color: theme.tint }]}>Full rankings ›</Text>
        </TouchableOpacity>
      </View>

      <View>
        {topEntries.map((team, i) => {
          const logoSrc =
            scheme === 'dark'
              ? team.franchiseLogoUrlDark ?? team.franchiseLogoUrlLight ?? team.franchiseLogoUrl
              : team.franchiseLogoUrlLight ?? team.franchiseLogoUrlDark ?? team.franchiseLogoUrl;
          return (
            <TouchableOpacity
              key={team.franchiseSeasonId || team.rank}
              onPress={() => openTeam(team.franchiseSlug)}
              activeOpacity={0.6}
              accessibilityRole="button"
              accessibilityLabel={`Open ${team.franchiseName}`}
              style={[
                styles.row,
                i > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: theme.border },
              ]}
            >
              <Text style={[styles.rank, { color: theme.textSecondary }]}>{team.rank}</Text>
              {/* Slot always renders so logo-less rows keep column alignment. */}
              <View style={styles.logoSlot}>
                {logoSrc ? <Image source={{ uri: logoSrc }} style={styles.logo} /> : null}
              </View>
              <Text style={[styles.name, { color: theme.text }]} numberOfLines={1}>
                {team.franchiseName || 'Unknown'}
              </Text>
              <Text style={[styles.record, { color: theme.textSecondary }]}>
                {team.wins}-{team.losses}
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
    borderWidth: 1,
    paddingHorizontal: 16,
    paddingVertical: 12,
    marginBottom: 12,
  },
  headerRow: {
    flexDirection: 'row',
    alignItems: 'baseline',
    justifyContent: 'space-between',
    marginBottom: 4,
  },
  eyebrow: {
    fontSize: 12,
    fontWeight: '700',
    letterSpacing: 1.5,
  },
  full: {
    fontSize: 13,
    fontWeight: '700',
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 7,
    gap: 8,
  },
  rank: {
    minWidth: 22,
    textAlign: 'right',
    fontWeight: '700',
    fontVariant: ['tabular-nums'],
  },
  logoSlot: {
    width: 20,
    alignItems: 'center',
  },
  logo: {
    width: 20,
    height: 20,
    resizeMode: 'contain',
  },
  name: {
    flex: 1,
    fontWeight: '500',
  },
  record: {
    fontSize: 13,
    fontVariant: ['tabular-nums'],
  },
});
