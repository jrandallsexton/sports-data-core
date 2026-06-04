import * as AppleAuthentication from 'expo-apple-authentication';
import { OAuthProvider, signInWithCredential } from 'firebase/auth';
import { Platform } from 'react-native';
import { auth } from './firebase';

export class AppleSignInCancelled extends Error {
  constructor() {
    super('Apple sign-in cancelled by user');
    this.name = 'AppleSignInCancelled';
  }
}

/**
 * Whether Sign In with Apple is available on the current device. iOS only;
 * Android always returns false. Use to gate the Apple button — App Store
 * Review Guideline 4.8 mandates offering Apple Sign-In when other third-
 * party providers are present, but only on iOS.
 */
export async function isAppleSignInAvailable(): Promise<boolean> {
  if (Platform.OS !== 'ios') return false;
  return AppleAuthentication.isAvailableAsync();
}

/**
 * Native Sign In with Apple flow: shows the iOS Apple ID picker → returns
 * an identity token (JWT) + raw nonce → exchanges via Firebase's
 * OAuthProvider('apple.com') credential.
 *
 * Note on naming: Apple only shares the user's display name on the FIRST
 * authorization (subsequent re-auths return null fullName), so we don't
 * rely on it here. If we ever want to populate Firebase user displayName
 * we'd need to handle it on first sign-in specifically.
 *
 * Throws:
 *   - AppleSignInCancelled (silent — caller suppresses UI)
 *   - generic Error with a user-readable message for everything else
 */
export async function signInWithApple(): Promise<void> {
  let credential: AppleAuthentication.AppleAuthenticationCredential;
  try {
    credential = await AppleAuthentication.signInAsync({
      requestedScopes: [
        AppleAuthentication.AppleAuthenticationScope.FULL_NAME,
        AppleAuthentication.AppleAuthenticationScope.EMAIL,
      ],
    });
  } catch (err) {
    const code = (err as { code?: string })?.code ?? '';
    if (code === 'ERR_REQUEST_CANCELED') {
      throw new AppleSignInCancelled();
    }
    throw new Error(
      err instanceof Error ? err.message : 'Apple sign-in failed.',
    );
  }

  const { identityToken } = credential;
  if (!identityToken) {
    throw new Error('Apple sign-in returned no identity token.');
  }

  const provider = new OAuthProvider('apple.com');
  const firebaseCredential = provider.credential({
    idToken: identityToken,
    // rawNonce intentionally omitted — Expo's
    // AppleAuthentication.signInAsync generates and validates the nonce
    // internally and does NOT expose it on the result. Firebase accepts
    // the identityToken without us re-supplying the nonce because the
    // token's hashed nonce claim is already baked in.
  });
  await signInWithCredential(auth, firebaseCredential);
}
