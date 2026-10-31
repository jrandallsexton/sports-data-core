import { useEffect, useMemo } from 'react';
import { AppState } from 'react-native';

import { useAuthStore } from '@/src/stores/authStore';
import { statsKey, useMiniGameResultsStore } from '@/src/stores/miniGameResultsStore';
import { mergeStats, summarize } from '@/src/utils/minigames/stats';

/**
 * The signed-in user's record for one mini-game: last cached server stats plus
 * anything still queued on the device, so it's current offline too. Opening it
 * uploads the queue and refreshes from the server.
 */
export function useMiniGameStats(gameKey: string) {
  const uid = useAuthStore((s) => s.user?.uid ?? null);
  const queue = useMiniGameResultsStore((s) => s.queue);
  const cached = useMiniGameResultsStore((s) => (uid ? s.stats[statsKey(uid, gameKey)] : undefined));
  const hydrated = useMiniGameResultsStore((s) => s.hydrated);

  useEffect(() => {
    const { hydrate, flush, refresh } = useMiniGameResultsStore.getState();
    void hydrate();
    if (!uid) return;
    void flush().then(() => refresh(gameKey));
  }, [uid, gameKey]);

  const stats = useMemo(
    () => mergeStats(cached ?? null, queue.filter((q) => q.ownerUid === uid && q.gameKey === gameKey)),
    [cached, queue, uid, gameKey],
  );

  return { stats, summary: summarize(stats), hydrated };
}

/**
 * App-wide upload trigger for queued mini-game results: on sign-in (including
 * app start with a restored session) and whenever the app returns to the
 * foreground. Results are also uploaded the moment a game finishes.
 */
export function useMiniGameResultSync() {
  const uid = useAuthStore((s) => s.user?.uid ?? null);

  useEffect(() => {
    if (!uid) return;
    const { flush } = useMiniGameResultsStore.getState();
    void flush();
    const sub = AppState.addEventListener('change', (state) => {
      if (state === 'active') void flush();
    });
    return () => sub.remove();
  }, [uid]);
}
