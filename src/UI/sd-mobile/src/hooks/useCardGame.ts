import { useCallback, useEffect, useReducer, useRef, useState } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import * as Sentry from '@sentry/react-native';
import { Abandoned, createGame, type CardGame } from '@/src/utils/cardgame/engine';

const CONFIRM_KEY = 'cardgame.confirmPlays';

/**
 * Owns one card game at a time. The engine mutates its state in place and
 * calls onChange; this hook turns that into re-renders. Leaving the screen
 * abandons the game so the CPU's turn loop stops.
 */
export function useCardGame() {
  const [game, setGame] = useState<CardGame | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [, bump] = useReducer((n: number) => n + 1, 0);
  const gameRef = useRef<CardGame | null>(null);

  const start = useCallback(() => {
    gameRef.current?.abandon();
    setError(null);
    const g = createGame({ onChange: bump });
    gameRef.current = g;
    setGame(g);
    g.run()
      .then(bump)
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
