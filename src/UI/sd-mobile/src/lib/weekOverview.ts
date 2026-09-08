import type {
  LeagueWeekContest,
  LeagueWeekMember,
  LeagueWeekOverview,
  UserPick,
} from '@/src/types/models';

// Pure derivations for the By Week pane (design handoff: D consensus rail,
// A expanded card, A3 pre-lock readiness). Kept free of React/RN so the
// pane's logic is unit-testable; the component only renders these shapes.

export type ContestPhase = 'unlocked' | 'locked' | 'live' | 'final';

/**
 * Phase off the contest's own fields. `isLocked` is server-stamped (kickoff
 * − 5 min, the reveal boundary — never re-derive it client-side); final is
 * completion/finalization; live is locked + kicked off + not final.
 */
export function contestPhase(contest: LeagueWeekContest, nowMs: number): ContestPhase {
  if (!contest.isLocked) return 'unlocked';
  if (contest.completedUtc || contest.finalizedUtc) return 'final';
  return nowMs >= Date.parse(contest.startDateUtc) ? 'live' : 'locked';
}

export interface SideSplit {
  awayCount: number;
  homeCount: number;
  /** 0..1 share of revealed picks on the away side (0.5 when none revealed). */
  awayShare: number;
}

/** Revealed-pick counts per side for the strip's majority split bar. */
export function sideSplit(contest: LeagueWeekContest, picks: UserPick[]): SideSplit {
  let awayCount = 0;
  let homeCount = 0;
  for (const p of picks) {
    if (p.franchiseSeasonId === contest.awayFranchiseSeasonId) awayCount += 1;
    else if (p.franchiseSeasonId === contest.homeFranchiseSeasonId) homeCount += 1;
  }
  const total = awayCount + homeCount;
  return { awayCount, homeCount, awayShare: total === 0 ? 0.5 : awayCount / total };
}

export interface MemberPickRow {
  member: LeagueWeekMember;
  /** Undefined = no revealed pick for this contest (no pick, or withheld). */
  pick?: UserPick;
  /** Short name of the picked team, derived by franchiseSeasonId match. */
  teamShort?: string;
  pickedHome?: boolean;
}

/**
 * One row per member for a contest, sorted: revealed picks by confidence
 * desc (the big bets on top — handoff A), then alphabetical; no-pick rows
 * trail in name order. The caller decides how many to show (A2 collapses to
 * the top 5 for 10+ member leagues).
 */
export function memberRowsForContest(
  contest: LeagueWeekContest,
  members: LeagueWeekMember[],
  picksByUser: Map<string, Map<string, UserPick>>,
): MemberPickRow[] {
  const rows: MemberPickRow[] = members.map((member) => {
    const pick = picksByUser.get(member.userId)?.get(contest.contestId);
    if (!pick) return { member };
    const pickedHome = pick.franchiseSeasonId === contest.homeFranchiseSeasonId;
    const teamShort = pickedHome
      ? contest.homeShort
      : pick.franchiseSeasonId === contest.awayFranchiseSeasonId
        ? contest.awayShort
        : undefined;
    return { member, pick, teamShort, pickedHome };
  });

  return rows.sort((a, b) => {
    if (!!a.pick !== !!b.pick) return a.pick ? -1 : 1;
    const conf = (b.pick?.confidencePoints ?? 0) - (a.pick?.confidencePoints ?? 0);
    if (conf !== 0) return conf;
    return a.member.displayName.localeCompare(b.member.displayName);
  });
}

/** picksByUser[userId][contestId] — one pass over the payload. */
export function indexPicks(picks: UserPick[]): Map<string, Map<string, UserPick>> {
  const byUser = new Map<string, Map<string, UserPick>>();
  for (const p of picks) {
    let inner = byUser.get(p.userId);
    if (!inner) {
      inner = new Map();
      byUser.set(p.userId, inner);
    }
    inner.set(p.contestId, p);
  }
  return byUser;
}

export interface WeekPhaseSummary {
  /** Contests to render as rail strips, in start order (locked/live/final). */
  revealed: LeagueWeekContest[];
  /** Count of games whose picks are still hidden. */
  unlockedCount: number;
  /** First lock instant (kickoff − 5 min) across unlocked games, ms epoch. */
  firstLockMs: number | null;
  /** True = nothing revealed yet: render the A3 pre-lock readiness state. */
  preLock: boolean;
}

const LOCK_LEAD_MS = 5 * 60 * 1000;

export function summarizeWeek(overview: LeagueWeekOverview, nowMs: number): WeekPhaseSummary {
  const revealed: LeagueWeekContest[] = [];
  let unlockedCount = 0;
  let firstLockMs: number | null = null;

  for (const contest of overview.contests) {
    if (contestPhase(contest, nowMs) === 'unlocked') {
      unlockedCount += 1;
      const lockMs = Date.parse(contest.startDateUtc) - LOCK_LEAD_MS;
      if (firstLockMs === null || lockMs < firstLockMs) firstLockMs = lockMs;
    } else {
      revealed.push(contest);
    }
  }

  return {
    revealed,
    unlockedCount,
    firstLockMs,
    preLock: revealed.length === 0 && overview.contests.length > 0,
  };
}

export interface ReadinessRow {
  member: LeagueWeekMember;
  submitted: number;
  total: number;
}

/**
 * The A3 "Who's Ready" list: submitted-count per member (safe metadata from
 * the server — reveal enforcement withholds the picks themselves), most
 * ready first, ties alphabetical.
 */
export function readinessRows(
  members: LeagueWeekMember[],
  totalGames: number,
): ReadinessRow[] {
  return members
    .map((member) => ({
      member,
      submitted: Math.min(member.submittedPickCount, totalGames),
      total: totalGames,
    }))
    .sort(
      (a, b) =>
        b.submitted - a.submitted ||
        a.member.displayName.localeCompare(b.member.displayName),
    );
}
