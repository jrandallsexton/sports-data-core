import AsyncStorage from '@react-native-async-storage/async-storage';
import * as Crypto from 'expo-crypto';

/**
 * Stable per-install device identifier. Minted once (a v4 UUID) and persisted,
 * so it survives FCM token rotation and account switches — unlike the FCM token
 * (rotates) or the signed-in user (changes on account switch).
 *
 * The backend keys a device's push registration on this id, giving each device
 * exactly one current owner: registering under a new account reassigns the
 * device instead of leaving a second row that cross-delivers to it. See
 * docs/mobile/device-identity-and-push-ownership.md.
 */
const STORAGE_KEY = 'device-installation-id';

let cached: string | null = null;
// Single-flight guard. Without it, two concurrent first-time callers (e.g. the
// initial sign-in registration and an onTokenRefresh firing together on a fresh
// install) could both miss the cache + AsyncStorage and mint different UUIDs,
// double-registering the device. All concurrent callers await the same promise.
let inFlight: Promise<string> | null = null;

export function getOrCreateInstallationId(): Promise<string> {
  if (cached) return Promise.resolve(cached);

  if (!inFlight) {
    inFlight = (async () => {
      try {
        const existing = await AsyncStorage.getItem(STORAGE_KEY);
        const id = existing ?? Crypto.randomUUID();
        if (!existing) {
          await AsyncStorage.setItem(STORAGE_KEY, id);
        }
        cached = id;
        return id;
      } finally {
        // Reset so a failed attempt can be retried; success is served from cache.
        inFlight = null;
      }
    })();
  }

  return inFlight;
}
