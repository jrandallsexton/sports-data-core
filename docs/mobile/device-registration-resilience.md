# Push device registration: resilience + observability

**Status:** Approved — implementing.
**Owner:** Randall.
**Scope:** `src/UI/sd-mobile` (native push-device registration). No backend change.
Ships via **TestFlight/EAS**, which is a separate deploy track from the k8s
notification-service deploy hold — so this is **not blocked** by that hold.

## Problem

A signed-in, notification-permitted device can end up with **no `UserDevice` row**
in the Notification service, so every push is `Suppressed_NoDevice`.

Observed: iPhone (account "sportDeets") has its row and works; iPad (signed in as
`jrandallsexton@gmail.com`, permission granted via TestFlight) has a `User` row
but **no `UserDevice` row at all** (confirmed: exactly one device row exists, and
it's the iPhone's). So the iPad **never registered** — this is not an ownership
race.

## Root cause

`useRegisterPushDevice` fetches the FCM token and POSTs **exactly once per
sign-in**, and only re-runs on **auth change** or **FCM token rotation** — never
on app foreground or a permission-state change. `getFcmTokenIfGranted()` returns
a token **only if permission is already granted at that instant**, and it
**swallows every error** (`catch { return null }`). The hook likewise swallows
POST failures (`console.log` only, "Never surface to the user").

So if, on the iPad, at the one moment the hook ran:
- permission wasn't granted **yet** (granted later via prompt or iOS Settings), **or**
- the APNs token wasn't ready **yet** (`getToken()` returned null/threw — common on
  a TestFlight first run), **or**
- the POST failed (network/JWT/API),

…registration silently no-ops and **nothing ever re-triggers it**. Permission
being on *now* doesn't help — the hook won't look again. And because all three
failure points are silent, the failure left **zero trace** (nothing in Seq, no
Sentry event) — which is itself the core defect: this was un-diagnosable.

We don't need to distinguish the three timings to fix it — they share one defect
(**one-shot + silent + no re-trigger**) and one fix.

## Fix

### 1. Re-attempt on foreground (the automatic fix)

Add an `AppState` listener: when the app becomes `active` while authenticated and
registration hasn't yet succeeded this session, re-attempt. A cheap permission
check short-circuits when permission isn't granted (no POST spam); once a
registration succeeds, foreground retries stop (a session flag). This covers
permission-granted-late, APNs-not-ready, and transient POST failures — the iPad
gets its next chance the moment it's foregrounded.

### 2. Stop swallowing — surface failures to Sentry

- `getFcmTokenIfGranted()` returns the **rich** `FcmTokenResult`
  (`{ token, permissionStatus, error }`), mirroring its prompting sibling
  `getFcmToken()`, instead of a bare `string | null` that discards the error.
- A new composing helper `registerThisDevice({ prompt })` reports failures to
  **Sentry** (`area: push-registration`): a *warning* when permission is granted
  but the token is still unavailable, and a captured *exception* when the POST
  throws. TestFlight builds report under the `preview` environment, so the next
  occurrence is diagnosable.

### 3. Manual "register this device" escape hatch

On the notification-settings screen, a **"This device"** action that runs
`registerThisDevice({ prompt: true })` (prompting for permission if needed) and
shows the outcome via `Alert` — so a user who somehow ends up unregistered can
fix it themselves and *see* the result (the exact affordance whose absence made
this hard to diagnose).

### 4. (Deferred) backend "enabled but no device" signal

A user whose prefs are enabled but who has no `UserDevice` row is a detectable
"wants notifications, can't receive them" state. Worth surfacing later (a
dispatcher log/metric on `Suppressed_NoDevice` for opted-in users); **out of
scope** for this pass.

## Files

- `src/lib/notifications/pushNotifications.ts` — `getFcmTokenIfGranted()` returns
  `FcmTokenResult` (never prompts); no longer swallows the error.
- **New** `src/lib/notifications/registerPushDevice.ts` — `registerThisDevice({
  prompt })`: token → installationId → POST, with Sentry reporting on failure.
  The single place registration happens; web/undetermined no-ops cleanly.
- `src/hooks/useRegisterPushDevice.ts` — delegates to `registerThisDevice`,
  adds the `AppState` foreground re-attempt + session success flag, keeps the
  auth-change and token-rotation triggers.
- `app/settings/notifications.tsx` — "This device" section with the manual
  re-register action + `Alert` feedback.

## Tests

`registerThisDevice` is the testable core (mock `getFcmToken`/
`getFcmTokenIfGranted`, `devicesApi`, `getOrCreateInstallationId`, Sentry):
- token + POST ok → `{ ok: true }`.
- permission granted, no token → `{ ok: false }` + a Sentry warning.
- permission not granted → `{ ok: false }`, **no** Sentry noise.
- POST throws → `{ ok: false }` + a captured Sentry exception.

`tsc --noEmit` clean; Jest 29 suite green.

## How this resolves the reported iPad case

Once shipped: the iPad, already permitted, re-attempts on the very next
foreground and registers — the `UserDevice` row appears and pushes flow. If it
*still* fails, the Sentry event (with `permissionStatus` + the underlying error)
tells us exactly which of the three timings it was — the observability we lacked.
