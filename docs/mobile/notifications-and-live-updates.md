# Mobile Notifications & Live Updates — Scoping Plan

**Status**: Draft / not yet scheduled
**Target delivery**: Before 2026 NCAAFB kickoff (September 5, 2026) — ~4.5 months from today
**Primary driver**: Mobile engagement. The web app can survive on polling; the mobile app cannot. Push notifications (pick-deadline reminders) are the single biggest engagement lever the product is missing.

**Transport decision (2026-04-22)**: Server-Sent Events over SignalR. The full rationale lives in §5, but the short version: our usage is strictly one-way server→client, .NET 10 ships first-class SSE support (`TypedResults.ServerSentEvents`), SSE eliminates the Azure SignalR service tier as a dependency, and the client-side story is simpler (native `EventSource` on web, `react-native-sse` polyfill on mobile). The existing `NotificationHub` + `useSignalRClient` code is **deprecated** and will be removed as part of Phase 1.

---

## 1. Why this matters now

The mobile app ships to phones that live in pockets, not browser tabs. Three capabilities are missing, all of which the web app either has or can live without:

1. **Real-time score + play updates** while a user is watching their picks during a game. Today mobile polls `/contest-overview` every 30s — good enough to demo, embarrassing when the web feels ahead.
2. **In-app notifications** (banner / toast / inbox) for events that occur while the user is active — pick submitted, preview ready, friend joined league, etc. Today: nothing.
3. **Device push notifications** for events that occur while the user is *not* active — pick deadlines, kickoff reminders, league invitations. This is the engagement killer if we skip it.

This document scopes all three pillars, pins the architecture, and breaks the work into phases that can ship independently.

Related existing docs — skim before reading further:
- `docs/LiveGameStreaming-Complete.md` — the Producer-side live-streaming backend (already built, Phase 1 complete).
- `docs/LIVE_UPDATES_REFACTORING.md` — roadmap for Phase 2/3/4 of the streaming backend.
- `docs/mobile/mobile-app-overview.md` — "Must Have" list already flags push notifications as #1 critical.

---

## 2. Ground-truth snapshot (April 2026)

### Backend

| Component | Status | Location |
|---|---|---|
| SignalR hub (`NotificationHub`) | **To be retired** in Phase 1 — currently a stub with only `Ping` / `SendMessageToUser`, no domain events wired | `SportsData.Api/Infrastructure/Notifications/NotificationHub.cs` |
| Azure SignalR registration | **To be removed** in Phase 1 | `SportsData.Api/Program.cs` |
| Live game ESPN polling → DB | **Done** (Producer) — `FootballCompetitionStreamer`, 5 workers per game, 5–60s cadence | `SportsData.Producer` |
| Broadcast `ContestStatusChanged` to clients | **Missing** — event published to MassTransit, never forwarded to clients |
| SendGrid email | Done (template-based, used for league invitations) | `SportsData.Api/Infrastructure/Notifications/NotificationService.cs` |
| Twilio SMS | **Stub** — code commented out |
| `SportsData.Notification` service | **Empty shell** — no consumers, no controllers, no scheduled jobs |
| Pick-deadline / reminder scheduling | **Missing** — no Hangfire recurring jobs, no MassTransit scheduler usage |
| Device token table / API | **Missing** |
| Runtime | **.NET 10** (first-class SSE support via `TypedResults.ServerSentEvents`) |

### Web (`sd-ui`)

- `useSignalRClient` hook exists, connects to `/hubs/notifications`, listens for `PreviewGenerated` and `ContestStatusChanged` — but the backend never publishes those events to the hub, so the listener fires zero times today. This hook will be deleted in Phase 1 and replaced with a native `EventSource`-based equivalent.
- No polling fallback in the hook itself; live-game UX currently degrades silently.

### Mobile (`sd-mobile`)

- 30s `refetchInterval` on `useContestOverview` (game detail screen).
- 30s `staleTime` on `useMatchups` (picks tab).
- No SignalR or SSE dependency.
- No `expo-notifications`, `expo-device`, or FCM/APNS setup.
- Firebase SDK installed but used for auth only (no FCM).

### Summary in one sentence

> The *data* is live (Producer streams in real time); the *transport to clients* is polling; the *alert-me-when-I'm-away* path doesn't exist at all.

---

## 3. Scope of work — three pillars

### Pillar A — SSE live updates (mobile + web)

**Goal**: when a game's status, score, or play count changes, connected clients see it within <2s without a manual refresh.

**Transport**: Server-Sent Events. One-way HTTP stream, `text/event-stream`, standard bearer-token auth, no WebSocket upgrade dance. See §5 for the full SSE vs SignalR decision.

