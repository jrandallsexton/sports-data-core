import { initializeApp, getApps } from 'firebase/app';
import { initializeAuth, getAuth, getReactNativePersistence } from 'firebase/auth';
import ReactNativeAsyncStorage from '@react-native-async-storage/async-storage';

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
const existingApps = getApps();
const app = existingApps.length === 0 ? initializeApp(firebaseConfig) : existingApps[0];

// initializeAuth (with persistence) must only be called once per app instance.
// On Fast Refresh the app already exists, so fall back to getAuth.
export const auth = existingApps.length === 0
  ? initializeAuth(app, { persistence: getReactNativePersistence(ReactNativeAsyncStorage) })
  : getAuth(app);
export { app };
