import { useCallback, useEffect, useRef, useState } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';

const KEY_PREFIX = 'standings-last-league:';

export interface RememberedStandingsLeague {
  /** The league Standings last showed for this user, from a previous session. */
  rememberedLeagueId: string | null;
  /**
   * True once the stored value has been read (or the read failed, or there is
   * no user). Until then the screen must not remember anything: its cold-start
   * default (the first league) would overwrite the stored id before it loads.
   */
  hydrated: boolean;
  /** Record the league Standings is showing. Fire-and-forget. */
  remember: (leagueId: string) => void;
}

/**
 * The league the Standings tab was last showing, persisted per user per
 * device, so a cold start reopens Standings on it instead of the first league.
 *
 * Standings-only on purpose. The app-wide leagueSelectionStore stays
 * session-scoped (its contract): persisting it would also reopen Picks and
 * Home on the last league after a restart, which nobody asked for. The screen
 * feeds this value into useSeasonLeagueSelection as a one-shot preference
 * only until an explicit choice is made elsewhere in the session, so the
 * cross-tab behavior while the app is running is unchanged.
 *
 * Keyed by uid so a second account on the same device never inherits the
 * first account's league (it would not be in their list anyway, and the
 * selection hook ignores ids it can't find). Reads and writes degrade to the
 * existing default: a failed read behaves as "nothing remembered".
 */
export function useRememberedStandingsLeague(userId: string | null | undefined): RememberedStandingsLeague {
  // The stored value tagged with the user it was read for. hydrated and
  // rememberedLeagueId are DERIVED from it, never reset in an effect: on the
  // render where userId changes, an effect-based reset would still be pending,
  // so the previous user's value (and hydrated: true) would be visible for
  // that render and remember() could write the old selection under the new
  // user's key. Deriving makes the switch atomic.
  const [loaded, setLoaded] = useState<{ userId: string; leagueId: string | null } | null>(null);
  const isLoadedForUser = !!userId && loaded?.userId === userId;
  const hydrated = !userId || isLoadedForUser;
  const rememberedLeagueId = isLoadedForUser ? loaded!.leagueId : null;

  // Skip redundant writes: the screen calls remember() whenever the selection
  // changes, and the same id repeats. Tagged by user like the loaded value.
  const lastWrittenRef = useRef<{ userId: string; leagueId: string } | null>(null);

  useEffect(() => {
    if (!userId) return;
    let cancelled = false;

    AsyncStorage.getItem(KEY_PREFIX + userId)
      .then((v) => {
        if (!cancelled) setLoaded({ userId, leagueId: v });
      })
      .catch(() => {
        // A failed read behaves as "nothing remembered".
        if (!cancelled) setLoaded({ userId, leagueId: null });
      });

    return () => {
      cancelled = true;
    };
  }, [userId]);

  const remember = useCallback(
    (leagueId: string) => {
      // Not until THIS user's stored value has loaded (see above).
      if (!userId || loaded?.userId !== userId) return;
      const previous =
        lastWrittenRef.current?.userId === userId ? lastWrittenRef.current.leagueId : loaded.leagueId;
      if (previous === leagueId) return;
      lastWrittenRef.current = { userId, leagueId };
      // A failed write only means the next cold start uses the default.
      AsyncStorage.setItem(KEY_PREFIX + userId, leagueId).catch(() => {});
    },
    [userId, loaded],
  );

  return { rememberedLeagueId, hydrated, remember };
}
