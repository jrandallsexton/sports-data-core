# Notification: dead FCM token pruning + per-device visibility

**Status:** Approved — implementing.
**Owner:** Randall.
**Scope:** `SportsData.Notification` service (send path). No mobile/API change.

## Problem

A `UserDevice` row can hold a **dead FCM token** — the app was uninstalled, the
token rotated, or the device was reset. FCM rejects sends to it forever
(`Unregistered` / `InvalidArgument`), but today we **never prune** it:

- The per-notification audit is **aggregate**: any one device succeeding →
  `"Sent"`. So a user with a live phone and a dead iPad token shows `"Sent"` and
  the dead device is invisible (only in the joined `FailureReason` string).
- Nothing removes the dead row, so every future send re-attempts it and fails.

`FirebasePushNotificationSender` already catches `FirebaseMessagingException` and
has `ex.MessagingErrorCode` in hand — its own comment says *"future
token-deactivation logic will branch on `ex.MessagingErrorCode` here."* This is
that follow-up.

## Design

### 1. Surface dead-token failures structurally
In `FirebasePushNotificationSender`, map the FCM error code to the result status:

- `Unregistered` (token no longer valid) and `InvalidArgument` (malformed token)
  → **`ResultStatus.NotFound`** — "this token/device is gone."
- Everything else — `Unavailable` / `Internal` / `QuotaExceeded` (transient) and
  `SenderIdMismatch` (a project-config error, not a per-token problem) → stays
  **`ResultStatus.Error`**.

Reuses the existing `ResultStatus` enum (no Core change); the FCM-specific
mapping stays in the sender. `NoOpPushNotificationSender` is unchanged.

### 2. Prune on dead token
A small shared extension `MarkDeadDeviceForRemoval(this AppDataContext, result,
deviceId, logger)`: when a send returns `Failure` with `Status == NotFound`, it
**marks the `UserDevice` row for deletion** (tracked `Remove`, no immediate save)
and logs a structured "pruning dead device" line. The consumer's/dispatcher's
existing terminal `SaveChangesAsync` (which writes the claim's outcome) flushes
the delete **in the same transaction** — no extra round-trip, no premature flush.

**Decision: delete, not disable.** The token is genuinely dead, so the row is
worthless; deleting it is clean and the device **re-registers on next launch**
via the resilient hook (#508). Marking `NotificationsEnabled = false` would
conflate with a user opt-out and leave a zombie row that never self-heals.

Applied at all six send loops: `ContestOddsUpdatedConsumer`,
`PickemGroupMemberAddedConsumer`, `UserInvitedToPickemGroupConsumer`,
`UserPickScoredConsumer`, and both `NotificationDispatcher` reminders.

### 3. Per-device visibility (minimal)
The prune log line + the existing per-device `FailureReason` are the visibility
for this pass. A dedicated per-device outcome table is **deferred** — the
self-healing (pruning) is the higher-value half and stands alone.

## Files

- `Infrastructure/Notifications/FirebasePushNotificationSender.cs` — map
  `Unregistered`/`InvalidArgument` → `NotFound`.
- **New** `Infrastructure/Notifications/DeadDevicePruning.cs` — the
  `MarkDeadDeviceForRemoval` extension + an `IsDeadTokenFailure` predicate.
- The four consumers + `NotificationDispatcher` — call the extension in the
  send-failure branch.

## Tests

- `IsDeadTokenFailure` / `MarkDeadDeviceForRemoval` unit tests (InMemory): a
  `NotFound` failure marks the row for deletion (gone after `SaveChanges`); an
  `Error` failure or a success leaves it.
- `UserPickScoredConsumer` integration test (mocked sender returns `NotFound`
  for the device): after `Consume`, the device row is pruned and the claim
  finalizes `Failed_FcmError` (no live device left).

## Notes

- Notification-service change → **won't deploy until the table-per-type rollout
  completes** (deploy hold). Codeable/mergeable now; takes effect at that deploy.
- Orthogonal to PR4 (OddsChange) / PR5 (retire `NotificationLog`): those change
  the **claim table**, not the send-failure handling — low conflict.
