import { useEffect, useRef } from 'react';
import { AppState, Platform } from 'react-native';
import messaging from '@react-native-firebase/messaging';
import AsyncStorage from '@react-native-async-storage/async-storage';

import { useAuth } from './useAuth';
import { registerThisDevice } from '@/src/lib/notifications/registerPushDevice';

// One-time Android permission prompt marker. Android 13+ makes
// POST_NOTIFICATIONS a runtime permission that starts undetermined, and the
// silent registration path never prompts — so without this, an Android install
// receives ZERO pushes until the user finds Settings → Notifications on their
// own (which is exactly how the founder's own device went unregistered).
// Asked once ever, at the first authenticated attempt; a denial is respected
// (no re-nag) and the settings screen's prompt=true action stays the recovery
// path. iOS keeps the fully silent strategy — its one-shot prompt is too
// precious to spend at sign-in.
const ANDROID_PROMPTED_KEY = 'push-permission-prompted';

/**
 * Silently registers this device's FCM token with the API once the user is
 * authenticated and notification permission has already been granted.
 *
 * Design choices:
 * - NO permission prompt. Registration goes through
 *   {@link registerThisDevice} with prompt=false, which only returns a token
 *   when permission is already granted — sign-in never fires an unsolicited
 *   iOS prompt. (The manual settings action uses prompt=true.)
 * - Native-only. RN-Firebase messaging has no web equivalent; the hook no-ops
 *   on web.
 * - Resilient. The one-shot sign-in attempt used to be the ONLY automatic
 *   trigger, so a device whose permission or APNs token wasn't ready at that
 *   instant (or whose POST failed) silently never registered. We now re-attempt
 *   on every foreground until a registration succeeds this session, and on FCM
 *   token rotation. See docs/mobile/device-registration-resilience.md.
 *
 * Call once from the root layout (native-only).
 */
export function useRegisterPushDevice(): void {
  const { isAuthenticated, user } = useAuth();

  // Whether registration has already succeeded this session. Reset on sign-out
  // / account switch. Gates the foreground retry loop so we stop once done.
  const registeredRef = useRef(false);

  useEffect(() => {
    if (Platform.OS === 'web') return;

    if (!isAuthenticated) {
      // Signed out — forget success so the next user's device re-registers.
      registeredRef.current = false;
      return;
    }

    // Fresh start for this sign-in. The effect also re-runs on a direct account
    // switch (user?.uid A -> B with no intermediate sign-out).
    registeredRef.current = false;

    let cancelled = false;
    // Serializes attempts. The Android permission dialog fires an AppState
    // active transition when dismissed, which would re-enter attempt() while
    // the first call is still resolving — without this guard that second
    // entry could double-prompt (the prompted flag isn't written until the
    // first attempt completes).
    let attemptInFlight = false;

    // Gated on not-yet-succeeded. Android's FIRST attempt ever prompts for
    // permission (see ANDROID_PROMPTED_KEY); every other attempt is silent —
    // a cheap permission check short-circuits inside registerThisDevice when
    // permission isn't granted, so foreground retries don't POST-spam while
    // denied.
    const attempt = async () => {
      if (cancelled || registeredRef.current || attemptInFlight) return;
      attemptInFlight = true;
      try {
        let prompt = false;
        if (Platform.OS === 'android') {
          try {
            prompt = (await AsyncStorage.getItem(ANDROID_PROMPTED_KEY)) === null;
          } catch {
            // Storage read failed — stay silent rather than risk re-nagging
            // a user who was already asked.
          }
        }
        const outcome = await registerThisDevice({ prompt });
        if (prompt) {
          // Mark AFTER the dialog resolved so a crash mid-prompt retries next
          // launch. Written regardless of outcome — the user answered; a
          // denial must not be re-nagged (the settings action is the
          // deliberate re-ask path).
          await AsyncStorage.setItem(ANDROID_PROMPTED_KEY, '1').catch(() => undefined);
        }
        if (!cancelled && outcome.ok) registeredRef.current = true;
      } finally {
        attemptInFlight = false;
      }
    };

    // Initial attempt for this sign-in.
    void attempt();

    // Re-attempt on foreground until we've succeeded once. Covers permission
    // granted after launch, an APNs token that wasn't ready at sign-in, and
    // transient POST failures — none of which the one-shot attempt recovered.
    const appStateSub = AppState.addEventListener('change', (state) => {
      if (state === 'active') void attempt();
    });

    // Token rotation must reach the backend even after a prior success, so this
    // re-registers unconditionally (not gated by registeredRef).
    const unsubscribeTokenRefresh = messaging().onTokenRefresh(() => {
      void (async () => {
        const outcome = await registerThisDevice();
        if (!cancelled && outcome.ok) registeredRef.current = true;
      })();
    });

    return () => {
      cancelled = true;
      appStateSub.remove();
      unsubscribeTokenRefresh();
    };
  }, [isAuthenticated, user?.uid]);
}
