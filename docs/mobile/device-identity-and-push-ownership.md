# Device Identity & Push Ownership

Status: implemented. Issue: #475.

## Problem

An FCM token is **per app-install, not per user** — one device has one token `T`
regardless of who is signed in. The original model keyed a push registration on
`(UserId, FcmToken)`, so every account that signed in on a device created its own
row pointing at the same `T`. The dispatcher sends per user
(`WHERE UserId == target && NotificationsEnabled` → push to `d.FcmToken`), so
both accounts' notifications landed on whatever device held `T`, regardless of
who was currently signed in. With no sign-out cleanup, a signed-out account kept
receiving on the device. Low harm for one person's two accounts; a privacy leak
for genuinely different users sharing a device.

## Model

Each physical install mints a **stable installation id** (a v4 UUID, persisted
in `AsyncStorage`) that survives token rotation and account switches. The device
row is keyed on `InstallationId` (unique), so a device has exactly **one current
owner**:

- **Register** (sign-in, token refresh): upsert by `InstallationId`; set
  `UserId` + `FcmToken` to the current user/token. Registering under a different
  account **reassigns** the row instead of adding a second one — the prior owner
  immediately stops receiving on that device.
- **Unregister** (sign-out): delete the row scoped to
  `(InstallationId, UserId)` — best-effort, so it only removes your own current
  ownership and never blocks sign-out.
- **Dispatch**: unchanged (`WHERE UserId == target && NotificationsEnabled`).
  Correct because each install row has a single owner.

## Opt-out tradeoff

`NotificationsEnabled` is per-row (per-device). On a same-owner re-register it is
**preserved** (a token refresh must not silently re-enable a device the user
turned off). On an **owner change** it **resets to `true`** — the new owner
starts with notifications on. Today this is moot: per-device opt-out is not yet
writable anywhere (nothing sets `false`). If/when per-device opt-out ships and we
want a user's opt-out to survive switching away and back on a shared device, that
needs a separate per-`(UserId, InstallationId)` preference store — captured as
the deferred option in #475.

## Components

- Event contract: `SportsData.Core/Eventing/Events/Users/UserDeviceRegistered.cs`
  (carries `InstallationId`), `UserDeviceUnregistered.cs`.
- API: `SportsData.Api/Application/UI/Devices/` — `POST /ui/devices`,
  `DELETE /ui/devices/{installationId}` (both JWT-scoped; user resolved from the
  token, never the body).
- Notification: `UserDeviceRegisteredConsumer` / `UserDeviceUnregisteredConsumer`
  project into `UserDevices` (unique on `InstallationId`).
- Mobile: `src/lib/device/installationId.ts` (mint/persist),
  `src/hooks/useRegisterPushDevice.ts` (register), `app/(tabs)/profile.tsx`
  (`performSignOut` unregister).

## Migration note

The `AddInstallationIdToUserDevice` migration **clears `UserDevices`** before
adding the required, uniquely-indexed column — existing rows predate
`InstallationId` and can't be backfilled. The table is a self-rebuilding
projection: every device re-registers on its next app launch / token refresh.
