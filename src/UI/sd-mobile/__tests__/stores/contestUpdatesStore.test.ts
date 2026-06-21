import { useContestUpdatesStore } from '@/src/stores/contestUpdatesStore';
import type {
  BaseballPlayCompletedPayload,
  ContestFinalizedPayload,
  ContestStatusChangedPayload,
  FootballPlayCompletedPayload,
} from '@/src/types/signalR';

const CID = '00000000-0000-0000-0000-000000000001';

const statusPayload = (overrides?: Partial<ContestStatusChangedPayload>): ContestStatusChangedPayload => ({
  contestId: CID,
  status: 'STATUS_SCHEDULED',
  statusDescription: 'Scheduled',
  ...overrides,
});

const footballPayload = (overrides?: Partial<FootballPlayCompletedPayload>): FootballPlayCompletedPayload => ({
  contestId: CID,
  competitionId: '00000000-0000-0000-0000-0000000000aa',
  playId: '00000000-0000-0000-0000-0000000000bb',
  playDescription: '10-yard pass',
  period: 'Q1',
  clock: '14:53',
  awayScore: 0,
  homeScore: 0,
  possessionFranchiseSeasonId: null,
  isScoringPlay: false,
  ballOnYardLine: 25,
  ...overrides,
});

const baseballPayload = (overrides?: Partial<BaseballPlayCompletedPayload>): BaseballPlayCompletedPayload => ({
  contestId: CID,
  competitionId: '00000000-0000-0000-0000-0000000000aa',
  playId: '00000000-0000-0000-0000-0000000000bb',
  playDescription: 'Single to right',
  inning: 1,
  halfInning: 'Top',
  awayScore: 0,
  homeScore: 0,
  balls: 0,
  strikes: 0,
  outs: 0,
  runnerOnFirst: false,
  runnerOnSecond: false,
  runnerOnThird: false,
  atBatAthleteSeasonId: null,
  atBatShortName: null,
  atBatPositionAbbreviation: null,
  atBatHeadshotUrl: null,
  pitchingAthleteSeasonId: null,
  pitchingShortName: null,
  pitchingPositionAbbreviation: null,
  pitchingHeadshotUrl: null,
  ...overrides,
});

