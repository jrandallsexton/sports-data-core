import { useEffect, useRef } from 'react';
import { Platform } from 'react-native';
import messaging from '@react-native-firebase/messaging';

import { useAuth } from './useAuth';
import { getFcmTokenIfGranted } from '@/src/lib/notifications/pushNotifications';
import { devicesApi } from '@/src/services/api/devicesApi';

/**
 * Silently registers this device's FCM token with the API once the user is
 * authenticated and notification permission has already been granted. Replaces
 * the manual copy-paste step on the admin push-token screen.
 *
 * Design choices:
 * - NO permission prompt. We use {@link getFcmTokenIfGranted}, which only
 *   returns a token when permission is already granted, so sign-in never fires
 *   an unsolicited iOS prompt. Requesting permission (with context) is a
 *   separate product flow.
 * - Native-only. expo-notifications / RN-Firebase messaging have no web
 *   equivalent; the hook no-ops on web.
 * - Idempotent + cheap. The backend upserts on (UserId, FcmToken), so repeat
 *   calls are harmless; the per-session token set just avoids needless POSTs.
 * - Token rotation. Subscribes to onTokenRefresh so a mid-session FCM token
 *   rotation re-registers without waiting for the next launch.
 *
 * Call once from the root layout (native-only).
 */
export function useRegisterPushDevice(): void {
  const { isAuthenticated, user } = useAuth();

  // Tokens already registered this app session. Backend idempotency makes this
  // a chattiness guard, not a correctness one.
  const registeredTokensRef = useRef<Set<string>>(new Set());

  useEffect(() => {
    if (Platform.OS === 'web') return;

    if (!isAuthenticated) {
      // Signed out — forget what we registered so the next user's device
      // re-registers cleanly.
      registeredTokensRef.current.clear();
      return;
    }

    let cancelled = false;
    const platform = Platform.OS === 'ios' ? 'ios' : 'android';

    const register = async (token: string | null) => {
      if (cancelled || !token) return;
      if (registeredTokensRef.current.has(token)) return;

      try {
        await devicesApi.registerDevice({ fcmToken: token, platform });
        registeredTokensRef.current.add(token);
      } catch (err) {
        // Non-fatal: reminders just won't reach this device until a later
        // retry (next launch / token refresh). Never surface to the user.
        const message = err instanceof Error ? err.message : String(err);
        console.log('[push] device registration failed', { message });
      }
    };

    // Initial registration for this sign-in.
    void (async () => {
      const token = await getFcmTokenIfGranted();
      await register(token);
    })();

    // Re-register if FCM rotates the token while signed in.
    const unsubscribe = messaging().onTokenRefresh((token) => {
      void register(token);
    });

    return () => {
      cancelled = true;
      unsubscribe();
    };
  }, [isAuthenticated, user?.uid]);
}
