# OTA updates: why every one has failed, and the Monday fix

**Status:** plan, written 2026-09-17 after the production OTA outage. Nothing
here is implemented yet.

**Supersedes the publish steps in [`ota-updates.md`](./ota-updates.md)**, which
are stale and are what produced the outage. See [Docs to correct](#6-docs-to-correct).

---

## 1. What actually broke

`eas update --branch production` dark-screened iOS 1.2.1 in production on
2026-09-17. Rolled back on both platforms with `eas update:rollback`; device
recovered.

Exactly **one** variable was wrong in the published bundle:

| variable | shipped in the OTA bundle | should have been |
|---|---|---|
| `EXPO_PUBLIC_API_BASE_URL` | `http://localhost:5262` | `https://api.sportdeets.com` |

On a phone, `localhost:5262` is the phone itself. Every API call fails, the app
never leaves its loading state, and you get a dark screen.

### What was NOT wrong

Worth stating plainly, because the first read of this outage assumed otherwise
and it changes the fix:

- **Firebase config was correct.** `.env.local` carries the *production*
  Firebase project (`sportdeets`), byte-identical to the `eas.json` production
  block. Auth was never misconfigured.
- **Sentry was correct and working.** `.env.local` carries the same DSN as
  production, and `src/lib/sentry.ts` falls back to `environment: 'production'`
  when `EXPO_PUBLIC_SENTRY_ENV` is unset. Sentry initialised, pointed at the
  right project.

So **"nothing in Sentry" was not a reporting failure. There was nothing to
report.** A fetch to an unreachable host is a handled network error, not an
exception. The app failed silently and correctly, by its own error handling.

That is the most important finding on this page. The failure mode is not "the
app crashed and we could not see it." It is **"the app was misconfigured and
nothing in the system treated that as an error."** Fix 3.3 is the one that
addresses it.

### Why it hit every update, ever

`EXPO_PUBLIC_*` values are **inlined into the JS bundle at bundle time**, and
`eas update` runs the bundler **on the machine you type it on**. Metro
auto-loads `.env.local` there. The JS payload never mattered, which is exactly
consistent with never having had a working OTA update, and which no JS bug
could explain.

| | `eas build --profile production` | `eas update` |
|---|---|---|
| where the bundler runs | EAS servers | **your machine** |
| sees `sd-mobile/.env.local`? | **no**, gitignored (`.gitignore:34`), never uploaded | **yes**, Metro auto-loads it |
| where env comes from | `eas.json` profile `env` block | `.env.local` wins |

This asymmetry is the whole bug. It is also why the May 2026 fix in
[`firebase-config-in-eas-builds.md`](./firebase-config-in-eas-builds.md),
pasting values into `eas.json`, correctly fixed builds and did nothing for
updates.

### Evidence

- Update log `docs/mobile/expo-update-log.txt`: iOS group
  `6491e79e-a7db-4f42-b527-79215d473dda`, runtime `96e0abcb…`.
- `eas build:list --platform ios` shows 1.2.1 on runtime `96e0abcb…`. **Exact
  match**, so the update reached every 1.2.1 install.
- `git status -- src/UI/sd-mobile` at publish time: clean. The asterisk on
  commit `25aa64b2` was non-mobile dirt. **PR #767 was not the cause.**
- `eas env:list --environment production` returns only `SENTRY_AUTH_TOKEN`. None
  of the `EXPO_PUBLIC_*` values exist server-side.
- `.env.local` against the `eas.json` production block: 7 of 8 keys identical,
  only `EXPO_PUBLIC_API_BASE_URL` differs. `EXPO_PUBLIC_SENTRY_ENV` is absent
  from `.env.local` and falls back to `production`. Harmless, but it does mean
  OTA bundles have never been distinguishable from store builds in Sentry.

---

## 2. "Build from GitHub" is the right instinct on the wrong lever

The instinct, *stop bundling on Bender*, is correct. But the EAS GitHub
integration governs **builds**, and builds have not been broken since May. They
already run on EAS servers with the right env. Turning it on would not have
prevented this outage.

What carries the instinct to the path that actually broke:

- **EAS Workflows** (`eas workflow:create`, config in `.eas/workflows/*.yml`).
  Supported by the installed CLI; the project defines none yet. A workflow runs
  `eas update` on EAS infrastructure, where `.env.local` does not exist.
- **GitHub Actions.** `.github/workflows/ci-mobile.yml` already runs Jest for
  the mobile app. An update job extends something already working.

Either takes Bender out of the publish path permanently. Sections 3.1 through
3.3 make publishing safe before that exists, and are worth doing regardless.

---

## 3. The fix, in priority order

### 3.1 Resolve production config server-side

Production values live only in `eas.json`, which `eas update` ignores. Push them
to EAS so both paths resolve identically. **Do not push `.env.local`**, it holds
the local API base, which is the bug.

```sh
cd src/UI/sd-mobile

# Generate the temp files from the eas.json env blocks (9 keys each; *.tmp is
# gitignored). Do not hand-write them. PowerShell, from src/UI/sd-mobile:
.\write-eas-env-files.ps1

eas env:push --environment production --path ./.env.production.tmp
eas env:push --environment preview    --path ./.env.preview.tmp
Remove-Item .env.production.tmp, .env.preview.tmp

eas env:list --environment production   # expect 9 EXPO_PUBLIC_* plus SENTRY_AUTH_TOKEN
```

Then always name the environment when publishing:

```sh
eas update --branch production --environment production --clear-cache -m "<what changed>"
```

The CLI states `--environment` is **required for Expo SDK 55+**, and this
project is on `expo ~55.0.24`. Its absence is why nothing resolved.

> **Verified 2026-09-21** against the installed eas-cli (24.4.2,
> `commands/update/index.js`, the `maybeServerEnv` block): when `--environment`
> is passed, the CLI injects the server-side values **and sets
> `EXPO_NO_DOTENV=1`** for the bundler. `.env.local` is not out-ranked; it is
> never read. This step is a complete fix on its own, with one caveat that
> section 3.2 covers: the Metro cache.

### 3.2 Verify the bundle before publishing

Whatever the precedence rules turn out to be, this observes the artifact
directly instead of trusting the tooling:

```sh
cd src/UI/sd-mobile
npx expo export --clear --platform ios --output-dir dist-verify

# MUST return nothing:
grep -r "localhost:5262" dist-verify/ && echo "STOP - dev config in bundle"

# MUST return a hit:
grep -rl "api.sportdeets.com" dist-verify/ >/dev/null && echo "prod API present"

rm -rf dist-verify
```

If the first grep hits, **do not publish**. Cheap, decisive, and it would have
caught every failed update to date.

**`--clear` is not optional, and neither is `--clear-cache` on `eas update`.**
Verified 2026-09-21 with three local exports on a warm Metro cache:

| export | env | Metro cache | `localhost:5262` | `api.sportdeets.com` |
|---|---|---|---|---|
| default, reads `.env.local` | dev | warm | present | absent |
| `EXPO_NO_DOTENV=1` + production values | prod | warm | **present** | **absent** |
| same as above | prod | `--clear` | absent | present |

The middle row is what `eas update --environment production` does on Bender
after a normal dev session: Metro replays the previously inlined
`EXPO_PUBLIC_*` literals and ships the localhost bundle again, with the env fix
in place and no error. The cache is a second, independent way to reproduce the
outage. Section 3.1 closes the first; only a cache clear closes this one.

### 3.3 Make a misconfigured bundle fail loudly

This is the real lesson of the outage. The app had no notion that its own
configuration could be invalid, so a fatal misconfiguration produced silence
instead of a signal.

`src/services/api/client.ts:5` reads
`process.env.EXPO_PUBLIC_API_BASE_URL ?? 'https://api.sportdeets.com'`. The
fallback never fired, because `.env.local` *sets* the value, making it
defined-and-wrong rather than undefined. A fallback cannot catch this class of
bug. An assertion can.

Add a boot-time guard that, when `!__DEV__`, throws a named error if
`EXPO_PUBLIC_API_BASE_URL` resolves to `localhost`, `127.0.0.1`, or a private
LAN address. `src/hooks/useSignalRClient.ts:11` reads the same variable, so put
the guard in one module that both import rather than at each call site.

Two details that make it work:

- Import it **after** `src/lib/sentry.ts` in `app/_layout.tsx`, which is already
  the first import, so the thrown error gets reported.
- A thrown error at boot is a visible crash, not a dark screen. That is the
  point. A loud failure on a canary device beats a silent one in production.

Optionally warn when `EXPO_PUBLIC_SENTRY_ENV` is unset, so OTA bundles stop
masquerading as store builds in Sentry.

### 3.4 Publish to preview first, then promote

`eas.json` already defines a `preview` profile on its own channel, and its env
block is **identical to production** apart from `EXPO_PUBLIC_SENTRY_ENV`. A
preview build therefore exercises the real API and the real Firebase project, so
verification there is meaningful and separating the channels costs nothing.

Order: publish to `preview`, verify on a preview build, then promote to
`production`. Nothing reaches the production channel unverified.

---

## 4. Monday checklist

**Status 2026-09-21 morning.** Preconditions checked: local `main` matches
`origin/main`, `git status -- src/UI/sd-mobile` is clean, PR #767 merged 09-17.
EAS server-side env for `production` and `preview` still holds only
`SENTRY_AUTH_TOKEN`. No `.eas/workflows` exist. The bundle grep gate (3.2) has
been exercised locally and behaves as designed; see the table there.

Two things changed since this was written on 09-17, and the order below
reflects both:

- The `.env.local` precedence question is **closed**: `--environment` disables
  dotenv loading outright (3.1). Pushing env server-side is a real fix.
- The Metro transform cache is a **second trap** (3.2). Every publish must clear
  it. Without this, step 1 alone would have re-shipped the bad bundle.

Hard rule for every command in this list: run from `src/UI/sd-mobile`, and
never publish anything the grep gate has not just passed.

1. **Push production and preview env to EAS.** Nine `EXPO_PUBLIC_*` keys each,
   taken from the matching `eas.json` env block. They differ only in
   `EXPO_PUBLIC_SENTRY_ENV`. Do not push `.env.local`.

   ```sh
   # Generate the temp files from the eas.json env blocks (9 keys each; *.tmp is
   # gitignored). Do not hand-write them. PowerShell, from src/UI/sd-mobile:
   .\write-eas-env-files.ps1

   eas env:push --environment production --path ./.env.production.tmp
   eas env:push --environment preview    --path ./.env.preview.tmp
   Remove-Item .env.production.tmp, .env.preview.tmp
   eas env:list --environment production   # 9 EXPO_PUBLIC_* + SENTRY_AUTH_TOKEN
   eas env:list --environment preview      # same
   ```

   Pushing preview too is what makes step 6 meaningful: a preview publish
   without server-side env would bundle `undefined` for Firebase and crash at
   boot, which is the May failure, not this one.

2. **Config guard PR (3.3).** One module under `src/lib/`, imported in
   `app/_layout.tsx` directly after `src/lib/sentry.ts`. Throws in `!__DEV__`
   when `EXPO_PUBLIC_API_BASE_URL` is `localhost`, `127.0.0.1`, or a private
   LAN address. Warns when `EXPO_PUBLIC_SENTRY_ENV` is unset. Test lives in
   `__tests__/lib/`, matching the existing layout. Ordinary PR; branch off
   `origin/main`.

3. **Correct the two docs (section 6)** in the same PR as step 2, so the
   runbook that caused the outage cannot be followed again once this merges.

4. **Production-shaped smoke of `main` on Bender.** `npx expo start --no-dev
   --minify`. TypeScript and Jest never exercise Hermes plus minification. #767
   has not been run this way yet.

5. **Fingerprint gate.** An update only reaches builds whose runtime matches
   the fingerprint of the tree being published. Confirm the match before
   every publish, from **PowerShell**. On this machine the fingerprint's
   config-loader spawn fails under Git Bash and silently yields a garbage
   hash (`1588df86...` on 2026-09-21); PowerShell gave the correct
   `96e0abcb...`. A publish made under a wrong hash reaches nobody and
   reports success.

   ```powershell
   eas fingerprint:generate --platform ios       # must equal the iOS build's Runtime Version
   eas fingerprint:generate --platform android   # must equal the Android build's Runtime Version
   eas build:list --platform ios --limit 1       # Runtime Version to compare against
   ```

   Checked 2026-09-21: iOS `96e0abcb...` and Android `174107aa...`, both
   matching the store builds. If either drifts, the change was native and
   needs `eas build`, not an update.

6. **Publish to preview.** Gate, then publish, then verify on a preview build.

   ```sh
   npx expo export --clear --platform ios --output-dir dist-verify
   grep -r "localhost:5262" dist-verify/ && echo "STOP - dev config in bundle"
   grep -rl "api.sportdeets.com" dist-verify/ >/dev/null && echo "prod API present"
   rm -rf dist-verify

   eas update --branch preview --environment preview --clear-cache -m "<what changed>"
   ```

   On the preview device: relaunch twice, confirm the app leaves loading and
   the Sentry environment tag reads `preview`.

7. **Promote to production.** Same gate, same flags, production names.

   ```sh
   eas update --branch production --environment production --clear-cache -m "<what changed>"
   ```

   Keep the rollback cheat sheet (section 5) open. Verify on a production
   install before walking away.

8. **Then** EAS Workflows or a GitHub Actions job (section 2), so Bender leaves
   the publish path. Both run on infrastructure with no `.env.local` and no
   warm Metro cache, which retires both traps structurally rather than by
   discipline.

Step 1 is the only step that changes anything outside this machine before a
publish. Steps 2 through 5 are local and can proceed in any order relative to
it.

---

## 5. Rollback cheat sheet

What was run today, recorded for next time:

```sh
eas update:rollback <groupId>          # once per platform
```

- The group must be **the latest for its branch and runtime version**.
- If no earlier update shares that runtime fingerprint, this publishes a
  **roll back to embedded**, so clients revert to the bundle inside the store
  binary. That was the only valid target here. Every prior iOS update was on
  runtime `1.0.1` or older, from before the `appVersion` to `fingerprint` policy
  change (#702).
- Republishing an older update does **not** work across a fingerprint change.
- Rollback does not remove the bad bundle from a device that already fetched it.
  Delete and reinstall from the App Store for an immediate fix on your own phone.
- **A rollback does not block the next update, and does not require a new
  build.** The roll-back-to-embedded is one more entry on the branch for that
  runtime. The next normal publish on the same runtime supersedes it, and
  devices that reverted to embedded fetch it like any other update. Confirmed
  2026-09-21: the production branch's latest groups for both runtimes are the
  09-17 rollbacks, and the working tree still fingerprints to those runtimes.

---

## 6. Docs to correct

- **[`ota-updates.md`](./ota-updates.md)**, actively dangerous. Fix or delete
  the publish section:
  - it documents `runtimeVersion: { "policy": "appVersion" }` at version
    `0.1.0`. Both wrong since #702, now `fingerprint` at `1.2.1`
    (`app.json:5,106`).
  - its step 2 is `eas update --branch production --message "…"`, with no
    `--environment` and no `.env.local` warning. **This is the runbook that was
    followed on 2026-09-17.**
- **[`firebase-config-in-eas-builds.md`](./firebase-config-in-eas-builds.md)**.
  Its closing General Rule reads "Metro reads `.env*`; EAS does not." True for
  builds, **exactly inverted for updates**, where Metro runs locally and reads
  `.env.local` while the `eas.json` env block is ignored. Add the qualifier. As
  written it walks a reader into this trap.

---

## 7. Loose end, unrelated to the outage

`.env.local` points local development at the **production** Firebase project
(`sportdeets`). That may be deliberate, but it means local runs authenticate
real users against real auth state. Same hazard class as the local stack holding
real push credentials. Worth a decision, separately from this work.