describe('contestUpdatesStore', () => {
  beforeEach(() => {
    useContestUpdatesStore.setState(useContestUpdatesStore.getInitialState(), true);
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  describe('handleStatusUpdate', () => {
    it('writes status and contestId for a fresh contest', () => {
      useContestUpdatesStore.getState().handleStatusUpdate(statusPayload({ status: 'STATUS_IN_PROGRESS', statusDescription: 'In Progress' }));

      const record = useContestUpdatesStore.getState().contests[CID];
      expect(record).toBeDefined();
      expect(record.contestId).toBe(CID);
      expect(record.status).toBe('STATUS_IN_PROGRESS');
      expect(record.statusDescription).toBe('In Progress');
      expect(record.lastUpdated).toEqual(expect.any(Number));
    });

    it('ignores payloads missing contestId', () => {
      useContestUpdatesStore.getState().handleStatusUpdate({ contestId: '', status: 'STATUS_IN_PROGRESS', statusDescription: 'In Progress' });
      expect(Object.keys(useContestUpdatesStore.getState().contests)).toHaveLength(0);
    });

    it('overwrites prior status without dropping other fields', () => {
      useContestUpdatesStore.getState().handleFootballPlayCompleted(footballPayload({ period: 'Q3' }));
      useContestUpdatesStore.getState().handleStatusUpdate(statusPayload({ status: 'STATUS_FINAL', statusDescription: 'Final' }));

      const record = useContestUpdatesStore.getState().contests[CID];
      expect(record.status).toBe('STATUS_FINAL');
      expect(record.period).toBe('Q3');
    });
  });

  describe('handleFootballPlayCompleted', () => {
    it('promotes status to STATUS_IN_PROGRESS even if no prior status event arrived (self-heal)', () => {
      useContestUpdatesStore.getState().handleFootballPlayCompleted(footballPayload());

      expect(useContestUpdatesStore.getState().contests[CID].status).toBe('STATUS_IN_PROGRESS');
      expect(useContestUpdatesStore.getState().contests[CID].statusDescription).toBe('In Progress');
    });

    it('writes football scoreboard fields', () => {
      useContestUpdatesStore.getState().handleFootballPlayCompleted(
        footballPayload({
          period: 'Q4',
          clock: '0:32',
          awayScore: 21,
          homeScore: 24,
          ballOnYardLine: 8,
        }),
      );

      const record = useContestUpdatesStore.getState().contests[CID];
      expect(record.period).toBe('Q4');
      expect(record.clock).toBe('0:32');
      expect(record.awayScore).toBe(21);
      expect(record.homeScore).toBe(24);
      expect(record.ballOnYardLine).toBe(8);
    });

    it('auto-clears isScoringPlay after 2 seconds', () => {
      useContestUpdatesStore.getState().handleFootballPlayCompleted(
        footballPayload({ isScoringPlay: true }),
      );

      expect(useContestUpdatesStore.getState().contests[CID].isScoringPlay).toBe(true);

      jest.advanceTimersByTime(1999);
      expect(useContestUpdatesStore.getState().contests[CID].isScoringPlay).toBe(true);

      jest.advanceTimersByTime(1);
      expect(useContestUpdatesStore.getState().contests[CID].isScoringPlay).toBe(false);
    });

    it('does not schedule a clear-timer when isScoringPlay is false', () => {
      useContestUpdatesStore.getState().handleFootballPlayCompleted(
        footballPayload({ isScoringPlay: false }),
      );

      // No timer should be pending — jest will fail if pending fake timers run.
      expect(jest.getTimerCount()).toBe(0);
    });

    it('debounces overlapping scoring plays — the flash window resets on the latest play', () => {
      // Scoring play 1 at t=0
      useContestUpdatesStore.getState().handleFootballPlayCompleted(
        footballPayload({ isScoringPlay: true }),
      );
      jest.advanceTimersByTime(1500);
      expect(useContestUpdatesStore.getState().contests[CID].isScoringPlay).toBe(true);

      // Scoring play 2 at t=1500ms — must reset the flash window. Without
      // the debounce, the t=0 timer would still fire at t=2000ms and clear
      // the flag prematurely.
      useContestUpdatesStore.getState().handleFootballPlayCompleted(
        footballPayload({ isScoringPlay: true }),
      );

      // At t=2000ms (would-be-fire of play-1's timer if not cleared).
      jest.advanceTimersByTime(500);
      expect(useContestUpdatesStore.getState().contests[CID].isScoringPlay).toBe(true);

      // At t=3500ms (2s after play 2). Flash clears.
      jest.advanceTimersByTime(1500);
      expect(useContestUpdatesStore.getState().contests[CID].isScoringPlay).toBe(false);
    });
  });

  describe('handleBaseballPlayCompleted', () => {
    it('promotes status to STATUS_IN_PROGRESS (self-heal)', () => {
      useContestUpdatesStore.getState().handleBaseballPlayCompleted(baseballPayload());

      expect(useContestUpdatesStore.getState().contests[CID].status).toBe('STATUS_IN_PROGRESS');
      expect(useContestUpdatesStore.getState().contests[CID].statusDescription).toBe('In Progress');
    });

    it('writes baseball scoreboard fields including runners and at-bat header', () => {
      useContestUpdatesStore.getState().handleBaseballPlayCompleted(
        baseballPayload({
          inning: 9,
          halfInning: 'Bottom',
          balls: 3,
          strikes: 2,
          outs: 2,
          runnerOnFirst: true,
          runnerOnSecond: false,
          runnerOnThird: true,
          atBatShortName: 'A. Judge',
          pitchingShortName: 'C. Sale',
        }),
      );

      const record = useContestUpdatesStore.getState().contests[CID];
      expect(record.inning).toBe(9);
      expect(record.halfInning).toBe('Bottom');
      expect(record.balls).toBe(3);
      expect(record.strikes).toBe(2);
      expect(record.outs).toBe(2);
      expect(record.runnerOnFirst).toBe(true);
      expect(record.runnerOnSecond).toBe(false);
      expect(record.runnerOnThird).toBe(true);
      expect(record.atBatShortName).toBe('A. Judge');
      expect(record.pitchingShortName).toBe('C. Sale');
    });
  });

  describe('handleContestFinalized', () => {
    const finalizedPayload = (overrides?: Partial<ContestFinalizedPayload>): ContestFinalizedPayload => ({
      contestId: CID,
      awayScore: 1,
      homeScore: 4,
      winnerFranchiseSeasonId: '11111111-1111-1111-1111-111111111111',
      spreadWinnerFranchiseSeasonId: '22222222-2222-2222-2222-222222222222',
      overUnderResultRaw: 1, // Over
      completedUtc: '2026-06-20T22:30:00Z',
      ...overrides,
    });

    it('promotes status to STATUS_FINAL even if no prior status event arrived (self-heal)', () => {
      useContestUpdatesStore.getState().handleContestFinalized(finalizedPayload());

      const record = useContestUpdatesStore.getState().contests[CID];
      expect(record.status).toBe('STATUS_FINAL');
      expect(record.statusDescription).toBe('Final');
    });

    it('writes enrichment-result fields', () => {
      useContestUpdatesStore.getState().handleContestFinalized(finalizedPayload());

      const record = useContestUpdatesStore.getState().contests[CID];
      expect(record.awayScore).toBe(1);
      expect(record.homeScore).toBe(4);
      expect(record.winnerFranchiseSeasonId).toBe('11111111-1111-1111-1111-111111111111');
      expect(record.spreadWinnerFranchiseSeasonId).toBe('22222222-2222-2222-2222-222222222222');
      expect(record.completedUtc).toBe('2026-06-20T22:30:00Z');
    });

    it.each([
      [1, 'Over'],
      [2, 'Under'],
      [3, 'Push'],
    ])('translates overUnderResultRaw %s to %s', (raw, expected) => {
      useContestUpdatesStore.getState().handleContestFinalized(finalizedPayload({ overUnderResultRaw: raw }));

      expect(useContestUpdatesStore.getState().contests[CID].overUnderResult).toBe(expected);
    });

    it.each([0, null, undefined])('leaves overUnderResult null when raw is %s (None / not enriched)', (raw) => {
      useContestUpdatesStore.getState().handleContestFinalized(
        finalizedPayload({ overUnderResultRaw: raw }),
      );

      expect(useContestUpdatesStore.getState().contests[CID].overUnderResult).toBeNull();
    });

    it('ignores payloads missing contestId', () => {
      useContestUpdatesStore.getState().handleContestFinalized(finalizedPayload({ contestId: '' }));
      expect(Object.keys(useContestUpdatesStore.getState().contests)).toHaveLength(0);
    });

    it('preserves prior live fields when finalizing (merges into existing record)', () => {
      // Arrange: a live football play arrived earlier with possession +
      // ball position; ContestFinalized should not clobber them.
      useContestUpdatesStore.getState().handleFootballPlayCompleted(footballPayload({
        possessionFranchiseSeasonId: '33333333-3333-3333-3333-333333333333',
        ballOnYardLine: 42,
      }));

      useContestUpdatesStore.getState().handleContestFinalized(finalizedPayload());

      const record = useContestUpdatesStore.getState().contests[CID];
      expect(record.possessionFranchiseSeasonId).toBe('33333333-3333-3333-3333-333333333333');
      expect(record.ballOnYardLine).toBe(42);
      // And the new fields are present.
      expect(record.winnerFranchiseSeasonId).toBe('11111111-1111-1111-1111-111111111111');
    });
  });

  describe('clearContestUpdate / clearAllUpdates', () => {
    it('clearContestUpdate removes a single record', () => {
      useContestUpdatesStore.getState().handleStatusUpdate(statusPayload());
      useContestUpdatesStore.getState().clearContestUpdate(CID);

      expect(useContestUpdatesStore.getState().contests[CID]).toBeUndefined();
    });

    it('clearAllUpdates removes every record', () => {
      useContestUpdatesStore.getState().handleStatusUpdate(statusPayload());
      useContestUpdatesStore.getState().handleStatusUpdate(statusPayload({ contestId: 'another' }));

      useContestUpdatesStore.getState().clearAllUpdates();

      expect(Object.keys(useContestUpdatesStore.getState().contests)).toHaveLength(0);
    });
  });
});
