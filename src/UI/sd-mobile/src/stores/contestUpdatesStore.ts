import { create } from 'zustand';
import type {
  BaseballPlayCompletedPayload,
  ContestStatusChangedPayload,
  FootballPlayCompletedPayload,
} from '@/src/types/signalR';

/**
 * Merged live snapshot for a single contest. Fields are accumulated from
 * the three event types — readers should treat anything they don't expect
 * for the current sport as undefined.
 */
export interface ContestLiveRecord {
  contestId: string;
  /** Raw ESPN status type name (e.g. "STATUS_IN_PROGRESS"). */
  status?: string;
  /** Human-readable status (e.g. "In Progress"). For display. */
  statusDescription?: string;

  awayScore?: number;
  homeScore?: number;

  // Football fields
  period?: string;
  clock?: string;
  possessionFranchiseSeasonId?: string | null;
  isScoringPlay?: boolean;
  ballOnYardLine?: number | null;

  // Baseball fields
  inning?: number;
  halfInning?: string;
  balls?: number;
  strikes?: number;
  outs?: number;
  runnerOnFirst?: boolean;
  runnerOnSecond?: boolean;
  runnerOnThird?: boolean;
  atBatAthleteSeasonId?: string | null;
  atBatShortName?: string | null;
  atBatPositionAbbreviation?: string | null;
  atBatHeadshotUrl?: string | null;
  pitchingAthleteSeasonId?: string | null;
  pitchingShortName?: string | null;
  pitchingPositionAbbreviation?: string | null;
  pitchingHeadshotUrl?: string | null;

  lastPlayId?: string;
  lastPlayDescription?: string;
  lastPlayAt?: number;
  lastUpdated?: number;
}

interface ContestUpdatesState {
  contests: Record<string, ContestLiveRecord>;

  handleStatusUpdate: (data: ContestStatusChangedPayload) => void;
  handleFootballPlayCompleted: (data: FootballPlayCompletedPayload) => void;
  handleBaseballPlayCompleted: (data: BaseballPlayCompletedPayload) => void;
  clearContestUpdate: (contestId: string) => void;
  clearAllUpdates: () => void;
}

const initialState = {
  contests: {} as Record<string, ContestLiveRecord>,
};

/**
 * Per-contest pending "clear isScoringPlay" timer. Kept outside Zustand
 * because it's a side-effect bookkeeping concern, not state consumers
 * should subscribe to. A second scoring play within the 2s flash window
 * must clear the prior timer; otherwise the earlier timer fires first
 * and ends the flash before the new window completes.
 */
const scoringFlashTimers = new Map<string, ReturnType<typeof setTimeout>>();

export const useContestUpdatesStore = create<ContestUpdatesState>((set) => ({
  ...initialState,

  handleStatusUpdate: (data) => {
    if (!data?.contestId) return;
    const now = Date.now();
    set((state) => ({
      contests: {
        ...state.contests,
        [data.contestId]: {
          ...state.contests[data.contestId],
          contestId: data.contestId,
          status: data.status,
          statusDescription: data.statusDescription,
          lastUpdated: now,
        },
      },
    }));
  },

  handleFootballPlayCompleted: (data) => {
    if (!data?.contestId) return;
    const now = Date.now();
    set((state) => ({
      contests: {
        ...state.contests,
        [data.contestId]: {
          ...state.contests[data.contestId],
          contestId: data.contestId,
          // SignalR has no buffer; a client that connects after the
          // ContestStatusChanged fan-out would otherwise stay stuck on
          // the prior status. Receiving any play is itself proof the
          // contest is live — promote here. (Web counterpart: PR #322.)
          status: 'STATUS_IN_PROGRESS',
          statusDescription: 'In Progress',
          period: data.period,
          clock: data.clock,
          awayScore: data.awayScore,
          homeScore: data.homeScore,
          possessionFranchiseSeasonId: data.possessionFranchiseSeasonId,
          isScoringPlay: data.isScoringPlay ?? false,
          ballOnYardLine: data.ballOnYardLine,
          lastPlayId: data.playId,
          lastPlayDescription: data.playDescription,
          lastPlayAt: now,
          lastUpdated: now,
        },
      },
    }));

    // Auto-clear scoring flash after the UI's animation window. A
    // second scoring play within 2s replaces the prior timer so the
    // flash always runs the full window from the most recent play.
    if (data.isScoringPlay) {
      const prior = scoringFlashTimers.get(data.contestId);
      if (prior !== undefined) clearTimeout(prior);

      const timer = setTimeout(() => {
        scoringFlashTimers.delete(data.contestId);
        set((state) => {
          const existing = state.contests[data.contestId];
          if (!existing) return state;
          return {
            contests: {
              ...state.contests,
              [data.contestId]: { ...existing, isScoringPlay: false },
            },
          };
        });
      }, 2000);

      scoringFlashTimers.set(data.contestId, timer);
    }
  },

  handleBaseballPlayCompleted: (data) => {
    if (!data?.contestId) return;
    const now = Date.now();
    set((state) => ({
      contests: {
        ...state.contests,
        [data.contestId]: {
          ...state.contests[data.contestId],
          contestId: data.contestId,
          // Same self-heal rationale as handleFootballPlayCompleted.
          status: 'STATUS_IN_PROGRESS',
          statusDescription: 'In Progress',
          inning: data.inning,
          halfInning: data.halfInning,
          awayScore: data.awayScore,
          homeScore: data.homeScore,
          balls: data.balls,
          strikes: data.strikes,
          outs: data.outs,
          runnerOnFirst: data.runnerOnFirst,
          runnerOnSecond: data.runnerOnSecond,
          runnerOnThird: data.runnerOnThird,
          atBatAthleteSeasonId: data.atBatAthleteSeasonId,
          atBatShortName: data.atBatShortName,
          atBatPositionAbbreviation: data.atBatPositionAbbreviation,
          atBatHeadshotUrl: data.atBatHeadshotUrl,
          pitchingAthleteSeasonId: data.pitchingAthleteSeasonId,
          pitchingShortName: data.pitchingShortName,
          pitchingPositionAbbreviation: data.pitchingPositionAbbreviation,
          pitchingHeadshotUrl: data.pitchingHeadshotUrl,
          lastPlayId: data.playId,
          lastPlayDescription: data.playDescription,
          lastPlayAt: now,
          lastUpdated: now,
        },
      },
    }));
  },

  clearContestUpdate: (contestId) => {
    const pending = scoringFlashTimers.get(contestId);
    if (pending !== undefined) {
      clearTimeout(pending);
      scoringFlashTimers.delete(contestId);
    }
    set((state) => {
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
      const { [contestId]: _removed, ...rest } = state.contests;
      return { contests: rest };
    });
  },

  clearAllUpdates: () => {
    scoringFlashTimers.forEach((t) => clearTimeout(t));
    scoringFlashTimers.clear();
    set({ contests: {} });
  },
}));

/**
 * Per-contest selector hook. Only consumers whose `contestId` actually
 * changed re-render — avoids the broadcast re-render storm the equivalent
 * React Context would produce during a busy slate.
 */
export const useContestUpdate = (
  contestId: string | undefined,
): ContestLiveRecord | undefined =>
  useContestUpdatesStore((state) =>
    contestId ? state.contests[contestId] : undefined,
  );

export const useHasLiveUpdate = (contestId: string | undefined): boolean =>
  useContestUpdatesStore((state) =>
    contestId ? !!state.contests[contestId] : false,
  );
