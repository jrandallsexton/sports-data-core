import { useEffect, useRef } from 'react';
import { AppState, type AppStateStatus } from 'react-native';
import * as signalR from '@microsoft/signalr';
import { getAuth } from 'firebase/auth';

import { createSignalRConnection } from '@/src/services/signalR/connection';
import { useContestUpdatesStore } from '@/src/stores/contestUpdatesStore';

const SIGNALR_URL =
  process.env.EXPO_PUBLIC_SIGNALR_URL ??
  process.env.EXPO_PUBLIC_API_BASE_URL ??
  'https://api.sportdeets.com';

/**
 * Owns the SignalR connection lifecycle. Auth-gated by convention — the
 * caller is responsible for only mounting this hook once a Firebase user
 * exists (see `SignalRGate`). The hook still defends against a transient
 * null token during Firebase refresh by failing the negotiate gracefully.
 *
 * Mobile lifecycle additions vs the web hook: iOS aggressively kills idle
 * sockets when the app backgrounds, so an idle reconnect loop would burn
 * battery. AppState listener stops the connection on background and
 * starts it again on foreground.
 *
 * Event handlers dispatch directly into `contestUpdatesStore` rather than
 * accepting callback props — keeps the wire-up simple and matches the way
 * the mobile codebase already wires global state.
 */
export function useSignalRClient(): void {
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  // Zustand returns stable function references for the lifetime of the
  // store, so these selector subscriptions don't cause the effect below
  // to tear down + reconnect on every render.
  const handleStatusUpdate = useContestUpdatesStore((s) => s.handleStatusUpdate);
  const handleContestFinalized = useContestUpdatesStore(
    (s) => s.handleContestFinalized,
  );
  const handleFootballPlayCompleted = useContestUpdatesStore(
    (s) => s.handleFootballPlayCompleted,
  );
  const handleBaseballPlayCompleted = useContestUpdatesStore(
    (s) => s.handleBaseballPlayCompleted,
  );

  useEffect(() => {
    const connection = createSignalRConnection({
      url: SIGNALR_URL,
      accessTokenFactory: async () => {
        const user = getAuth().currentUser;
        if (!user) return '';
        try {
          return await user.getIdToken();
        } catch (err) {
          // Don't console.error — Expo dev menu pops on error-level logs
          // and a transient token-fetch miss is recoverable. Empty string
          // signals "no token", server 401s, auto-reconnect retries.
          console.warn('[useSignalRClient] token fetch failed', err);
          return '';
        }
      },
    });

    connection.on('ContestStatusChanged', handleStatusUpdate);
    // ContestFinalized fires AFTER ContestStatusChanged(STATUS_FINAL) and
    // carries the enriched result fields (winner, spread winner, over/
    // under, final scores) that the status event can't. Without this
    // handler the matchup card would sit on raw STATUS_FINAL (no cover
    // line, no SU checkmark) until the user pulls to refresh.
    connection.on('ContestFinalized', handleContestFinalized);
    connection.on('FootballPlayCompleted', handleFootballPlayCompleted);
    connection.on('BaseballPlayCompleted', handleBaseballPlayCompleted);

    connection
      .start()
      .then(() => console.log('[SignalR] connected'))
      .catch((err) => console.warn('[SignalR] connection failed', err));

    connectionRef.current = connection;

    const appStateSub = AppState.addEventListener(
      'change',
      (next: AppStateStatus) => {
        const conn = connectionRef.current;
        if (!conn) return;

        if (next === 'background' || next === 'inactive') {
          if (conn.state === signalR.HubConnectionState.Connected) {
            // Idempotent — swallow rejection in case stop() races with
            // an in-flight reconnect attempt.
            conn.stop().catch(() => undefined);
          }
        } else if (next === 'active') {
          if (conn.state === signalR.HubConnectionState.Disconnected) {
            conn
              .start()
              .then(() => console.log('[SignalR] reconnected on foreground'))
              .catch((err) =>
                console.warn('[SignalR] foreground reconnect failed', err),
              );
          }
        }
      },
    );

    return () => {
      appStateSub.remove();
      connection.stop().catch(() => undefined);
      connectionRef.current = null;
    };
  }, [
    handleStatusUpdate,
    handleContestFinalized,
    handleFootballPlayCompleted,
    handleBaseballPlayCompleted,
  ]);
}
