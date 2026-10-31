import { EMPTY_STATS, mergeStats, RECENT_COUNT, summarize } from '@/src/utils/minigames/stats';
import { outcomeOf, type MiniGameResult, type MiniGameStats } from '@/src/utils/minigames/types';

let n = 0;
const result = (score: number, opponentScore: number, minute = n): MiniGameResult => ({
  id: `r${n++}`,
  gameKey: 'football-cards',
  outcome: outcomeOf(score, opponentScore),
  score,
  opponentScore,
  durationSeconds: 300,
  gameVersion: '1',
  playedUtc: new Date(Date.UTC(2026, 9, 30, 12, minute)).toISOString(),
});

const server: MiniGameStats = {
  wins: 3,
  losses: 1,
  ties: 0,
  winMarginTotal: 21,
  lossMarginTotal: 7,
  currentStreak: { outcome: 'Win', length: 2 },
  bestWinStreak: 2,
  recent: [{ id: 'srv1', outcome: 'Win', score: 14, opponentScore: 7, playedUtc: '2026-10-29T12:00:00.000Z' }],
};

describe('mergeStats', () => {
  it('returns the server stats untouched when nothing is queued', () => {
    expect(mergeStats(server, [])).toBe(server);
    expect(mergeStats(null, [])).toEqual(EMPTY_STATS);
  });

  it('adds queued results to totals and margins', () => {
    const merged = mergeStats(server, [result(21, 14), result(3, 10)]);
    expect(merged.wins).toBe(4);
    expect(merged.losses).toBe(2);
    expect(merged.winMarginTotal).toBe(28);
    expect(merged.lossMarginTotal).toBe(14);
  });

  it('extends the server streak and raises the best streak', () => {
    const merged = mergeStats(server, [result(7, 0), result(7, 0)]);
    expect(merged.currentStreak).toEqual({ outcome: 'Win', length: 4 });
    expect(merged.bestWinStreak).toBe(4);
  });

  it('a tie breaks a win streak', () => {
    const merged = mergeStats(server, [result(7, 7)]);
    expect(merged.currentStreak).toEqual({ outcome: 'Tie', length: 1 });
    expect(merged.ties).toBe(1);
    expect(merged.winMarginTotal).toBe(21);
  });

  it('orders queued results by play time, newest first in recent', () => {
    const later = result(7, 0, 50);
    const earlier = result(0, 7, 10);
    const merged = mergeStats(server, [later, earlier]);
    expect(merged.recent.map((r) => r.id)).toEqual([later.id, earlier.id, 'srv1']);
    expect(merged.currentStreak).toEqual({ outcome: 'Win', length: 1 });
  });

  it('caps recent', () => {
    const queued = Array.from({ length: RECENT_COUNT + 3 }, () => result(7, 0));
    expect(mergeStats(server, queued).recent).toHaveLength(RECENT_COUNT);
  });

  it('skips a queued result the server already reports', () => {
    const uploaded = { ...result(14, 7), id: 'srv1' };
    expect(mergeStats(server, [uploaded])).toBe(server);
  });
});

describe('summarize', () => {
  it('formats the record and averages', () => {
    const s = summarize(server);
    expect(s.games).toBe(4);
    expect(s.record).toBe('3-1');
    expect(s.avgWinMargin).toBe(7);
    expect(s.avgLossMargin).toBe(7);
    expect(s.streak).toBe('W2');
  });

  it('shows ties only once there is one, and nulls before any game', () => {
    expect(summarize({ ...server, ties: 1 }).record).toBe('3-1-1');
    const empty = summarize(EMPTY_STATS);
    expect(empty.games).toBe(0);
    expect(empty.avgWinMargin).toBeNull();
    expect(empty.streak).toBeNull();
  });
});
