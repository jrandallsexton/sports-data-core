import {
  GoogleSignin,
  isSuccessResponse,
  isCancelledResponse,
  statusCodes,
} from '@react-native-google-signin/google-signin';
import { GoogleAuthProvider, signInWithCredential } from 'firebase/auth';
import { auth } from './firebase';

// Web OAuth Client ID for the sportdeets Firebase project. Public-readable
// (it ships in the IPA bundle anyway via the plugin's URL scheme config);
// security comes from Firebase Auth rules + the OAuth consent screen, not
// from hiding this value. If we ever stand up a separate prod Firebase
// project, this would be parameterized via EXPO_PUBLIC_FIREBASE_WEB_CLIENT_ID.
const WEB_CLIENT_ID =
  '812654295319-dgb618dhnl9oflea534fjgdlipkgoi5f.apps.googleusercontent.com';

// Configure exactly once. Fast Refresh keeps the module instance alive
// between reloads; calling configure() multiple times is safe per the
// library but the guard avoids the noise.
let configured = false;
function configureOnce() {
  if (configured) return;
  GoogleSignin.configure({ webClientId: WEB_CLIENT_ID });
  configured = true;
}

export class GoogleSignInCancelled extends Error {
  constructor() {
    super('Google sign-in cancelled by user');
    this.name = 'GoogleSignInCancelled';
  }
}

/**
 * Native Google sign-in flow: shows the iOS account picker → returns an
 * ID token → exchanges it for a Firebase credential and signs in. The
 * existing onAuthStateChanged listener in useAuthInit picks up the new
 * session and the AuthGuard handles the navigation.
 *
 * Throws:
 *   - GoogleSignInCancelled (silent — caller suppresses UI)
 *   - generic Error with a user-readable message for everything else
 */
export async function signInWithGoogle(): Promise<void> {
  configureOnce();

  let response: Awaited<ReturnType<typeof GoogleSignin.signIn>>;
  try {
    // hasPlayServices is a no-op on iOS but required to be called first
    // on Android. Cheap to leave unconditional.
    await GoogleSignin.hasPlayServices({ showPlayServicesUpdateDialog: false });
    response = await GoogleSignin.signIn();
  } catch (err) {
    const code = (err as { code?: string })?.code ?? '';
    if (code === statusCodes.IN_PROGRESS) {
      // A previous tap is still resolving — treat as a soft cancel.
      throw new GoogleSignInCancelled();
    }
    if (code === statusCodes.PLAY_SERVICES_NOT_AVAILABLE) {
      throw new Error('Google Play services are unavailable on this device.');
    }
    throw new Error(
      err instanceof Error ? err.message : 'Google sign-in failed.',
    );
  }

  // The current library returns a CancelledResponse rather than throwing
  // when the user backs out of the picker. Handle both shapes explicitly
  // and bail out via GoogleSignInCancelled so the caller can suppress UI.
  if (isCancelledResponse(response)) {
    throw new GoogleSignInCancelled();
  }
  if (!isSuccessResponse(response)) {
    throw new Error('Google sign-in did not complete.');
  }

  const { idToken } = response.data;
  if (!idToken) {
    throw new Error('Google sign-in returned no ID token.');
  }

  const credential = GoogleAuthProvider.credential(idToken);
  await signInWithCredential(auth, credential);
}

/**
 * Companion to signInWithGoogle. Must be called alongside Firebase's
 * signOut on logout — otherwise the next "Continue with Google" tap
 * silently re-auths the last-used Google account without showing the
 * account picker, which is confusing on shared devices.
 */
export async function signOutGoogle(): Promise<void> {
  configureOnce();
  try {
    await GoogleSignin.signOut();
  } catch {
    // Best-effort; the user is signing out anyway.
  }
}
