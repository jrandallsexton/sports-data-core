import { useEffect, useState } from 'react';
import { COUNTDOWN_WINDOW_MS, joinClosesState, type JoinClosesState } from './joinDisplay';

// setTimeout treats delays above 2^31-1 ms (~24.8 days) as 0.
const MAX_TIMEOUT_MS = 2 ** 31 - 1;
const TICK_MS = 60 * 1000;

/**
 * Live join-status for a league: recomputes as the clock crosses the 10-day
 * countdown window and the close instant, so a screen left open transitions
 * date -> countdown -> Closed on its own. This is the single source of truth
 * for both the JoinClosesLabel display AND the Join affordances — a Join
 * button must not stay enabled after the countdown reaches zero.
 */
export function useLiveJoinState(
  closesAtUtc: string | null | undefined,
  isJoinable: boolean,
): JoinClosesState {
  const [now, setNow] = useState(() => Date.now());

  const closesMs = closesAtUtc ? new Date(closesAtUtc).getTime() : NaN;
  const remaining = closesMs - now;
  const inCountdown =
    Number.isFinite(closesMs) && remaining > 0 && remaining <= COUNTDOWN_WINDOW_MS;

  useEffect(() => {
    if (!Number.isFinite(closesMs) || remaining <= 0) return undefined;
    if (inCountdown) {
      // Minute tick; the render after the tick that crosses zero flips to Closed.
      const id = setInterval(() => setNow(Date.now()), TICK_MS);
      return () => clearInterval(id);
    }
    // Outside the window: one timer aimed at the boundary.
    const untilWindow = Math.min(remaining - COUNTDOWN_WINDOW_MS, MAX_TIMEOUT_MS);
    const id = setTimeout(() => setNow(Date.now()), Math.max(untilWindow, TICK_MS));
    return () => clearTimeout(id);
  }, [closesMs, inCountdown, remaining]);

  return joinClosesState(closesAtUtc, isJoinable, now);
}
