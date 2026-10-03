import {
  activeAccuracyLeagues,
  formatPercent,
  meanAccuracy,
  seasonTotals,
} from '@/src/lib/pickAccuracy';
import type { League, PickAccuracyByWeek } from '@/src/types/models';

const week = (week: number, correct: number, total: number) => ({
  week,
  correctPicks: correct,
  totalPicks: total,
  accuracyPercent: total > 0 ? Math.round((correct / total) * 1000) / 10 : 0,
});

const chartEntry = (
  leagueId: string,
  leagueName: string,
  weeks: ReturnType<typeof week>[],
): PickAccuracyByWeek => ({
  userId: 'u1',
  userName: 'Randall',
  leagueId,
  leagueName,
  weeklyAccuracy: weeks,
  overallAccuracyPercent: 0,
});

const league = (id: string, name: string): League => ({ id, name });

describe('activeAccuracyLeagues', () => {
  it('keeps only leagues present in /user/me, in /user/me order', () => {
    const chart = [
      chartEntry('old', 'Last Season', [week(1, 5, 10)]),
      chartEntry('b', 'Beta', [week(1, 7, 10)]),
      chartEntry('a', 'Alpha', [week(1, 6, 10)]),
    ];
    const active = [league('a', 'Alpha'), league('b', 'Beta')];
    expect(activeAccuracyLeagues(chart, active).map((l) => l.leagueId)).toEqual(['a', 'b']);
  });

  it('drops leagues with no graded week', () => {
    const chart = [
      chartEntry('a', 'Alpha', [week(1, 0, 0)]),
      chartEntry('b', 'Beta', []),
      chartEntry('c', 'Gamma', [week(1, 4, 8)]),
    ];
    const active = [league('a', 'Alpha'), league('b', 'Beta'), league('c', 'Gamma')];
    expect(activeAccuracyLeagues(chart, active).map((l) => l.leagueId)).toEqual(['c']);
  });

  it('strips ungraded weeks from a league that also has graded ones', () => {
    const chart = [chartEntry('a', 'Alpha', [week(1, 4, 8), week(2, 0, 0)])];
    const result = activeAccuracyLeagues(chart, [league('a', 'Alpha')]);
    expect(result[0].weeklyAccuracy.map((w) => w.week)).toEqual([1]);
  });

  it('returns nothing when the chart or the active list is empty', () => {
    expect(activeAccuracyLeagues(undefined, [league('a', 'Alpha')])).toEqual([]);
    expect(activeAccuracyLeagues([], [league('a', 'Alpha')])).toEqual([]);
    expect(activeAccuracyLeagues([chartEntry('a', 'Alpha', [week(1, 1, 1)])], [])).toEqual([]);
  });
});

describe('meanAccuracy', () => {
  it('averages the weekly percentages, not the pick counts', () => {
    // 50% and 100% average to 75 even though the pooled rate is 3/4 too here;
    // use unequal weeks to prove it is the mean of percentages.
    expect(meanAccuracy([week(1, 1, 2), week(2, 10, 10)])).toBe(75);
    expect(meanAccuracy([week(1, 1, 10), week(2, 10, 10)])).toBe(55);
  });

  it('is 0 for no weeks', () => {
    expect(meanAccuracy([])).toBe(0);
  });
});

describe('seasonTotals', () => {
  it('sums correct and total picks', () => {
    expect(seasonTotals([week(1, 6, 10), week(2, 7, 9)])).toEqual({ correct: 13, total: 19 });
  });
});

describe('formatPercent', () => {
  it('drops the decimal for whole numbers only', () => {
    expect(formatPercent(64)).toBe('64%');
    expect(formatPercent(64.2)).toBe('64.2%');
    expect(formatPercent(0)).toBe('0%');
  });
});
