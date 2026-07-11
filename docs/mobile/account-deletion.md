# Account Deletion (anonymize model)

Status: implemented
Last updated: 2026-07-11

## Why

App Store Guideline 5.1.1(v) requires any app with account sign-up to offer
**in-app account deletion** (not just deactivation). GDPR/CCPA similarly require
removal of personal data on request.

## Model: anonymize, keep game history

sportDeets is a pick'em product built on shared league history and standings. A
hard delete of a departing user would corrupt co-players' leagues (their picks
feed weekly results and standings). So deletion **anonymizes** the user rather
than removing their rows:

- **Removed / neutralized:** login (Firebase auth user deleted), and all PII on
  the canonical `User` — email, username, display name, timezone, FirebaseUid —
  replaced with per-id sentinels. `DeletedUtc` is stamped.
- **Retained (anonymized):** picks, league memberships, standings, week results
  — so other users' leagues stay intact. In any UI the user now reads as
  "Deleted user".
- **Purged downstream:** the Notification service drops the user's devices,
  notification preferences, projection rows, and scheduled jobs (stops all push
  and reminders).

This satisfies "account deleted, can't log in, personal data removed" while
preserving the shared product.

## Flow

```text
Mobile (profile → Delete account → confirm)
  └─ DELETE /user/me
       API DeleteAccountCommandHandler:
         1. load User (by JWT user id); capture FirebaseUid
         2. Firebase Admin DeleteUserAsync(firebaseUid)   // kills login
         3. anonymize User row (PII → sentinels, DeletedUtc = now)
         4. SaveChanges
         5. publish UserDeleted
  └─ on 2xx: signOut() → welcome screen

Notification UserDeletedConsumer:
  purge UserDevice, UserNotificationPreferences, User projection,
  PendingScheduledJob, NotificationLog for that UserId
```

### Anonymization sentinels (canonical `User`)
- `FirebaseUid` → `deleted-{id:N}` (keeps the unique index satisfied; login already gone)
- `Email` → `deleted-{id:N}@deleted.invalid`
- `Username` → `del_{id:N}`, truncated to 30 (unique index)
- `DisplayName` → `Deleted user`
- `Timezone` → null; `EmailVerified` → false
- `DeletedUtc` → now

`GetInviteableUsers` excludes `DeletedUtc != null` so deleted users can't be
invited. Leaderboards/standings still show them as "Deleted user".

## Collision handling (folded in)

Sign-in with a provider whose email already exists under a different provider
(e.g. Google then Apple) surfaces Firebase `auth/account-exists-with-different-credential`.
The mobile sign-in libs catch it and show a clear message ("already registered
with X — use that method"). Full auto-linking is a deferred follow-up.

## Out of scope
- Auto-linking providers for the same email.
- User-initiated data export.
- Undo / grace-period restore (deletion is immediate).