**Backend changes**

1. **Introduce `IEventStreamBroker`** — a singleton in the API service that maintains an in-memory registry mapping topic (`contest:{id}`, `user:{id}`) → set of subscriber `Channel<SseEvent>` instances. Exposes:
   - `Subscribe(topic, cancellationToken) : ChannelReader<SseEvent>` — called from the SSE endpoint.
   - `Publish(topic, payload)` — called from MassTransit consumers.
   On disposal (client disconnect), the subscriber is removed and its channel completed.

2. **Multi-pod fan-out via existing RabbitMQ**. The API runs >1 pod; a consumer in pod A must reach subscribers on pod B. Use a fanout exchange `sse.fanout` where every API pod binds a uniquely-named auto-delete queue. When `IEventStreamBroker.Publish` runs, it (a) notifies local subscribers immediately and (b) forwards the same envelope to `sse.fanout`. All pods see the envelope and dispatch to their local subscribers. No Redis dependency needed — we already have Rabbit in every environment.

3. **SSE endpoints** (minimal APIs on `SportsData.Api`):
   - `GET /events/contest/{contestId}` — public; anyone can subscribe.
   - `GET /events/user` — authenticated; derives `userId` from the Firebase JWT, subscribes to `user:{userId}`.

   Implementation uses .NET 10's `TypedResults.ServerSentEvents(...)` which handles framing, flushing, and cancellation automatically. Sketch:
   ```csharp
   app.MapGet("/events/contest/{contestId:guid}", (Guid contestId, IEventStreamBroker broker, CancellationToken ct) =>
       TypedResults.ServerSentEvents(
           broker.Subscribe($"contest:{contestId}", ct),
           new ServerSentEventsOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) }));
   ```

4. **Event bus → broker relay**. Thin MassTransit consumers translate domain events to SSE payloads:
   - `ContestStatusChangedConsumer` → `broker.Publish($"contest:{contestId}", new ContestStatusChangedSse(...))`
   - `PlayAddedConsumer` → same pattern (once the event exists; Phase 2 of the streamer roadmap).
   These consumers live in `SportsData.Api`, not in the Notification service — Pillar A is purely transport; the Notification service owns the "what do I send to whom" decision for in-app and push (Pillars B/C).

5. **Ingress configuration**. Self-hosted nginx buffers response bodies by default, which batches SSE events. Add to the API ingress manifest:
   ```yaml
   nginx.ingress.kubernetes.io/proxy-buffering: "off"
   nginx.ingress.kubernetes.io/proxy-read-timeout: "3600"
   ```
   Scope these annotations to the `/events/*` path prefix if nginx supports per-location annotations in our version; otherwise apply at the whole-Ingress level.

6. **Leagues-wide topic** (`league:{leagueId}`) — deferred. Don't build it in Pillar A; wait until there's a consumer.

**Web changes**

1. Delete `useSignalRClient` hook.
2. New `useLiveContest(contestId)` hook using native `EventSource` + Firebase JWT (passed via `fetch` polyfill; see §5 for the auth detail).
3. On `contestStatusChanged`, `queryClient.setQueryData(...)` splices the update into the existing React Query cache.
4. Fall back to existing 30s polling if the stream stays down >10s.

**Mobile changes**

1. Add `react-native-sse` (~3KB). Unlike web, RN doesn't ship `EventSource` natively; this polyfill is the actively-maintained standard and supports custom headers (needed for Firebase JWT).
2. New hook `useLiveContest(contestId)` — same shape as web's, different underlying client.
3. Reuse existing Firebase JWT; attach as `Authorization: Bearer <token>` header via the polyfill's options.
4. Background-state handling: close the stream on `AppState.change → background`, reopen on `active`. iOS will tear down the TCP connection anyway; doing it ourselves avoids dangling state.

**Scope NOT included here**

- Per-play animations, drive flow visualizations — product work, not transport work.
- Real-time standings updates — see Pillar B.
- Azure SignalR service — **actively removed** in Phase 1 (delete hub, delete registration, delete config key, cancel service).

**Sizing**: 4–6 days. Backend-heavier than the SignalR version because we own the broker and the multi-pod fan-out, but the client side is meaningfully simpler (delete a dependency, add a ~3KB polyfill). Includes the ingress annotation rollout and the SignalR deletion sweep.

### Pillar B — In-app notifications

**Goal**: while the app is open, surface non-blocking notifications for events the user cares about — pick confirmed, preview ready, friend joined league, commissioner activity.

