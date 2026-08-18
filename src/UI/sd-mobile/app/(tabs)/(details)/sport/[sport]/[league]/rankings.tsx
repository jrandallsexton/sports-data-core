import React, { useState } from 'react';
import { View, StyleSheet, TouchableOpacity, Image, ScrollView } from 'react-native';
import { Stack, useLocalSearchParams, useRouter } from 'expo-router';
import { useQuery } from '@tanstack/react-query';

import { Text } from '@/src/components/ui/AppText';
import { LoadingSpinner } from '@/src/components/ui/LoadingSpinner';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { rankingsApi, rankingsKeys, type RankingsPoll } from '@/src/services/api/rankingsApi';
import { useCurrentSeasonYear } from '@/src/hooks/useCurrentSeasonYear';

/**
 * Full rankings screen — every poll as a tab, all 25 entries. Web parity
 * with sd-ui's RankingsPage at /app/sport/:sport/:league/rankings.
 *
 * Route: /sport/[sport]/[league]/rankings with optional ?season=&week=
 * search params (the mobile twin of the web's path segments).
 *
 * Only football/ncaa has poll data today and the backend week endpoint is
 * not scope-aware yet, so other tuples get an honest empty state — same
 * gate as the web page. CFP is filtered app-wide until those rankings
 * publish (operator call; the bracket surface needs its own pass first).
 */
