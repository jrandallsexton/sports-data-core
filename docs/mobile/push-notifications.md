# Mobile Push Notifications — Design

**Status**: Pipeline validated end-to-end on iOS (TestFlight build → Firebase Admin SDK send → device banner) on 2026-06-04. Mobile-side FCM token retrieval and the admin test endpoint are shipped (PRs #388, #394). The production-shaped dispatcher (`UserDeviceToken` auto-registration, `NotificationDispatchLog`, `PickDeadlineReminderJob`) is still future work.
**Scope**: Mobile-only (sd-mobile). Web notifications are deferred indefinitely; if ever added, they will be in-app, not browser-based.
**Last updated**: 2026-06-04

## Goal

Deliver actionable, deep-linked push notifications to the mobile app. The v1 use case is a **pick-deadline reminder**: ~10 minutes before a contest starts, ping each user who has unsubmitted picks for that contest with a notification they can tap to land directly on the picks page for that league, with that contest in focus.

Sample copy (v1): `"Pick deadline for {AwayShort} @ {HomeShort} is in 10 minutes."`

Mobile is the assumed dominant client surface (≥90% of traffic). Building for mobile first matches usage.

## Why FCM (not Expo Push, not OneSignal, etc.)

Firebase Auth is already in the stack. Using Firebase Cloud Messaging means:

- One vendor relationship, one set of credentials, one billing line.
- Admin SDK is well-supported in .NET (`FirebaseAdmin.Messaging`).
- iOS APNS relay is handled by FCM automatically (we still need the Apple Developer cert / APNS key on the Firebase project once).
- Tokens are FCM device tokens, not Expo opaque tokens — survives ejecting from Expo if we ever do.

Expo Push Service is simpler to integrate but adds a dependency we don't otherwise need and routes our push traffic through Expo's infrastructure. Pass for now.

## Component map

```
┌─────────────────────────────────────┐    ┌──────────────────────────┐
│ sd-mobile                           │    │ SportsData.Api           │
│                                     │    │                          │
│ NotificationProvider                │    │ POST /user/devices       │
│   ├─ request permission (1st run)   │───▶│   { token, platform }    │
│   ├─ FCM token → store + POST       │    │                          │
│   └─ foreground/background handlers │    │ POST /user/devices/:tok/ │
│                                     │    │   refresh                │
│ Notification tap                    │    │                          │
│   └─ expo-router push to            │    │ NotificationDispatcher   │
│      /(tabs)/picks?leagueId=…&      │    │   ├─ Firebase Admin SDK  │
│        contestId=…                  │    │   ├─ multicast batch     │
│                                     │    │   └─ idempotency log     │
│ PicksPage                           │    │                          │
│   └─ scroll/focus matchup card by   │    │ Hangfire recurring       │
│      contestId param                │    │   "PickDeadlineReminder" │
└─────────────────────────────────────┘    │   every 5 min            │
                                            └──────────────────────────┘
```

## Schema additions

### `UserDeviceToken` (API)

| Column            | Type                 | Notes                                     |
|-------------------|----------------------|-------------------------------------------|
| `Id`              | Guid PK              |                                           |
| `UserId`          | Guid FK → User       | Many tokens per user (multi-device).      |
| `Token`           | varchar(512), unique | FCM token. Treat as opaque.               |
| `Platform`        | enum ("ios","android") | For analytics + future routing.         |
| `AppVersion`      | varchar(20)          | For diagnosing token issues per release.  |
| `CreatedUtc`      | timestamptz          |                                           |
| `LastSeenUtc`     | timestamptz          | Bumped on every refresh ping.             |
| `DeactivatedUtc`  | timestamptz nullable | Set when FCM returns `Unregistered` / `InvalidRegistration` from a send attempt. |

Tokens churn — apps reinstall, users sign out, FCM rotates them. The deactivation flag is critical: when a send returns an error, we mark the token dead instead of deleting (audit trail), and the dispatcher only sends to non-deactivated tokens.

### `NotificationDispatchLog` (API)

| Column           | Type            | Notes                              |
|------------------|-----------------|------------------------------------|
| `Id`             | Guid PK         |                                    |
| `UserId`         | Guid FK         |                                    |
| `ContestId`      | Guid            | The contest the notification was about. |
| `NotificationKind`| enum            | "PickDeadlineReminder" for v1.    |
| `SentUtc`        | timestamptz     |                                    |
| `Status`         | enum            | Sent, Failed, Skipped.             |

Idempotency: the Hangfire job filters by `(UserId, ContestId, NotificationKind) NOT IN (existing rows)` before dispatching. Hangfire retry of a partially-completed batch won't re-send to users who already got the message.

## Notification payload

FCM `data` payload (mobile parses this and renders the notification + handles the tap):

```json
{
  "notification": {
    "title": "Pick deadline approaching",
    "body": "Pick deadline for TEX @ LAA is in 10 minutes."
  },
  "data": {
    "kind": "PickDeadlineReminder",
    "leagueId": "ff1ec6d7-...",
    "contestId": "82bda8a6-...",
    "deepLink": "sportdeets://picks/ff1ec6d7-...?contestId=82bda8a6-..."
  }
}
```

- `notification` block is what FCM/APNS uses for the system banner when the app is backgrounded.
- `data` block is what the app reads on tap. `deepLink` is the canonical URL; the per-field IDs are redundant but easier to consume in the handler without parsing the URL.
- Mobile registers `sportdeets://` as the URL scheme in `app.json` (already in place for the Firebase Auth dynamic-link flow — confirm).

### Deep-link routing (mobile)

`expo-router` handles the route resolution. The notification-tap handler in the root layout listens for `Notifications.addNotificationResponseReceivedListener`, extracts the `deepLink` (or builds it from `leagueId`/`contestId`), and `router.push(deepLink)`.

The `PicksPage` accepts an optional `contestId` query param: if present, after the matchup list renders, the page scrolls the matching `MatchupCard` into view and applies a brief highlight pulse so the user sees which game the notification was about. Same component, additive prop.

## Hangfire scheduling

A single recurring job `PickDeadlineReminderJob`, registered to fire every **5 minutes** (cron `*/5 * * * *`).

Per fire:
1. Query the API DB for contests with `StartDateUtc` between `now + 8 min` and `now + 12 min` (4-minute window absorbs the 5-min cron jitter while still landing in the "~10 minutes out" range).
2. Join to `PickemGroupMatchup` and `UserPick` to find users in any league containing the contest who have NOT submitted a pick.
3. For each `(User, Contest)` tuple, check `NotificationDispatchLog` — skip if already sent.
4. Resolve `UserDeviceToken` rows (active only). Build the FCM payload. Send via multicast (FCM supports up to 500 tokens per call).
5. Insert `NotificationDispatchLog` rows. Mark deactivated tokens on FCM error responses.

The 4-minute window means a contest is in the eligible band for one cron fire only — preventing duplicate sends without needing a more elaborate "scheduled send" model.

## Open questions / decisions

1. **iOS APNS setup**: needs an Apple Developer account + an APNS auth key uploaded to the Firebase project. Who owns that? (Likely the user, as solo founder.)
2. **Permission prompt timing**: ask on first launch, after first pick, or via a settings toggle? Default lean: ask after the user creates or joins their first league — the value is concrete by then. Track this as a UX call.
3. **Notification settings UI**: out of scope for v1, but the schema should support per-user mute (a `NotificationPreferences` table or a few bool columns on `User`). Defer the table until we have a second notification kind.
4. **Quiet hours**: not a concern for v1 (pick deadline is intrinsically tied to game time, which the user already cares about), but worth adding when we add other notification kinds.
5. **Token registration on logout**: clear the token from the server when the user signs out so a shared device doesn't keep getting notifications for the prior user.
6. **Per-league opt-out**: deferred. Default: notifications fire for every league the user is in. If a power-user is in 10 leagues, they'll get multiple notifications per game; live with that for v1.

## Future kinds (not v1)

- `ContestStarted` — heads up that a game in your league is now live (low signal, defer)
- `PickResultsReady` — week-end summary push when a week's contests all complete
- `LeaderboardShift` — your standing in a league changed (probably digest-based, not push)
- `LeagueInvite` — somebody invited you to a league
- `CommissionerAnnouncement` — broadcast within a league
- Web in-app (not browser push) — same backend dispatch, different transport

The schema (`NotificationKind` enum, dispatch log) is designed to absorb these without rework.

## Effort estimate

For v1 (single kind, mobile-only, no settings UI):
- **Mobile**: ~3 files — `NotificationProvider`, root-layout listener, `PicksPage` contestId-focus prop. Maybe a `useNotifications` hook.
- **API**: ~5 files — `UserDeviceToken` entity + migration, `NotificationDispatchLog` entity + migration, `DeviceTokenController`, `NotificationDispatcher` service, `PickDeadlineReminderJob`.
- **Firebase setup**: APNS key upload (one-time, ~30 min).
- **Total**: 1-2 focused days plus an EAS build cycle for permission flow + device-token round-trip testing.

## Validation notes & lessons learned (2026-06-03 → 2026-06-04)

End-to-end validation of the FCM pipeline ran into multiple stacked issues over ~24 hours. Most of them turned out to be red herrings; the actual root cause is captured at the bottom. Documenting the full diagnostic path because the failure modes here have a strong "looks like X, is actually Y" pattern and the surface-level error message (`Unregistered: Requested entity was not found`) is identical across many of them.

### Symptom

- `FirebaseAdmin.Messaging.FirebaseMessagingException: Requested entity was not found.`, surfaced to the API endpoint as `MessagingErrorCode = Unregistered` / `ErrorCode = NotFound`.
- **Identical failure** when bypassing the API and using Firebase Console's built-in "Send test message" feature directly. This is the most useful early diagnostic: if Firebase's own send fails the same way, the issue is between Firebase and APNs, not in our send code.

### What was ruled out before finding the real cause

In rough order of cheapness:

1. **Firebase project mismatch between API service account and device token.** Verified by checking `CommonConfig:Firebase:ClientEmail` — must contain the project ID matching the mobile `GoogleService-Info.plist` `PROJECT_ID`.
2. **Stale FCM token from a prior install.** Token caching survives app upgrades, including TestFlight upgrades. **Fix: full uninstall + reinstall, then call `getToken()` post-permission-grant.** The dev screen's "Refresh" button does not actually re-issue a token without a `deleteToken()` first (TODO in the dev screen).
3. **APNs Auth Key environment mismatch.** Apple's modern Auth Keys are explicitly environment-bound (Production vs Sandbox), visible in Apple Dev Console's Keys list with an "APNs Environment" column. The historical "one Auth Key works for both environments" guidance is **outdated**. TestFlight builds use Production APNs (which is correct), so the Production Auth Key needs to be uploaded to the Firebase **Production APNs auth key** slot. Uploading a Sandbox key to that slot causes silent rejection.
4. **Wrong Team ID in Firebase APNs config.** Firebase's APNs Auth Key upload UI asks for Key ID + Team ID alongside the `.p8`. Both must match the Apple Developer account that owns the key. Visible in Firebase Console → Project Settings → Cloud Messaging tab → Apple app configuration for each slot.
5. **FCM v1 API not enabled in the GCP project.** For freshly-created Firebase projects, FCM v1 isn't always auto-enabled. Visible in Firebase Console → Cloud Messaging tab as a banner at the top. Enable via the three-dot menu → "Manage API in Google Cloud Console" → Enable.
6. **Missing entitlement on the IPA.** Confirmed by whether the iOS permission prompt fires on first launch — if it does, the `aps-environment` entitlement is present in the signed IPA.
7. **`aps-environment` set to `development` instead of `production` on a TestFlight build.** Forced explicitly via `app.json`'s `ios.entitlements.aps-environment` to override any potential EAS misdetection. Defensive but didn't help in this case.
8. **Corrupted `.p8` file.** Apple's one-shot download policy means corruption can't be re-verified; only fix is to revoke and regenerate. Doing this didn't help in this case, but it's a cheap intervention.
9. **EAS credentials drift.** Verified with `eas credentials` — push notifications key shown as present and matching the bundle ID.
10. **Stale `GoogleService-Info.plist` in the repo.** Re-downloaded from Firebase Console and committed; values matched what was already there in this case.

### The actual root cause

After deleting and re-adding the iOS app entry in Firebase Console once, **Firebase reused the same `GOOGLE_APP_ID`** (`1:812654295319:ios:124e10809ef6bee99a1f52`). The `GoogleService-Info.plist` downloaded after that "re-create" was byte-identical to the previous version, and the underlying issue persisted because Firebase's server-side state still referenced the original iOS app entry.

Only on the **second** delete + re-add did Firebase mint a fresh `GOOGLE_APP_ID` (`1:812654295319:ios:b944b1d0e5f0ed139a1f52`). With the new plist embedded in the next EAS build, **uninstalled + reinstalled** on the device to force a token re-registration against the new entry, the round-trip worked immediately — both Firebase Console's test send and our API endpoint.

The takeaway: **after a delete + re-add of the iOS app entry in Firebase, always verify the new `GoogleService-Info.plist`'s `GOOGLE_APP_ID` is different from the previous one.** If it's the same, the re-create was a server-side no-op and you need to do it again.

### Diagnostic playbook for future "Unregistered" issues

If FCM returns `Unregistered` against a token that should be valid:

1. **Bypass our API first.** Test via Firebase Console → Messaging → "Send test message" with the same token. If Firebase's own send fails, the issue is in Firebase/APNs config, not in our code. If Firebase's send succeeds, the issue is in our API's Firebase Admin SDK configuration.
2. **Verify token freshness.** Uninstall the app from the device, reinstall (from TestFlight), open it, grant permission, call `getToken()` post-grant. The token displayed before that flow is suspect.
3. **Verify the iOS app entry exists in Firebase under the right project.** Firebase Console → Project Settings → Your apps → iOS app with bundle ID matching the IPA. Note the `GOOGLE_APP_ID`.
4. **Verify the `GOOGLE_APP_ID` matches what the device knows about.** Compare to `GoogleService-Info.plist`'s `GOOGLE_APP_ID` field. If they differ, the plist embedded in the build is stale.
5. **Verify APNs Auth Keys uploaded to both slots** (Cloud Messaging tab → Apple app configuration). Both rows should show a file, the correct Key ID, and Team ID `J3P72XEPJP`.
6. **Verify FCM v1 API is enabled** (Cloud Messaging tab → top banner).
7. **If all of the above check out:** delete and re-add the iOS app entry in Firebase. **Then verify the new plist's `GOOGLE_APP_ID` actually changed.** If not, repeat. Embed the new plist in a fresh EAS build, uninstall + reinstall, retry.

### Confirmed-working components

- Admin endpoint `POST /admin/notifications/test-push` (PR #394) — accepts FCM token + optional title/body/data, sends via `FirebaseMessaging.DefaultInstance.SendAsync`.
- Mobile FCM token retrieval via `@react-native-firebase/messaging`'s `messaging().getToken()` (PR #388). Token surfaced in a developer-only screen at `app/admin/push-token.tsx`, gated on `me?.isAdmin`.
- iOS notification permission prompt + system banner rendering with the SportDeets brand mark.
- Foreground notifications shown via `Notifications.setNotificationHandler` with `shouldShowBanner: true` (the default would silence them).
- Round-trip from API → FCM → APNs → device confirmed with custom title/body and the design-doc's literal `"Pick deadline for {AwayShort} @ {HomeShort} is in 10 minutes."` copy.

## Decision log

| Date       | Decision                                       | Rationale                                       |
|------------|------------------------------------------------|-------------------------------------------------|
| 2026-05-24 | FCM over Expo Push or other providers          | Firebase already in stack for Auth              |
| 2026-05-24 | Mobile only for v1                             | ≥90% traffic; web push if ever will be in-app   |
| 2026-05-24 | Per-user dispatch with idempotency log         | Personalization headroom; safe under Hangfire retry |
| 2026-05-24 | Single T-10 reminder for v1                    | Avoid over-notifying during MVP; iterate later  |
| 2026-05-24 | 5-min cron with 4-min eligibility window       | Single-fire guarantee without per-message scheduling |
| 2026-06-04 | When debugging FCM `Unregistered`, bypass our API via Firebase Console's "Send test message" first | Isolates whether the issue is in our send code or in Firebase ↔ APNs config; saves hours of false-trail debugging |
| 2026-06-04 | After a Firebase iOS app delete + re-add, ALWAYS verify the new `GoogleService-Info.plist`'s `GOOGLE_APP_ID` is different from the previous one | Firebase can silently reuse the old `GOOGLE_APP_ID` on first re-create; only a true ID change confirms the server-side state is fresh |
| 2026-06-04 | Treat Apple APNs Auth Keys as environment-specific (Production vs Sandbox) | Apple's modern UI labels them this way and the keys are not interchangeable across environments; historical "one key works for both" guidance is outdated |
