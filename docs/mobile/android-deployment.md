# Android deployment via EAS

Companion to `expo-deployment-model.md`, which covers iOS / TestFlight.
This doc focuses on the Android side: getting builds onto a physical
test device, then onto the Google Play Console for friend testers and
eventually production users.

Status: **Android Firebase wiring complete** as of 2026-06-04 (PR #405
landed `google-services.json` and `android.googleServicesFile` in
`app.json`, unblocking EAS Android builds). iOS path is shipped end-
to-end (TestFlight builds + Firebase Auth + Apple Sign-In + push
notifications round-trip validated). Android first-build is now
mechanically possible; remaining work is **EAS profile config**,
**Play Console listing**, and **distribution-track setup**. Google
Play Developer account ($25) paid and verified 2026-05-18, dedicated
Android test device acquired the same day.

---

## Current state

What's already in place:

- `app.json` declares `android.package: com.sportdeets.mobile`, the
  adaptive icon asset set, AND `android.googleServicesFile` pointing
  at `./google-services.json` (landed in PR #405).
- `src/UI/sd-mobile/google-services.json` — Firebase Android app
  registered in the `sportdeets` project with package name
  `com.sportdeets.mobile`; config file committed to the repo.
- `eas.json` defines `development` / `preview` / `production` build
  profiles, but only iOS resource classes are configured. There is no
  Android-specific build config and no `submit` section.

What's missing:

- Android-specific build configuration in `eas.json` (`buildType` per
  profile, `resourceClass` if we want anything other than the default).
- `submit` section in `eas.json` for automated Play Console pushes.
- Google Cloud service account JSON for EAS Submit to authenticate
  against the Play Console API.
- Play Console store listing (app name, description, screenshots,
  content rating questionnaire).
- Internal Testing track configured with tester Google accounts.
- **SHA-1 debug fingerprint registered in Firebase Console** for the
  Android app — required for Google Sign-In on Android (Firebase
  needs to verify which client signed the OAuth request). Get the
  SHA-1 via `eas credentials -p android` after the first build, then
  paste into Firebase Console → Project Settings → Your apps → Android
  app → "Add fingerprint". Not required for FCM or email/password
  sign-in.

---

## Two distribution paths

Android has two parallel paths, useful at different stages:

### Path A — Direct APK install

Build a `.apk` (single-file Android Package), download via the EAS
build URL on the device's browser, sideload directly. **No Play
Console involvement at all.**

- **Fast.** No store listing required, no review, no propagation
  delay.
- **Dev-only.** Sideloaded APKs don't auto-update; each new build is
  a fresh install.
- **Right for.** Daily dev validation on the dedicated test device.
  Sanity-checking that EAS builds for Android, that the app launches,
  that Firebase / SignalR / Sentry all wire up correctly on Android.

### Path B — Google Play Console testing tracks

Build an `.aab` (Android App Bundle), upload to one of Play Console's
testing tracks, testers install through the Play Store on their
devices. Path B has **four tracks** of progressively wider audience,
and you escalate through them as you gain confidence:

| Track | Audience | Review time | Typical use |
|---|---|---|---|
| **Internal testing** | Up to 100 testers added by Google account email | **Minutes** (no human review) | Friends, early validation, the TestFlight-Internal equivalent |
| **Closed testing** | Specific tester list / Google Groups (larger) | Hours to ~1 day | Wider beta, still gated, structured QA |
| **Open testing** | Anyone with the opt-in URL | Hours to ~1 day | Public beta — appears in Play Store as "beta" |
| **Production** | Public | **Days** (full review, multiple cycles common) | App-Store equivalent — slow, deliberate release |

For "friends bang on it before public" — **Internal testing** is the
right track. Some advantages over TestFlight worth knowing:

- **Review is effectively instant.** Apple's TestFlight Internal still
  goes through a beta review (hours to days). Play Console Internal
  Testing has no human review at all — submit, available in minutes.
- **100 testers without the split.** TestFlight allocates 25 internal
  testers + 10,000 external (with separate review processes). Play's
  Internal track is a flat 100, all on the fast path.
- **Auto-update behaves like prod.** Once a tester opts in via the
  share link, the app appears in their Play Store as if installed
  normally. Updates auto-deliver — no separate "TestFlight app"
  intermediary.

The two paths are not mutually exclusive — most projects use Path A
during early dev and graduate to Path B once the build is solid.

---

## The first-build-is-special gotcha

**Google requires the very first `.aab` of a new app to be uploaded
manually through the Play Console UI** — not via the API, not via
`eas submit`. This is one-time setup that associates Google Play App
Signing with your app.

After that initial manual upload, subsequent `eas submit` calls work
on autopilot. Plan for it in the sequence below.

---

## Recommended sequence

Numbered for execution order. Time estimates are rough.

| # | Step | Time | Outcome |
| - | ---- | ---- | ------- |
| 1 | EAS Android build config + first APK build | ~30 min | APK lands on the dev device; daily dev unblocked |
| 2 | Play Console store listing + Internal Testing track | ~1 hr | App exists in Play Console; tester list configured |
| 3 | Manual upload of first `.aab` to Internal Testing | ~15 min | Play App Signing initialized; tester install link live |
| 4 | Service account JSON + `eas.json` `submit` profile | ~15 min | `eas submit --platform android` works |
| 5 | Steady state: `eas build && eas submit` | ongoing | Each release ships to Internal Testing automatically |

Steps 1 alone unblocks dev iteration. Steps 2–5 are sequenced for
when distribution to friend testers becomes relevant.

The tedious step is **#2** — Play Console listing requirements
(screenshots at multiple resolutions, description text, content
rating questionnaire, target audience declaration, data safety form).
Budget for that one accordingly.

---

## Decisions to make before editing config

### 1. Profile naming

Current iOS-flavored profiles: `preview` = TestFlight Internal,
`production` = App Store.

Android parallel: `preview` = direct-install APK,
`production` = Play Console AAB.

Options:

- **(a)** Same profile names, platform-flag at build time.
  `eas build --profile preview --platform android` produces APK;
  `--platform ios` produces the iOS TestFlight build. Same env vars
  apply to both.
- **(b)** Separate profiles per platform: `android-preview` /
  `android-production` alongside `preview` / `production`.

Recommendation: **(a)** — same profile names. Keeps env vars in one
place, keeps `eas.json` smaller, matches Expo's expected pattern.

### 2. APK vs AAB for the `preview` profile

- **APK**: Single file, sideloads cleanly via a browser download
  link. Easy direct-install on the test device.
- **AAB**: Required for Play Console upload, but can't be sideloaded
  without `bundletool` gymnastics.

Options:

- **(a)** `preview` = APK, `production` = AAB. Distinct artifacts for
  distinct purposes.
- **(b)** Both = AAB. Use Path B (Play Console Internal Testing) for
  all distribution including dev.

Recommendation: **(a)** — APK for `preview`, AAB for `production`.
Path A is just faster for daily dev; once we've validated the build
works, we don't need to keep using it for ourselves.

### 3. Where to start

Options:

- **(a)** Step 1 only — APK direct-install. Validate end-to-end on
  the dedicated device before fighting Play Console's listing forms.
- **(b)** Full sequence in one pass.

Recommendation: **(a)** — Step 1 first. Cheaper to discover that
Firebase / SignalR / Sentry have an Android-specific config issue
through a 5-minute APK install than through a 90-minute Play Console
session.

---

## What changes when we execute

### `eas.json` additions (Step 1)

Each profile grows an `android` section. Example for `preview`:

```json
"preview": {
  "distribution": "internal",
  "android": {
    "buildType": "apk"
  },
  "env": { ... unchanged ... }
}
```

And `production` (AAB for Play Console):

```json
"production": {
  "autoIncrement": true,
  "android": {
    "buildType": "app-bundle"
  },
  "env": { ... unchanged ... }
}
```

`autoIncrement: true` already exists on `production` and handles
`versionCode` on the Android side just like it handles `buildNumber`
on iOS.

### `eas.json` `submit` section (Step 4)

```json
"submit": {
  "production": {
    "android": {
      "serviceAccountKeyPath": "./google-play-service-account.json",
      "track": "internal",
      "releaseStatus": "completed"
    }
  }
}
```

`track: internal` ships to the Internal Testing track. Move to
`alpha` / `beta` / `production` when you graduate from Internal
Testing.

### Environment variables (Steps 1–5)

Android builds consume the same `EXPO_PUBLIC_*` env vars as iOS — no
duplication needed. Firebase config lives in the same env block
already configured for iOS preview/production builds.

The `sportdeets` Firebase project is in use for both iOS and Android,
preview and production. The iOS app, Android app, and Web app are all
registered under the same Firebase project, so a single set of
`EXPO_PUBLIC_FIREBASE_*` env vars works for every build target.
Native bundle-side config (`GoogleService-Info.plist` for iOS,
`google-services.json` for Android) lives in `src/UI/sd-mobile/`
and is wired through `app.json`'s `ios.googleServicesFile` /
`android.googleServicesFile` declarations.

### Google Cloud service account (Step 4)

One-time setup:

1. Open Google Cloud Console for the project linked to your Play
   Developer account (or create a new project).
2. Create a service account with no special role.
3. Grant the service account access to your Play Console:
   - Play Console → Setup → API access → Link service account
   - Grant **Release apps to testing tracks** + **Manage production
     releases** permissions.
4. Create + download a JSON key for the service account.
5. Save the JSON outside the repo (do **not** commit). Reference it
   from `eas.json` via `serviceAccountKeyPath` (local path) or
   `serviceAccountKey` (an EAS secret).

The JSON file is sensitive — treat it like a credential. EAS Secret
is the preferred storage for production setups.

---

## Per-build workflow (steady state, post-Step 5)

```bash
# Bump versionCode (handled by autoIncrement on production profile,
# manual otherwise). Make code changes. Then:

eas build -p android --profile production
# ... wait for cloud build to finish, ~10-20 min ...

eas submit -p android --latest --track internal
# ... pushes the most-recent production AAB to the Internal Testing
# track. --latest skips the interactive build-picker; --track
# internal is the explicit override even if eas.json defaults the
# track elsewhere.

# Testers see the update in Play Store within ~15 min.
```

A few `eas submit` variants worth knowing:

- `eas submit -p android --latest --track internal` — most common
  during friend-tester phase. Pushes the freshest production AAB.
- `eas submit -p android --id <build-id> --track internal` — pick a
  specific older build (e.g. roll back to last-known-good).
- `eas submit -p android --latest --track production` — graduate to
  the live Play Store track. Subject to full review.

The `--track` flag overrides whatever is in `eas.json`'s `submit`
config, which is helpful when the same profile needs to ship to
different tracks at different times.

For dev iteration without going through Play Console:

```bash
eas build -p android --profile preview
# ... wait for build ...
# EAS prints a QR code / URL. Open on the test device's browser,
# download the APK, install. Done.
```

---

## Hotfixes between builds

EAS Update (OTA JS updates) works the same on Android as on iOS —
the same `eas update` commands apply once we configure it. Native
changes still require a new build; JS-only changes can ship as OTA.

EAS Update is not currently wired up for this project (per
`expo-deployment-model.md`). When it is, Android comes along for free.

---

## Gotchas to watch for

1. **First `.aab` upload must be manual.** Don't try `eas submit`
   before the manual upload through the Play Console UI — it will
   fail in a confusing way.
2. **`versionCode` is an integer, monotonically increasing.** Unlike
   iOS `buildNumber` (which is a string), Android `versionCode` must
   be a strict integer. EAS `autoIncrement` handles this.
3. **`com.sportdeets.mobile` is the immutable package identifier.**
   Once an app is created in Play Console with a package name, you
   cannot change it. Different package = different app.
4. **Play Console requires a privacy policy URL** as part of listing
   setup. Even if you have nothing fancy, you need *something* hosted
   at a public URL. (Cheap fix: a static page on `sportdeets.com`.)
5. **Data Safety form is mandatory.** Play Console will block release
   until you declare what user data your app collects (Firebase Auth
   counts; Sentry counts). Be honest; users see this in the listing.
6. **Adaptive icon background color must match.** `app.json` sets
   `android.adaptiveIcon.backgroundColor` (currently `#1B3A6B` — the
   navy brand color). Verify this renders correctly on the test
   device's launcher before submitting; some launchers crop
   aggressively.

---

## Pricing

- **Google Play Developer account**: $25 one-time. Already paid
  (2026-05-18).
- **EAS Build**: same as iOS — included in the EAS plan tier already
  used. No additional cost per-platform.
- **EAS Submit**: same as build — included in plan.

---

## References

- `expo-deployment-model.md` — iOS / TestFlight equivalent of this
  doc. Steady-state workflows mirror each other closely.
- `firebase-config-in-eas-builds.md` — Firebase env-var setup for EAS
  builds. The same env block applies to Android.
- `eas.json` — actual config we'll be editing in Steps 1 and 4.
- `app.json` — Android package, adaptive icon, versioning declared
  here.
