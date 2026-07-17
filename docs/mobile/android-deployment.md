# Android deployment via EAS

Companion to `expo-deployment-model.md`, which covers iOS / TestFlight.
This doc focuses on the Android side: getting builds onto a physical
test device, then onto the Google Play Console for friend testers and
eventually production users.

Status: **Play Console app created, Internal Testing prep in flight**
as of 2026-06-04. PR #405 landed `google-services.json` and
`android.googleServicesFile` in `app.json` (Firebase Android wiring).
Play Console app registered with package `com.sportdeets.mobile` the
same day. iOS path is shipped end-to-end (TestFlight builds +
Firebase Auth + Apple Sign-In + push notifications round-trip
validated). Remaining work for friend-tester distribution: **four
App-content forms** (content rating / target audience / data safety
/ privacy policy URL), **manual first AAB upload** through Play
Console UI, **service account JSON** for `eas submit`. Google Play
Developer account ($25) paid and verified 2026-05-18, dedicated
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
- **Play Console app registered** (2026-06-04): app name SportDeets,
  package `com.sportdeets.mobile`, free, country list configured.
  Status: "You can start testing your app right away using internal
  testing." Closed test + production access apply for later graduation.

What's missing for Internal Testing (friend distribution):

- **App content forms**, all four required:
  - Content rating questionnaire (~10 min)
  - Target audience declaration (~2 min)
  - Data safety form (~15 min — declare Firebase Auth, Sentry, FCM
    collection)
  - Privacy policy URL — required because we collect personal data
    via Firebase Auth (user IDs, emails). Cheap fix: a static page on
    sportdeets.com.
  - **Account-deletion URL** — because the app has account creation,
    Google's Data Safety flow also requires a publicly reachable
    account-deletion mechanism (both in-app and a web URL that lets a
    user request deletion without logging in). See
    `account-deletion.md`. Host this alongside the privacy policy —
    it's a second static page/URL, not just one.
- **Manual first `.aab` upload** through Play Console UI (initializes
  Play App Signing — `eas submit` cannot do this).
- **Internal testing track** populated with tester Google account
  emails + share link sent to friends.

What's missing for `eas submit` automation:

- Android-specific build configuration in `eas.json` (`buildType` per
  profile, `resourceClass` if we want anything other than the default).
- `submit` section in `eas.json` for automated Play Console pushes.
- Google Cloud service account JSON for EAS Submit to authenticate
  against the Play Console API (also referenced from `eas.json`'s
  `submit.production.android.serviceAccountKeyPath`).

What's missing for Production (eventual public release):

- Full store listing: screenshots (phone + tablet), feature graphic,
  short description, long description, categorization.
- **Closed test completion** before Google grants production access.
  Play Console explicitly requires this gate now: complete a closed
  test (typically 12+ testers for 14+ days) and apply for production
  access — it's no longer enough to just submit to production.

What's missing for Google Sign-In on Android (separate from Play):

- **TWO SHA-1 fingerprints must be registered in Firebase Console**
  for the Android app — required for Google Sign-In on Android
  (Firebase verifies the signature of the *installed* app against the
  OAuth client). As of now `google-services.json` contains only a
  type-3 (web) OAuth client and **no Android certificate hash at all**,
  so Google Sign-In on Android is not yet wired. You need both:
  - **Upload / build keystore SHA-1** — makes Google Sign-In work for
    **direct-APK installs (Path A)**. Get it via
    `eas credentials -p android` after the first build.
  - **Play App Signing SHA-1** — makes Google Sign-In work for
    **Play-installed testers (Path B / Internal Testing)**. This is
    the trap: Google **re-signs** your app with the Play App Signing
    key when it's delivered through any Play track, so the installed
    app's signature is *different* from your upload keystore. Sign-In
    validates against the delivered signature, so the upload-key SHA-1
    alone will fail for every Internal Testing tester. This SHA-1 only
    exists **after the first manual AAB upload** (Step 5) initializes
    Play App Signing; grab it from **Play Console → Setup → App
    integrity → App signing key certificate**.

  Register both in **Firebase Console → Project Settings → Your apps →
  Android app → "Add fingerprint"**, then re-download
  `google-services.json` and commit it (an Android OAuth client with a
  certificate hash should now appear). Not required for FCM or
  email/password sign-in.

  > **Symptom if you skip the Play App Signing SHA-1:** Google Sign-In
  > works in your sideloaded dev APK but throws a `DEVELOPER_ERROR`
  > (code 10) for every friend who installs from the Internal Testing
  > track. This is the #1 "worked in dev, broke in Play" gotcha.

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

| Track | Audience | Review time | Listing requirements | Typical use |
|---|---|---|---|---|
| **Internal testing** | Up to 100 testers added by Google account email | **Minutes** (no human review) | Minimal — four App-content forms + privacy policy URL. No screenshots, no description copy required. | Friends, early validation, the TestFlight-Internal equivalent |
| **Closed testing** | Specific tester list / Google Groups (larger) | Hours to ~1 day | Full store listing required (screenshots, description, feature graphic) | Wider beta, structured QA, **also the production-access gate** (see below) |
| **Open testing** | Anyone with the opt-in URL | Hours to ~1 day | Full store listing required | Public beta — appears in Play Store as "beta" |
| **Production** | Public | **Days** (full review, multiple cycles common) | Full store listing + completed closed test + apply for production access | App-Store equivalent — slow, deliberate release |

