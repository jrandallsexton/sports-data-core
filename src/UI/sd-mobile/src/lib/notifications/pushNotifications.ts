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
  // Declared outside the try so the catch can return the actual
  // resolved permission state — without this, a failure in
  // registerDeviceForRemoteMessages / getToken (which only run AFTER
  // permission resolves) would mask a granted permission as
  // "undetermined" in the UI.
  let permissionStatus: PermissionStatus = 'undetermined';
  try {
    permissionStatus = await requestNotificationPermission();
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
    return { token: null, permissionStatus, error: message };
  }
}

/**
 * Like {@link getFcmToken}, but NEVER prompts. Returns the FCM token only
 * when notification permission has ALREADY been granted; returns null
 * otherwise (denied / undetermined / web / error).
 *
 * Used for silent automatic device registration at sign-in so we don't fire
 * an unsolicited iOS permission prompt on launch. Permission is requested
 * explicitly elsewhere (the admin push-token screen today; a priming flow in
 * the future), and once granted this picks the token up on the next sign-in.
 */
export async function getFcmTokenIfGranted(): Promise<string | null> {
  if (Platform.OS === 'web') return null;
  try {
    const { status } = await Notifications.getPermissionsAsync();
    if (status !== 'granted') return null;

    if (Platform.OS === 'ios') {
      await messaging().registerDeviceForRemoteMessages();
    }

    const token = await messaging().getToken();
    return token || null;
  } catch {
    return null;
  }
}
