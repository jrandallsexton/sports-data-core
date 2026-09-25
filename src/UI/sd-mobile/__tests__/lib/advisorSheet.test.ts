import {
  ADVISOR_LEVELS,
  applicablePicks,
  applyLabel,
  describePick,
  describeStanding,
  describeStatBot,
  formatProbability,
  levelMeta,
} from '@/src/lib/advisorSheet';
import type { AdvisedPick, PickAdviceAnalysis } from '@/src/types/models';

const pick = (over: Partial<AdvisedPick>): AdvisedPick => ({
  contestId: 'c',
  headline: null,
  kind: 'Lock',
  franchiseSeasonId: 't',
  confidencePoints: null,
  modelProbability: null,
  previewAgrees: null,
  isCoinFlip: false,
  differsFromExisting: true,
  ...over,
});

const analysis = (over: Partial<PickAdviceAnalysis>): PickAdviceAnalysis => ({
  rank: 5,
  lastWeekRank: null,
  memberCount: 10,
  totalPoints: 100,
  weeklyAverage: 50,
  pointsPerGame: 5,
  pickAccuracy: 60,
  leaderName: 'Aesop',
  leaderTotalPoints: 814,
  leaderWeeklyAverage: 80,
  leaderPointsPerGame: 9,
  deficit: 714,
  regularSeasonWeeksLeft: 9,
  gamesThisWeek: 21,
  maxPointsThisWeek: 231,
  leaderExpectedThisWeek: 190,
  canCloseGapThisWeek: false,
  bestCaseRankThisWeek: 3,
  nextAheadName: 'Dave',
  nextAheadRank: 4,
  pointsBehindNextAhead: 40,
  nextAheadExpectedThisWeek: 130,
  statBot: null,
  ...over,
});

describe('levelMeta', () => {
  it('lists four levels in increasing risk and resolves by key', () => {
    expect(ADVISOR_LEVELS.map((l) => l.key)).toEqual(['Prevent', 'GoalLine', 'QbDraw', 'HailMary']);
    expect(levelMeta('HailMary').name).toBe('Hail Mary');
    expect(levelMeta(null).key).toBe('Prevent');
  });
});

describe('formatProbability', () => {
  it('renders a whole percent and tolerates null', () => {
    expect(formatProbability(0.7312)).toBe('73%');
    expect(formatProbability(null)).toBeNull();
  });
});

describe('describePick', () => {
  it('speaks in probability with the right names: deetsMeter is the number, StatBot the preview', () => {
    expect(describePick({ kind: 'Lock', modelProbability: 0.85, previewAgrees: true })).toBe(
      'deetsMeter 85% — lock, StatBot agrees',
    );
    expect(describePick({ kind: 'Lock', modelProbability: 0.85, previewAgrees: null })).toBe(
      'deetsMeter 85% — lock',
    );
    expect(describePick({ kind: 'Lean', modelProbability: 0.55, previewAgrees: null })).toBe(
      'Coin flip (55%) — staying with the deetsMeter',
    );
    expect(describePick({ kind: 'Lean', modelProbability: 0.78, previewAgrees: false })).toBe(
      'deetsMeter 78% but StatBot disagrees — treated as a coin flip, staying with the deetsMeter',
    );
    expect(describePick({ kind: 'Flip', modelProbability: 0.48, previewAgrees: null })).toBe(
      'Coin flip — taking the other side (48%) to gain ground',
    );
    expect(describePick({ kind: 'Flip', modelProbability: 0.3, previewAgrees: false })).toBe(
      "Coin flip — deetsMeter and StatBot split; taking StatBot's side (30%)",
    );
    expect(describePick({ kind: 'Locked', modelProbability: null, previewAgrees: null })).toMatch(/untouched/);
    expect(describePick({ kind: 'NoPrediction', modelProbability: null, previewAgrees: null })).not.toMatch(/StatBot/);
  });
});

