import type { PublicLeague } from '@/src/services/api/leaguesApi';

// Web parity: SPORT_LABEL / PICK_TYPE_LABEL / countdown behavior mirror
// sd-ui's discovery components so the two apps read the same.
// See docs/mobile/web-parity-join-discovery.md.

export const SPORT_LABEL: Record<PublicLeague['sport'], string> = {
  FootballNcaa: 'NCAAFB',
  FootballNfl: 'NFL',
  BaseballMlb: 'MLB',
};

export const SPORT_ICON: Record<PublicLeague['sport'], string> = {
  FootballNcaa: '🏈',
  FootballNfl: '🏈',
  BaseballMlb: '⚾',
};

// PublicLeague.pickType is the BE enum's int value.
export const PICK_TYPE_LABEL: Record<number, string> = {
  1: 'SU',
  2: 'ATS',
  3: 'O/U',
};

// Live countdown only inside this window — "closes in 4 months" is noise.
// Operator-set threshold (matches sd-ui's JoinClosesLabel): ~10 days.
export const COUNTDOWN_WINDOW_MS = 10 * 24 * 60 * 60 * 1000;

/** Human "Xd Yh" / "Yh Zm" / "Zm" for a positive remaining-ms. */
export function formatRemaining(ms: number): string {
  const totalMinutes = Math.max(0, Math.floor(ms / 60000));
  const days = Math.floor(totalMinutes / (60 * 24));
  const hours = Math.floor((totalMinutes % (60 * 24)) / 60);
  const minutes = totalMinutes % 60;
  if (days > 0) return `${days}d ${hours}h`;
  if (hours > 0) return `${hours}h ${minutes}m`;
  return `${minutes}m`;
}

export interface JoinClosesState {
  text: string;
  /** "countdown" inside the 10-day window; "closed"; else "open"/"date". */
  kind: 'open' | 'date' | 'countdown' | 'closed';
}

/**
 * The join-status text for a league at time `now`. Mirrors sd-ui:
 *   closed / past   -> "Closed"
 *   > 10 days out   -> "Closes Sep 15"
 *   <= 10 days      -> "Closes in 2d 4h"
 *   no closesAtUtc  -> "Open"
 */
export function joinClosesState(
  closesAtUtc: string | null | undefined,
  isJoinable: boolean,
  now: number,
): JoinClosesState {
  const closesMs = closesAtUtc ? new Date(closesAtUtc).getTime() : NaN;

  if (isJoinable === false || (Number.isFinite(closesMs) && closesMs - now <= 0)) {
    return { text: 'Closed', kind: 'closed' };
  }
  if (!Number.isFinite(closesMs)) {
    return { text: 'Open', kind: 'open' };
  }

  const remaining = closesMs - now;
  if (remaining <= COUNTDOWN_WINDOW_MS) {
    return { text: `Closes in ${formatRemaining(remaining)}`, kind: 'countdown' };
  }

  const d = new Date(closesMs);
  const label = d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
  return { text: `Closes ${label}`, kind: 'date' };
}

// BE enum names -> user-facing phrasing (mirrors sd-ui's create form).
export const TIEBREAKER_LABEL: Record<string, string> = {
  TotalPoints: 'Closest total points',
  HomeAndAwayScores: 'Home and away scores',
  EarliestSubmission: 'Earliest submission',
};

const fmtDate = (iso: string | null): string | null => {
  if (!iso) return null;
  const d = new Date(iso);
  return Number.isNaN(d.getTime())
    ? null
    : d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
};

/** "Full Season" / "Sep 1 – Sep 30" / "From Sep 1" / "Through Sep 30". */
export function windowLabel(startsOn: string | null, endsOn: string | null): string {
  const s = fmtDate(startsOn);
  const e = fmtDate(endsOn);
  if (!s && !e) return 'Full Season';
  if (s && e) return `${s} – ${e}`;
  return s ? `From ${s}` : `Through ${e}`;
}
