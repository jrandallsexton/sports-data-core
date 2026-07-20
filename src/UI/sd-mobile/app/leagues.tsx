import React, { useState } from 'react';
import {
  View,
  ScrollView,
  TouchableOpacity,
  StyleSheet,
  ActivityIndicator,
  useWindowDimensions,
} from 'react-native';
import { Stack, useRouter } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import Toast from 'react-native-toast-message';

import { Text } from '@/src/components/ui/AppText';
import { SegmentedControl } from '@/src/components/ui/SegmentedControl';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { CloneLeagueModal } from '@/src/components/features/leagues/CloneLeagueModal';
import { LeagueCard } from '@/src/components/features/leagues/LeagueCard';
import { leaguesApi, type LeagueSummary } from '@/src/services/api/leaguesApi';
import { standingsKeys } from '@/src/hooks/useStandings';

export const leaguesKeys = {
  mine: ['leagues', 'mine'] as const,
};

const ALL_LEAGUES = 'All';
const CONTENT_PADDING = 16; // matches styles.content horizontal padding
const GRID_GAP = 12; // matches styles.grid gap

/**
 * "My Leagues" — mobile's league management surface, mirroring sd-ui's
 * /app/leagues. One card per league with a collapsible overview (standing in for
 * web's dedicated /app/league/{id} page), plus Picks + Duplicate.
 *
 * Filters mirror the web's: a sport-league filter and a past-leagues toggle. A
 * season-year filter is expected on both platforms later.
 */
export default function LeaguesScreen() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const router = useRouter();
  const queryClient = useQueryClient();

  const [cloneTarget, setCloneTarget] = useState<LeagueSummary | null>(null);
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [showPast, setShowPast] = useState(false);
  const [leagueFilter, setLeagueFilter] = useState<string>(ALL_LEAGUES);

  // Always fetch past leagues so the toggle is instant rather than a refetch.
  // A user's league count is small, and every row is needed the moment they
  // flip "Past" on.
  const {
    data: leagues = [],
    isLoading,
    isError,
    refetch,
  } = useQuery({
    queryKey: leaguesKeys.mine,
    queryFn: () => leaguesApi.getUserLeagues({ includeDeactivated: true }).then((r) => r.data),
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

  // Everything below is derived from `leagues` rather than mirrored into state,
  // so the filters can't drift out of sync with a refetch.
  const hasPast = leagues.some((l) => l.deactivatedUtc);
  const scoped = showPast ? leagues : leagues.filter((l) => !l.deactivatedUtc);

  const availableLeagues = [...new Set(scoped.map((l) => l.league).filter(Boolean))].sort();

  // Self-healing: if the selected league leaves scope (e.g. it only existed
  // among past leagues and "Past" was switched off), fall back to All instead
  // of stranding the user on an empty list.
  const activeFilter = availableLeagues.includes(leagueFilter as LeagueSummary['league'])
    ? leagueFilter
    : ALL_LEAGUES;

  const visibleLeagues =
    activeFilter === ALL_LEAGUES ? scoped : scoped.filter((l) => l.league === activeFilter);

  const showFilterBar = leagues.length > 0 && (hasPast || availableLeagues.length > 1);
  const filterOptions = [ALL_LEAGUES, ...availableLeagues].map((v) => ({ value: v, label: v }));

  const openPicks = (leagueId: string) =>
    router.push({ pathname: '/(tabs)/picks', params: { leagueId } } as never);

  // Responsive columns: phones stay single-column; tablets flow into 2 (portrait)
  // or 3 (wide/landscape) so the cards stop wasting horizontal space. Width-driven
  // (not device type) so it also tracks orientation and split-screen.
  const { width } = useWindowDimensions();
  const columns = width >= 1000 ? 3 : width >= 680 ? 2 : 1;
  const cardWidth =
    columns === 1
      ? undefined
      : (width - CONTENT_PADDING * 2 - GRID_GAP * (columns - 1)) / columns;

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
        {showFilterBar && (
          <View style={styles.filterBar}>
            {availableLeagues.length > 1 && (
              <SegmentedControl
                value={activeFilter}
                options={filterOptions}
                onChange={setLeagueFilter}
                accessibilityLabel="Filter by league"
              />
            )}
            {hasPast && (
              <TouchableOpacity
                style={[
                  styles.pastToggle,
                  { borderColor: showPast ? theme.tint : theme.border },
                  showPast && { backgroundColor: theme.tint },
                ]}
                onPress={() => setShowPast(!showPast)}
                accessibilityRole="button"
                accessibilityLabel={showPast ? 'Hide past leagues' : 'Show past leagues'}
                accessibilityState={{ selected: showPast }}
              >
                <Ionicons
                  name={showPast ? 'eye-outline' : 'eye-off-outline'}
                  size={14}
                  color={showPast ? theme.textOnAccent : theme.textMuted}
                />
                <Text
                  style={[
                    styles.pastToggleText,
                    { color: showPast ? theme.textOnAccent : theme.textMuted },
                  ]}
                >
                  Past
                </Text>
              </TouchableOpacity>
            )}
          </View>
        )}

        {isLoading ? (
          <ActivityIndicator style={styles.loading} color={theme.tint} />
        ) : isError ? (
          // Must precede the empty branch: on failure `leagues` falls back to []
          // and would otherwise render "you're not part of any leagues yet",
          // making a network blip look like data loss.
          <View style={styles.empty}>
            <Text style={[styles.emptyText, { color: theme.textMuted }]}>
              Couldn&rsquo;t load your leagues.
            </Text>
            <TouchableOpacity
              style={[styles.createButton, { backgroundColor: theme.tint }]}
              onPress={() => refetch()}
              accessibilityRole="button"
              accessibilityLabel="Retry loading your leagues"
            >
              <Text style={[styles.createButtonText, { color: theme.textOnAccent }]}>
                Try again
              </Text>
            </TouchableOpacity>
          </View>
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
        ) : visibleLeagues.length === 0 ? (
          <Text style={[styles.emptyText, { color: theme.textMuted }]}>
            No leagues match this filter.
          </Text>
        ) : (
          <View style={styles.grid}>
            {visibleLeagues.map((league) => (
              <View
                key={league.id}
                style={cardWidth != null ? { width: cardWidth } : styles.cardFull}
              >
                <LeagueCard
                  league={league}
                  expanded={expandedId === league.id}
                  onToggleExpanded={() =>
                    setExpandedId((prev) => (prev === league.id ? null : league.id))
                  }
                  onOpenPicks={() => openPicks(league.id)}
                  onDuplicate={() => setCloneTarget(league)}
                />
              </View>
            ))}
          </View>
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
  // Wrapping grid; cards top-align (flex-start) so an expanded card grows down
  // without stretching its shorter row-mates. Item widths are set inline.
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: 12, alignItems: 'flex-start' },
  cardFull: { width: '100%' },
  filterBar: { gap: 10 },
  pastToggle: {
    alignSelf: 'flex-start',
    flexDirection: 'row',
    alignItems: 'center',
    gap: 5,
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 999,
    borderWidth: 1,
  },
  pastToggleText: { fontSize: 12, fontWeight: '700' },
  loading: { marginTop: 32 },
  empty: { alignItems: 'center', gap: 16, marginTop: 48 },
  emptyText: { fontSize: 15, textAlign: 'center' },
  createButton: { paddingHorizontal: 20, paddingVertical: 12, borderRadius: 10 },
  createButtonText: { fontSize: 15, fontWeight: '700' },
});
