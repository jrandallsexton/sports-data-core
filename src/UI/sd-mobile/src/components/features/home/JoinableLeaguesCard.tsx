import React, { useState } from 'react';
import { View, StyleSheet, TouchableOpacity } from 'react-native';
import { useRouter } from 'expo-router';
import { useQuery } from '@tanstack/react-query';

import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { leaguesApi, leaguesKeys, type PublicLeague } from '@/src/services/api/leaguesApi';
import { JoinLeagueConfirmSheet } from '@/src/components/features/leagues/JoinLeagueConfirmSheet';
import { JoinClosesLabel } from '@/src/components/features/leagues/JoinClosesLabel';
import { SPORT_ICON, SPORT_LABEL } from '@/src/components/features/leagues/joinDisplay';

const MAX_ROWS = 4;

/**
 * Tier 3 home rail — "Leagues you can join". Web parity with sd-ui's
 * JoinableLeaguesCard: surfaces public-league discovery on the landing page so
 * a league-less (or any) user lands on actionable joins, not a bare countdown.
 * Renders nothing while loading or when there is nothing joinable, so the home
 * page never shows an empty shell. See docs/mobile/web-parity-join-discovery.md.
 */
export function JoinableLeaguesCard() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const router = useRouter();
  const [confirming, setConfirming] = useState<PublicLeague | null>(null);

  const { data } = useQuery({
    queryKey: leaguesKeys.public,
    queryFn: async () => (await leaguesApi.getPublicLeagues()).data,
  });

  const joinable = (data ?? []).filter((l) => l.isJoinable);
  if (joinable.length === 0) return null;

  const visible = joinable.slice(0, MAX_ROWS);

  return (
    <View style={[styles.card, { backgroundColor: theme.card, borderColor: theme.border }]}>
      <View style={styles.headerRow}>
        <Text style={[styles.eyebrow, { color: theme.tint }]}>LEAGUES YOU CAN JOIN</Text>
        <TouchableOpacity
          onPress={() => router.push('/league/discover' as never)}
          hitSlop={8}
          accessibilityRole="button"
          accessibilityLabel="Browse all public leagues"
        >
          <Text style={[styles.browse, { color: theme.tint }]}>Browse all ›</Text>
        </TouchableOpacity>
      </View>

      <View style={styles.list}>
        {visible.map((league) => (
          <TouchableOpacity
            key={league.id}
            onPress={() => setConfirming(league)}
            activeOpacity={0.6}
            accessibilityRole="button"
            accessibilityLabel={`Join ${league.name}`}
            style={[styles.row, { borderTopColor: theme.border }]}
          >
            <View style={styles.info}>
              <Text style={[styles.name, { color: theme.text }]} numberOfLines={1}>
                {league.name}
              </Text>
              <View style={styles.metaRow}>
                <Text style={[styles.meta, { color: theme.textMuted }]}>
                  {SPORT_ICON[league.sport] ?? '🏆'} {SPORT_LABEL[league.sport] ?? league.sport}{' '}
                  {league.seasonYear} · {league.memberCount}{' '}
                  {league.memberCount === 1 ? 'member' : 'members'} ·{' '}
                </Text>
                <JoinClosesLabel
                  closesAtUtc={league.closesAtUtc}
                  isJoinable={league.isJoinable}
                  style={styles.meta}
                />
              </View>
            </View>
            <View style={[styles.joinPill, { backgroundColor: theme.tint }]}>
              <Text style={[styles.joinText, { color: theme.textOnAccent }]}>Join</Text>
            </View>
          </TouchableOpacity>
        ))}
      </View>

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
  card: {
    borderRadius: 14,
    borderWidth: StyleSheet.hairlineWidth,
    padding: 16,
  },
  headerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 6,
  },
  eyebrow: { fontSize: 11, fontWeight: '700', letterSpacing: 1.5 },
  browse: { fontSize: 12, fontWeight: '700' },
  list: {},
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 12,
    paddingVertical: 10,
    borderTopWidth: StyleSheet.hairlineWidth,
  },
  info: { flex: 1, minWidth: 0 },
  name: { fontSize: 15, fontWeight: '600' },
  metaRow: { flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', marginTop: 2 },
  meta: { fontSize: 12 },
  joinPill: { paddingHorizontal: 14, paddingVertical: 6, borderRadius: 8 },
  joinText: { fontSize: 13, fontWeight: '700' },
});
