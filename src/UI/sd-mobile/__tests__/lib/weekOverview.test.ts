import {
  contestPhase,
  indexPicks,
  memberRowsForContest,
  readinessRows,
  sideSplit,
  summarizeWeek,
} from '@/src/lib/weekOverview';
import type {
  LeagueWeekContest,
  LeagueWeekMember,
  LeagueWeekOverview,
  UserPick,
} from '@/src/types/models';

const NOW = Date.parse('2026-09-07T18:00:00Z');

function contest(overrides: Partial<LeagueWeekContest> = {}): LeagueWeekContest {
  return {
    startDateUtc: '2026-09-07T17:00:00Z',
    contestId: 'c1',
    isLocked: true,
    awayShort: 'SJSU',
    awayFranchiseSeasonId: 'fs-away',
    awaySlug: 'sjsu',
    homeShort: 'USC',
    homeFranchiseSeasonId: 'fs-home',
    homeSlug: 'usc',
    ...overrides,
  };
}

function member(userId: string, displayName: string, submitted = 0): LeagueWeekMember {
  return { userId, displayName, isSynthetic: false, submittedPickCount: submitted };
}

function pick(userId: string, contestId: string, overrides: Partial<UserPick> = {}): UserPick {
  return {
    id: `${userId}-${contestId}`,
    userId,
    contestId,
    franchiseSeasonId: 'fs-home',
    pickType: 'StraightUp',
    ...overrides,
  };
}

describe('contestPhase', () => {
  it('unlocked when the server says so, regardless of clock', () => {
    expect(contestPhase(contest({ isLocked: false }), NOW)).toBe('unlocked');
  });

  it('locked before kickoff, live after, final once completed', () => {
    const preKick = contest({ startDateUtc: '2026-09-07T18:03:00Z' });
    expect(contestPhase(preKick, NOW)).toBe('locked');

    const kicked = contest({ startDateUtc: '2026-09-07T17:00:00Z' });
    expect(contestPhase(kicked, NOW)).toBe('live');

    const done = contest({ completedUtc: '2026-09-07T17:50:00Z' });
    expect(contestPhase(done, NOW)).toBe('final');
  });

  it('finalizedUtc alone is final too', () => {
    expect(contestPhase(contest({ finalizedUtc: '2026-09-07T17:59:00Z' }), NOW)).toBe('final');
  });
});

describe('sideSplit', () => {
  it('counts revealed picks per side; foreign franchise ids count nowhere', () => {
    const c = contest();
    const split = sideSplit(c, [
      pick('u1', 'c1', { franchiseSeasonId: 'fs-home' }),
      pick('u2', 'c1', { franchiseSeasonId: 'fs-home' }),
      pick('u3', 'c1', { franchiseSeasonId: 'fs-away' }),
      pick('u4', 'c1', { franchiseSeasonId: 'fs-elsewhere' }),
    ]);
    expect(split.homeCount).toBe(2);
    expect(split.awayCount).toBe(1);
    expect(split.awayShare).toBeCloseTo(1 / 3);
  });

  it('splits 50/50 with nothing revealed', () => {
    expect(sideSplit(contest(), []).awayShare).toBe(0.5);
  });
});

describe('memberRowsForContest', () => {
  it('sorts revealed picks by confidence desc, no-pick rows trail alphabetically', () => {
    const members = [
      member('u-low', 'Zed'),
      member('u-high', 'Aesop'),
      member('u-none-b', 'Beta'),
      member('u-none-a', 'Alpha'),
    ];
    const picks = indexPicks([
      pick('u-low', 'c1', { confidencePoints: 3 }),
      pick('u-high', 'c1', { confidencePoints: 30 }),
    ]);
    const rows = memberRowsForContest(contest(), members, picks);
    expect(rows.map((r) => r.member.userId)).toEqual(['u-high', 'u-low', 'u-none-a', 'u-none-b']);
  });

  it('derives the picked team via franchiseSeasonId match', () => {
    const rows = memberRowsForContest(
      contest(),
      [member('u1', 'A'), member('u2', 'B')],
      indexPicks([
        pick('u1', 'c1', { franchiseSeasonId: 'fs-home' }),
        pick('u2', 'c1', { franchiseSeasonId: 'fs-away' }),
      ]),
    );
    expect(rows.find((r) => r.member.userId === 'u1')?.teamShort).toBe('USC');
    expect(rows.find((r) => r.member.userId === 'u2')?.teamShort).toBe('SJSU');
  });
});

describe('summarizeWeek', () => {
  const overview = (contests: LeagueWeekContest[]): LeagueWeekOverview => ({
    contests,
    userPicks: [],
    members: [],
  });

  it('pre-lock when every game is unlocked, with the earliest lock instant', () => {
    const s = summarizeWeek(
      overview([
        contest({ contestId: 'c1', isLocked: false, startDateUtc: '2026-09-12T16:00:00Z' }),
        contest({ contestId: 'c2', isLocked: false, startDateUtc: '2026-09-12T20:00:00Z' }),
      ]),
      NOW,
    );
    expect(s.preLock).toBe(true);
    expect(s.revealed).toHaveLength(0);
    expect(s.unlockedCount).toBe(2);
    // kickoff − 5 min of the EARLIEST game
    expect(s.firstLockMs).toBe(Date.parse('2026-09-12T15:55:00Z'));
  });

  it('mid-week: revealed strips plus a count of still-hidden games', () => {
    const s = summarizeWeek(
      overview([
        contest({ contestId: 'c1' }),
        contest({ contestId: 'c2', isLocked: false, startDateUtc: '2026-09-12T16:00:00Z' }),
      ]),
      NOW,
    );
    expect(s.preLock).toBe(false);
    expect(s.revealed.map((c) => c.contestId)).toEqual(['c1']);
    expect(s.unlockedCount).toBe(1);
  });

  it('an empty week is not pre-lock (renders empty, not the reveal card)', () => {
    expect(summarizeWeek(overview([]), NOW).preLock).toBe(false);
  });
});

describe('readinessRows', () => {
  it('sorts most-ready first, ties alphabetical, counts capped at the slate', () => {
    const rows = readinessRows(
      [member('u1', 'Zed', 12), member('u2', 'Aesop', 12), member('u3', 'Ranman', 8), member('u4', 'New', 99)],
      12,
    );
    expect(rows.map((r) => r.member.displayName)).toEqual(['Aesop', 'New', 'Zed', 'Ranman']);
    expect(rows[1].submitted).toBe(12); // capped
  });
});