**Decision point first**: do we need an "inbox" (persistent history) or is a toast/banner enough for v1? Recommendation: **toasts only in v1.** An inbox adds a `Notifications` table, a read/unread API, and a dedicated screen — all of which are nice-to-have but blow up scope. Ship toasts first; add the inbox in a Pillar B.2 if users actually request it.

**Backend changes**

1. Reuse the SSE transport from Pillar A. Define a thin `InAppNotification` payload (`id`, `type`, `title`, `body`, `actionUrl?`, `createdUtc`) published on the `user:{userId}` topic.
2. Wire the `SportsData.Notification` service as the **fan-out consumer**: it subscribes to MassTransit events (`PreviewGenerated`, `PickSubmitted`, `LeagueInvited`, etc.) and calls `IEventStreamBroker.Publish("user:{userId}", ...)` for users who should receive the notification. In multi-pod deployments the fanout exchange from Pillar A §2 carries the payload to the pod holding that user's subscription.
3. This is the *one* service that knows "what events produce what user-visible notifications." Lock that mapping in one place, not scattered across every consumer.
4. Scheduling (Hangfire or MassTransit scheduler) for "deadline in 1 hour" reminders — same pattern, just fired by a timer instead of an event.

**Mobile changes**

1. Toast component (or `react-native-toast-message` if we want to skip writing one — ~5KB, well-maintained).
2. Hook `useInAppNotifications()` that subscribes to `/events/user` via `react-native-sse` and dispatches toasts.
3. Tap-to-navigate: if `actionUrl` is set, deep link to it (same expo-router paths we use everywhere).

**Sizing**: 3–4 days, mostly backend (Notification service wiring up consumers for each event type we care about).

### Pillar C — Device push notifications (iOS + Android)

**Goal**: notify the user even when the app is closed. Pick deadlines, kickoff reminders, league invitations.

**Stack decision**: **Expo Push Service**, not direct FCM/APNS.

Rationale:
- Expo SDK 55 managed workflow is already our baseline. Direct FCM on iOS means ejecting or using expo-dev-client + `@react-native-firebase/messaging`, both of which complicate CI (EAS) and add native build steps.
- Expo push sits on top of APNS + FCM. We send one HTTP call to `exp.host/--/api/v2/push/send`; it fans out. Same stack Discord, Coinbase, and others use in RN.
- Cost: free up to ~600 notifications/second. Our entire user base will be 2–3 orders of magnitude under this for years. If we ever outgrow it, migrating to direct FCM is a contained refactor.
- Trade-off accepted: vendor lock-in on Expo's push service. Offset by the fact that we'd be eating their SDK lock-in anyway.

**Backend changes**

1. **User device table**: new table `UserDevice` with `UserId`, `ExpoPushToken`, `Platform` (ios/android/web), `LastSeenUtc`, `NotificationsEnabled`. Index by `UserId`. Soft-delete on unregister.
2. **Registration API**: `POST /user/devices` with `{ expoPushToken, platform }`. Upsert on `(UserId, ExpoPushToken)`. `DELETE /user/devices/{id}` for unregister.
3. **Push-sender**: `IExpoPushSender` with `SendAsync(tokens, payload)`. Payload shape matches Expo's API (title, body, data, sound, badge). Batch up to 100 tokens per request. Use the `expo-server-sdk-dotnet` NuGet package — it handles batching, receipts, and invalid-token cleanup.
4. **Fan-out**: the `SportsData.Notification` service (same one from Pillar B) subscribes to the same events; when the target user has registered push tokens and `NotificationsEnabled=true`, it also sends an Expo push. One event → up to two delivery channels (in-app via SSE + push via Expo), decided per-event-type via a simple mapping table.
5. **Scheduled notifications** (pick deadlines, kickoff reminders): Hangfire recurring job or MassTransit delayed publish. Initial target: 1 hour before pick deadline, 15 min before kickoff (configurable later).
6. **Invalid token cleanup**: Expo returns `DeviceNotRegistered` receipts for uninstalled apps. A Hangfire job drains the receipts queue daily and marks tokens inactive.

**Mobile changes**

1. Add `expo-notifications` + `expo-device`.
2. On first sign-in after notification permission granted, call `Notifications.getExpoPushTokenAsync()`, POST to `/user/devices`.
3. Permission prompt flow: **don't** prompt on app launch. Prompt after the user submits their first pick ("Want a reminder when picks are due next week?"). Conversion rate is dramatically higher for contextual permission prompts.
4. Foreground behavior: when a push arrives while the app is open, route through the Pillar B toast path instead of showing the OS banner. Configurable via `setNotificationHandler`.
5. Tap-handling: deep-link to `data.actionUrl` (same contract as in-app notifications).
6. Profile > Notifications screen: toggle per-category (pick reminders, kickoff reminders, league activity). Writes to `UserDevice.NotificationsEnabled` and a per-category preference table (cf. Open Question 4).

