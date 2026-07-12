# Account Deletion (anonymize model)

Status: implemented (mobile + web)
Last updated: 2026-07-12

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

The **web app** (`sd-ui`) offers the same deletion from Settings → Account. It
hits the identical `DELETE /user/me` endpoint (server logic is unchanged), then
tears the local session down exactly like web sign-out — `Auth.clearToken()` →
Firebase `signOut()` → toast → redirect to `/`. The confirmation is a two-step
inline confirm (deliberately **not** the shared `ConfirmationDialog`, whose
"Do not ask me again" option is unsafe for a permanent, irreversible action).
Web registers no push devices, so there is nothing device-side to unregister.

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

## Returning users (re-signup after deletion)

A deleted user can sign up again cleanly — the model is built for it:

- The Firebase user is **hard-deleted** (not disabled), so the email is released
  on Firebase's side and the next sign-in mints a **new `FirebaseUid`**.
- Anonymization frees our only two unique indexes (`FirebaseUid → deleted-{id}`,
  `Username → del_{id}`); `Email` is not unique-constrained.
- `UpsertUser` looks a user up **by `FirebaseUid`**, so the new uid isn't found
  and a **fresh `User` row** is created (new generated username, real email
  reused).

Consequences (by design, not bugs):
- **It's a brand-new account.** Old picks/standings stay anonymized as "Deleted
  user"; there is no restore or merge. "Same email = my old account back" is a
  UX expectation to manage in copy, not a behavior we provide.
- The old username is freed but **not reclaimed** — the returning user gets a
  generated handle.

Robustness note: the handler deletes the Firebase login **before** anonymizing +
`SaveChanges`. If the Firebase delete succeeds but the save fails and nothing
retries, an un-anonymized orphan row can linger (real email/name, `DeletedUtc`
null → not excluded from invites). A retried delete self-heals it; a
reconciliation sweep for "dangling deletions" is a possible future hardening.

## Out of scope
- Auto-linking providers for the same email.
- User-initiated data export.
- Undo / grace-period restore (deletion is immediate).
- Restoring/merging a returning user's prior history (they return as a new account).
