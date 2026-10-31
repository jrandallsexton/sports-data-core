import { useCallback, useEffect, useReducer, useRef, useState } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import * as Sentry from '@sentry/react-native';
import * as Crypto from 'expo-crypto';
import { useMiniGameResultsStore } from '@/src/stores/miniGameResultsStore';
import {
  Abandoned,
  createGame,
  GAME_KEY,
  GAME_VERSION,
  seededRng,
  type CardGame,
} from '@/src/utils/cardgame/engine';
import { outcomeOf } from '@/src/utils/minigames/types';

const CONFIRM_KEY = 'cardgame.confirmPlays';

/**
 * Queue a finished game for the player's record. Only games that reach a
 * final score get here -- abandoned ones reject with Abandoned instead and
 * are never recorded.
 */
function recordFinished(g: CardGame, seed: number, startedAt: number) {
  const me = g.state.players.find((p) => p.human);
  const opp = g.state.players.find((p) => !p.human);
  if (!me || !opp) return;
  const now = Date.now();
  void useMiniGameResultsStore.getState().record({
    id: Crypto.randomUUID(),
    gameKey: GAME_KEY,
    outcome: outcomeOf(me.score, opp.score),
    score: me.score,
    opponentScore: opp.score,
    durationSeconds: Math.round((now - startedAt) / 1000),
    gameVersion: GAME_VERSION,
    seed: String(seed),
    playedUtc: new Date(now).toISOString(),
  });
}

/**
 * Owns one card game at a time. The engine mutates its state in place and
 * calls onChange; this hook turns that into re-renders. Leaving the screen
 * abandons the game so the CPU's turn loop stops. A finished game is
 * recorded; the seed makes it replayable.
 */
export function useCardGame() {
  const [game, setGame] = useState<CardGame | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [, bump] = useReducer((n: number) => n + 1, 0);
  const gameRef = useRef<CardGame | null>(null);

  const start = useCallback(() => {
    gameRef.current?.abandon();
    setError(null);
    const seed = Math.floor(Math.random() * 2 ** 32);
    const startedAt = Date.now();
    const g = createGame({ rng: seededRng(seed), onChange: bump });
    gameRef.current = g;
    setGame(g);
    g.run()
      .then(() => {
        recordFinished(g, seed, startedAt);
        bump();
      })
      .catch((e: unknown) => {
        if (e instanceof Abandoned || gameRef.current !== g) return;
        Sentry.captureException(e);
        setError(e instanceof Error ? e.message : String(e));
      });
  }, []);

  useEffect(() => () => gameRef.current?.abandon(), []);

  return { game, error, start };
}

/**
 * "Confirm plays": off by default -- one tap plays a playable card (owner
 * call, 2026-10-20). On, a tap selects and a second tap plays, for players
 * still learning the deck. Saved per device.
 */
export function useConfirmPlays() {
  const [confirm, setConfirm] = useState(false);
  useEffect(() => {
    AsyncStorage.getItem(CONFIRM_KEY)
      .then((v) => setConfirm(v === '1'))
      .catch(() => {});
  }, []);
  const update = useCallback((on: boolean) => {
    setConfirm(on);
    AsyncStorage.setItem(CONFIRM_KEY, on ? '1' : '0').catch(() => {});
  }, []);
  return [confirm, update] as const;
}