describe('applicablePicks', () => {
  it('keeps only writable rows that would change something', () => {
    const picks = [
      pick({ contestId: 'a', kind: 'Lock' }),
      pick({ contestId: 'b', kind: 'Lock', differsFromExisting: false }),
      pick({ contestId: 'c', kind: 'Locked', differsFromExisting: false }),
      pick({ contestId: 'd', kind: 'NoPrediction', franchiseSeasonId: null }),
      pick({ contestId: 'e', kind: 'Flip' }),
    ];
    expect(applicablePicks(picks).map((p) => p.contestId)).toEqual(['a', 'e']);
    expect(applicablePicks(undefined)).toEqual([]);
  });

  it('never submits a valueless row in a confidence league', () => {
    const picks = [
      pick({ contestId: 'a', confidencePoints: 3 }),
      pick({ contestId: 'b', confidencePoints: null }),
    ];
    expect(applicablePicks(picks, true).map((p) => p.contestId)).toEqual(['a']);
    expect(applicablePicks(picks, false).map((p) => p.contestId)).toEqual(['a', 'b']);
  });
});

describe('applyLabel', () => {
  const rows = [pick({ contestId: 'a' }), pick({ contestId: 'b' }), pick({ contestId: 'c' })];
  const picked = (...ids: string[]) => new Map(ids.map((id) => [id, { franchiseSeasonId: 'x' }]));

  it('says Set on an unpicked week, never Replace', () => {
    expect(applyLabel(rows, new Map())).toBe('Set 3 picks');
    expect(applyLabel(rows, undefined)).toBe('Set 3 picks');
  });

  it('says Replace only when every affected row already has a pick, and splits a mix', () => {
    expect(applyLabel(rows, picked('a', 'b', 'c'))).toBe('Replace 3 picks');
    expect(applyLabel(rows.slice(0, 1), picked('a'))).toBe('Replace 1 pick');
    expect(applyLabel(rows, picked('a'))).toBe('Set 2, replace 1');
    expect(applyLabel([], new Map())).toBe('Nothing to change');
  });
});

describe('describeStanding', () => {
  it('handles unranked and leading', () => {
    expect(describeStanding(analysis({ rank: null }))).toMatch(/No scored weeks/);
    expect(
      describeStanding(
        analysis({ rank: 1, deficit: 0, gamesThisWeek: 4, maxPointsThisWeek: 10, regularSeasonWeeksLeft: 6 }),
      ),
    ).toBe("You're leading. 4 games this week, up to 10 points on the table. 6 weeks left in the regular season.");
  });

  it('says a perfect week takes the lead only with the leader scoring at pace', () => {
    expect(
      describeStanding(
        analysis({
          rank: 3, deficit: 10, gamesThisWeek: 5, maxPointsThisWeek: 15, regularSeasonWeeksLeft: 6,
          canCloseGapThisWeek: true, leaderExpectedThisWeek: 4, leaderName: 'Leader',
        }),
      ),
    ).toBe(
      "You're 3rd, 10 points behind Leader. 5 games this week, up to 15 points on the table. 6 weeks left in the regular season. A perfect week takes the lead even if Leader adds about 4 on this slate at their per-game pace.",
    );
  });

  it('offers a reachable rank, never forecasts games, never says "a week"', () => {
    const text = describeStanding(analysis({}));
    expect(text).toBe(
      "You're 5th, 714 points behind Aesop. 21 games this week, up to 231 points on the table. 9 weeks left in the regular season. With everyone at pace, a perfect week moves you to 3rd at best. Dave in 4th is 40 points ahead and, at their per-game pace, adds about 130 on this slate.",
    );
    expect(text).not.toMatch(/a game to|left this season|a week/);
  });

  it('says so when nobody is reachable, and omits weeks when the calendar was unavailable', () => {
    const text = describeStanding(
      analysis({ regularSeasonWeeksLeft: null, bestCaseRankThisWeek: 5, pointsBehindNextAhead: 300 }),
    );
    expect(text).toBe(
      "You're 5th, 714 points behind Aesop. 21 games this week, up to 231 points on the table. Dave in 4th is 300 points ahead and, at their per-game pace, adds about 130 on this slate — out of reach this week, so play for next.",
    );
    expect(text).not.toMatch(/week left|weeks left|a week/);
  });
});

describe('describeStatBot', () => {
  it('is honest when StatBot trails the user', () => {
    expect(
      describeStatBot(analysis({ rank: 2, statBot: { rank: 5, totalPoints: 1, weeklyAverage: 1, pointsPerGame: 4.1 } })),
    ).toMatch(/behind you/);
    expect(
      describeStatBot(analysis({ rank: 6, statBot: { rank: 2, totalPoints: 1, weeklyAverage: 1, pointsPerGame: 6.2 } })),
    ).toBe('StatBot is #2 in this league, 6.2 points per game.');
    expect(describeStatBot(analysis({ statBot: null }))).toBeNull();
  });
});
