# Mobile Notifications & Live Updates — Scoping Plan

**Status**: Refreshed 2026-06-23 — pre-implementation, foundational decisions locked
**Target delivery**: Before 2026 NCAAFB kickoff (September 5, 2026) — ~10 weeks from today
**Primary driver**: Mobile engagement. The web app can survive on polling; the mobile app cannot.

**Transport + placement decisions locked (2026-06-23)**:

- **Live updates: SignalR** (retained, not retired as the April draft proposed). PR #441 broadcasts `ContestFinalized` over SignalR for picks-page updates; per-sport `*PlayCompleted` consumers also fan out via SignalR. Meaningful work has shipped on this path. The April-draft SSE migration is deferred indefinitely — revisit only if SignalR specifically causes a problem we can't solve in place.
- **Device push: Firebase Cloud Messaging (FCM)** (not Expo Push as the April draft proposed). Already wired via `@react-native-firebase/messaging` v24 in `sd-mobile`; APNS on iOS routes through Firebase too. Single SDK for both platforms, leverages the Firebase auth setup we already manage.
- **Service home: `SportsData.Notification`** (not `SportsData.Api`). API is already heading toward needing an Api/Worker role split like Provider and Producer; piling Hangfire jobs, MassTransit consumers, and background sweeps onto API accelerates that pressure. Notification service owns user-visible delivery from day one.
- **Migration is parallel, not destructive**: the proof-of-concept push endpoint at `SportsData.Api/Application/Admin/Commands/SendTestPushNotification/` stays in place until the equivalent path in `SportsData.Notification` is working end-to-end against a real `UserDevice`-registered token. Then the API endpoint retires in a small cleanup PR.

---

## 1. Why this matters now

The mobile app ships to phones that live in pockets, not browser tabs. Three capabilities are missing, all of which the web app either has or can live without:

1. **Real-time score + play updates** — already handled via SignalR (`ContestUpdatesContext` → SignalR hub), expanding incrementally. This pillar is no longer in scope here.
2. **In-app notifications** (banner / toast / inbox) for events while the app is open — pick submitted, preview ready, friend joined league, commissioner activity. Today: nothing.
3. **Device push notifications** for events while the app is *not* open — pick deadlines, kickoff reminders, league invitations. APN + FCM proof-of-concept landed a few weeks ago; nothing wired to a real event-driven sender yet.

This document scopes the remaining work (pillars B + C), pins the architecture, and breaks delivery into phases that can ship independently.

---

## 2. Ground-truth snapshot (June 2026)

### Backend

