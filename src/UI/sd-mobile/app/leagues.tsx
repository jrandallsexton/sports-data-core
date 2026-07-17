import React, { useState } from 'react';
import { View, ScrollView, TouchableOpacity, StyleSheet, ActivityIndicator } from 'react-native';
import { Stack, useRouter } from 'expo-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import Toast from 'react-native-toast-message';

import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { CloneLeagueModal } from '@/src/components/features/leagues/CloneLeagueModal';
import { leaguesApi, type LeagueSummary } from '@/src/services/api/leaguesApi';
import { standingsKeys } from '@/src/hooks/useStandings';

export const leaguesKeys = {
  mine: ['leagues', 'mine'] as const,
};

// Mirrors YourLeaguesCard's SPORT_ICON map.
const SPORT_ICON: Record<LeagueSummary['sport'], string> = {
  FootballNcaa: '🏈',
  FootballNfl: '🏈',
  BaseballMlb: '⚾',
};

const PICK_TYPE_LABEL: Record<LeagueSummary['leagueType'], string> = {
  StraightUp: 'Straight Up',
  AgainstTheSpread: 'Against The Spread',
  OverUnder: 'Over / Under',
};

/**
 * "My Leagues" — mobile's league management surface, mirroring sd-ui's
 * /app/leagues. One card per league with Picks + Duplicate.
 *
 * No Settings action yet: mobile has no league-settings screen (only
 * create-league and league-invite exist), so the card stops at what mobile can
 * actually route to. Add it here when that screen lands.
 *
 * Past-season leagues aren't shown — the BE excludes them unless the caller
 * opts in via includeDeactivated, and mobile has no past-leagues toggle yet
 * (web grew one in #519). That's also why no card needs to hide Duplicate: every
 * league reaching this screen is active and therefore cloneable.
 */
export default function LeaguesScreen() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const router = useRouter();
  const queryClient = useQueryClient();

  const [cloneTarget, setCloneTarget] = useState<LeagueSummary | null>(null);

  const { data: leagues = [], isLoading } = useQuery({
    queryKey: leaguesKeys.mine,
    queryFn: () => leaguesApi.getUserLeagues().then((r) => r.data),
  });

  const cloneMutation = useMutation({
    mutationFn: ({ name, inviteMembers }: { name: string; inviteMembers: boolean }) => {
      if (!cloneTarget) throw new Error('No league selected to duplicate.');
      return leaguesApi.cloneLeague(cloneTarget.id, { name, inviteMembers }).then((r) => r.data);
    },
    onSuccess: async () => {
      setCloneTarget(null);
      Toast.show({ type: 'success', text1: 'League duplicated!' });
      // Refresh both surfaces that list leagues: this screen and the home
      // YourLeaguesCard (which reads leagues off /user/me).
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: leaguesKeys.mine }),
        queryClient.invalidateQueries({ queryKey: standingsKeys.me }),
      ]);
    },
    onError: (err: unknown) => {
      const serverMessage = (
        err as { response?: { data?: { errors?: { errorMessage?: string }[] } } }
      )?.response?.data?.errors?.[0]?.errorMessage;
      Toast.show({
        type: 'error',
        text1: 'Could not duplicate league',
        text2: serverMessage || 'Something went wrong. Please try again.',
      });
    },
  });

  const openPicks = (leagueId: string) =>
    router.push({ pathname: '/(tabs)/picks', params: { leagueId } } as never);

  return (
    <>
      <Stack.Screen
        options={{
          title: 'My Leagues',
          headerStyle: { backgroundColor: theme.card },
          headerTintColor: theme.text,
        }}
      />

      <ScrollView
        style={{ backgroundColor: theme.background }}
        contentContainerStyle={styles.content}
      >
        {isLoading ? (
          <ActivityIndicator style={styles.loading} color={theme.tint} />
        ) : leagues.length === 0 ? (
          <View style={styles.empty}>
            <Text style={[styles.emptyText, { color: theme.textMuted }]}>
              You&rsquo;re not part of any leagues yet.
            </Text>
            <TouchableOpacity
              style={[styles.createButton, { backgroundColor: theme.tint }]}
              onPress={() => router.push('/create-league' as never)}
            >
              <Text style={[styles.createButtonText, { color: theme.textOnAccent }]}>
                Create League
              </Text>
            </TouchableOpacity>
          </View>
        ) : (
          leagues.map((league) => (
            <View
              key={league.id}
              style={[styles.card, { backgroundColor: theme.card, borderColor: theme.border }]}
            >
              <View style={styles.cardHeader}>
                <Text
                  style={styles.cardIcon}
                  accessibilityElementsHidden
                  importantForAccessibility="no-hide-descendants"
                >
                  {SPORT_ICON[league.sport]}
                </Text>
                <Text style={[styles.cardName, { color: theme.text }]} numberOfLines={1}>
                  {league.name}
                </Text>
              </View>

              <Text style={[styles.cardMeta, { color: theme.textMuted }]}>
                {PICK_TYPE_LABEL[league.leagueType] ?? league.leagueType}
                {league.useConfidencePoints ? ' · Confidence' : ''}
                {' · '}
                {league.memberCount} member{league.memberCount === 1 ? '' : 's'}
              </Text>

              <View style={styles.cardActions}>
                <TouchableOpacity
                  style={[styles.action, { backgroundColor: theme.tint }]}
                  onPress={() => openPicks(league.id)}
                  accessibilityRole="button"
                  accessibilityLabel={`Open picks for ${league.name}`}
                >
                  <Text style={[styles.actionText, { color: theme.textOnAccent }]}>Picks</Text>
                </TouchableOpacity>
                <TouchableOpacity
                  style={[styles.action, styles.actionSecondary, { borderColor: theme.border }]}
                  onPress={() => setCloneTarget(league)}
                  accessibilityRole="button"
                  accessibilityLabel={`Duplicate ${league.name}`}
                >
                  <Text style={[styles.actionText, { color: theme.text }]}>Duplicate</Text>
                </TouchableOpacity>
              </View>
            </View>
          ))
        )}
      </ScrollView>

      <CloneLeagueModal
        visible={!!cloneTarget}
        league={cloneTarget}
        submitting={cloneMutation.isPending}
        onClose={() => {
          if (!cloneMutation.isPending) setCloneTarget(null);
        }}
        onConfirm={(name, inviteMembers) => cloneMutation.mutate({ name, inviteMembers })}
      />
    </>
  );
}

const styles = StyleSheet.create({
  content: { padding: 16, gap: 12 },
  loading: { marginTop: 32 },
  empty: { alignItems: 'center', gap: 16, marginTop: 48 },
  emptyText: { fontSize: 15 },
  createButton: { paddingHorizontal: 20, paddingVertical: 12, borderRadius: 10 },
  createButtonText: { fontSize: 15, fontWeight: '700' },
  card: {
    borderRadius: 14,
    borderWidth: StyleSheet.hairlineWidth,
    padding: 16,
    gap: 8,
  },
  cardHeader: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  cardIcon: { fontSize: 18 },
  cardName: { fontSize: 17, fontWeight: '700', flexShrink: 1 },
  cardMeta: { fontSize: 13 },
  cardActions: { flexDirection: 'row', gap: 10, paddingTop: 6 },
  action: {
    flex: 1,
    paddingVertical: 10,
    borderRadius: 10,
    alignItems: 'center',
  },
  actionSecondary: { borderWidth: 1 },
  actionText: { fontSize: 14, fontWeight: '700' },
});
