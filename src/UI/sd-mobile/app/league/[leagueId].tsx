import React, { useEffect, useRef, useState } from 'react';
import {
  View,
  ScrollView,
  StyleSheet,
  ActivityIndicator,
  TouchableOpacity,
  TextInput,
  Share,
  Alert,
} from 'react-native';
import { Stack, useLocalSearchParams, useRouter } from 'expo-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import Toast from 'react-native-toast-message';

import { Text } from '@/src/components/ui/AppText';
import { Button } from '@/src/components/ui/Button';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import {
  leaguesApi,
  leaguesKeys,
  type InviteableUser,
  type LeagueDetail,
} from '@/src/services/api/leaguesApi';
import { useCurrentUser, standingsKeys } from '@/src/hooks/useStandings';

// League ids are GUIDs — same guard as the league-invite deep-link screen.
const GUID_RE =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

// "Sep 1, 2026" from an ISO instant; null for a missing bound.
function formatWindowBound(iso: string | null): string | null {
  if (!iso) return null;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return null;
  return d.toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

function windowLabel(league: LeagueDetail): string {
  const start = formatWindowBound(league.startsOn);
  const end = formatWindowBound(league.endsOn);
  if (!start && !end) return 'Full Season';
  if (start && end) return `${start} – ${end}`;
  if (start) return `From ${start}`;
  return `Through ${end}`;
}

/**
 * League detail — mobile parity with web's /app/league/{id}: the landing page
 * after creating a league. Shows the league's parameters, its members, invite
 * affordances (share link, registered-user search, email), and a
 * commissioner-only delete. Past (deactivated) leagues render read-only:
 * invite and delete are hidden.
 */
export default function LeagueDetailScreen() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const router = useRouter();
  const queryClient = useQueryClient();
  const params = useLocalSearchParams<{ leagueId?: string | string[] }>();
  const { data: me } = useCurrentUser();

  const leagueId =
    typeof params.leagueId === 'string' && GUID_RE.test(params.leagueId)
      ? params.leagueId
      : undefined;

  const {
    data: league,
    isLoading,
    isError,
    refetch,
  } = useQuery({
    queryKey: leaguesKeys.detail(leagueId ?? 'invalid'),
    enabled: !!leagueId,
    queryFn: async () => (await leaguesApi.getLeagueById(leagueId!)).data,
  });

  const commissioner = league?.members.find((m) => m.role === 'commissioner');
  const isCommissioner = !!me?.id && me.id === commissioner?.userId;
  const isPast = !!league?.deactivatedUtc;

  // ── Invite: registered-user search (debounced, mirrors web) ────────────────
  const [search, setSearch] = useState('');
  const [results, setResults] = useState<InviteableUser[]>([]);
  const [searching, setSearching] = useState(false);
  const [invitingUserId, setInvitingUserId] = useState<string | null>(null);
  const searchSeq = useRef(0);
  // Invited this session — excluded from later searches (the BE has no record
  // of an in-flight invite). Ref so the async search callback reads current.
  const invitedRef = useRef<Set<string>>(new Set());

  useEffect(() => {
    const term = search.trim();
    if (!leagueId || term.length < 2) {
      setResults([]);
      setSearching(false);
      return;
    }
    setSearching(true);
    const seq = ++searchSeq.current;
    const timer = setTimeout(async () => {
      try {
        const { data } = await leaguesApi.searchInviteableUsers(leagueId, term);
        if (seq === searchSeq.current) {
          setResults(data.filter((u) => !invitedRef.current.has(u.userId)));
        }
      } catch {
        if (seq === searchSeq.current) setResults([]);
      } finally {
        if (seq === searchSeq.current) setSearching(false);
      }
    }, 300);
    return () => clearTimeout(timer);
  }, [search, leagueId]);

  const inviteUser = async (user: InviteableUser) => {
    if (!leagueId || invitingUserId) return;
    setInvitingUserId(user.userId);
    try {
      await leaguesApi.inviteUser(leagueId, user.userId);
      invitedRef.current.add(user.userId);
      setResults((prev) => prev.filter((u) => u.userId !== user.userId));
      Toast.show({ type: 'success', text1: `Invited @${user.username}` });
    } catch {
      Toast.show({ type: 'error', text1: 'Could not send invite. Please try again.' });
    } finally {
      setInvitingUserId(null);
    }
  };

  // ── Invite: email ──────────────────────────────────────────────────────────
  const [inviteeName, setInviteeName] = useState('');
  const [inviteEmail, setInviteEmail] = useState('');
  const [sendingEmail, setSendingEmail] = useState(false);

  const sendEmailInvite = async () => {
    const email = inviteEmail.trim();
    if (!leagueId || !email || sendingEmail) return;
    setSendingEmail(true);
    try {
      await leaguesApi.sendInvite(leagueId, email, inviteeName.trim() || null);
      setInviteeName('');
      setInviteEmail('');
      Toast.show({ type: 'success', text1: 'Invitation sent!' });
    } catch {
      Toast.show({ type: 'error', text1: 'Could not send the invitation.' });
    } finally {
      setSendingEmail(false);
    }
  };

  // ── Invite: share link (mobile-native affordance for web's copy-link) ──────
  const shareInviteLink = async () => {
    if (!leagueId || !league) return;
    const inviteUrl = `https://www.sportdeets.com/app/join/${leagueId.replace(/-/g, '')}`;
    try {
      await Share.share({
        message: `Join my sportDeets league "${league.name}": ${inviteUrl}`,
      });
    } catch {
      // User dismissed the share sheet — nothing to do.
    }
  };

  // ── Commissioner delete ────────────────────────────────────────────────────
  const deleteMutation = useMutation({
    mutationFn: () => leaguesApi.deleteLeague(leagueId!),
    onSuccess: async () => {
      // The league is gone — drop its detail cache entirely (an invalidate
      // would just refetch a 404) and refresh the lists that contained it.
      queryClient.removeQueries({ queryKey: leaguesKeys.detail(leagueId!) });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: leaguesKeys.mine }),
        queryClient.invalidateQueries({ queryKey: standingsKeys.me }),
      ]);
      Toast.show({ type: 'success', text1: 'League deleted.' });
      router.replace('/leagues' as never);
    },
    onError: (err: unknown) => {
      // Surface the BE's specific refusal (e.g. picks already recorded).
      const serverMessage = (
        err as { response?: { data?: { errors?: { errorMessage?: string }[] } } }
      )?.response?.data?.errors?.[0]?.errorMessage;
      Alert.alert(
        'Could not delete league',
        serverMessage || 'Something went wrong. Please try again.',
      );
    },
  });

  const confirmDelete = () => {
    Alert.alert(
      'Delete League?',
      'Are you sure you want to delete this league? This cannot be undone.',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Yes, Delete League',
          style: 'destructive',
          onPress: () => deleteMutation.mutate(),
        },
      ],
    );
  };

  const openPicks = () => {
    if (!leagueId) return;
    router.push({ pathname: '/(tabs)/picks', params: { leagueId } } as never);
  };

  return (
    <>
      <Stack.Screen
        options={{
          title: league?.name ?? 'League',
          headerStyle: { backgroundColor: theme.card },
          headerTintColor: theme.text,
          headerBackButtonDisplayMode: 'minimal',
        }}
      />
      <ScrollView
        style={{ backgroundColor: theme.background }}
        contentContainerStyle={styles.content}
        keyboardShouldPersistTaps="handled"
      >
        {!leagueId ? (
          // Malformed/missing id: the query never ran, so a retry can't help —
          // distinct from a fetch failure on a valid id below.
          <View style={styles.centered}>
            <Text style={[styles.emptyText, { color: theme.textMuted }]}>
              This league link isn&rsquo;t valid.
            </Text>
            <Button
              title="Back"
              variant="secondary"
              onPress={() => (router.canGoBack() ? router.back() : router.replace('/(tabs)' as never))}
            />
          </View>
        ) : isLoading ? (
          <ActivityIndicator style={styles.loading} color={theme.tint} />
        ) : isError || !league ? (
          <View style={styles.centered}>
            <Text style={[styles.emptyText, { color: theme.textMuted }]}>
              Couldn&rsquo;t load this league.
            </Text>
            <Button title="Try again" variant="secondary" onPress={() => refetch()} />
          </View>
        ) : (
          <>
            {/* Parameters */}
            <View style={[styles.card, { backgroundColor: theme.card, borderColor: theme.border }]}>
              {league.description ? (
                <Text style={[styles.byline, { color: theme.textMuted }]}>
                  {league.description}
                </Text>
              ) : null}
              <Button
                title={isPast ? 'View Picks' : 'Make Your Picks'}
                onPress={openPicks}
                fullWidth
                size="md"
                style={styles.picksButton}
              />
              {(
                [
                  ['Pick Type', league.pickType],
                  ['Tiebreaker', league.tiebreakerType],
                  ['Tie Policy', league.tiebreakerTiePolicy],
                  ['Confidence Points', league.useConfidencePoints ? 'Yes' : 'No'],
                  ['Ranking Filter', league.rankingFilter ?? 'None'],
                  ['League Window', windowLabel(league)],
                  ['Visibility', league.isPublic ? 'Public' : 'Private'],
                  [
                    'Conferences',
                    [...new Set(league.conferenceSlugs ?? [])].join(', ') || 'None',
                  ],
                ] as const
              ).map(([label, value]) => (
                <View
                  key={label}
                  style={[styles.paramRow, { borderBottomColor: theme.separator }]}
                >
                  <Text style={[styles.paramLabel, { color: theme.textMuted }]}>{label}</Text>
                  <Text style={[styles.paramValue, { color: theme.text }]}>{value}</Text>
                </View>
              ))}
            </View>

            {/* Members */}
            <View style={[styles.card, { backgroundColor: theme.card, borderColor: theme.border }]}>
              <Text style={[styles.sectionTitle, { color: theme.textMuted }]}>Members</Text>
              {league.members.length > 0 ? (
                league.members.map((member) => (
                  <View key={member.userId} style={styles.memberRow}>
                    {member.role === 'commissioner' && (
                      <Text accessibilityLabel="Commissioner">👑 </Text>
                    )}
                    <Text style={[styles.memberName, { color: theme.text }]}>
                      {member.username}
                    </Text>
                  </View>
                ))
              ) : (
                <Text style={[styles.emptyText, { color: theme.textMuted }]}>
                  No members yet.
                </Text>
              )}
            </View>

            {/* Invite — hidden for past leagues (read-only records) */}
            {!isPast && (
              <View style={[styles.card, { backgroundColor: theme.card, borderColor: theme.border }]}>
                <Text style={[styles.sectionTitle, { color: theme.textMuted }]}>
                  Invite Friends
                </Text>
                <Button
                  title="Share Invite Link"
                  onPress={shareInviteLink}
                  variant="secondary"
                  fullWidth
                  size="md"
                />

                <Text style={[styles.inviteSub, { color: theme.textMuted }]}>
                  Invite a sportDeets user
                </Text>
                <TextInput
                  style={[
                    styles.input,
                    { backgroundColor: theme.background, borderColor: theme.border, color: theme.text },
                  ]}
                  placeholder="Search by username or name"
                  placeholderTextColor={theme.textMuted}
                  value={search}
                  onChangeText={setSearch}
                  autoCapitalize="none"
                  autoCorrect={false}
                />
                {searching && (
                  <Text style={[styles.inviteHint, { color: theme.textMuted }]}>Searching…</Text>
                )}
                {results.map((user) => (
                  <View key={user.userId} style={styles.resultRow}>
                    <View style={styles.resultText}>
                      <Text style={[styles.memberName, { color: theme.text }]}>
                        {user.displayName}
                      </Text>
                      <Text style={[styles.inviteHint, { color: theme.textMuted }]}>
                        @{user.username}
                      </Text>
                    </View>
                    <TouchableOpacity
                      style={[styles.inviteButton, { borderColor: theme.tint }]}
                      onPress={() => inviteUser(user)}
                      disabled={invitingUserId !== null}
                      accessibilityRole="button"
                      accessibilityLabel={`Invite ${user.username}`}
                    >
                      <Text style={{ color: theme.tint, fontWeight: '600' }}>
                        {invitingUserId === user.userId ? 'Inviting…' : 'Invite'}
                      </Text>
                    </TouchableOpacity>
                  </View>
                ))}

                <Text style={[styles.inviteSub, { color: theme.textMuted }]}>
                  Or invite by email
                </Text>
                <TextInput
                  style={[
                    styles.input,
                    { backgroundColor: theme.background, borderColor: theme.border, color: theme.text },
                  ]}
                  placeholder="Name (optional)"
                  placeholderTextColor={theme.textMuted}
                  value={inviteeName}
                  onChangeText={setInviteeName}
                />
                <TextInput
                  style={[
                    styles.input,
                    { backgroundColor: theme.background, borderColor: theme.border, color: theme.text },
                  ]}
                  placeholder="friend@example.com"
                  placeholderTextColor={theme.textMuted}
                  value={inviteEmail}
                  onChangeText={setInviteEmail}
                  autoCapitalize="none"
                  autoCorrect={false}
                  keyboardType="email-address"
                />
                <Button
                  title="Send Invitation"
                  onPress={sendEmailInvite}
                  loading={sendingEmail}
                  disabled={!inviteEmail.trim()}
                  fullWidth
                  size="md"
                />
              </View>
            )}

            {/* Danger zone — commissioner only, active leagues only */}
            {isCommissioner && !isPast && (
              <View style={[styles.card, { backgroundColor: theme.card, borderColor: theme.error }]}>
                <Text style={[styles.sectionTitle, { color: theme.errorText }]}>
                  Danger Zone
                </Text>
                <Button
                  title={deleteMutation.isPending ? 'Deleting…' : 'Delete League'}
                  onPress={confirmDelete}
                  variant="danger"
                  fullWidth
                  size="md"
                  disabled={deleteMutation.isPending}
                />
              </View>
            )}
          </>
        )}
      </ScrollView>
    </>
  );
}

