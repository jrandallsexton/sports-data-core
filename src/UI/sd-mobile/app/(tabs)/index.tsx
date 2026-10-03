import React, { useCallback, useMemo, useState } from 'react';
import { ScrollView, View, StyleSheet, RefreshControl, useWindowDimensions } from 'react-native';
import { useQueryClient } from '@tanstack/react-query';
import { leaguesKeys } from '@/src/services/api/leaguesApi';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { LoadingSpinner } from '@/src/components/ui/LoadingSpinner';
import { useCurrentUser } from '@/src/hooks/useStandings';
import { getLeagues } from '@/src/lib/leagues';
import { PrimarySlotOffSeasonCountdown } from '@/src/components/features/home/PrimarySlotOffSeasonCountdown';
import { PlayerPickemTeaserCard } from '@/src/components/features/home/PlayerPickemTeaserCard';
import { PendingInvitesCard } from '@/src/components/features/home/PendingInvitesCard';
import { YourLeaguesCard } from '@/src/components/features/home/YourLeaguesCard';
import { JoinableLeaguesCard } from '@/src/components/features/home/JoinableLeaguesCard';
import { RankingsCard } from '@/src/components/features/home/RankingsCard';
import { PickAccuracyCard } from '@/src/components/features/home/PickAccuracyCard';
import { pickAccuracyKeys, usePickAccuracy } from '@/src/hooks/usePickAccuracy';
import { activeAccuracyLeagues } from '@/src/lib/pickAccuracy';

/**
 * Post-login landing — mirrors web's HomePage (PR #272 / docs/post-login-landing-design.md).
 *
 * Tier 1 is PrimarySlotOffSeasonCountdown for EVERY user (mirrors web's #571
 * fix): the countdown is gate-aware, so a non-admin sees the kickoff copy with
 * DISABLED "opens {date}" create CTAs, while an admin (who bypasses the gate)
 * sees enabled ones. The old zero-league branch (PrimarySlotNewUser) was
 * removed — it hardcoded the season and led with a "Create a league" action
 * the per-sport gates block until each sport opens.
 *
 * Tier 2 (YourLeaguesCard) lists the user's active leagues, shown only when
 * they have any. The BE filters /user/me to `PickemGroup.DeactivatedUtc IS
 * NULL` (see PR #273), so prior-season leagues never reach this screen.
 * Tier 3 (JoinableLeaguesCard) is the public-league rail, self-nulling.
 *
 * Pick record + standings widgets were deliberately removed: during off-season
 * they're empty/stale, and the Tier 2 league list is a more useful anchor.
 *
 * In-season, Tier 1 becomes PickAccuracyCard (the mobile port of the web
 * PickAccuracyWidget) as soon as the user has an ACTIVE league with at least
 * one graded week. Until then, the countdown stays: a user with no league,
 * or with leagues but no scored picks yet, sees exactly what they saw before
 * (owner call, 2026-09-21). The swap is decided here, not inside the card,
 * so the two never render together and there is no loading flicker: the
 * screen's spinner gates on both /user/me and /ui/picks/chart.
 */
export default function HomeScreen() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const {
    data: me,
    isLoading: meLoading,
    refetch: refetchMe,
  } = useCurrentUser();
  const leagues = useMemo(() => getLeagues(me), [me]);

  // Runs in parallel with /user/me (both are user-scoped; neither needs the
  // other's result). The intersection with ACTIVE leagues happens here.
  const { data: accuracyChart, isLoading: accuracyLoading } = usePickAccuracy();
  const accuracyLeagues = useMemo(
    () => activeAccuracyLeagues(accuracyChart, leagues),
    [accuracyChart, leagues],
  );

  // Pull-to-refresh must refresh EVERYTHING this screen renders, not just
  // /user/me — the JoinableLeaguesCard runs its own leaguesKeys.public query,
  // and with a 5-min staleTime + refetchOnWindowFocus off, a league created
  // elsewhere (e.g. on web) never appeared until app restart. The pull
  // gesture is the user's explicit freshness request; invalidating the
  // public list forces its active query to refetch alongside /user/me.
  const queryClient = useQueryClient();
  const [refreshing, setRefreshing] = useState(false);
  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    try {
      await Promise.all([
        refetchMe(),
        queryClient.invalidateQueries({ queryKey: leaguesKeys.public }),
        queryClient.invalidateQueries({ queryKey: leaguesKeys.invitations }),
        queryClient.invalidateQueries({ queryKey: pickAccuracyKeys.chart }),
      ]);
    } finally {
      setRefreshing(false);
    }
  }, [refetchMe, queryClient]);

  // On tablet-width screens, place the countdown and leagues side by side so the
  // countdown stops stretching awkwardly across the full width. Same width-driven
  // breakpoint as the leagues grid.
  const { width } = useWindowDimensions();
  const twoColumn = width >= 680;

  if (meLoading || accuracyLoading) {
    return <LoadingSpinner message="Loading…" fullScreen />;
  }

  const hasLeagues = leagues.length > 0;
  const showAccuracy = accuracyLeagues.length > 0;
  const primarySlot = showAccuracy ? (
    <PickAccuracyCard leagues={accuracyLeagues} />
  ) : (
    <PrimarySlotOffSeasonCountdown />
  );

  return (
    <ScrollView
      style={{ backgroundColor: theme.background }}
      contentContainerStyle={styles.container}
      showsVerticalScrollIndicator={false}
      refreshControl={
        <RefreshControl
          refreshing={refreshing}
          onRefresh={onRefresh}
          tintColor={theme.tint}
        />
      }
    >
      {hasLeagues && twoColumn ? (
        <View style={styles.twoCol}>
          <View style={styles.col}>
            {primarySlot}
            {/* Directly below the primary slot — web parity: HomePage places
                the teaser between the countdown and Pending Invitations. */}
            <PlayerPickemTeaserCard />
          </View>
          <View style={styles.col}>
            {/* Above Your Leagues — an unanswered invite is the most
                actionable thing on the page. Web parity: HomePage renders
                PendingInvitesCard before YourLeaguesCard. Self-nulls when
                there are none. */}
            <PendingInvitesCard />
            <YourLeaguesCard leagues={leagues} />
          </View>
        </View>
      ) : (
        <>
          {primarySlot}
          <PlayerPickemTeaserCard />
          <PendingInvitesCard />
          {hasLeagues && <YourLeaguesCard leagues={leagues} />}
        </>
      )}

      {/* Tier 2 — current poll Top 10, linking to the full rankings screen.
          Self-nulls when no poll exists for the current season, so Home
          never shows a broken card. Web parity: HomePage's RankingsCard. */}
      <RankingsCard />

      {/* Tier 3 — public-league discovery. Rendered for every user (self-nulls
          when nothing is joinable), so a league-less user lands on actionable
          joins rather than only the new-user slot. Web parity: HomePage. */}
      <JoinableLeaguesCard />
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    padding: 16,
    paddingBottom: 32,
    gap: 16,
  },
  // Countdown | leagues side by side; top-aligned so the leagues list can grow
  // without stretching the countdown column.
  twoCol: { flexDirection: 'row', gap: 16, alignItems: 'flex-start' },
  // gap covers the invites + leagues stack sharing the right column.
  col: { flex: 1, gap: 16 },
});
