import React, { useState } from 'react';
import { View, StyleSheet, ScrollView, RefreshControl, ActivityIndicator } from 'react-native';
import { Stack, useRouter } from 'expo-router';
import { useQuery } from '@tanstack/react-query';

import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { leaguesApi, leaguesKeys, type PublicLeague } from '@/src/services/api/leaguesApi';
import { JoinLeagueConfirmSheet } from '@/src/components/features/leagues/JoinLeagueConfirmSheet';
import { JoinClosesLabel } from '@/src/components/features/leagues/JoinClosesLabel';
import {
  SPORT_ICON,
  SPORT_LABEL,
  PICK_TYPE_LABEL,
} from '@/src/components/features/leagues/joinDisplay';

/**
 * Public-league browse screen (web parity: sd-ui's LeagueDiscoverPage). Lists
 * every public league the caller isn't in; closed ones are badged with a
 * disabled affordance rather than a Join that 400s. Reached from the home rail.
 */
export default function LeagueDiscoverScreen() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const router = useRouter();
  const [confirming, setConfirming] = useState<PublicLeague | null>(null);

  const { data, isLoading, isError, refetch, isRefetching } = useQuery({
    queryKey: leaguesKeys.public,
    queryFn: async () => (await leaguesApi.getPublicLeagues()).data,
  });

  const leagues = data ?? [];

  return (
    <View style={[styles.container, { backgroundColor: theme.background }]}>
      <Stack.Screen options={{ title: 'Discover Leagues' }} />

      <ScrollView
        contentContainerStyle={styles.content}
        refreshControl={
          <RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={theme.tint} />
        }
      >
        {isLoading ? (
          <ActivityIndicator color={theme.tint} style={styles.spinner} />
        ) : isError ? (
          <Text style={[styles.empty, { color: theme.textMuted }]}>
            Couldn&apos;t load public leagues. Pull to retry.
          </Text>
        ) : leagues.length === 0 ? (
          <Text style={[styles.empty, { color: theme.textMuted }]}>
            No public leagues available right now.
          </Text>
        ) : (
          leagues.map((league) => (
            <View
              key={league.id}
              style={[styles.card, { backgroundColor: theme.card, borderColor: theme.border }]}
            >
              <Text style={[styles.name, { color: theme.text }]}>{league.name}</Text>
              {league.description ? (
                <Text style={[styles.desc, { color: theme.textMuted }]} numberOfLines={2}>
                  {league.description}
                </Text>
              ) : null}

              <View style={styles.metaRow}>
                <Text style={[styles.meta, { color: theme.textMuted }]}>
                  {SPORT_ICON[league.sport] ?? '🏆'} {SPORT_LABEL[league.sport] ?? league.sport}{' '}
                  {league.seasonYear} · {PICK_TYPE_LABEL[league.pickType] ?? '—'}
                  {league.useConfidencePoints ? ' · Confidence' : ''} · {league.memberCount}{' '}
                  {league.memberCount === 1 ? 'member' : 'members'}
                </Text>
              </View>

              <View style={styles.footerRow}>
                <JoinClosesLabel
                  closesAtUtc={league.closesAtUtc}
                  isJoinable={league.isJoinable}
                  style={styles.meta}
                />
                {league.isJoinable ? (
                  <Text
                    onPress={() => setConfirming(league)}
                    accessibilityRole="button"
                    accessibilityLabel={`Join ${league.name}`}
                    style={[styles.joinPill, { backgroundColor: theme.tint, color: theme.textOnAccent }]}
                  >
                    Join
                  </Text>
                ) : (
                  <Text style={[styles.closedPill, { borderColor: theme.border, color: theme.textMuted }]}>
                    Closed
                  </Text>
                )}
              </View>
            </View>
          ))
        )}
      </ScrollView>

      <JoinLeagueConfirmSheet
        league={confirming}
        onCancel={() => setConfirming(null)}
        onJoined={(joined) => {
          setConfirming(null);
          router.push({ pathname: '/(tabs)/picks', params: { leagueId: joined.id } } as never);
        }}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1 },
  content: { padding: 16, gap: 12, paddingBottom: 32 },
  spinner: { marginTop: 40 },
  empty: { textAlign: 'center', marginTop: 40, fontSize: 14 },
  card: {
    borderRadius: 14,
    borderWidth: StyleSheet.hairlineWidth,
    padding: 16,
    gap: 4,
  },
  name: { fontSize: 16, fontWeight: '700' },
  desc: { fontSize: 13 },
  metaRow: { marginTop: 4 },
  meta: { fontSize: 12 },
  footerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginTop: 8,
  },
  joinPill: {
    paddingHorizontal: 16,
    paddingVertical: 7,
    borderRadius: 8,
    fontSize: 13,
    fontWeight: '700',
    overflow: 'hidden',
  },
  closedPill: {
    paddingHorizontal: 16,
    paddingVertical: 6,
    borderRadius: 8,
    borderWidth: 1,
    fontSize: 13,
    fontWeight: '600',
    overflow: 'hidden',
  },
});
