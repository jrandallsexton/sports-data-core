import React from 'react';
import { View, StyleSheet, ScrollView, ActivityIndicator } from 'react-native';
import { Stack, useLocalSearchParams, useRouter } from 'expo-router';
import { useQuery } from '@tanstack/react-query';

import { Text } from '@/src/components/ui/AppText';
import { Button } from '@/src/components/ui/Button';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { leaguesApi, leaguesKeys } from '@/src/services/api/leaguesApi';
import { JoinClosesLabel } from '@/src/components/features/leagues/JoinClosesLabel';
import { useJoinLeagueMutation } from '@/src/hooks/useJoinLeagueMutation';

// League ids are GUIDs. Used to reject malformed/array-like route params
// before any request is built.
const GUID_RE =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

/**
 * League-invite preview. Reached by tapping a LeagueInvite push (see
 * docs/mobile/league-invite-deep-link.md). Shows the league and a single Join
 * CTA — tapping a notification is not consent to join. On Join we add the user
 * to the league, refresh /user/me so the league is in their list, then forward
 * into that league's picks page. Dismiss just closes with no state change.
 */
export default function LeagueInviteScreen() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const router = useRouter();
  const params = useLocalSearchParams<{ leagueId?: string | string[] }>();
  // Route params can arrive as undefined or an array (duplicate keys); only a
  // single, GUID-shaped string is a real league id. Anything else stays
  // undefined, which disables the query (so league is never populated) and
  // renders the existing "Invite unavailable" path — no API request is built.
  const leagueId =
    typeof params.leagueId === 'string' && GUID_RE.test(params.leagueId)
      ? params.leagueId
      : undefined;

  const {
    data: league,
    isLoading,
    isError,
  } = useQuery({
    queryKey: leaguesKeys.detail(leagueId ?? 'invalid'),
    enabled: !!leagueId,
    queryFn: async () => (await leaguesApi.getLeagueById(leagueId!)).data,
  });

  // Shared hook: invalidates discovery + My Leagues + /user/me, so a join
  // here also drops the league from the public browse list.
  const joinMutation = useJoinLeagueMutation((id) => {
    router.replace({ pathname: '/(tabs)/picks', params: { leagueId: id } } as never);
  });

  const dismiss = () => {
    if (router.canGoBack()) router.back();
    else router.replace('/(tabs)' as never);
  };

  return (
    <View style={[styles.container, { backgroundColor: theme.background }]}>
      <Stack.Screen options={{ title: 'League Invite', presentation: 'modal' }} />

      <ScrollView contentContainerStyle={styles.content}>
        {isLoading ? (
          <ActivityIndicator color={theme.tint} style={styles.spinner} />
        ) : isError || !league ? (
          <View style={styles.centered}>
            <Text style={[styles.title, { color: theme.text }]}>
              Invite unavailable
            </Text>
            <Text style={[styles.subtitle, { color: theme.textMuted }]}>
              This invite couldn&apos;t be loaded. It may have been revoked.
            </Text>
            <Button title="Close" variant="secondary" onPress={dismiss} />
          </View>
        ) : (
          <>
            <Text style={[styles.kicker, { color: theme.textMuted }]}>
              You&apos;ve been invited to
            </Text>
            <Text style={[styles.title, { color: theme.text }]}>{league.name}</Text>

            {league.description ? (
              <Text style={[styles.subtitle, { color: theme.textMuted }]}>
                {league.description}
              </Text>
            ) : null}

            <View style={[styles.metaCard, { backgroundColor: theme.card, borderColor: theme.border }]}>
              <Text style={[styles.metaRow, { color: theme.text }]}>
                {league.memberCount}{' '}
                {league.memberCount === 1 ? 'member' : 'members'}
              </Text>
              <Text style={[styles.metaRow, { color: theme.textMuted }]}>
                {league.isPublic ? 'Public league' : 'Private league'}
              </Text>
              <JoinClosesLabel
                closesAtUtc={league.closesAtUtc}
                isJoinable={league.isJoinable}
                style={styles.metaRow}
              />
            </View>

            {league.isJoinable === false ? (
              // A shared invite link outlives the league's join window — the BE
              // gate would reject the join, so don't offer it.
              <View style={styles.actions}>
                <Text style={[styles.subtitle, { color: theme.textMuted }]}>
                  This league is no longer accepting new members.
                </Text>
                <Button title="Close" variant="secondary" onPress={dismiss} />
              </View>
            ) : (
              <View style={styles.actions}>
                <Button
                  title={joinMutation.isPending ? 'Joining…' : 'Join League'}
                  onPress={() => joinMutation.mutate(leagueId!)}
                  loading={joinMutation.isPending}
                />
                <Button title="Not now" variant="ghost" onPress={dismiss} />
              </View>
            )}

            {joinMutation.isError ? (
              <Text style={[styles.errorText, { color: theme.error }]}>
                Couldn&apos;t join the league. Please try again.
              </Text>
            ) : null}
          </>
        )}
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1 },
  content: { padding: 24, gap: 12 },
  centered: { gap: 12, alignItems: 'center', marginTop: 48 },
  spinner: { marginTop: 64 },
  kicker: { fontSize: 14, textTransform: 'uppercase', letterSpacing: 1 },
  title: { fontSize: 26, fontWeight: '700' },
  subtitle: { fontSize: 15, lineHeight: 21 },
  metaCard: { borderWidth: 1, borderRadius: 12, padding: 16, gap: 6, marginTop: 8 },
  metaRow: { fontSize: 15 },
  actions: { gap: 10, marginTop: 16 },
  errorText: { fontSize: 14, marginTop: 8 },
});
