import React, { useState, useEffect, useMemo, useCallback } from 'react';
import {
  View,
  Text,
  FlatList,
  StyleSheet,
  RefreshControl,
} from 'react-native';
import { useRouter } from 'expo-router';
import { useNavigation } from '@react-navigation/native';
import { useColorScheme } from 'react-native';
import { getTheme } from '@/constants/Colors';
import { MatchupCard } from '@/src/components/features/games/MatchupCard';
import { LeagueWeekSelector } from '@/src/components/features/selectors/LeagueWeekSelector';
import { LoadingSpinner } from '@/src/components/ui/LoadingSpinner';
import { EmptyState } from '@/src/components/ui/EmptyState';
import { usePicks, useSubmitPick } from '@/src/hooks/useContest';
import { useMatchups } from '@/src/hooks/useMatchups';
import { useCurrentUser } from '@/src/hooks/useStandings';
import { getLeagues } from '@/src/lib/leagues';

// ─── Screen ───────────────────────────────────────────────────────────────────

export default function PicksScreen() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const router = useRouter();
  const navigation = useNavigation();

  const { data: me, isLoading: meLoading } = useCurrentUser();
  const leagues = useMemo(() => getLeagues(me), [me]);

  const [leagueId, setLeagueId] = useState<string | null>(null);
  const [selectedWeek, setSelectedWeek] = useState<number | null>(null);

  useEffect(() => {
    if (!leagueId && leagues.length > 0) {
      setLeagueId(leagues[0].id);
      setSelectedWeek(leagues[0].maxSeasonWeek ?? 1);
    }
  }, [leagues]);

  const selectedLeague = leagues.find((l) => l.id === leagueId) ?? null;
  const maxWeek = selectedLeague?.maxSeasonWeek ?? 18;

  const handleLeagueChange = useCallback(
    (id: string) => {
      setLeagueId(id);
      const league = leagues.find((l) => l.id === id);
      setSelectedWeek(league?.maxSeasonWeek ?? 1);
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
  const made = myPicks.length;
  const remaining = total - made;

  // ── Inject pick counter into this tab's header ──────────────────────────────
  useEffect(() => {
    if (total === 0) {
      navigation.setOptions({ headerRight: undefined });
      return;
    }
    navigation.setOptions({
      headerRight: () => (
        <View style={headerStyles.pill}>
          <Text style={[headerStyles.pillText, { color: theme.tint }]}>
            {made}/{total}
          </Text>
          {remaining > 0 && (
            <Text style={[headerStyles.pillSub, { color: theme.textMuted }]}>
              {' '}· {remaining} left
            </Text>
          )}
        </View>
      ),
    });
  }, [made, total, remaining, theme]);

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
          maxWeek={maxWeek}
          onWeekChange={setSelectedWeek}
        />
      )}

      {isLoading ? (
        <LoadingSpinner message="Loading picks…" />
      ) : (
        <FlatList
          data={entries}
          keyExtractor={(item) => item.matchup.contestId}
          renderItem={({ item }) => (
            <MatchupCard
              matchup={item.matchup}
              pick={item.pick}
              onPress={() =>
                router.push(
                  { pathname: '/game/[id]', params: { id: item.matchup.contestId, leagueId: leagueId ?? '', week: String(selectedWeek ?? 1), backTitle: 'Games' } },
                )
              }
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
          )}
          contentContainerStyle={styles.list}
          showsVerticalScrollIndicator={false}
          refreshControl={
            <RefreshControl
              refreshing={isRefetching}
              onRefresh={refetch}
              tintColor={theme.tint}
            />
          }
          ItemSeparatorComponent={() => <View style={{ height: 10 }} />}
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
  list: { padding: 14, paddingBottom: 24, gap: 10 },
});

const headerStyles = StyleSheet.create({
  pill: {
    flexDirection: 'row',
    alignItems: 'baseline',
    marginRight: 12,
  },
  pillText: {
    fontSize: 15,
    fontWeight: '700',
  },
  pillSub: {
    fontSize: 13,
    fontWeight: '500',
  },
});
