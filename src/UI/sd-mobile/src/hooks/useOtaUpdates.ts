import { useEffect, useRef } from 'react';
import { AppState, type AppStateStatus } from 'react-native';
import * as Updates from 'expo-updates';

/**
 * Applies EAS OTA updates while the app is *running*, so users who never
 * force-quit still get them.
 *
 * The default expo-updates behavior only checks on a cold start and applies on
 * the *next* launch — most users keep the app warm for days, so they'd run stale
 * JS indefinitely. This hook closes that gap with the least-intrusive policy:
 *
 *   - On every foreground (and once at launch), check for and silently download
 *     an available update in the background.
 *   - Only reload (which restarts the JS bundle and drops in-memory nav/scroll
 *     state) when the user returns after being away at least
 *     {@link APPLY_AFTER_BACKGROUND_MS} — a genuine "left and came back", which
 *     already feels like a fresh open, so the reload is invisible. A quick
 *     app-switch never interrupts an active session (e.g. mid-pick).
 *
 * Inert in dev / Expo Go (`Updates.isEnabled` is false there). Mirrors the
 * AppState idiom in useSignalRClient. See docs/mobile/ota-updates.md.
 */

// A downloaded update is applied on a foreground only after the app has been
// backgrounded at least this long. Tunable — shorter propagates faster but
// risks reloading after a brief glance away.
const APPLY_AFTER_BACKGROUND_MS = 3 * 60 * 1000;

// Don't hit the update server more than once per this window.
const CHECK_THROTTLE_MS = 60 * 1000;

export function useOtaUpdates() {
  const backgroundedAt = useRef<number | null>(null);
  const updateReady = useRef(false);
  const lastCheckAt = useRef(0);
  const checking = useRef(false);

  useEffect(() => {
    // expo-updates is disabled in dev / Expo Go — nothing to do.
    if (!Updates.isEnabled) return;

    const fetchIfAvailable = async () => {
      if (checking.current) return;
      const now = Date.now();
      if (now - lastCheckAt.current < CHECK_THROTTLE_MS) return;
      checking.current = true;
      lastCheckAt.current = now;
      try {
        const result = await Updates.checkForUpdateAsync();
        if (result.isAvailable) {
          await Updates.fetchUpdateAsync();
          updateReady.current = true;
        }
      } catch (err) {
        // Offline / server hiccup — retry on the next foreground.
        console.log('[OTA] check failed (will retry next foreground)', err);
      } finally {
        checking.current = false;
      }
    };

    const applyIfReady = async () => {
      if (!updateReady.current) return;
      try {
        // Restarts the JS with the new bundle; does not return.
        await Updates.reloadAsync();
      } catch (err) {
        console.warn('[OTA] reload failed', err);
      }
    };

    // Launch pass: fetch a just-published update, but don't reload — the user
    // just opened the app, and the cold start already applied any prior update
    // before JS ran.
    void fetchIfAvailable();

    const sub = AppState.addEventListener('change', (next: AppStateStatus) => {
      if (next === 'background' || next === 'inactive') {
        // Record only the first transition away — iOS fires inactive→background.
        if (backgroundedAt.current === null) backgroundedAt.current = Date.now();
        return;
      }
      if (next === 'active') {
        const awayMs = backgroundedAt.current
          ? Date.now() - backgroundedAt.current
          : 0;
        backgroundedAt.current = null;
        void (async () => {
          await fetchIfAvailable();
          if (awayMs >= APPLY_AFTER_BACKGROUND_MS) {
            await applyIfReady();
          }
        })();
      }
    });

    return () => sub.remove();
  }, []);
}
