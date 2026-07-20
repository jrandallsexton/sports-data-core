import React, { useState, useEffect, useMemo, useCallback, useRef } from 'react';
import {
  View,
  FlatList,
  ScrollView,
  StyleSheet,
  RefreshControl,
  Pressable,
  useWindowDimensions,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { Text } from '@/src/components/ui/AppText';
import { useRouter, useLocalSearchParams } from 'expo-router';
import { useNavigation } from '@react-navigation/native';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { MatchupCard } from '@/src/components/features/games/MatchupCard';
import { LeagueWeekSelector } from '@/src/components/features/selectors/LeagueWeekSelector';
import { LoadingSpinner } from '@/src/components/ui/LoadingSpinner';
import { EmptyState } from '@/src/components/ui/EmptyState';
import { usePicks, useSubmitPick } from '@/src/hooks/useContest';
import { useMatchups } from '@/src/hooks/useMatchups';
import { useCurrentUser } from '@/src/hooks/useStandings';
import { useImportAvailability, useImportPicks } from '@/src/hooks/useImportPicks';
import { ImportPicksModal } from '@/src/components/features/picks/ImportPicksModal';
import { getLeagues } from '@/src/lib/leagues';
import { resolveSportLeague } from '@/src/utils/sportLinks';
import { useQuery } from '@tanstack/react-query';
import { leaguesApi } from '@/src/services/api/leaguesApi';
import { leaguesKeys } from '../leagues';
import type { League } from '@/src/types/models';
import Toast from 'react-native-toast-message';

// ─── Screen ───────────────────────────────────────────────────────────────────

export default function PicksScreen() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const router = useRouter();
  const navigation = useNavigation();

  const {
    data: me,
    isLoading: meLoading,
    refetch: refetchMe,
    isRefetching: meRefetching,
  } = useCurrentUser();
  const leagues = useMemo(() => getLeagues(me), [me]);

  // Optional deep-link param: home "Your Leagues" card pushes
  // /(tabs)/picks?leagueId=<id> so the screen opens on that league directly.
  const { leagueId: leagueIdParam } = useLocalSearchParams<{ leagueId?: string }>();

  // Viewing a PAST (deactivated) league: /user/me is active-only, so a deep-link
  // param that isn't in the active set is fetched on demand (getUserLeagues
  // includes deactivated) and rendered read-only. Reuses the My Leagues query
  // key so arriving from that screen costs no extra request.
  const candidatePastId = useMemo(
    () =>
      leagueIdParam && !leagues.some((l) => l.id === leagueIdParam)
        ? leagueIdParam
        : null,
    [leagueIdParam, leagues],
  );
  const { data: allLeagues, isFetched: allLeaguesFetched } = useQuery({
    queryKey: leaguesKeys.mine,
    queryFn: () =>
      leaguesApi.getUserLeagues({ includeDeactivated: true }).then((r) => r.data),
    enabled: !!candidatePastId,
  });
  const pastLeagueAsLeague = useMemo<League | null>(() => {
    if (!candidatePastId || !allLeagues) return null;
    // Only a genuinely deactivated league becomes a read-only past view — an
    // active league merely missing from a stale /user/me snapshot must not be.
    const found = allLeagues.find((l) => l.id === candidatePastId && l.deactivatedUtc);
    return found
      ? { id: found.id, name: found.name, sport: found.sport, seasonWeeks: found.seasonWeeks }
      : null;
  }, [candidatePastId, allLeagues]);

  // Active leagues plus the viewed past league — used for resolution + selector.
  const selectableLeagues = useMemo(
    () => (pastLeagueAsLeague ? [...leagues, pastLeagueAsLeague] : leagues),
    [leagues, pastLeagueAsLeague],
  );

  const [leagueId, setLeagueId] = useState<string | null>(null);
  const [selectedWeek, setSelectedWeek] = useState<number | null>(null);
  const [hidePicked, setHidePicked] = useState(false);
  const [importOpen, setImportOpen] = useState(false);

  // Latest week = last element of the ascending seasonWeeks list.
  const latestWeek = (l: { seasonWeeks?: number[] } | null | undefined) =>
    l?.seasonWeeks?.length ? l.seasonWeeks[l.seasonWeeks.length - 1] : null;

  // eslint-disable-next-line react-hooks/exhaustive-deps — intentionally excluding leagueId to only initialize/target, not rerun on user selection
  useEffect(() => {
    // Deep-link param wins: an active league, or the on-demand past league.
    if (leagueIdParam) {
      const active = leagues.find((l) => l.id === leagueIdParam);
      if (active) {
        setLeagueId(active.id);
        setSelectedWeek(latestWeek(active));
        return;
      }
      if (pastLeagueAsLeague && pastLeagueAsLeague.id === leagueIdParam) {
        setLeagueId(pastLeagueAsLeague.id);
        setSelectedWeek(latestWeek(pastLeagueAsLeague));
        return;
      }
      // Param is a past league still being fetched → wait rather than default to
      // the first active league (that was the bug).
      if (candidatePastId === leagueIdParam && !allLeaguesFetched) return;
      // Otherwise it's not one of the user's leagues → fall through to default.
    }

    // Initialize once to the first active league.
    if (!leagueId && leagues.length > 0) {
      setLeagueId(leagues[0].id);
      setSelectedWeek(latestWeek(leagues[0]));
    }
  }, [leagues, leagueIdParam, pastLeagueAsLeague, candidatePastId, allLeaguesFetched]);

  const selectedLeague = selectableLeagues.find((l) => l.id === leagueId) ?? null;
  const seasonWeeks = selectedLeague?.seasonWeeks ?? [];

  // Read-only when viewing a deactivated league — no pick submission.
  const isReadOnly = !!pastLeagueAsLeague && leagueId === pastLeagueAsLeague.id;

  // A league with no weeks means this /user/me snapshot predates its slate
  // build. Create/clone return before the slate exists (~700ms), and anything
  // invalidating /user/me on that response caches the one empty frame — which
  // useCurrentUser's 5-minute staleTime then serves for the next five minutes.
  // Refetch to heal, once per league so a genuinely week-less league (e.g. a
  // future-season league whose SeasonWeeks aren't sourced yet) can't loop.
  // A Set, not a single id: with one slot, switching between two week-less
  // leagues evicts the guard and each switch back refetches again.
  const healedLeaguesRef = useRef<Set<string>>(new Set());
  useEffect(() => {
    if (!selectedLeague || seasonWeeks.length > 0) return;
    if (healedLeaguesRef.current.has(selectedLeague.id)) return;
    healedLeaguesRef.current.add(selectedLeague.id);
    refetchMe();
  }, [selectedLeague, seasonWeeks.length, refetchMe]);

  // Snap to the latest week once the weeks actually arrive. The init effect
  // above only runs on league/param change, so without this a heal-refetch
  // would land the data and still leave no week selected.
  useEffect(() => {
    if (selectedWeek !== null) return;
    const week = latestWeek(selectedLeague);
    if (week !== null) setSelectedWeek(week);
  }, [selectedLeague, selectedWeek]);

  const handleLeagueChange = useCallback(
    (id: string) => {
      setLeagueId(id);
      // Search selectableLeagues (active + viewed past) so switching to the past
      // league resolves its weeks instead of transiently clearing selectedWeek.
      const league = selectableLeagues.find((l) => l.id === id);
      setSelectedWeek(latestWeek(league));
    },
    [selectableLeagues],
  );

  const {
    data: myPicks = [],
    isLoading: picksLoading,
    refetch,
    isRefetching,
  } = usePicks(leagueId, selectedWeek);

  const { data: matchupsResponse, isLoading: matchupsLoading } = useMatchups(
    leagueId,
    selectedWeek,
  );
  const submitPick = useSubmitPick();
  const pickType = matchupsResponse?.pickType ?? 'StraightUp';
  // Resolves LeagueWeekMatchupsDto.Sport ("FootballNcaa" | "FootballNfl" |
  // "BaseballMlb") to {sport, league} URL segments. null when the response
  // hasn't arrived yet or the sport enum isn't in the known map — in that
  // case the game-detail navigation is simply disabled.
  const sportLeague = resolveSportLeague(matchupsResponse?.sport);

  const isLoading = meLoading || picksLoading || matchupsLoading;
  const matchups = matchupsResponse?.matchups ?? [];

  const pickMap = useMemo(
    () => new Map(myPicks.map((p) => [p.contestId, p])),
    [myPicks],
  );

  const entries = useMemo(
    () => matchups.map((m) => ({ matchup: m, pick: pickMap.get(m.contestId) ?? null })),
    [matchups, pickMap],
  );

  const total = entries.length;
  // Count picks that correspond to currently-displayed matchups rather
  // than myPicks.length directly — picks and matchups load on separate
  // query cycles, so during a league/week transition (or if myPicks
  // carries a stale pick for a contest no longer in the matchups list)
  // raw myPicks.length can briefly exceed entries with picks. Derive
  // from entries so total / made / allPicked stay aligned with what's
  // on screen.
  const made = entries.filter((e) => e.pick !== null).length;
  const allPicked = total > 0 && made >= total;

  // ── Cross-league pick import ────────────────────────────────────────────────
  // Only check availability once picks + matchups have loaded and there are
  // unpicked games to fill (React Query's enabled replaces the web's manual
  // picks-loaded gating).
  const importEnabled =
    !isReadOnly && !picksLoading && !matchupsLoading && total > 0 && made < total;
  const { data: availabilityData } = useImportAvailability(
    leagueId,
    selectedWeek,
    importEnabled,
  );
  const importPicks = useImportPicks();

  // Enrich the previewed sources for display, scoped to this week's still-unpicked
  // matchups; drop sources with nothing left to offer.
  const importSources = useMemo(() => {
    const avail = availabilityData ?? [];
    const byContest = new Map(matchups.map((m) => [m.contestId, m]));
    return avail
      .map((src) => ({
        leagueId: src.leagueId,
        name: src.name,
        items: src.toImport
          .filter((i) => byContest.has(i.contestId) && !pickMap.has(i.contestId))
          .map((i) => {
            const m = byContest.get(i.contestId)!;
            const isHome = i.franchiseSeasonId === m.homeFranchiseSeasonId;
            return {
              contestId: i.contestId,
              franchiseSeasonId: i.franchiseSeasonId,
              team: isHome ? m.home : m.away,
              matchupLabel: `${m.away} @ ${m.home}`,
            };
          }),
      }))
      .filter((src) => src.items.length > 0);
  }, [availabilityData, matchups, pickMap]);

  const handleImport = useCallback(
    (sourceLeagueId: string, contestIds: string[]) => {
      if (isReadOnly) return; // deactivated leagues are view-only
      if (!leagueId || selectedWeek == null) return;
      importPicks.mutate(
        { leagueId, week: selectedWeek, sourceLeagueId, contestIds },
        {
          onSuccess: (res) => {
            setImportOpen(false);
            if (res?.requiresConfidence) {
              Toast.show({
                type: 'info',
                text1: 'Not available here yet',
                text2: 'This league uses confidence points.',
              });
            } else {
              const n = res?.imported ?? contestIds.length;
              Toast.show({ type: 'success', text1: `Imported ${n} pick${n === 1 ? '' : 's'}!` });
            }
          },
          onError: () => {
            Toast.show({
              type: 'error',
              text1: 'Failed to import picks',
              text2: 'Please try again.',
            });
          },
        },
      );
    },
    [isReadOnly, leagueId, selectedWeek, importPicks],
  );

  // Hide Picked is a no-op once allPicked flips true — the toggle is also
  // hidden in that branch of the header, so respecting it would strand the
  // user with an empty FlatList ("No games this week") and no in-UI escape.
  // Treating the filter as inactive when allPicked === true keeps the cards
  // visible after the final pick while the header reads "All Picks Made".
  const visibleEntries = useMemo(
    () => (hidePicked && !allPicked ? entries.filter((e) => !e.pick) : entries),
    [entries, hidePicked, allPicked],
  );

  // Responsive columns: phones stay single-column; tablets get a multi-column
  // grid like the web app. Recomputes on rotation via useWindowDimensions, and
  // the FlatList remounts (key) when the count changes — RN requires that.
  const { width } = useWindowDimensions();
  const numColumns = width >= 1100 ? 3 : width >= 680 ? 2 : 1;

  // Pad the final row with invisible slots so a lone last card doesn't stretch
  // to the full row width under flex:1. Each row (real or placeholder) carries a
  // stable key; placeholders have a null matchup.
  type PicksGridItem = {
    matchup: (typeof entries)[number]['matchup'] | null;
    pick: (typeof entries)[number]['pick'];
    key: string;
  };
  const gridData = useMemo<PicksGridItem[]>(() => {
    const rows: PicksGridItem[] = visibleEntries.map((e) => ({
      matchup: e.matchup,
      pick: e.pick,
      key: e.matchup.contestId,
    }));
    if (numColumns === 1) return rows;
    const remainder = rows.length % numColumns;
    if (remainder === 0) return rows;
    for (let i = 0; i < numColumns - remainder; i++) {
      rows.push({ matchup: null, pick: null, key: `placeholder-${i}` });
    }
    return rows;
  }, [visibleEntries, numColumns]);

  // Pick-mode badge label. Mirrors web's pill in PicksPage.jsx; suppressed
  // when pickType is missing or unrecognized so a misconfigured league
  // doesn't render a stray "?".
  const pickModeLabel = useMemo(() => {
    switch (pickType) {
      case 'StraightUp': return 'SU';
      case 'AgainstTheSpread': return 'ATS';
      case 'OverUnder': return 'O/U';
      default: return null;
    }
  }, [pickType]);

  // ── Inject pick counter into this tab's header ──────────────────────────────
  useEffect(() => {
    if (total === 0) {
      navigation.setOptions({ headerRight: undefined });
      return;
    }
    navigation.setOptions({
      headerRight: () => (
        <View style={headerStyles.pill}>
          {isReadOnly && (
            <View style={[headerStyles.modeBadge, { borderColor: theme.textMuted }]}>
              <Text style={[headerStyles.modeBadgeText, { color: theme.textMuted }]}>
                🔒 ENDED
              </Text>
            </View>
          )}
          {pickModeLabel ? (
            <View style={[headerStyles.modeBadge, { borderColor: theme.tint }]}>
              <Text style={[headerStyles.modeBadgeText, { color: theme.tint }]}>
                {pickModeLabel}
              </Text>
            </View>
          ) : null}
          {allPicked ? (
            <Text style={[headerStyles.pillText, { color: theme.tint }]}>
              All Picks Made
            </Text>
          ) : (
            <>
              <Text style={[headerStyles.pillText, { color: theme.tint }]}>
                {made}/{total}
              </Text>
              <Pressable
                onPress={() => setHidePicked((v) => !v)}
                hitSlop={6}
                style={headerStyles.hideToggle}
                accessibilityRole="checkbox"
                accessibilityState={{ checked: hidePicked }}
                accessibilityLabel="Hide picked games"
              >
                <Ionicons
                  name={hidePicked ? 'checkbox' : 'square-outline'}
                  size={18}
                  color={hidePicked ? theme.tint : theme.textMuted}
                />
                <Text style={[headerStyles.pillSub, { color: theme.textMuted }]}>
                  {' '}Hide Picked
                </Text>
              </Pressable>
            </>
          )}
        </View>
      ),
    });
  }, [made, total, allPicked, hidePicked, theme, pickModeLabel, isReadOnly]);

  if (meLoading) {
    return <LoadingSpinner message="Loading picks…" fullScreen />;
  }

  if (!leagueId) {
    return (
      <EmptyState
        icon="🗓️"
        title="No active league"
        subtitle="You haven't joined a pick'em group yet."
      />
    );
  }

  // No week to show: either the slate is still being built, or this snapshot is
  // stale and the heal-refetch above is in flight. Previously this fell back to
  // week 1 — a week that doesn't exist for every sport (MLB's start at 17) — and
  // rendered an empty slate that read as "the league failed to build".
  //
  // Scrollable so pull-to-refresh actually works: the heal fires only once per
  // league, so this is the user's only way out if it didn't resolve.
  if (selectedWeek === null) {
    return (
      <ScrollView
        style={[styles.container, { backgroundColor: theme.background }]}
        contentContainerStyle={styles.setupContent}
        refreshControl={
          <RefreshControl
            refreshing={meRefetching}
            onRefresh={refetchMe}
            tintColor={theme.tint}
          />
        }
      >
        <EmptyState
          icon="🗓️"
          title="Setting up your league"
          subtitle="Your games are being scheduled. This usually takes a moment — pull down to refresh if they don't appear."
        />
      </ScrollView>
    );
  }

  return (
    <View style={[styles.container, { backgroundColor: theme.background }]}>
      {selectedWeek !== null && (
        <LeagueWeekSelector
          leagues={selectableLeagues}
          selectedLeagueId={leagueId}
          onLeagueChange={handleLeagueChange}
          selectedWeek={selectedWeek}
          seasonWeeks={seasonWeeks}
          onWeekChange={setSelectedWeek}
        />
      )}

      {importSources.length > 0 && !allPicked && (
        <Pressable
          onPress={() => setImportOpen(true)}
          style={[styles.importBanner, { backgroundColor: theme.tint }]}
          accessibilityRole="button"
        >
          <Ionicons name="download-outline" size={18} color={theme.textOnAccent} />
          <Text style={[styles.importBannerText, { color: theme.textOnAccent }]}>
            {importSources.length === 1
              ? `Import ${importSources[0].items.length} pick${
                  importSources[0].items.length === 1 ? '' : 's'
                } from ${importSources[0].name}`
              : 'Import picks from another league'}
          </Text>
        </Pressable>
      )}

      {isLoading ? (
        <LoadingSpinner message="Loading picks…" />
      ) : (
        <FlatList
          // Remount when the column count changes (RN requires it for numColumns).
          key={`cols-${numColumns}`}
          data={gridData}
          numColumns={numColumns}
          columnWrapperStyle={numColumns > 1 ? styles.columnWrapper : undefined}
          keyExtractor={(item) => item.key}
          renderItem={({ item }) => {
            // Invisible slot filling the last row so real cards keep their width.
            if (item.matchup === null) return <View style={styles.cardSlot} />;
            const matchup = item.matchup;
            return (
            <View style={styles.cardSlot}>
            <MatchupCard
              matchup={matchup}
              pick={item.pick}
              leagueSport={matchupsResponse?.sport ?? null}
              leagueAsOfDate={matchupsResponse?.asOfDate ?? null}
              pickType={pickType}
              onPress={() => {
                if (!sportLeague) {
                  // Sport hasn't resolved yet (matchups response still in flight)
                  // OR the backend returned a sport enum we don't know how to map.
                  // In practice the isLoading branch above covers the first case —
                  // this log fires on the second (e.g., a new sport added BE-side
                  // without a mobile-side sportLinks entry). Captures the raw value
                  // so it's grep-able in logs / Seq instead of a silent no-op.
                  console.warn(
                    '[PicksScreen] Could not resolve sport for navigation; staying put. Raw sport value:',
                    matchupsResponse?.sport ?? '(no matchups response)',
                  );
                  return;
                }
                router.push(
                  {
                    pathname: '/sport/[sport]/[league]/game/[id]',
                    params: {
                      sport: sportLeague.sport,
                      league: sportLeague.league,
                      id: matchup.contestId,
                      leagueId: leagueId ?? '',
                      week: String(selectedWeek ?? 1),
                      backTitle: 'Games',
                      // Force the back button to return to the Games tab rather
                      // than popping into a previously-visited game in the
                      // shared details stack.
                      backHref: '/(tabs)/picks',
                    },
                  } as never,
                );
              }}
              onPressTeam={(side) => {
                if (!sportLeague) {
                  console.warn(
                    '[PicksScreen] Could not resolve sport for team navigation. Raw sport value:',
                    matchupsResponse?.sport ?? '(no matchups response)',
                  );
                  return;
                }
                const slug =
                  side === 'home' ? matchup.homeSlug : matchup.awaySlug;
                router.push(
                  {
                    pathname: '/sport/[sport]/[league]/team/[slug]',
                    params: {
                      sport: sportLeague.sport,
                      league: sportLeague.league,
                      slug,
                      season: String(matchupsResponse?.seasonYear ?? ''),
                      backTitle: 'Games',
                      backHref: '/(tabs)/picks',
                    },
                  } as never,
                );
              }}
              onPick={(m, _choice, franchiseSeasonId) => {
                // Past (deactivated) leagues are view-only — the season is over.
                if (isReadOnly) {
                  Toast.show({
                    type: 'info',
                    text1: 'League has ended',
                    text2: 'Picks are read-only.',
                  });
                  return;
                }
                if (!leagueId || !selectedWeek) return;
                submitPick.mutate({
                  pickemGroupId: leagueId,
                  contestId: m.contestId,
                  pickType,
                  franchiseSeasonId,
                  week: selectedWeek,
                });
              }}
            />
            </View>
            );
          }}
          contentContainerStyle={styles.list}
          showsVerticalScrollIndicator={false}
          refreshControl={
            <RefreshControl
              refreshing={isRefetching}
              onRefresh={refetch}
              tintColor={theme.tint}
            />
          }
          ListEmptyComponent={
            <EmptyState
              title="No games this week"
              subtitle="Check back closer to the season."
            />
          }
        />
      )}

      <ImportPicksModal
        visible={importOpen}
        sources={importSources}
        importing={importPicks.isPending}
        onClose={() => setImportOpen(false)}
        onImport={handleImport}
      />
    </View>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  container: { flex: 1 },
  // flexGrow so EmptyState's own flex:1 can center in a short ScrollView, while
  // still leaving the content pullable.
  setupContent: { flexGrow: 1 },
  // gap = vertical spacing between rows (replaces the old ItemSeparator).
  list: { padding: 14, paddingBottom: 24, gap: 10 },
  // Horizontal spacing between columns in a multi-column row.
  columnWrapper: { gap: 10 },
  // Each card fills its share of the row so columns stay equal width.
  cardSlot: { flex: 1 },
  // "Import picks" banner shown above the slate when picks can be pulled in.
  importBanner: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    marginHorizontal: 14,
    marginTop: 10,
    paddingVertical: 12,
    borderRadius: 12,
  },
  importBannerText: { fontSize: 15, fontWeight: '700' },
});

const headerStyles = StyleSheet.create({
  pill: {
    flexDirection: 'row',
    alignItems: 'center',
    marginRight: 12,
  },
  // SU / ATS / O/U pill — accent-bordered chip preceding the picks-made
  // counter. Mirrors the web .pick-mode-badge pattern.
  modeBadge: {
    borderWidth: 1,
    borderRadius: 999,
    paddingHorizontal: 8,
    paddingVertical: 1,
    marginRight: 8,
  },
  modeBadgeText: {
    fontSize: 11,
    fontWeight: '800',
    letterSpacing: 0.5,
  },
  pillText: {
    fontSize: 15,
    fontWeight: '700',
  },
  pillSub: {
    fontSize: 13,
    fontWeight: '500',
  },
  hideToggle: {
    flexDirection: 'row',
    alignItems: 'center',
    marginLeft: 10,
  },
});
