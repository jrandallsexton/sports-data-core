# OTA updates (EAS Update) — runbook

How to ship a JavaScript-only change to the mobile app **without** a new store
build or review, using `eas update`. Run everything from `src/UI/sd-mobile`.

## When OTA is valid vs. when you must rebuild

**OTA (`eas update`) is enough** when the change is JS/TS/asset only:
- React component / screen / styling changes (e.g. the tablet picks grid)
- Business logic, hooks, API calls, copy, images bundled in the JS

**You MUST run `eas build`** (and usually `eas submit`) when the change touches
native code or anything baked into the binary:
- Adding/removing/upgrading a native module (anything with an iOS/Android pod/gradle side)
- Changes to `app.json` native config: permissions, entitlements, `plugins`,
  splash/icon, `ios.buildNumber` handling, URL schemes, background modes
- Bumping the Expo SDK / React Native version
- Changing `version` (see runtimeVersion below) — a new runtime can't receive
  updates published under the old one

Rule of thumb: if you edited only files under `app/`, `src/`, or assets, OTA is
fine. If you edited `app.json`'s native sections or `package.json` native deps,
rebuild.

## Our config (for reference)

- `expo-updates` is installed; `app.json` → `updates.url` =
  `https://u.expo.dev/7605ab96-0b46-4b5e-8940-3d492ead7d75` (EAS project
  `7605ab96-…`, owner `sportdeets`).
- `runtimeVersion: { "policy": "appVersion" }` → the runtime is the app
  `version` (currently `0.1.0`). **Every build at version 0.1.0 shares runtime
  `0.1.0`**, so one update reaches all of them. Bumping `version` starts a new
  runtime that old builds won't receive.
- `eas.json` build profiles map to channels: `development`, `preview`,
  `production`. Our store/TestFlight builds come from
  `eas build --profile production`, so they listen on the **`production`**
  channel.

## Ship an OTA update (step by step)

1. **Be on the exact code you want live.** `eas update` bundles your current
   working directory. Normally: land the change on `main`, then
   `git checkout main && git pull`.

2. **Publish to the production channel's branch:**
   ```
   eas update --branch production --message "<what changed>"
   ```

3. **Confirm the channel points at that branch** (silent failure if not):
   ```
   eas channel:view production
   ```
   It should show branch **production** receiving 100%. If it shows no branch or
   a different one, link it once:
   ```
   eas channel:edit production --branch production
   ```

4. **To verify immediately, relaunch TWICE.** ⚠️ This is the classic gotcha.
   `expo-updates` **downloads** the update in the background on one launch and
   **applies** it on the *next* launch. Fully quit the app (swipe it away) and
   reopen — then quit and reopen a **second** time. Opening once and seeing no
   change does **not** mean OTA failed. (Real users don't have to do this — see
   below.)

## Automatic runtime updates (what real users get)

Most users never force-quit the app, so relying on the cold-start check alone
would leave them on stale JS for days. The `useOtaUpdates` hook (wired in
`app/_layout.tsx`) fixes that:

- On every foreground (and at launch) it **checks + silently downloads** any
  available update in the background.
- It **applies** the update (reloads the JS) only when the user returns after
  being backgrounded a few minutes — a real "left and came back", which already
  feels like a fresh open, so the reload is invisible. A quick app-switch never
  interrupts an active session (e.g. mid-pick).

So after you publish, real users pick it up automatically within a session or
two without doing anything. The manual double-relaunch above is just for
verifying a publish right away on your own device. The hook is inert in dev /
Expo Go (`Updates.isEnabled` is false there); test it in a real build.

## Verify / diagnose a no-show

- `eas update:list --branch production` — confirm the update exists; note its
  **runtimeVersion**.
- The installed build must **match** that runtimeVersion. With the `appVersion`
  policy that means the device's build must be the same `version` (e.g.
  `0.1.0`). If a build since bumped `version`, the mismatch is expected and the
  update is correctly ignored — you need a fresh `eas build`.
- The installed build must be on the **`production`** channel. A
  `development`/`preview` build won't receive production updates. To test OTA in
  isolation on a non-production build, publish to that build's channel instead
  (e.g. `eas update --branch preview`).
- EAS dashboard marks an update as **downloaded** once a device pulls it — use
  that to confirm the device received it, independent of whether it's applied.

## Cautions

- `--branch production` targets **every** build on the production channel.
  Pre-launch that's just our own TestFlight/dev installs. **Once we're live in
  the App Store, a production OTA reaches real users immediately (no review)** —
  treat it with the same care as a release.
- OTA can't fix a crash-on-launch that happens before `expo-updates` loads the
  new bundle. A bad JS update can brick the app; keep changes small and test on a
  device before publishing to `production`.
- Related: EAS build/submit flow is unchanged —
  `eas build --platform ios --profile production` then
  `eas submit --platform ios --latest` for native releases.
