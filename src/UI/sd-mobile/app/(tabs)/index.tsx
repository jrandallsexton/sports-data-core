import React, { useMemo } from 'react';
import { ScrollView, View, StyleSheet, RefreshControl, useWindowDimensions } from 'react-native';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { LoadingSpinner } from '@/src/components/ui/LoadingSpinner';
import { useCurrentUser } from '@/src/hooks/useStandings';
import { getLeagues } from '@/src/lib/leagues';
import { PrimarySlotOffSeasonCountdown } from '@/src/components/features/home/PrimarySlotOffSeasonCountdown';
import { YourLeaguesCard } from '@/src/components/features/home/YourLeaguesCard';
import { JoinableLeaguesCard } from '@/src/components/features/home/JoinableLeaguesCard';

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
 */
export default function HomeScreen() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const {
    data: me,
    isLoading: meLoading,
    isRefetching: meRefetching,
    refetch: refetchMe,
  } = useCurrentUser();
  const leagues = useMemo(() => getLeagues(me), [me]);

  // On tablet-width screens, place the countdown and leagues side by side so the
  // countdown stops stretching awkwardly across the full width. Same width-driven
  // breakpoint as the leagues grid.
  const { width } = useWindowDimensions();
  const twoColumn = width >= 680;

  if (meLoading) {
    return <LoadingSpinner message="Loading…" fullScreen />;
  }

  const hasLeagues = leagues.length > 0;

  return (
    <ScrollView
      style={{ backgroundColor: theme.background }}
      contentContainerStyle={styles.container}
      showsVerticalScrollIndicator={false}
      refreshControl={
        <RefreshControl
          refreshing={meRefetching}
          onRefresh={refetchMe}
          tintColor={theme.tint}
        />
      }
    >
      {hasLeagues && twoColumn ? (
        <View style={styles.twoCol}>
          <View style={styles.col}>
            <PrimarySlotOffSeasonCountdown />
          </View>
          <View style={styles.col}>
            <YourLeaguesCard leagues={leagues} />
          </View>
        </View>
      ) : (
        <>
          <PrimarySlotOffSeasonCountdown />
          {hasLeagues && <YourLeaguesCard leagues={leagues} />}
        </>
      )}

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
  col: { flex: 1 },
});
