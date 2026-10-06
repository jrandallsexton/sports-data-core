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
  const [rememberedLeagueId, setRememberedLeagueId] = useState<string | null>(null);
  const [hydrated, setHydrated] = useState(false);
  // Skip redundant writes: the screen calls remember() on every render path
  // that changes the selection, and the same id repeats across re-renders.
  const lastWrittenRef = useRef<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setRememberedLeagueId(null);
    lastWrittenRef.current = null;

    if (!userId) {
      setHydrated(true);
      return;
    }

    setHydrated(false);
    AsyncStorage.getItem(KEY_PREFIX + userId)
      .then((v) => {
        if (cancelled) return;
        setRememberedLeagueId(v);
        lastWrittenRef.current = v;
        setHydrated(true);
      })
      .catch(() => {
        if (!cancelled) setHydrated(true);
      });

    return () => {
      cancelled = true;
    };
  }, [userId]);

  const remember = useCallback(
    (leagueId: string) => {
      if (!userId || !hydrated) return;
      if (lastWrittenRef.current === leagueId) return;
      lastWrittenRef.current = leagueId;
      // A failed write only means the next cold start uses the default.
      AsyncStorage.setItem(KEY_PREFIX + userId, leagueId).catch(() => {});
    },
    [userId, hydrated],
  );

  return { rememberedLeagueId, hydrated, remember };
}
