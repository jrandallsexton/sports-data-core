import AsyncStorage from '@react-native-async-storage/async-storage';

import { miniGamesApi } from '@/src/services/api/miniGamesApi';
import { useAuthStore } from '@/src/stores/authStore';
import { statsKey, useMiniGameResultsStore } from '@/src/stores/miniGameResultsStore';
import { EMPTY_STATS } from '@/src/utils/minigames/stats';
import type { MiniGameResult } from '@/src/utils/minigames/types';

jest.mock('@/src/services/api/miniGamesApi', () => ({
  miniGamesApi: { submitResults: jest.fn(), getStats: jest.fn() },
}));
jest.mock('@sentry/react-native', () => ({ captureMessage: jest.fn() }));

const api = miniGamesApi as jest.Mocked<typeof miniGamesApi>;

const signIn = (uid: string | null) =>
  useAuthStore.setState({ user: uid ? ({ uid } as never) : null });

let n = 0;
const result = (): MiniGameResult => ({
  id: `r${n++}`,
  gameKey: 'football-cards',
  outcome: 'Win',
  score: 21,
  opponentScore: 14,
  durationSeconds: 300,
  gameVersion: '1',
  playedUtc: '2026-10-30T12:00:00.000Z',
});

const serverStats = { ...EMPTY_STATS, wins: 1, winMarginTotal: 7 };

beforeEach(async () => {
  jest.clearAllMocks();
  await AsyncStorage.clear();
  useMiniGameResultsStore.setState({ queue: [], stats: {}, hydrated: false });
  signIn('alice');
  api.getStats.mockResolvedValue({ data: serverStats } as never);
  api.submitResults.mockImplementation(async (results) => ({
    data: { accepted: results.map((r) => r.id), duplicate: [], rejected: [] },
  }) as never);
});

const store = () => useMiniGameResultsStore.getState();

it('uploads a finished game, caches stats, then clears the queue', async () => {
  const r = result();
  await store().record(r);

  expect(api.submitResults).toHaveBeenCalledWith([r]);
  expect(store().queue).toEqual([]);
  expect(store().stats[statsKey('alice', 'football-cards')]).toEqual(serverStats);
  expect(JSON.parse((await AsyncStorage.getItem('minigames.queue'))!)).toEqual([]);
});

it('keeps the game queued (and persisted) when offline', async () => {
  api.submitResults.mockRejectedValue(new Error('Network Error'));
  const r = result();

  await store().record(r);

  expect(store().queue).toEqual([{ ...r, ownerUid: 'alice' }]);
  const persisted = JSON.parse((await AsyncStorage.getItem('minigames.queue'))!);
  expect(persisted).toEqual([{ ...r, ownerUid: 'alice' }]);
});

it('keeps the game queued when stats cannot be refreshed, so the record never dips', async () => {
  api.getStats.mockRejectedValue(new Error('Network Error'));

  await store().record(result());

  expect(store().queue).toHaveLength(1);
});

it('drops duplicates and rejects from the queue too', async () => {
  api.submitResults.mockRejectedValueOnce(new Error('offline'));
  const a = result();
  const b = result();
  await store().record(a);
  api.submitResults.mockRejectedValueOnce(new Error('offline'));
  await store().record(b);
  api.submitResults.mockResolvedValue({ data: { accepted: [], duplicate: [a.id], rejected: [b.id] } } as never);

  await store().flush();

  expect(store().queue).toEqual([]);
});

it("never uploads another account's queued games", async () => {
  api.submitResults.mockRejectedValueOnce(new Error('offline'));
  const alices = result();
  await store().record(alices);

  signIn('bob');
  await store().flush();
  expect(api.submitResults).toHaveBeenCalledTimes(1); // only Alice's failed attempt
  expect(store().queue.map((q) => q.ownerUid)).toEqual(['alice']);

  signIn('alice');
  await store().flush();
  expect(api.submitResults).toHaveBeenLastCalledWith([alices]);
  expect(store().queue).toEqual([]);
});

it('records nothing when signed out', async () => {
  signIn(null);
  await store().record(result());
  expect(store().queue).toEqual([]);
  expect(api.submitResults).not.toHaveBeenCalled();
});

it('restores the queue from storage and uploads it', async () => {
  const r = { ...result(), ownerUid: 'alice' };
  await AsyncStorage.setItem('minigames.queue', JSON.stringify([r]));

  await store().flush();

  const { ownerUid: _, ...sent } = r;
  expect(api.submitResults).toHaveBeenCalledWith([sent]);
  expect(store().queue).toEqual([]);
});

it('a game finished mid-upload gets its own pass', async () => {
  let release!: () => void;
  const first = result();
  api.submitResults.mockImplementationOnce(
    (results) =>
      new Promise((resolve) => {
        release = () =>
          resolve({ data: { accepted: results.map((x) => x.id), duplicate: [], rejected: [] } } as never);
      }),
  );
  const p1 = store().record(first);
  await new Promise((r) => setTimeout(r, 0));

  const second = result();
  const p2 = store().record(second);
  await new Promise((r) => setTimeout(r, 0));
  release();
  await Promise.all([p1, p2]);

  expect(api.submitResults).toHaveBeenCalledTimes(2);
  expect(api.submitResults).toHaveBeenLastCalledWith([second]);
  expect(store().queue).toEqual([]);
});
