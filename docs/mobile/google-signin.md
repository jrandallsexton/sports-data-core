# Mobile Google Sign-In — Scope

**Status:** Scoping
**Date:** 2026-06-03
**Driver:** Mobile currently supports only email/password auth. Web supports Google sign-in (recently migrated). This scopes adding Google sign-in to the mobile sign-in flow as the first third-party provider; Apple Sign-In is the required follow-up (see [§Apple Sign-In is not optional](#apple-sign-in-is-not-optional-for-app-store-submission) below).

## Current state (post-exploration)

- **Auth library:** Firebase JS SDK (`firebase/auth`), not `@react-native-firebase/auth`. Confirmed in `src/lib/firebase.ts`. JS SDK is wired with `getReactNativePersistence(AsyncStorage)` for native session survival across cold launches.
- **Sign-in flow:** `app/(auth)/sign-in.tsx` is the only auth screen. Email/password via `signInWithEmailAndPassword(auth, ...)`. No sign-up screen on mobile — users currently sign up on web and sign in on mobile.
- **Auth state plumbing:** `src/hooks/useAuth.ts` wraps `onAuthStateChanged` → Zustand store. Adding a new sign-in provider plugs into the same listener path; no changes to the post-sign-in flow.
- **Existing dev-build constraint:** Mobile already requires an EAS dev build (not Expo Go) because of `@react-native-firebase/messaging`. Google sign-in's preferred library has the same constraint, so this isn't a new tradeoff.

## Library choice

Two viable paths to get a Google ID token (which we then exchange for a Firebase credential via `signInWithCredential(auth, GoogleAuthProvider.credential(idToken))`):

| Library | Pros | Cons | Recommend |
|---|---|---|---|
| `@react-native-google-signin/google-signin` | Native Google sign-in modal (best UX). Account picker matches iOS conventions. Handles silent re-auth. | Requires dev build (we already have one). Native modules → adds build time. | **Yes, use this.** |
| `expo-auth-session` + `AuthSession.useAuthRequest` | Pure JS, works in Expo Go. No native module overhead. | Opens an in-app browser for the auth flow (less polished). UX feels web-like, not native. | No. |
| Web client only (route through web) | Reuses existing web auth. | Forces users to leave the app for sign-in. Terrible UX. | No. |

Picking `@react-native-google-signin/google-signin` (henceforth just "GoogleSignin").

## Apple Sign-In is not optional (for App Store submission)

**App Store Review Guideline 4.8** requires that any iOS app offering third-party sign-in (Google, Facebook, etc.) also offer Sign-In with Apple. Shipping Google-only on iOS will fail App Store review.

**Implication for sequencing:**

- If the goal is to validate the implementation pattern internally before going public, Google-first is fine.
- If the goal is to ship to TestFlight external testers or the App Store, Apple Sign-In has to be done in the same release.

Apple Sign-In on Expo uses `expo-apple-authentication`. The Firebase exchange is parallel to Google's: `OAuthProvider('apple.com').credential({...})` → `signInWithCredential(auth, credential)`. Roughly the same surface area as Google, plus an Apple Developer Console capability flip.

**Recommendation:** treat Google + Apple as one work-stream rather than separate releases. The shared infrastructure (sign-in screen layout, button styling, post-sign-in flow, error handling) is built once and applies to both.

This doc focuses on Google as scoped; Apple gets a parallel section at the bottom calling out only the deltas.

## Firebase prerequisites (already done)

The migration earlier today already handled most of the Firebase-side setup:

- ✅ Google sign-in provider enabled on the `sportdeets` project.
- ✅ iOS app registered in Firebase with bundle ID `com.sportdeets.mobile`.
- ✅ `GoogleService-Info.plist` committed at `src/UI/sd-mobile/GoogleService-Info.plist`.
- ✅ OAuth consent screen App Name set to "sportDeets".
- ✅ Authorized domains include `sportdeets.com` / `www.sportdeets.com`.

**What's still needed in Firebase Console:**

- [ ] Verify the `GoogleService-Info.plist` in the repo contains a `REVERSED_CLIENT_ID` key — this is the iOS OAuth client identifier used for the URL scheme. If it's not there, the iOS app registration in Firebase needs Google sign-in enabled (Project Settings → Your apps → iOS app → check "OAuth"). Firebase generates the iOS OAuth client automatically when this is enabled.
- [ ] Note the **Web client ID** (Firebase Console → Project Settings → General → Web SDK config → there's a "Web client ID" field, also visible in GCP Console → APIs & Services → Credentials). This goes into `GoogleSignin.configure({ webClientId })`. The Web client ID is what's used to obtain the Firebase-compatible ID token regardless of platform.

## Account-linking strategy

If a user previously signed up with email/password using `randall@example.com` and then taps "Continue with Google" with the same email, Firebase's default behavior depends on the **"One account per email address"** setting:

- **Enabled** (recommended): the user gets `auth/account-exists-with-different-credential` and we present a sign-in-and-link flow. Same email = same UID, accounts merge.
- **Disabled** (default): Firebase creates a second account with a different UID. We'd have two separate users with the same email in our Postgres `User` table. Bad.

**Action item:** verify in Firebase Console → Authentication → Settings → "One account per email address" is enabled. If not, enable it before shipping.

If we hit the `account-exists-with-different-credential` error, the conventional UX is:

1. Show the user a message: "An account with this email already exists. Please sign in with your password to link Google."
2. Prompt for password → sign in with email/password.
3. Link the Google credential: `linkWithCredential(currentUser, googleCredential)`.
4. Future sign-ins via either provider land on the same UID.

For an MVP, we can defer the link flow and just surface "Please sign in with your existing password" without auto-linking. Decision worth making early.

## Code changes

### Dependencies

- `npx expo install @react-native-google-signin/google-signin`

(For Apple Sign-In follow-up: `npx expo install expo-apple-authentication`.)

### `app.json` plugin additions

```json
"plugins": [
  ...
  [
    "@react-native-google-signin/google-signin",
    {
      "iosUrlScheme": "<from GoogleService-Info.plist REVERSED_CLIENT_ID>"
    }
  ]
]
```

The plugin auto-injects the iOS URL scheme entry into the built IPA's Info.plist so Google's auth callback can return to the app.

### New file: `src/lib/googleSignIn.ts`

Wraps the GoogleSignin SDK + Firebase exchange. Shape:

```ts
import { GoogleSignin, statusCodes } from '@react-native-google-signin/google-signin';
import { GoogleAuthProvider, signInWithCredential } from 'firebase/auth';
import { auth } from './firebase';

const WEB_CLIENT_ID = process.env.EXPO_PUBLIC_FIREBASE_WEB_CLIENT_ID;

let configured = false;
function configureOnce() {
  if (configured) return;
  GoogleSignin.configure({ webClientId: WEB_CLIENT_ID });
  configured = true;
}

export async function signInWithGoogle() {
  configureOnce();
  await GoogleSignin.hasPlayServices({ showPlayServicesUpdateDialog: false });
  const { idToken } = await GoogleSignin.signIn();
  if (!idToken) throw new Error('Google sign-in returned no ID token');
  const credential = GoogleAuthProvider.credential(idToken);
  return signInWithCredential(auth, credential);
}

export async function signOutGoogle() {
  configureOnce();
  await GoogleSignin.signOut();
}
```

Notes:
- `configureOnce()` guard prevents double-config under Fast Refresh.
- Pair `signOutGoogle()` with Firebase sign-out in the existing logout flow so the next "Continue with Google" tap doesn't silently re-auth the previous account.
- Error mapping for the `signInWithGoogle` flow needs to handle: `statusCodes.SIGN_IN_CANCELLED` (user cancelled — silent), `statusCodes.IN_PROGRESS` (treat as no-op), `statusCodes.PLAY_SERVICES_NOT_AVAILABLE` (Android-specific; surface as "Google Play services unavailable"). On the Firebase side: `auth/account-exists-with-different-credential` is the account-linking path described above.

### Updated: `app/(auth)/sign-in.tsx`

Add a "Continue with Google" button above or below the email/password form, with an "or" divider. Tap → calls `signInWithGoogle()` → success → existing AuthGuard handles the redirect. Error mapping surfaces in the same error banner used for email/password failures.

### Updated: sign-out flow

Wherever the sign-out button lives (likely `profile.tsx` based on earlier exploration), wrap the existing `signOut(auth)` call to also call `signOutGoogle()` first. Failing to do this means the next sign-in attempt silently re-auths the previously-signed-in Google account without showing the account picker — confusing UX for shared-device cases.

### Env var

Add `EXPO_PUBLIC_FIREBASE_WEB_CLIENT_ID` to whatever env-loading path mobile uses (likely `eas.json` env section + a `.env.local` for local dev). The value is the Web client ID noted above. Public-readable in the bundle (like the other `EXPO_PUBLIC_FIREBASE_*` values), so committing it as a plugin arg is also fine if env-var plumbing is heavier.

## iOS-side configuration verification

After the plugin is added and the next EAS dev build runs:

1. The IPA's Info.plist should contain a `CFBundleURLTypes` entry with the `REVERSED_CLIENT_ID` URL scheme.
2. Google's OAuth flow returns to the app via that URL scheme — without it, the auth callback dead-ends.
3. The `@react-native-google-signin/google-signin` Expo plugin handles this declaratively from app.json — no manual Info.plist edit needed.

Easy verification post-build: decode the IPA's Info.plist and grep for `com.googleusercontent.apps.` — if present, the plugin did its job.

## Testing plan

- [ ] Fresh install on iOS device → tap "Continue with Google" → Google account picker appears.
- [ ] Pick an account that doesn't exist in Firebase yet → new Firebase user created → AuthGuard redirects to `(tabs)`.
- [ ] Pick an account that already exists (created via email/password earlier) → if "One account per email address" is on, gets the link-flow error; verify error is surfaced clearly.
- [ ] Sign out → "Continue with Google" again → account picker should re-appear (proves `signOutGoogle()` was called on logout).
- [ ] Background the app mid-sign-in → return → flow doesn't deadlock.
- [ ] No network → tap "Continue with Google" → clear error message instead of silent failure.
- [ ] Existing email/password flow still works post-change (regression check).

## Effort estimate

| Work | Hours |
|---|---|
| Add dependency + plugin config | 0.5 |
| `googleSignIn.ts` module | 1 |
| Sign-in screen UI (button, divider, layout) | 1.5 |
| Sign-out plumbing | 0.5 |
| Account-linking decision + error mapping | 1-2 (depending on whether we ship the auto-link flow now) |
| EAS dev build + on-device validation | 1 (plus build wait) |
| Apple Sign-In parity (separate phase or same release) | +4-6 |

**Google-only MVP:** ~5-6 hours of active work + EAS build cycles.
**Google + Apple together (recommended for any external distribution):** ~10-12 hours.

## Open questions to settle before kicking off

- **Account-linking auto-flow** (Google → existing email account) — ship the auto-link UX in v1 or punt to a follow-up?
- **Sign-up screen on mobile** — currently doesn't exist. Does Google sign-in make the email/password sign-up screen unnecessary (since Google handles new-user creation transparently)? Or do we want both?
- **Apple Sign-In timing** — same release as Google (required for App Store), or are we explicitly staying internal-only on TestFlight for now?
- **EAS env var** — add `EXPO_PUBLIC_FIREBASE_WEB_CLIENT_ID` to `eas.json` env section per profile, or commit it as a plugin arg in `app.json`? Both work; team preference.

## Related

- `docs/firebase-project-migration.md` — parent migration that established the `sportdeets` Firebase project Google sign-in is going against.
- `docs/firebase-custom-auth-domain.md` — orthogonal follow-up cleanup for the OAuth dialog domain branding.
