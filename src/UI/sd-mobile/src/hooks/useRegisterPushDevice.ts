import { useEffect, useRef } from 'react';
import { AppState, Platform } from 'react-native';
import messaging from '@react-native-firebase/messaging';

import { useAuth } from './useAuth';
import { registerThisDevice } from '@/src/lib/notifications/registerPushDevice';

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

    // Silent attempt, gated on not-yet-succeeded. A cheap permission check
    // short-circuits inside registerThisDevice when permission isn't granted,
    // so foreground retries don't POST-spam while denied.
    const attempt = async () => {
      if (cancelled || registeredRef.current) return;
      const outcome = await registerThisDevice();
      if (!cancelled && outcome.ok) registeredRef.current = true;
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
