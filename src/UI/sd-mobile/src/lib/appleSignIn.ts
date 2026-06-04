import * as AppleAuthentication from 'expo-apple-authentication';
import * as Crypto from 'expo-crypto';
import { OAuthProvider, signInWithCredential } from 'firebase/auth';
import { Platform } from 'react-native';
import { auth } from './firebase';

/**
 * Generates an OIDC nonce pair for the Apple sign-in flow:
 *
 *   - rawNonce: a cryptographically random opaque string. We hand the
 *     unhashed value to Firebase's OAuthProvider.credential.
 *   - hashedNonce: SHA-256 of rawNonce, hex-encoded. We hand THIS value
 *     to Apple via AppleAuthentication.signInAsync. Apple embeds it in
 *     the identity token's `nonce` claim verbatim.
 *
 * Firebase verifies the token's nonce claim by hashing the rawNonce we
 * supply and comparing — protects against identity-token replay attacks
 * by binding the token to a specific sign-in attempt that only this
 * client could have initiated.
 *
 * 32 bytes of randomness is the common recommendation (Firebase's own
 * web SDK docs use 32). Hex encoding gives 64 chars, well within
 * Apple's nonce length limit.
 */
async function generateNoncePair(): Promise<{
  rawNonce: string;
  hashedNonce: string;
}> {
  const randomBytes = await Crypto.getRandomBytesAsync(32);
  const rawNonce = Array.from(randomBytes)
    .map((b) => b.toString(16).padStart(2, '0'))
    .join('');
  const hashedNonce = await Crypto.digestStringAsync(
    Crypto.CryptoDigestAlgorithm.SHA256,
    rawNonce,
  );
  return { rawNonce, hashedNonce };
}

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
  // OIDC nonce flow: generate a raw nonce + its SHA-256 hash, hand the
  // hash to Apple, and the raw value to Firebase. Firebase rehashes the
  // raw value and compares against the token's nonce claim. Mismatched
  // or missing nonce = identity token rejected.
  const { rawNonce, hashedNonce } = await generateNoncePair();

  let credential: AppleAuthentication.AppleAuthenticationCredential;
  try {
    credential = await AppleAuthentication.signInAsync({
      requestedScopes: [
        AppleAuthentication.AppleAuthenticationScope.FULL_NAME,
        AppleAuthentication.AppleAuthenticationScope.EMAIL,
      ],
      nonce: hashedNonce,
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
    rawNonce,
  });
  await signInWithCredential(auth, firebaseCredential);
}
