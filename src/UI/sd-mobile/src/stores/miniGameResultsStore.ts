import { create } from 'zustand';
import AsyncStorage from '@react-native-async-storage/async-storage';
import * as Sentry from '@sentry/react-native';

import { miniGamesApi } from '@/src/services/api/miniGamesApi';
import { useAuthStore } from '@/src/stores/authStore';
import type { MiniGameResult, MiniGameStats, QueuedMiniGameResult } from '@/src/utils/minigames/types';

const QUEUE_KEY = 'minigames.queue';
const STATS_KEY = 'minigames.stats';
/** The API's batch cap. */
const BATCH_SIZE = 100;

export const statsKey = (uid: string, gameKey: string) => `${uid}:${gameKey}`;

interface MiniGameResultsState {
  /** Finished games not yet confirmed by the server, for every account on this device. */
  queue: QueuedMiniGameResult[];
  /** Last server stats, by statsKey(uid, gameKey), so the panel works offline. */
  stats: Record<string, MiniGameStats>;
  hydrated: boolean;

  hydrate: () => Promise<void>;
  /** Queue a finished game for the signed-in user and try to upload it. */
  record: (result: MiniGameResult) => Promise<void>;
  /** Upload the signed-in user's queued games. Safe to call any time; one runs at a time. */
  flush: () => Promise<void>;
  /** Re-read one game's stats from the server into the offline cache. */
  refresh: (gameKey: string) => Promise<void>;
}

let hydrating: Promise<void> | null = null;
let flushing: Promise<void> | null = null;
/** A result arrived mid-flush; run another pass when this one ends. */
let flushAgain = false;

const currentUid = () => useAuthStore.getState().user?.uid ?? null;

const save = (key: string, value: unknown) =>
  AsyncStorage.setItem(key, JSON.stringify(value)).catch(() => {});

function parse<T>(raw: string | null, fallback: T): T {
  if (!raw) return fallback;
  try {
    return JSON.parse(raw) as T;
  } catch {
    return fallback;
  }
}

/**
 * Offline-first mini-game results (docs/features/mini-game-results.md). A
 * finished game is queued in AsyncStorage, then uploaded. Nothing checks for
 * connectivity: an upload that fails leaves the queue alone for the next
 * trigger (a new result, app start, foreground, the stats panel opening).
 *
 * A queued result leaves the queue only after the server stats that include it
 * are cached, so the merged view (cached stats + queue) never dips in between.
 */
export const useMiniGameResultsStore = create<MiniGameResultsState>((set, get) => {
  async function fetchStats(uid: string, gameKey: string) {
    const { data } = await miniGamesApi.getStats(gameKey);
    const stats = { ...get().stats, [statsKey(uid, gameKey)]: data };
    set({ stats });
    await save(STATS_KEY, stats);
  }

  async function flushOnce() {
    await get().hydrate();
    const uid = currentUid();
    if (!uid) return;

    const mine = get().queue.filter((q) => q.ownerUid === uid);
    for (let i = 0; i < mine.length; i += BATCH_SIZE) {
      const batch = mine.slice(i, i + BATCH_SIZE);
      try {
        const { data } = await miniGamesApi.submitResults(batch.map(({ ownerUid: _, ...r }) => r));
        if (data.rejected.length) {
          Sentry.captureMessage(`Mini-game results rejected: ${data.rejected.length}`, 'warning');
        }
        for (const gameKey of new Set(batch.map((r) => r.gameKey))) {
          await fetchStats(uid, gameKey);
        }
        // Every reported id is settled: stored, already stored, or never storable.
        const done = new Set([...data.accepted, ...data.duplicate, ...data.rejected]);
        const queue = get().queue.filter((q) => !(q.ownerUid === uid && done.has(q.id)));
        set({ queue });
        await save(QUEUE_KEY, queue);
      } catch {
        // Offline or the API is down. The queue stays; a re-upload is a no-op.
        return;
      }
    }
  }

  return {
    queue: [],
    stats: {},
    hydrated: false,

    hydrate: () => {
      if (get().hydrated) return Promise.resolve();
      hydrating ??= Promise.all([AsyncStorage.getItem(QUEUE_KEY), AsyncStorage.getItem(STATS_KEY)])
        .then(([q, s]) => {
          // Anything recorded before hydration finished goes after the stored queue.
          set({
            queue: [...parse<QueuedMiniGameResult[]>(q, []), ...get().queue],
            stats: { ...parse<Record<string, MiniGameStats>>(s, {}), ...get().stats },
            hydrated: true,
          });
        })
        .catch(() => set({ hydrated: true }))
        .finally(() => {
          hydrating = null;
        });
      return hydrating;
    },

    record: async (result) => {
      const uid = currentUid();
      if (!uid) return;
      await get().hydrate();
      const queue = [...get().queue, { ...result, ownerUid: uid }];
      set({ queue });
      await save(QUEUE_KEY, queue);
      await get().flush();
    },

    flush: () => {
      if (flushing) {
        flushAgain = true;
        return flushing;
      }
      flushing = (async () => {
        do {
          flushAgain = false;
          await flushOnce();
        } while (flushAgain);
      })().finally(() => {
        flushing = null;
      });
      return flushing;
    },

    refresh: async (gameKey) => {
      await get().hydrate();
      const uid = currentUid();
      if (!uid) return;
      try {
        await fetchStats(uid, gameKey);
      } catch {
        // Offline: the cached stats stand.
      }
    },
  };
});
