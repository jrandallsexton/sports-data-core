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
- `runtimeVersion: { "policy": "fingerprint" }` (since #702; app `version` is
  `1.2.1`). The runtime is a hash of the native project, not the version
  string. An update reaches only builds whose **Runtime Version** equals the
  fingerprint of the tree you publish from. Any native change (a dependency
  with native code, `app.json` native config, an SDK bump) changes the hash
  and needs a new `eas build`; JS-only changes do not. Check before publishing
  with `eas fingerprint:generate --platform ios` against
  `eas build:list --platform ios --limit 1`.
- `eas.json` build profiles map to channels: `development`, `preview`,
  `production`. Our store/TestFlight builds come from
  `eas build --profile production`, so they listen on the **`production`**
  channel.

## Ship an OTA update (step by step)

> **Read first.** `eas update` runs Metro **on your machine**, and Metro
> auto-loads `sd-mobile/.env.local`, which sets
> `EXPO_PUBLIC_API_BASE_URL=http://localhost:5262`. Without the flags below,
> that value is inlined into the bundle and every install dark-screens. This
> is what happened on 2026-09-17 and on every earlier OTA attempt. The full
> analysis and the durable fix are in
> [`ota-pipeline-hardening.md`](./ota-pipeline-hardening.md). Two flags are
> therefore **mandatory** on every publish:
>
> - `--environment <env>` makes the CLI use the server-side EAS env and
>   disables `.env*` loading outright.
> - `--clear-cache`, because Metro's transform cache replays previously
>   inlined `EXPO_PUBLIC_*` literals even when the env is now correct.
>
> Run everything from a **PowerShell** console in `src/UI/sd-mobile`.

1. **Be on the exact code you want live.** `eas update` bundles your current
   working directory. Land the change on `main`, then `git checkout main`,
   `git pull`, and confirm `git status -- src/UI/sd-mobile` is clean.

2. **Confirm the server-side env exists** (once per environment; re-run after
   adding any `EXPO_PUBLIC_*` var):

   ```powershell
   eas env:list --environment production   # 9 EXPO_PUBLIC_* keys + SENTRY_AUTH_TOKEN
   ```

   If the `EXPO_PUBLIC_*` keys are missing, generate and push them from the
   `eas.json` env blocks:

   ```powershell
   .\write-eas-env-files.ps1
   eas env:push --environment production --path ./.env.production.tmp
   eas env:push --environment preview    --path ./.env.preview.tmp
   Remove-Item .env.production.tmp, .env.preview.tmp
   ```

3. **Fingerprint gate.** The hash must equal the store build's Runtime
   Version, or the update reaches nobody and still reports success:

   ```powershell
   eas fingerprint:generate --platform ios
   eas fingerprint:generate --platform android
   eas build:list --platform ios --limit 1   # compare Runtime Version
   ```

4. **Bundle gate.** Export exactly what would be published and inspect it.
   The export must run under the target EAS environment with dotenv disabled,
   which is what `eas update --environment` does; a bare `npx expo export`
   reads `.env.local` and always fails the gate on Bender:

   ```powershell
   $env:EXPO_NO_DOTENV = '1'
   eas env:exec preview "npx expo export --clear --platform ios --output-dir dist-verify"
   Select-String -Path dist-verify\_expo\static\js\ios\* -Pattern 'localhost:5262' -List   # MUST be empty
   Select-String -Path dist-verify\_expo\static\js\ios\* -Pattern 'api.sportdeets.com' -List  # MUST hit
   Remove-Item -Recurse -Force dist-verify
   ```

   Use `production` in place of `preview` before the production publish. If
   `localhost:5262` appears, **stop**. Do not publish.

5. **Publish to `preview` first**, verify on a preview (TestFlight) build, then
   promote to `production`:

   ```powershell
   eas update --branch preview    --environment preview    --clear-cache -m "<what changed>"
   # verify on a preview build (see step 7), then:
   eas update --branch production --environment production --clear-cache -m "<what changed>"
   ```

   The preview env is identical to production apart from
   `EXPO_PUBLIC_SENTRY_ENV`, so a preview verification is a real one.

6. **Confirm the channel points at that branch** (silent failure if not):

   ```powershell
   eas channel:view production
   ```

   It should show branch **production** receiving 100%. If it shows no branch or
   a different one, link it once:

   ```powershell
   eas channel:edit production --branch production
   ```

7. **To verify immediately, relaunch TWICE.** ⚠️ This is the classic gotcha.
   `expo-updates` **downloads** the update in the background on one launch and
   **applies** it on the *next* launch. Fully quit the app (swipe it away) and
   reopen — then quit and reopen a **second** time. Opening once and seeing no
   change does **not** mean OTA failed. (Real users don't have to do this — see
   below.)

8. **Verify adoption a day or two later.** Relaunching twice proves the
   publish reached *your* phone. It says nothing about users who never
   force-close. The `useOtaUpdates` hook (next section) is supposed to carry
   the update to warm apps, but its behaviour in production had never been
   observed before 2026-09-21, so check the numbers rather than assume.

   This app publishes **one update group per platform**, because the iOS and
   Android runtimes differ. Get both group IDs, then ask EAS for insights:

   ```powershell
   eas update:list --branch production --limit 2       # two groups, same message
   eas update:view <groupId> --insights --days 3       # once per group
   ```

   Per platform you get `Launches`, `Unique users`, `Failed launches`, and
   `Crash rate` for the update ID. What to look for:

   - `Unique users` climbing past 1 (you) over the first day or two means
     warm-app users are picking it up without a cold start. Flat at 1 means
     the hook is not doing its job, or nobody has opened the app.
   - `Failed launches` above 0 or a non-zero `Crash rate` means the bundle
     breaks at boot for someone. Roll back (see Cautions) before chasing it.

   Sentry gives the same signal per event: every event carries an
   `ota_updates` context with `update_id` and `runtime_version`, set by the
   Sentry Expo integration. Filter on `ota_updates.update_id` for the new ID
   and confirm events arrive from users other than you.

   If adoption is slow, the lever is `APPLY_AFTER_BACKGROUND_MS` in
   `src/hooks/useOtaUpdates.ts`, currently three minutes. Shorter propagates
   faster but reloads users who only glanced away. Change it on evidence.

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
- The installed build must **match** that runtimeVersion. With the
  `fingerprint` policy that means the build's Runtime Version (from
  `eas build:list`) equals the update's. If something native changed since the
  build, the mismatch is expected and the update is correctly ignored — you
  need a fresh `eas build`.
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
- **Rollback:** `eas update:rollback <groupId>`, once per platform, where the
  group is the latest for its branch and runtime. With no earlier update on the
  same runtime it publishes a roll-back-to-embedded, which is the right target
  after a bad first update on a new build. A rollback does not block the next
  update and does not require a new build. Details in
  [`ota-pipeline-hardening.md`](./ota-pipeline-hardening.md#5-rollback-cheat-sheet).
- OTA can't fix a crash-on-launch that happens before `expo-updates` loads the
  new bundle. A bad JS update can brick the app; keep changes small and test on a
  device before publishing to `production`.
- Related: EAS build/submit flow is unchanged —
  `eas build --platform ios --profile production` then
  `eas submit --platform ios --latest` for native releases.