**EAS build implications**

- iOS push requires an APNS key uploaded to Expo's credentials — one-time setup via `eas credentials`. Managed workflow handles the rest.
- Android requires an FCM server key in Expo's dashboard — same deal.
- Neither requires ejecting. Both are handled in `eas.json` and Expo's secure credential store.

**Sizing**: 5–8 days. Biggest item in the plan. Mostly backend (Notification service + device table + Expo sender + Hangfire jobs), but the permission/UX flow on mobile deserves care.

---

## 4. Phased delivery

Each phase ships independently. Ship in this order:

### Phase 1 — SSE for live games (Pillar A)
- Visible win; users see scores update without pulling down to refresh.
- Unblocks Pillar B because the SSE broker is the transport.
- Also retires SignalR + Azure SignalR service entirely.
- **~1 sprint (1 week).**

### Phase 2 — Push foundation (Pillar C, registration + ad-hoc send)
- Device table + registration API + Expo sender + one end-to-end trigger (e.g., "preview ready" since it's the easiest).
- No scheduled jobs yet; no toast UI yet. Just prove the rails.
- **~1 sprint (1 week).**

### Phase 3 — Scheduled push + in-app toasts (Pillars B + C, deadline reminders)
- Hangfire job for "deadline in 1 hour."
- Toast UI on mobile.
- Notification service becomes the one consumer that owns event → channel routing.
- **~1.5 sprints (1.5 weeks).**

### Phase 4 — Preferences, invalid-token cleanup, observability
- Profile > Notifications UI.
- Receipts job.
- Seq structured logging on both send side and client receipt side so we can debug "why didn't I get notified."
- **~0.5 sprint (2–3 days).**

### Out of scope for the 2026 season
- In-app notification **inbox** (persistent history). Revisit Year 3.
- Rich push (images, actions, categories). Pure text gets us to launch.
- Web push (browser notifications). Web users have the app open; skip.
- Silent-push data sync. Not needed; SSE covers live data while foreground.

**Total sizing**: ~4 weeks of focused work, which fits comfortably in the 4.5-month pre-kickoff window.

---

## 5. Architecture notes / decisions

### Why SSE over SignalR

We're on .NET 10, and our usage is strictly server→client fan-out (score updates, notifications). That's SSE's sweet spot.

**Wins**:
- **.NET 10 first-class support**. `TypedResults.ServerSentEvents` handles framing, keep-alives, JSON serialization, and client-disconnect cancellation. Endpoint code collapses to ~15 lines.
- **No Azure SignalR service**. One fewer vendor, no tier-upgrade blocker, no separate connection-string config. Concurrency is bounded by Kestrel/pod capacity — plenty at our scale.
- **Client simplicity**. Web uses native `EventSource` (built into every browser since ~2011). Mobile uses `react-native-sse` (~3KB) instead of `@microsoft/signalr` (~70KB).
- **Observability**. SSE is just a long-lived HTTP GET. Shows up in OTel traces, Seq logs, and ingress metrics the same way any other request does. No separate SignalR dashboard story.
- **Auth**. Standard `Authorization: Bearer <jwt>` header. Web note: browser `EventSource` doesn't support custom headers, so we either (a) use a `fetch`-based polyfill like `@microsoft/fetch-event-source` — same library Microsoft uses internally — which accepts headers, or (b) pass the JWT as a query-string parameter over HTTPS. The polyfill is cleaner and ~5KB.

**Costs / caveats**:
- **No built-in groups**. We own `IEventStreamBroker` and multi-pod fan-out. Mitigated by reusing existing RabbitMQ — no new infrastructure.
- **nginx buffers SSE by default**. Mitigated by `proxy-buffering: off` annotation (Pillar A §5).
- **Each subscriber holds one Kestrel request open**. At our scale (tens to low hundreds of concurrent live-game viewers) this is free; at 10k+ concurrent WebSockets would start to win on efficiency. Revisit if we ever approach that load.

**What we give up**: bi-directional RPC from the client back to the server. We don't use it, we don't plan to, and if that ever changes we add a regular HTTP endpoint — not a new transport.

### Single notification service owns event → channel routing

Every product-meaningful event (`PickSubmitted`, `PreviewGenerated`, `LeagueInvited`, `ContestStartingSoon`, `PickDeadlineApproaching`) gets routed through **one** consumer in `SportsData.Notification`. That consumer decides:

- Is this user in-app right now? → publish to `user:{userId}` SSE topic (in-app toast).
- Does this user have push enabled for this category? → Expo push.
- Is this an email-worthy event? → SendGrid.
- Or any combination.

The alternative — sprinkling notification sends across each event's handler — produces duplicate deliveries, makes category preferences impossible to centralize, and hides the full "what does the system notify about" inventory. Don't do the alternative.

Pillar A's transport-only consumers (`ContestStatusChangedConsumer`, `PlayAddedConsumer`) are an intentional exception: they're fanning out public game data to all viewers, not making "should this user be notified" decisions. They stay in `SportsData.Api`.

### SSE topic naming

- `user:{userId}` — private to a user; in-app notifications and per-user push confirmations. Authenticated endpoint; topic is derived from the JWT, never from a client-supplied parameter.
- `contest:{contestId}` — public; scores and play updates. Anyone can subscribe; no RBAC.
- `league:{leagueId}` — deferred until there's a consumer. Don't pre-build.

### Token lifecycle

- Device token is per-install, not per-user. Signing in/out rotates the **UserId** attached to the token, not the token itself. This means `POST /user/devices` is idempotent on `(UserId, ExpoPushToken)` and the same physical device can migrate between users cleanly.
- Tokens have no TTL. Expo invalidates them on uninstall via the receipts mechanism. Do not try to expire them on our side.

### AsyncStorage prerequisite

The Firebase auth persistence TODO in `src/lib/firebase.ts` is a blocker for Pillar C's UX — we can't prompt the user to enable notifications during sign-up if they're signed out on every app restart. This unblocks several other mobile features too; bundle its fix into Phase 2.

---

## 6. Open questions

1. **Notification categories**: finalize the list. Starter: `pickDeadline`, `kickoffReminder`, `previewReady`, `leagueInvite`, `friendPick` (once social lands), `myPickResult`.
2. **Per-league opt-out**: should I be able to silence notifications for a specific league (e.g., "my work league — don't buzz me")? Recommendation: **defer.** Per-category global toggles are enough for v1.
3. **Quiet hours**: "don't notify me between midnight and 7 AM local." Nice-to-have. Defer.
4. **Preference storage**: category toggles live in a new `UserNotificationPreferences` table keyed by `(UserId, Category)` — not on `UserDevice` — so a user's preference survives a phone swap.
5. **SMS**: keep commented out. Twilio's per-message cost and spam-filter heuristics make it a bad fit for a Pick'em app.
6. **Web push**: skip. Reconsider only if a "use sportDeets on your desktop" user segment emerges.
7. **Observability wiring**: every outbound push should log `{userId, category, eventId, channel, result}` to Seq. This is how we debug "I didn't get notified" support tickets, which will be ~40% of inbound once launched.
8. **SSE JWT on web**: pick `@microsoft/fetch-event-source` polyfill (Authorization header) vs query-string token. Polyfill is strictly cleaner; confirm no CSP blockers on our domain.

---

## 7. Success criteria

By 2026 kickoff:
- [ ] A user viewing the picks tab during a live game sees scores change within 2s of the underlying ESPN update.
- [ ] A user who opts into push notifications during onboarding receives a "picks are due in 1 hour" notification accurately.
- [ ] A user with the app closed still gets a notification when a league commissioner invites them.
- [ ] `SportsData.Notification` service is the sole consumer responsible for user-visible notification delivery; no other consumer calls SSE broker `Publish` for user topics or Expo directly.
- [ ] iOS and Android production builds on TestFlight / internal testing with push working end-to-end.
- [ ] Seq dashboard shows per-category push delivery rates.
- [ ] `NotificationHub`, `useSignalRClient`, and the Azure SignalR service registration are all deleted.

---

## 8. Handoff checklist before starting

- [ ] Fix AsyncStorage Firebase persistence (prereq, flagged in `src/lib/firebase.ts`).
- [ ] Confirm .NET 10 `TypedResults.ServerSentEvents` API surface against the version we're actually on (quick sanity check — API is stable but worth confirming).
- [ ] Confirm nginx ingress version supports per-path `proxy-buffering: off` annotation (or accept whole-Ingress scope).
- [ ] Decide final notification category list.
- [ ] Acquire APNS key + FCM server key for EAS credentials.
- [ ] Validate `expo-server-sdk-dotnet` is still maintained (last checked April 2026).
- [ ] Decide web SSE auth approach (`@microsoft/fetch-event-source` polyfill vs query-string token).
