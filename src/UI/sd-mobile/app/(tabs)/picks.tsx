import React, { useState, useEffect, useMemo, useCallback } from 'react';
import {
  View,
  FlatList,
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
import { getLeagues } from '@/src/lib/leagues';
import { resolveSportLeague } from '@/src/utils/sportLinks';

// ─── Screen ───────────────────────────────────────────────────────────────────

export default function PicksScreen() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const router = useRouter();
  const navigation = useNavigation();

  const { data: me, isLoading: meLoading } = useCurrentUser();
  const leagues = useMemo(() => getLeagues(me), [me]);

  // Optional deep-link param: home "Your Leagues" card pushes
  // /(tabs)/picks?leagueId=<id> so the screen opens on that league directly.
  const { leagueId: leagueIdParam } = useLocalSearchParams<{ leagueId?: string }>();

  const [leagueId, setLeagueId] = useState<string | null>(null);
  const [selectedWeek, setSelectedWeek] = useState<number | null>(null);
  const [hidePicked, setHidePicked] = useState(false);

  // Latest week = last element of the ascending seasonWeeks list.
  const latestWeek = (l: { seasonWeeks?: number[] } | null | undefined) =>
    l?.seasonWeeks?.length ? l.seasonWeeks[l.seasonWeeks.length - 1] : null;

  // eslint-disable-next-line react-hooks/exhaustive-deps — intentionally excluding leagueId to only initialize once and avoid rerunning on user selection
  useEffect(() => {
    if (leagues.length === 0) return;

    // If a deep-link param targets a league the user actually belongs to,
    // force-select it (overrides any prior in-session selection so tapping
    // a different league from the home card always lands on that league).
    const targeted = leagueIdParam
      ? leagues.find((l) => l.id === leagueIdParam)
      : null;
    if (targeted) {
      setLeagueId(targeted.id);
      setSelectedWeek(latestWeek(targeted) ?? 1);
      return;
    }

    // Otherwise initialize once to the first league in the list.
    if (!leagueId) {
      setLeagueId(leagues[0].id);
      setSelectedWeek(latestWeek(leagues[0]) ?? 1);
    }
  }, [leagues, leagueIdParam]);

  const selectedLeague = leagues.find((l) => l.id === leagueId) ?? null;
  const seasonWeeks = selectedLeague?.seasonWeeks ?? [];

  const handleLeagueChange = useCallback(
    (id: string) => {
      setLeagueId(id);
      const league = leagues.find((l) => l.id === id);
      setSelectedWeek(latestWeek(league) ?? 1);
    },
    [leagues],
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
  }, [made, total, allPicked, hidePicked, theme, pickModeLabel]);

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

  return (
    <View style={[styles.container, { backgroundColor: theme.background }]}>
      {selectedWeek !== null && (
        <LeagueWeekSelector
          leagues={leagues}
          selectedLeagueId={leagueId}
          onLeagueChange={handleLeagueChange}
          selectedWeek={selectedWeek}
          seasonWeeks={seasonWeeks}
          onWeekChange={setSelectedWeek}
        />
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
    </View>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  container: { flex: 1 },
  // gap = vertical spacing between rows (replaces the old ItemSeparator).
  list: { padding: 14, paddingBottom: 24, gap: 10 },
  // Horizontal spacing between columns in a multi-column row.
  columnWrapper: { gap: 10 },
  // Each card fills its share of the row so columns stay equal width.
  cardSlot: { flex: 1 },
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
