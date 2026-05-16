# Firebase Config in EAS Builds

**Status (2026-05-16)**: Fix applied. EAS `preview` and `production` profiles now embed the dev Firebase project's config so TestFlight builds boot past `initializeApp()`. Migration to a real prod Firebase project (`sportdeets`, separate from `sportdeets-dev`) is deferred until pre-season — see [Deferred Work](#deferred-work).

---

## The Problem

The first TestFlight build (build 2) crashed on launch with `EXC_BAD_ACCESS` in the Hermes JS engine, triggered by a TurboModule converting an NSException. Root cause: Firebase initialized with `apiKey: undefined` because the production build had no Firebase env vars baked in.

## Why Dev Works but Prod Doesn't

| | Dev (Metro on PC) | Prod (EAS Build in cloud) |
|---|---|---|
| How the JS bundle is built | `npx expo start` runs Metro, reads `.env.local` on disk, injects values into `process.env.EXPO_PUBLIC_*` at bundle time | EAS runs in a Mac VM in Expo's cloud, pulls a tarball of the repo. `.env.local` is **gitignored** → EAS never sees it |
| Where env vars come from | `.env.local` | The `env` block of the active profile in `eas.json`, plus EAS dashboard secrets |
| Before the fix | All six `EXPO_PUBLIC_FIREBASE_*` keys set | Only `EXPO_PUBLIC_API_BASE_URL` set; all `EXPO_PUBLIC_FIREBASE_*` → `undefined` |

Same Firebase project, two different config-delivery paths. Production path wasn't configured.

## What We Did

Pasted the existing `.env.local` Firebase values into the `env` block of both `preview` and `production` profiles in `eas.json`:

```json
"env": {
  "EXPO_PUBLIC_API_BASE_URL": "https://api.sportdeets.com",
  "EXPO_PUBLIC_FIREBASE_API_KEY": "AIzaSyBRkQwtEl3jeqoYKBN-hPv8VxTjUycNJgM",
  "EXPO_PUBLIC_FIREBASE_AUTH_DOMAIN": "sportdeets-dev.firebaseapp.com",
  "EXPO_PUBLIC_FIREBASE_PROJECT_ID": "sportdeets-dev",
  "EXPO_PUBLIC_FIREBASE_STORAGE_BUCKET": "sportdeets-dev.appspot.com",
  "EXPO_PUBLIC_FIREBASE_MESSAGING_SENDER_ID": "your-sender-id",
  "EXPO_PUBLIC_FIREBASE_APP_ID": "your-app-id"
}
```

For Firebase Web SDK Auth, only `apiKey` and `authDomain` are actually consulted at runtime. The other four (`projectId`, `storageBucket`, `messagingSenderId`, `appId`) sit unused for the current feature set, so the literal placeholder strings (`your-sender-id`, `your-app-id`) are fine — they'll get replaced when the prod Firebase project comes online.

**On committing keys to `eas.json`**: Firebase Web config values are designed to be public. They identify the project, not authorize anything — auth is gated by Firestore rules + Firebase Auth allow-list. Google explicitly documents pasting these into client code. Committing them is the standard pattern.

## Rebuild + Reinstall

From `src/UI/sd-mobile/`:

```bash
eas build --platform ios --profile production
eas submit --platform ios --latest
```

After Apple finishes processing the upload (~5–10 min), reinstall via TestFlight on the iPhone — TestFlight will offer the new build automatically. Open the app; it should reach the login screen instead of crashing.

If it crashes again, grab the new `.ips` from **Settings → Privacy & Security → Analytics & Improvements → Analytics Data**. The signature should look different from the original Hermes/TurboModule crash, which would confirm Firebase init is no longer the failure point — and that's when Sentry earns its keep.

## Deferred Work

Two related items planned for pre-season:

1. **Register a Web app in the prod Firebase project** (`sportdeets`, distinct from `sportdeets-dev`). This generates real values for the `messagingSenderId` and `appId` placeholders, and gives prod its own analytics/messaging scoping.
2. **Migrate the existing user(s)** from `sportdeets-dev` to the prod project.

When both land:
- New Firebase config values get pasted into `eas.json` `preview` + `production` profiles (replacing the dev values + placeholders)
- `.env.local` keeps the dev values for local development
- The web app (`sd-ui`) needs the same prod-config update

## The General Rule

Every new `EXPO_PUBLIC_*` env var added during dev needs a matching entry in `eas.json` (or an EAS dashboard secret) or it'll silently become `undefined` in production builds. Metro reads `.env*`; EAS doesn't.
