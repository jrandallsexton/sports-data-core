import React, { useMemo } from 'react';
import { ScrollView, StyleSheet, RefreshControl } from 'react-native';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { LoadingSpinner } from '@/src/components/ui/LoadingSpinner';
import { useCurrentUser } from '@/src/hooks/useStandings';
import { getLeagues } from '@/src/lib/leagues';
import { PrimarySlotNewUser } from '@/src/components/features/home/PrimarySlotNewUser';
import { PrimarySlotOffSeasonCountdown } from '@/src/components/features/home/PrimarySlotOffSeasonCountdown';
import { YourLeaguesCard } from '@/src/components/features/home/YourLeaguesCard';

/**
 * Post-login landing — mirrors web's HomePage (PR #272 / docs/post-login-landing-design.md).
 *
 * Rule resolver (top-down, most-urgent wins):
 *   - No leagues            → PrimarySlotNewUser (Tier 1 only; zero-state)
 *   - Has leagues           → PrimarySlotOffSeasonCountdown (Tier 1) + YourLeaguesCard (Tier 2)
 *
 * Tier 2 lists the user's active leagues. The BE filters /user/me to
 * `PickemGroup.DeactivatedUtc IS NULL` (see PR #273), so prior-season
 * leagues never reach this screen.
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
      {hasLeagues ? (
        <>
          <PrimarySlotOffSeasonCountdown />
          <YourLeaguesCard leagues={leagues} />
        </>
      ) : (
        <PrimarySlotNewUser />
      )}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    padding: 16,
    paddingBottom: 32,
    gap: 16,
  },
});
