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

export async function getOrCreateInstallationId(): Promise<string> {
  if (cached) return cached;

  const existing = await AsyncStorage.getItem(STORAGE_KEY);
  if (existing) {
    cached = existing;
    return existing;
  }

  const id = Crypto.randomUUID();
  await AsyncStorage.setItem(STORAGE_KEY, id);
  cached = id;
  return id;
}