| Component | Status | Location |
|---|---|---|
| SignalR hub (`NotificationHub`) | **Active** — broadcasts `ContestFinalized` (PR #441), `ContestScoreChanged`, sport-specific `*PlayCompleted` | `SportsData.Api/Infrastructure/Notifications/NotificationHub.cs` |
| Azure SignalR registration | **Active** — production transport | `SportsData.Api/Program.cs` |
| Live game ESPN polling → DB | **Done** — Football and Baseball streamers built | `SportsData.Producer` |
| Domain event → SignalR fan-out | **Done** — MassTransit consumers in API push to SignalR | `SportsData.Api` |
| SendGrid email | **Done** — template-based, used for league invitations | `SportsData.Api/Infrastructure/Notifications/NotificationService.cs` |
| Twilio SMS | **Stub, out of scope** | — |
| `SportsData.Notification` service | **Empty shell** — scaffold exists, no consumers, no controllers, no Hangfire jobs | `SportsData.Notification/Program.cs` |
| FCM push proof-of-concept | **Done** — admin endpoint that sends one push to a given token via Firebase | `SportsData.Api/Application/Admin/Commands/SendTestPushNotification/` |
| Pick-deadline / reminder scheduling | **Missing** | — |
| Device token persistence | **Missing** — no `UserDevice` table yet; tokens are pasted into the admin proof-of-concept | — |
| Per-category notification preferences | **Missing** | — |
| Runtime | **.NET 10** | — |

### Web (`sd-ui`)

- `useSignalRClient` hook actively subscribes to `ContestFinalized`, `ContestScoreChanged`, and play-completed events. **Stays.**

### Mobile (`sd-mobile`)

- 30s `refetchInterval` on `useContestOverview` (game detail screen). Acceptable for current scope.
- `@react-native-firebase/messaging` v24 installed and registered as an Expo plugin.
- `expo-notifications` installed for foreground-state handling.
- No device-token registration flow yet — tokens for the proof-of-concept admin endpoint are pasted from a debug screen.

### Summary in one sentence

> SignalR-driven live updates are live and shipping. Push is wired at the platform layer (Firebase → APNS/FCM) with a working admin proof-of-concept. The missing pieces are: device-token persistence, user preferences, the production sender pipeline in `SportsData.Notification`, and any actual events fanning out to push.

---

## 3. Scope of work — two pillars

The April draft's **Pillar A (SSE live updates)** is removed. Live updates run on SignalR and stay there for the foreseeable future. The work below is Pillars B and C only.

### Pillar B — In-app notifications

**Goal**: while the app is open, surface non-blocking notifications for events the user cares about — pick confirmed, preview ready, friend joined league, commissioner activity.

**Inbox decision deferred**: persistent notification history (an inbox screen) is a v2 feature. v1 is toasts only.

**Backend changes**

1. Reuse the SignalR transport already in production. Define a `UserNotification` payload (`id`, `type`, `title`, `body`, `actionUrl?`, `createdUtc`) broadcast on a per-user SignalR group (`user-{userId}`).
2. `SportsData.Notification` is the fan-out consumer: it subscribes to MassTransit events (`PreviewGenerated`, `PickSubmitted`, `LeagueInvited`, etc.) and decides whether to publish a `UserNotification` to the per-user SignalR group AND/OR send an FCM push.
3. This is the **one** service that knows "what events produce what user-visible notifications." Lock that mapping in one place, not scattered across every consumer.
4. Scheduled notifications (deadline reminders) also live here — Hangfire recurring job or MassTransit scheduler, fired by timer instead of event.

**Mobile changes**

1. Toast component (or `react-native-toast-message` — ~5KB, well-maintained).
2. Subscribe to the `user-{userId}` SignalR group via existing `useSignalRClient` infrastructure, dispatch toasts.
3. Tap-to-navigate: if `actionUrl` is set, deep link to it (same expo-router paths we already use).

**Sizing**: 3–4 days, mostly backend (Notification service wiring up consumers for the events we care about).

### Pillar C — Device push notifications (iOS + Android)

**Goal**: notify the user even when the app is closed. Pick deadlines, kickoff reminders, league invitations.

**Stack**: **Firebase Cloud Messaging** (FCM on Android, FCM-routed-to-APNS on iOS). The mobile app already has `@react-native-firebase/messaging` v24 set up; the backend already has a proof-of-concept sender at `SendTestPushNotificationCommandHandler` proving the wire works end-to-end.

**Why FCM-via-Firebase, not Expo Push** (the April draft's recommendation):

- We already use Firebase for auth on both web and mobile. Firebase Messaging keeps us on a single vendor SDK we already manage.
- iOS routing through FCM means one wire format and one server-side SDK — no Expo intermediary, no `expo-server-sdk-dotnet` to wrap around (the package is effectively abandoned; the April draft already flagged this).
- Direct Firebase Admin SDK for .NET (`FirebaseAdmin`) is officially maintained by Google.
- Trade-off accepted: vendor lock-in on Firebase. We're already there for auth, so the incremental risk is zero.

**Backend changes**

1. **User device table**: new `UserDevice` table in `SportsData.Notification`'s `AppDataContext`. Fields: `UserId`, `FcmToken`, `Platform` (ios/android/web), `LastSeenUtc`, `NotificationsEnabled`. Indexed by `UserId`. Soft-delete on unregister.
2. **Registration API**: `POST /user/devices` with `{ fcmToken, platform }`. Upsert on `(UserId, FcmToken)`. `DELETE /user/devices/{id}` for unregister. Both endpoints in `SportsData.Notification`.
3. **Push-sender**: typed `IFcmPushSender` with `SendAsync(tokens, payload)`. Uses `FirebaseAdmin.Messaging.FirebaseMessaging`. Responsibilities behind the interface: batching, calling Firebase, classifying responses, flagging invalid tokens for cleanup.
4. **Fan-out**: the Notification consumer (Pillar B item 2) also sends an FCM push when the target user has registered tokens and `NotificationsEnabled=true` for the relevant category. One event → up to two delivery channels (in-app via SignalR + push via FCM), decided per-event-type via a small mapping.
5. **Scheduled notifications** (pick deadlines, kickoff reminders): Hangfire recurring job lives in `SportsData.Notification`. Initial targets (1 hour before pick deadline, 15 min before kickoff) are placeholders; **final windows deferred**.
6. **Invalid token cleanup**: Firebase returns invalid-token errors for uninstalled apps. A Hangfire job processes these errors and marks tokens inactive in `UserDevice`.

**Mobile changes**

1. On first sign-in after notification permission granted, call `messaging().getToken()` and `POST /user/devices`.
2. Permission prompt flow: **don't** prompt on app launch. Prompt after the user submits their first pick ("Want a reminder when picks are due next week?"). Contextual permission prompts convert dramatically better.
3. Foreground behavior: when a push arrives while the app is open, route through the Pillar B toast path instead of showing the OS banner. Firebase Messaging's `onMessage` handler covers this.
4. Tap-handling: deep-link to `data.actionUrl`.
5. Profile > Notifications screen: per-category toggles. Writes to a per-category preference table (**schema deferred**).

**EAS build implications**

- iOS push already configured during the proof-of-concept work — APNS key uploaded to Firebase project. Verify still current in `eas credentials` before Phase 2 starts.
- Android FCM credentials similarly already in place.

**Sizing**: 5–8 days. Biggest item in the plan. Mostly backend (Notification service + device table + FCM sender + Hangfire jobs), but the permission/UX flow on mobile deserves care.

### Migration strategy from existing API endpoint

The proof-of-concept push endpoint at `SportsData.Api/Application/Admin/Commands/SendTestPushNotification/` is the canary for "FCM is working in production." It **stays in place** during all of Phases 1–2 below. Once `SportsData.Notification` is fanning out real pushes against `UserDevice`-registered tokens end-to-end, the API endpoint retires in a small cleanup PR.

---

## 4. Phased delivery

Each phase ships independently. Ship in this order:

### Phase 1 — Notification service stand-up (~1 sprint)

- `SportsData.Notification` gets a real `AppDataContext` with `UserDevice` table + EF migration.
- Device registration API (`POST/DELETE /user/devices`) wired and tested.
- Mobile token-registration flow wired (contextual permission prompt + token POST after first pick).
- API push proof-of-concept endpoint **unchanged** in this phase.
- Deliverable: registered devices appear in `UserDevice` for real beta users.

### Phase 2 — Push sender in Notification service (~1 sprint)

- `IFcmPushSender` in `SportsData.Notification` using `FirebaseAdmin.Messaging.FirebaseMessaging`.
- Admin endpoint in Notification that mirrors the API one but reads tokens from `UserDevice` instead of taking them in the body.
- Once verified end-to-end (test push sent to a real registered device via the Notification endpoint), **retire the API endpoint** in a cleanup PR.

### Phase 3 — Event fan-out + in-app toasts (~1.5 sprints)

- Notification service subscribes to MassTransit events (start small: `PreviewGenerated`, `ContestFinalized`).
- Mapping table for event-type → channels (SignalR / FCM / SendGrid / combinations).
- Per-user SignalR group broadcasts implemented; mobile toast UI wired.

### Phase 4 — Scheduled reminders + preferences + observability (~1 sprint)

- Hangfire recurring job for "deadline in 1 hour" and "kickoff in 15 min" (final windows TBD).
- Per-category preferences table (schema TBD).
- Seq structured logging on send side + token lifecycle.
- Mobile Profile > Notifications toggles.

### Out of scope for the 2026 season

- In-app notification **inbox** (persistent history). Revisit Year 3.
- Rich push (images, actions, categories). Pure text gets us to launch.
- Web push (browser notifications). Web users have the app open; skip.
- SMS via Twilio. Costs and deliverability not worth it for a Pick'em app.

**Total sizing**: ~4 sprints (~4 weeks), which fits comfortably in the ~10-week pre-kickoff window.

---

## 5. Architecture notes / decisions

### One notification service owns event → channel routing

Every product-meaningful event (`PickSubmitted`, `PreviewGenerated`, `LeagueInvited`, `ContestStartingSoon`, `PickDeadlineApproaching`) gets routed through **one** consumer in `SportsData.Notification`. That consumer decides:

- Is this user in-app right now? → broadcast to per-user SignalR group.
- Does this user have push enabled for this category? → FCM push.
- Is this an email-worthy event? → SendGrid.
- Or any combination.

The alternative — sprinkling notification sends across each event's handler — produces duplicate deliveries, makes preferences impossible to centralize, and hides the full "what does the system notify about" inventory. Don't do the alternative.

Live-update transport consumers in `SportsData.Api` (the existing `ContestFinalized`, `ContestScoreChanged`, `*PlayCompleted` SignalR fan-outs) are an intentional exception: they fan out public game data to all viewers of a contest, not user-targeted notifications. They stay in `SportsData.Api`.

### Why a separate Notification service vs. piling onto API

API is already heading toward needing an Api/Worker role split (the same split Provider went through in PR #178 and Producer is planned for). Each new responsibility added to API — Hangfire jobs, MassTransit consumers, background sweeps — moves that day closer.

Notifications carry their own large bag of responsibilities: device tokens, recurring deadline jobs, event consumers, invalid-token cleanup, per-category preferences. Putting all of that in API both grows the API role-split pressure and obscures the notification surface (anyone working on it has to grep across the whole API project).

A dedicated service:

- Keeps API focused on its existing role (HTTP query/command handling + a few thin consumers).
- Surfaces "what does the system notify about" in one project — discoverable, owned, testable.
- Lets the role-split decision happen on **Notification's** timeline (probably never — it's worker-shaped from day one) instead of forcing it on API.

### SignalR retained, SSE migration deferred

The April draft proposed retiring SignalR for SSE. Since then, meaningful work has shipped on the SignalR path:

- PR #441 broadcasts `ContestFinalized` for picks-page live updates.
- Per-sport `*PlayCompleted` events fan out via SignalR consumers.
- `useSignalRClient` (web) is stable and proven.

The SSE migration's stated wins (.NET 10 first-class support, no Azure SignalR dependency, simpler client) remain real but no longer outweigh the cost of rewriting a working production path that's actively being extended. Revisit only if SignalR specifically causes a scaling or operational problem we can't solve in place.

### FCM via Firebase Messaging, not Expo Push

Already covered above in Pillar C. Single SDK, already-managed vendor, official maintenance, no third-party-package risk.

### Token lifecycle

- Device token is per-install, not per-user. Signing in/out rotates the **UserId** attached to the token, not the token itself. `POST /user/devices` is idempotent on `(UserId, FcmToken)` so the same physical device migrates between users cleanly.
- Tokens have no TTL. Firebase invalidates them on uninstall; we mark them inactive via the error-response mechanism. Do not try to expire them on our side.

---

## 6. Deferred decisions (revisit before Phase 4)

These are noted here so they don't get lost; lock them as Phase 4 approaches.

1. **Notification category list.** Starter shape: `pickDeadline`, `kickoffReminder`, `previewReady`, `leagueInvite`, `friendPick` (when social lands), `myPickResult`. Final list deferred.
2. **Scheduling windows.** April draft proposed 1 hour before pick deadline + 15 min before kickoff. Final windows deferred (may end up configurable per league).
3. **Preference storage shape.** `UserNotificationPreferences` table keyed by `(UserId, Category)` is the leading proposal — survives phone swap, scales to per-category UI. Schema details deferred.
4. **Observability log shape.** Every outbound notification should log `{userId, category, eventId, channel, result}` to Seq for support debugging. Final field set deferred.

---

## 7. Success criteria

By 2026 kickoff:

- [ ] `SportsData.Notification` is running in production and is the sole consumer responsible for user-visible notification delivery.
- [ ] `UserDevice` table exists; mobile app registers tokens after permission grant.
- [ ] At least one event-driven push fires end-to-end (e.g., `PreviewGenerated` → notification to the league commissioner).
- [ ] At least one scheduled push fires end-to-end (e.g., "picks due in 1 hour" Hangfire job).
- [ ] Mobile Profile > Notifications screen exists with per-category toggles.
- [ ] Seq dashboard shows per-category delivery rates.
- [ ] The proof-of-concept `SendTestPushNotification` endpoint in `SportsData.Api` is retired.

---

## 8. Handoff checklist before starting

- [ ] Confirm Firebase Admin SDK for .NET version (`FirebaseAdmin`) and credential injection pattern. Service account JSON likely already in place for auth — Notification service can reuse the credential.
- [ ] Confirm Notification service is registered in the Kubernetes config repo (`sports-data-config`). Manifests likely exist; pod isn't running because the service does nothing yet.
- [ ] Confirm Azure App Config has placeholders for Notification-specific config keys (FCM project ID, default sender name, etc.).
- [ ] Confirm the mobile app's notification permission UX copy (one line for the contextual prompt after first pick).
- [ ] Confirm the Notification service is included in the per-sport messaging topology (RabbitMQ broker assignment per `reference_per_sport_rabbitmq_split`).