export default function RankingsScreen() {
  const { sport, league, season: seasonParam, week: weekParam } = useLocalSearchParams<{
    sport: string;
    league: string;
    season?: string;
    week?: string;
  }>();
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const router = useRouter();
  const [activePollId, setActivePollId] = useState<string | null>(null);

  const isSupported = sport?.toLowerCase() === 'football' && league?.toLowerCase() === 'ncaa';

  const { seasonYear: currentSeasonYear, loading: seasonLoading } = useCurrentSeasonYear(
    sport ?? 'football',
    league ?? 'ncaa'
  );

  const parsedSeason = /^\d{4}$/.test(seasonParam ?? '') ? Number(seasonParam) : undefined;
  const parsedWeek =
    parsedSeason && /^\d{1,2}$/.test(weekParam ?? '') && Number(weekParam) > 0
      ? Number(weekParam)
      : undefined;
  const seasonYear = parsedSeason ?? currentSeasonYear;

  const { data, isLoading } = useQuery({
    queryKey: parsedWeek
      ? rankingsKeys.week(sport!, league!, seasonYear ?? 0, parsedWeek)
      : rankingsKeys.season(sport!, league!, seasonYear ?? 0),
    queryFn: async () =>
      parsedWeek
        ? (await rankingsApi.getWeekRankings(seasonYear!, parsedWeek)).data
        : (await rankingsApi.getSeasonRankings(seasonYear!, sport, league)).data,
    enabled: isSupported && seasonYear !== null && seasonYear !== undefined,
  });

  // CFP hidden until those rankings publish — remove this filter to
  // re-enable; mirrors sd-ui's RankingsWidget.
  const polls: RankingsPoll[] = (data ?? []).filter((p) => p.pollId !== 'cfp');
  const activePoll = polls.find((p) => p.pollId === activePollId) ?? polls[0];

  const openTeam = (slug: string) => {
    router.push({
      pathname: '/sport/[sport]/[league]/team/[slug]',
      params: { sport: sport!, league: league!, slug, season: String(seasonYear) },
    } as never);
  };

  return (
    <>
      <Stack.Screen options={{ title: 'Rankings' }} />
      <ScrollView
        style={{ backgroundColor: theme.background }}
        contentContainerStyle={styles.content}
      >
        {!isSupported ? (
          <Text style={[styles.empty, { color: theme.textSecondary }]}>
            Rankings aren't available for this league yet.
          </Text>
        ) : isLoading || seasonLoading ? (
          <LoadingSpinner message="Loading rankings..." />
        ) : polls.length === 0 ? (
          <Text style={[styles.empty, { color: theme.textSecondary }]}>
            No rankings available.
          </Text>
        ) : (
          <>
            <View style={styles.tabs}>
              {polls.map((poll) => {
                const active = poll.pollId === activePoll?.pollId;
                return (
                  <TouchableOpacity
                    key={poll.pollId}
                    onPress={() => setActivePollId(poll.pollId)}
                    accessibilityRole="button"
                    accessibilityLabel={`Show ${poll.pollName}`}
                    style={[
                      styles.tab,
                      { borderColor: active ? theme.tint : theme.border },
                      active && { backgroundColor: theme.card },
                    ]}
                  >
                    <Text
                      style={[
                        styles.tabLabel,
                        { color: active ? theme.tint : theme.textSecondary },
                        active && styles.tabLabelActive,
                      ]}
                    >
                      {poll.pollName}
                    </Text>
                  </TouchableOpacity>
                );
              })}
            </View>

            {activePoll && (
              <View
                style={[styles.card, { backgroundColor: theme.card, borderColor: theme.border }]}
              >
                {activePoll.entries.map((team, i) => {
                  const logoSrc =
                    scheme === 'dark'
                      ? team.franchiseLogoUrlDark ??
                        team.franchiseLogoUrlLight ??
                        team.franchiseLogoUrl
                      : team.franchiseLogoUrlLight ??
                        team.franchiseLogoUrlDark ??
                        team.franchiseLogoUrl;
                  return (
                    <TouchableOpacity
                      key={team.franchiseSeasonId || team.rank}
                      onPress={() => openTeam(team.franchiseSlug)}
                      activeOpacity={0.6}
                      accessibilityRole="button"
                      accessibilityLabel={`Open ${team.franchiseName}`}
                      style={[
                        styles.row,
                        i > 0 && {
                          borderTopWidth: StyleSheet.hairlineWidth,
                          borderTopColor: theme.border,
                        },
                      ]}
                    >
                      <Text style={[styles.rank, { color: theme.textSecondary }]}>
                        {team.rank}
                      </Text>
                      <View style={styles.logoSlot}>
                        {logoSrc ? <Image source={{ uri: logoSrc }} style={styles.logo} /> : null}
                      </View>
                      <View style={styles.nameCol}>
                        <Text style={[styles.name, { color: theme.text }]} numberOfLines={1}>
                          {team.franchiseName || 'Unknown'}
                        </Text>
                        <Text style={[styles.record, { color: theme.textSecondary }]}>
                          {team.wins}-{team.losses}
                          {activePoll.hasPoints && team.points
                            ? ` · ${team.points} pts`
                            : ''}
                          {activePoll.hasFirstPlaceVotes && team.firstPlaceVotes
                            ? ` · ${team.firstPlaceVotes} first-place`
                            : ''}
                        </Text>
                      </View>
                      {activePoll.hasTrends && team.trend ? (
                        <Text style={[styles.trend, { color: theme.textSecondary }]}>
                          {team.trend}
                        </Text>
                      ) : null}
                    </TouchableOpacity>
                  );
                })}
              </View>
            )}
          </>
        )}
      </ScrollView>
    </>
  );
}

const styles = StyleSheet.create({
  content: {
    padding: 12,
    paddingBottom: 32,
  },
  empty: {
    textAlign: 'center',
    paddingVertical: 32,
  },
  tabs: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
    marginBottom: 12,
  },
  tab: {
    borderWidth: 1,
    borderRadius: 999,
    paddingHorizontal: 12,
    paddingVertical: 6,
  },
  tabLabel: {
    fontSize: 13,
  },
  tabLabelActive: {
    fontWeight: '700',
  },
  card: {
    borderRadius: 14,
    borderWidth: 1,
    paddingHorizontal: 14,
    paddingVertical: 6,
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 9,
    gap: 8,
  },
  rank: {
    minWidth: 24,
    textAlign: 'right',
    fontWeight: '700',
    fontVariant: ['tabular-nums'],
  },
  logoSlot: {
    width: 22,
    alignItems: 'center',
  },
  logo: {
    width: 22,
    height: 22,
    resizeMode: 'contain',
  },
  nameCol: {
    flex: 1,
  },
  name: {
    fontWeight: '600',
  },
  record: {
    fontSize: 12,
    fontVariant: ['tabular-nums'],
  },
  trend: {
    fontSize: 13,
    fontVariant: ['tabular-nums'],
  },
});
