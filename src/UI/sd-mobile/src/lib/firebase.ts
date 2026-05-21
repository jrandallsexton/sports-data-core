import { initializeApp, getApps } from 'firebase/app';
import {
  getAuth,
  initializeAuth,
  // @ts-expect-error — getReactNativePersistence is in firebase/auth's RN
  // runtime build (resolved by Metro via the "react-native" condition) but
  // the umbrella package's TypeScript exports map only exposes the browser
  // surface, so TS can't see it. Known Firebase issue; safe at runtime.
  getReactNativePersistence,
  type Auth,
} from 'firebase/auth';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { Platform } from 'react-native';

/**
 * Firebase config is loaded from EXPO_PUBLIC_ environment variables.
 * Create a `.env.local` at the project root (gitignored) with these values.
 */
const firebaseConfig = {
  apiKey: process.env.EXPO_PUBLIC_FIREBASE_API_KEY,
  authDomain: process.env.EXPO_PUBLIC_FIREBASE_AUTH_DOMAIN,
  projectId: process.env.EXPO_PUBLIC_FIREBASE_PROJECT_ID,
  storageBucket: process.env.EXPO_PUBLIC_FIREBASE_STORAGE_BUCKET,
  messagingSenderId: process.env.EXPO_PUBLIC_FIREBASE_MESSAGING_SENDER_ID,
  appId: process.env.EXPO_PUBLIC_FIREBASE_APP_ID,
};

// Guard against double-initialization in Expo's Fast Refresh.
const app = getApps().length === 0 ? initializeApp(firebaseConfig) : getApps()[0];

// initializeAuth wires AsyncStorage so the user's session survives cold
// launch (iOS kills backgrounded apps under memory pressure — without
// persistence the user re-signs-in every cold open).
//
// Platform split:
//   - iOS / Android: initializeAuth with getReactNativePersistence(AsyncStorage)
//   - Web: getAuth() — Firebase defaults to browserLocalPersistence on web,
//     which gives the same "session survives reload" via localStorage.
//     getReactNativePersistence is undefined on the browser build of
//     firebase/auth, so calling it on web throws TypeError (and breaks
//     `eas update`'s --platform=all bundle step during static rendering).
//
// On native: must be called exactly once per app instance. Fast Refresh
// reuses the app instance, so subsequent calls throw
// 'auth/already-initialized' — fall back to getAuth for that case only.
// Any other error means our config is broken (missing keys, AsyncStorage
// unavailable); rethrow so we don't silently degrade back to in-memory
// persistence, which is the bug this file fixes.
let authInstance: Auth;
if (Platform.OS === 'web') {
  authInstance = getAuth(app);
} else {
  try {
    authInstance = initializeAuth(app, {
      persistence: getReactNativePersistence(AsyncStorage),
    });
  } catch (err) {
    const code = (err as { code?: string })?.code;
    if (code === 'auth/already-initialized') {
      authInstance = getAuth(app);
    } else {
      console.error('[firebase] initializeAuth failed unexpectedly', err);
      throw err;
    }
  }
}

export const auth = authInstance;
export { app };