const styles = StyleSheet.create({
  content: { padding: 16, gap: 14, paddingBottom: 40 },
  loading: { marginTop: 48 },
  centered: { alignItems: 'center', gap: 14, marginTop: 48 },
  emptyText: { fontSize: 14 },
  card: {
    borderRadius: 14,
    borderWidth: StyleSheet.hairlineWidth,
    padding: 16,
    gap: 10,
  },
  byline: { fontSize: 13, lineHeight: 18 },
  picksButton: { marginBottom: 4 },
  paramRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    gap: 16,
    paddingVertical: 9,
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  paramLabel: { fontSize: 13, fontWeight: '600', flexShrink: 0 },
  paramValue: { fontSize: 13, textAlign: 'right', flex: 1 },
  sectionTitle: {
    fontSize: 12,
    fontWeight: '700',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  memberRow: { flexDirection: 'row', alignItems: 'center', paddingVertical: 5 },
  memberName: { fontSize: 15, fontWeight: '600' },
  inviteSub: { fontSize: 12, fontWeight: '600', marginTop: 8 },
  input: {
    borderWidth: 1,
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 15,
  },
  inviteHint: { fontSize: 12 },
  resultRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 12,
    paddingVertical: 6,
  },
  resultText: { flex: 1 },
  inviteButton: {
    borderWidth: 1.5,
    borderRadius: 999,
    paddingHorizontal: 14,
    paddingVertical: 6,
  },
});
