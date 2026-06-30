import React from 'react';
import { View, StyleSheet, ScrollView, ActivityIndicator } from 'react-native';
import { Stack, useLocalSearchParams, useRouter } from 'expo-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { Text } from '@/src/components/ui/AppText';
import { Button } from '@/src/components/ui/Button';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { leaguesApi } from '@/src/services/api/leaguesApi';
import { standingsKeys } from '@/src/hooks/useStandings';

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
  const queryClient = useQueryClient();
  const { leagueId } = useLocalSearchParams<{ leagueId?: string }>();

  const {
    data: league,
    isLoading,
    isError,
  } = useQuery({
    queryKey: ['league', leagueId],
    enabled: !!leagueId,
    queryFn: async () => (await leaguesApi.getLeagueById(leagueId!)).data,
  });

  const joinMutation = useMutation({
    mutationFn: () => leaguesApi.joinLeague(leagueId!),
    onSuccess: async () => {
      // Refresh /user/me so the just-joined league appears in the leagues
      // list the picks screen selects from.
      await queryClient.invalidateQueries({ queryKey: standingsKeys.me });
      router.replace({ pathname: '/(tabs)/picks', params: { leagueId } } as never);
    },
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
                {league.members.length}{' '}
                {league.members.length === 1 ? 'member' : 'members'}
              </Text>
              <Text style={[styles.metaRow, { color: theme.textMuted }]}>
                {league.isPublic ? 'Public league' : 'Private league'}
              </Text>
            </View>

            <View style={styles.actions}>
              <Button
                title={joinMutation.isPending ? 'Joining…' : 'Join League'}
                onPress={() => joinMutation.mutate()}
                loading={joinMutation.isPending}
              />
              <Button title="Not now" variant="ghost" onPress={dismiss} />
            </View>

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