**Production is gated by a closed test.** Google's current policy (in
the Play Console UI verbatim): *"To publish to everyone, you need to
finish setting up your app, complete a closed test, and apply for
production access."* In practice this means a closed test with 12+
testers active for 14+ days before you can apply. Plan the timeline
accordingly — production access is not a same-day operation.

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
`eas submit`. This is one-time setup that initializes Google Play App
Signing for your app. Since Internal Testing setup happens through
the Play Console UI anyway (you create the release and pick the AAB
from a file dialog), this requirement is satisfied naturally on the
first Internal Testing release. The gotcha is only painful if you
try `eas submit` as your *literal* first action without ever having
touched the Play Console UI — which will fail with cryptic errors.

After that initial manual upload, subsequent `eas submit` calls work
on autopilot.

---

## Recommended sequence

Numbered for execution order. Time estimates are rough. The sequence
splits into two phases: **Internal Testing** (friend distribution,
~2 hours total) and **Production graduation** (much later, weeks of
gated steps).

### Phase 1 — Internal Testing (friend distribution)

| # | Step | Time | Outcome |
| - | ---- | ---- | ------- |
| 1 | EAS Android build config + first APK build | ~30 min | APK lands on the dev device; daily dev unblocked |
| 2 | Play Console app created (DONE 2026-06-04) | — | App exists in Play Console |
| 3 | Privacy policy URL + account-deletion URL hosted on sportdeets.com | ~45 min | Required fields for Data Safety form |
| 4 | App-content forms: content rating + target audience + data safety + privacy URL + account-deletion URL | ~45 min | Internal Testing track unblocked |
| 5 | Manual upload of first `.aab` to Internal Testing via Play Console UI | ~15 min | Play App Signing initialized; tester install link live |
| 6 | Add tester Google emails to Internal track + send share link | ~5 min | Friends can install |
| 7 | Service account JSON + `eas.json` `submit` profile | ~15 min | `eas submit -p android --latest --track internal` works |
| 8 | Steady state: `eas build && eas submit` | ongoing | Each release ships to Internal Testing automatically |

Step 1 alone unblocks dev iteration on the test device. Steps 3–7
are the friend-distribution path. **Total budget: ~2 hours.**

### Phase 2 — Production graduation (weeks later)

| # | Step | Time | Outcome |
| - | ---- | ---- | ------- |
| 9 | Full store listing: screenshots, descriptions, feature graphic, categorization | ~2-4 hrs | Required for Closed/Open/Production tracks |
| 10 | Closed test with 12+ testers, run for 14+ days | 2+ weeks calendar time | Required gate for production access |
| 11 | Apply for production access through Play Console | ~1 hr forms + days of waiting | Google reviews and grants production capability |
| 12 | First production release | ~1-3 days review | App live in Play Store |

Phase 2 is not on the critical path until we're ready to leave beta.
Don't pre-do it — Google's policies for the production-access
application change and the screenshots you need will likely have
evolved by then.

The tedious step in Phase 1 is **#4** — the Data Safety form, which
requires honest declarations about Firebase Auth, Sentry, FCM, and
any other data-collecting SDKs. The honest-declaration part matters:
Google audits, and misdeclaration gets your app yanked. Budget the
~15 minutes accordingly and don't rush it.

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

### `eas.json` additions (Phase 1, Step 1)

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

### `eas.json` `submit` section (Phase 1, Step 7)

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

### Environment variables

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

### Google Cloud service account (Phase 1, Step 7)

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

## Per-build workflow (steady state, post-Phase 1)

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
   You also need an **account-deletion URL** (see gotcha #10) — two
   static pages, not one.
5. **Data Safety form is mandatory.** Play Console will block release
   until you declare what user data your app collects (Firebase Auth
   counts; Sentry counts). Be honest; users see this in the listing.
6. **Adaptive icon background color must match.** `app.json` sets
   `android.adaptiveIcon.backgroundColor` (currently `#0d1117` — the
   dark brand color). Verify this renders correctly on the test
   device's launcher before submitting; some launchers crop
   aggressively.
7. **Production access requires a completed closed test.** Google's
   current policy: you can't ship to Production directly. Must run a
   closed test (typically 12+ testers, 14+ day window) and apply for
   production access through the Play Console. Plan ~2 weeks of
   calendar time for the gate alone, separate from the actual review.
8. **Internal Testing has reduced listing requirements.** You do NOT
   need screenshots, feature graphic, or long description to ship to
   Internal Testing — only the four App-content forms (content
   rating, target audience, data safety, privacy policy URL). Don't
   pre-do the full listing when all you want is friends installing
   the app. (Closed/Open/Production require the full listing.)
9. **Free apps cannot be converted to Paid post-publish.** The Play
   Console toggle is one-way. Pick Free; monetize via in-app
   purchases / subscriptions through Google Play Billing instead.
   This is the universal mobile monetization pattern (Spotify,
   Netflix, etc. are all Free apps with paid tiers).
10. **Account-deletion mechanism is required for apps with account
    creation.** Google's Data Safety flow needs both an in-app path
    AND a public web URL where a user can request deletion without
    logging in. Host it next to the privacy policy. See
    `account-deletion.md`.
11. **Google Sign-In needs BOTH SHA-1 fingerprints in Firebase.** The
    upload-keystore SHA-1 covers direct-APK installs; the **Play App
    Signing** SHA-1 (available only after the first manual AAB upload)
    covers Play-installed testers, because Play re-signs the delivered
    app. Missing the Play App Signing SHA-1 → `DEVELOPER_ERROR`
    (code 10) for every Internal Testing tester while dev APKs still
    work. See the "Google Sign-In on Android" section above.

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
