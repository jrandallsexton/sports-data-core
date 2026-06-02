import { Platform } from 'react-native';
import * as Notifications from 'expo-notifications';
import messaging from '@react-native-firebase/messaging';

// Push-notification primitives. Wraps expo-notifications (for the
// permission prompt + the OS-level notification display/tap surface)
// and @react-native-firebase/messaging (for the FCM device token).
//
// Why both libraries:
// - expo-notifications gives us the cross-platform permission API,
//   notification handler config, and add*Listener hooks for foreground
//   receipt + tap events.
// - @react-native-firebase/messaging is required to get an FCM token on
//   iOS. expo-notifications.getDevicePushTokenAsync() returns the raw
//   APNs token on iOS, which won't work with our API's Firebase Admin
//   SDK send path — that needs an FCM token. RN-Firebase handles the
//   APNs→FCM registration internally and surfaces the FCM token.
//
// See docs/mobile/push-notifications.md for the full pipeline shape.

export type PermissionStatus = 'granted' | 'denied' | 'undetermined';

export interface FcmTokenResult {
  token: string | null;
  permissionStatus: PermissionStatus;
  error: string | null;
}

/**
 * Request notification permission from the OS. iOS shows a prompt
 * (one-shot — if the user denies, they have to flip the toggle in
 * Settings to re-enable). Android grants automatically on older API
 * levels; API 33+ also prompts. Safe to call repeatedly — returns
 * the current status without re-prompting once a decision is made.
 */
export async function requestNotificationPermission(): Promise<PermissionStatus> {
  const { status: existing } = await Notifications.getPermissionsAsync();
  if (existing === 'granted') return 'granted';
  if (existing === 'denied') return 'denied';

  const { status: next } = await Notifications.requestPermissionsAsync({
    ios: {
      allowAlert: true,
      allowBadge: true,
      allowSound: true,
    },
  });
  if (next === 'granted') return 'granted';
  if (next === 'denied') return 'denied';
  return 'undetermined';
}

/**
 * Returns the FCM device token for this device, requesting permission
 * first if needed. Returns null when permission is denied or token
 * retrieval fails — caller renders the error/permission status from
 * the result.
 *
 * iOS-specific: on iOS the underlying flow is APNs → Firebase → FCM
 * token. RN-Firebase handles the APNs registration internally; we just
 * need the user to grant permission first.
 */
export async function getFcmToken(): Promise<FcmTokenResult> {
  try {
    const permissionStatus = await requestNotificationPermission();
    if (permissionStatus !== 'granted') {
      return { token: null, permissionStatus, error: null };
    }

    // On iOS, ensure the device is registered for remote messages
    // before requesting the FCM token. Idempotent — safe to call on
    // every retrieval. Android registers automatically on app start.
    if (Platform.OS === 'ios') {
      await messaging().registerDeviceForRemoteMessages();
    }

    const token = await messaging().getToken();
    return {
      token: token || null,
      permissionStatus,
      error: token ? null : 'FCM returned empty token',
    };
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    return { token: null, permissionStatus: 'undetermined', error: message };
  }
}
