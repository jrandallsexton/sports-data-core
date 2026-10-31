import type { MiniGameResult, MiniGameStats } from './types';

export const RECENT_COUNT = 10;

export const EMPTY_STATS: MiniGameStats = {
  wins: 0,
  losses: 0,
  ties: 0,
  winMarginTotal: 0,
  lossMarginTotal: 0,
  currentStreak: null,
  bestWinStreak: 0,
  recent: [],
};

/**
 * The stats to show: the last server response plus results still queued on
 * the device. Queued results are newer than anything the server has seen, so
 * they extend the current streak and lead the recent list. A queued id the
 * server already reports (uploaded, queue not yet cleared) is skipped.
 */
export function mergeStats(server: MiniGameStats | null, pending: MiniGameResult[]): MiniGameStats {
  const base = server ?? EMPTY_STATS;
  const known = new Set(base.recent.map((r) => r.id));
  const fresh = pending
    .filter((p) => !known.has(p.id))
    .sort((a, b) => a.playedUtc.localeCompare(b.playedUtc));
  if (!fresh.length) return base;

  let { wins, losses, ties, winMarginTotal, lossMarginTotal, bestWinStreak } = base;
  let streak = base.currentStreak;
  for (const r of fresh) {
    if (r.outcome === 'Win') {
      wins++;
      winMarginTotal += r.score - r.opponentScore;
    } else if (r.outcome === 'Loss') {
      losses++;
      lossMarginTotal += r.opponentScore - r.score;
    } else {
      ties++;
    }
    streak = streak && streak.outcome === r.outcome
      ? { outcome: r.outcome, length: streak.length + 1 }
      : { outcome: r.outcome, length: 1 };
    if (streak.outcome === 'Win') bestWinStreak = Math.max(bestWinStreak, streak.length);
  }

  const recent = fresh
    .slice()
    .reverse()
    .map(({ id, outcome, score, opponentScore, playedUtc }) => ({ id, outcome, score, opponentScore, playedUtc }))
    .concat(base.recent)
    .slice(0, RECENT_COUNT);

  return { wins, losses, ties, winMarginTotal, lossMarginTotal, currentStreak: streak, bestWinStreak, recent };
}

export interface StatsSummary {
  games: number;
  /** "5-3", or "5-3-1" once there's a tie. */
  record: string;
  /** Null until the first win / loss. */
  avgWinMargin: number | null;
  avgLossMargin: number | null;
  /** "W2", "L1", "T1"; null before the first game. */
  streak: string | null;
}

export function summarize(s: MiniGameStats): StatsSummary {
  return {
    games: s.wins + s.losses + s.ties,
    record: s.ties ? `${s.wins}-${s.losses}-${s.ties}` : `${s.wins}-${s.losses}`,
    avgWinMargin: s.wins ? s.winMarginTotal / s.wins : null,
    avgLossMargin: s.losses ? s.lossMarginTotal / s.losses : null,
    streak: s.currentStreak ? `${s.currentStreak.outcome[0]}${s.currentStreak.length}` : null,
  };
}
