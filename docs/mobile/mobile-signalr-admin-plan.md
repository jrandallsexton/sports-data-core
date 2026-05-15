# Mobile SignalR Live Updates + Sport-Agnostic Admin Page — Plan

**Status:** In Progress — Phase 1 in PR [#325](https://github.com/jrandallsexton/sports-data-core/pull/325) on 2026-05-15; Phases 2–3 pending
**Author:** planning pass, 2026-05-15
**Target:** sd-mobile (Expo SDK 55, RN 0.83.2)
**Related docs:** `docs/mobile/notifications-and-live-updates.md` (future SSE migration), `docs/web-signalr-surface-audit.md`

---

## 1. Goals

1. Stand up a real-time live-updates pipeline in `src/UI/sd-mobile` that consumes the same SignalR hub (`/hubs/notifications`) the web app consumes today, with the same three event types: `ContestStatusChanged`, `FootballPlayCompleted`, `BaseballPlayCompleted`. (`PreviewGenerated` is intentionally out of scope for v1 — see §11.)
2. Make `MatchupCard.tsx` re-render against live data the moment events arrive, using the same enrichment pattern the web's `PicksPage` and `AdminBaseballPage` use (live-record merged over the static REST payload via nullish fallback).
3. Add a single sport-agnostic admin screen at `/admin` that consolidates what the web ships as two separate pages (`AdminFootballPage`, `AdminBaseballPage`). The screen offers a sport dropdown (FootballNfl / FootballNcaa / BaseballMlb), a contestId text input, a Load matchup button, a Trigger replay button, a rendered `MatchupCard`, and a scrolling debug-event log.
4. Validate end-to-end on a dev-client build against the staging API while a real MLB game is in progress (or via the admin replay endpoint against a recently-completed game).

## 2. Non-goals

- SSE migration. `docs/mobile/notifications-and-live-updates.md` proposes replacing SignalR with SSE before the 2026 NCAAFB season. This plan ships against today's SignalR pipeline because: (a) the SSE work is unscheduled, (b) MLB is live now and the mobile gap exists now, (c) the web app would still be on SignalR concurrently. The mobile SignalR client lifetime in this plan is treated as 3–9 months — short enough that we should avoid premature abstraction.
- Push notifications (FCM/APNS). That is its own scope.
- Polling fallback. The 30s `staleTime` on `useMatchups` already gives us a soft fallback; the live channel is purely additive.
- `PreviewGenerated` event. The mobile app has no `react-hot-toast` equivalent wired today and the existing in-flight preview UX is unfinished.
- Pure SignalR debug card port (`FootballDebugCard` / `BaseballDebugCard` from web). The mobile admin page exposes only the more valuable real-replay flow; the synthetic-event sandbox stays a web tool.

## 3. Current state (verified findings)

### Mobile app

- Expo SDK 55, React Native 0.83.2, React 19.2.0, `expo-router` (file-based routing, main = `expo-router/entry`). Confirmed in `src/UI/sd-mobile/package.json`.
- State management is Zustand + TanStack Query. There is exactly one React Context in the app — `ThemeContext` (`src/lib/theme/`). Auth state, the most "global" mobile state, is a Zustand store (`src/stores/authStore.ts`).
- Auth: Firebase 12.10.0. `onAuthStateChanged` is wired in `src/hooks/useAuth.ts` and pushed into the Zustand store. The axios client (`src/services/api/client.ts`) attaches `Bearer ${user.getIdToken()}` per request via an interceptor — the canonical pattern for "get a Firebase JWT now."
- `me.isAdmin` already exists on the mobile side: `app/create-league.tsx` reads `me?.isAdmin === true` from the existing `useCurrentUser` hook (lines 144–239). The MLB-onboarding admin gate is already live.
- `MatchupCard.tsx` at `src/components/features/games/MatchupCard.tsx` is static-only: it consumes `matchup.status`, `matchup.awayScore`, etc., but never subscribes to a live source. `GameStatus.tsx` already renders InProgress UI when `status === 'inprogress'` — once we feed it merged data, the InProgress branch will light up.
- `useMatchups.ts` is REST-only, 30s staleTime, no SignalR awareness.
- No SignalR library installed. `@microsoft/signalr` is absent from `package.json`.
- No admin routes. `app/` contains `(auth)`, `(tabs)`, `create-league.tsx`, `+not-found.tsx`. There is no `admin/` route group.
- Expo Router structure: `(tabs)` is the authenticated home. Auth gate lives in root `_layout.tsx`'s `AuthGuard` component, which redirects to `/(auth)/sign-in` when no user.

### Web app reference (canonical, mirror these)

- `src/UI/sd-ui/src/hooks/useSignalRClient.js` — builds a single `HubConnectionBuilder` against `${REACT_APP_SIGNALR_URL || REACT_APP_API_BASE_URL}/hubs/notifications`, attaches `accessTokenFactory` that calls `getAuth().currentUser.getIdToken()`, uses `withAutomaticReconnect()`, registers four `connection.on(...)` handlers, returns the connection ref.
- `src/UI/sd-ui/src/contexts/ContestUpdatesContext.jsx` — single React Context holding `{ contests: { [contestId]: liveRecord } }`. Exposes `handleStatusUpdate`, `handleFootballPlayCompleted`, `handleBaseballPlayCompleted`, `getContestUpdate(contestId)`, `hasLiveUpdate(contestId)`, `clearContestUpdate(contestId)`, `clearAllUpdates()`. Critical detail — the PR #322 self-heal: receiving a `*PlayCompleted` event forces `status: 'InProgress'` because SignalR has no buffer and post-connect clients miss any earlier `ContestStatusChanged`. Mirror exactly.
- `src/UI/sd-ui/src/MainApp.jsx` lines 88–127 — shows the wire-up: context handlers from `useContestUpdates()`, wrapped in `useCallback` so the SignalR effect doesn't tear down/reconnect on every render.
- `src/UI/sd-ui/src/components/admin/AdminBaseballPage.jsx` and `AdminFootballPage.jsx` — pattern: contest-id-from-localStorage, league-from-localStorage (football only), Load matchup button (REST), Start replay button (POST), real `<MatchupCard>` rendered with `enrichedMatchup` merging static + live via nullish fallback, plus a `BaseballLiveStatePanel` / `FootballLiveStatePanel` debug readout.
- `src/UI/sd-ui/src/api/adminApi.js` — endpoints: `GET /admin/baseball/contests/{id}/matchup`, `GET /admin/football/contests/{id}/matchup?league=ncaa|nfl`, `POST /admin/baseball/contests/{id}/replay`, `POST /admin/football/contests/{id}/replay?league=ncaa|nfl`.

### Backend (verified)

- Hub mapped at `/hubs/notifications` (`src/SportsData.Api/Program.cs:366`). Production URL is the API host.
- Events emitted today by `IHubContext<NotificationHub>.Clients.All.SendAsync(...)`: `ContestStatusChanged`, `FootballPlayCompleted`, `BaseballPlayCompleted`, `ContestScoreChanged`, `ContestRecapArticlePublished`, `ContestOddsUpdated`, `PreviewGenerated`. The first three are what mobile needs for v1.
- Firebase JWT auth on the hub — same scheme as REST.

## 4. Library choice

`@microsoft/signalr@^9.0.6` (latest 9.x at install time). Decision is forced — it is the only first-party JS client for SignalR negotiation, and our backend uses `MapHub` (not raw WebSocket). Alternatives:

- Rolling our own WebSocket client. Rejected: re-implements negotiate/protocol/reconnect for zero benefit and forks us from web.
- Switching to SSE first. Rejected: see §2.

Compatibility check items the implementation must verify on first install:
- React Native 0.83.2 ships a working WebSocket polyfill; `@microsoft/signalr` defers to global `WebSocket` and `XMLHttpRequest`. No EventSource polyfill needed for the WebSocket transport.
- Hermes engine (Expo 55 default). The `@microsoft/signalr` package has had Hermes issues historically with `MessagePack` protocol — stay on default JSON protocol. No `@microsoft/signalr-protocol-msgpack`.
- Metro `transformIgnorePatterns` in `package.json` (the Jest config) — `@microsoft/signalr` ships ES modules; if Jest barfs, add `@microsoft/signalr` to the negative-lookahead group.
- Expo Go vs dev-client: `@microsoft/signalr` is pure JS, no native code, so Expo Go is fine. No prebuild required.

## 5. Architecture

```text
                 (auth user signs in)
                          │
                          ▼
            ┌──────────────────────────────┐
            │ app/_layout.tsx              │
            │  QueryClientProvider         │
            │   ThemeProvider              │
            │    <Stack/>                  │
            │    <AuthGuard/>              │
            │    <SignalRGate/>  ◄── only mounts useSignalRClient when isAuthenticated
            └──────────────────────────────┘
                          │
                          ▼
            ┌──────────────────────────────────────────────┐
            │ useSignalRClient (hook)                      │
            │  builds HubConnection, accessTokenFactory =  │
            │  () => getAuth().currentUser?.getIdToken()   │
            │  registers .on('ContestStatusChanged', …)    │
            │            .on('FootballPlayCompleted', …)   │
            │            .on('BaseballPlayCompleted', …)   │
            │  + AppState listener: background → stop,     │
            │                       foreground → start     │
            │  + NetInfo listener: connectivity loss → log │
            └──────────────────────────────────────────────┘
                          │
                          ▼ (writes to Zustand store)
            ┌──────────────────────────────────────────────┐
            │ contestUpdatesStore (Zustand)                │
            │   contests: { [contestId]: liveRecord }      │
            │   actions: handleStatusUpdate,               │
            │            handleFootballPlayCompleted,      │
            │            handleBaseballPlayCompleted,      │
            │            clearContestUpdate, clearAll      │
            │   selectors: useContestUpdate(contestId)     │
            └──────────────────────────────────────────────┘
                          │
                          ▼ (selector subscription)
            ┌──────────────────────────────────────────────┐
            │ MatchupCard (or any consumer)                │
            │   const live = useContestUpdate(contestId);  │
            │   const enriched = useMemo(() =>             │
            │     merge(matchup, live), [matchup, live]);  │
            └──────────────────────────────────────────────┘
```

### State management decision — Zustand, not React Context

The web uses Context because `MainApp.jsx` was already context-shaped before live updates existed. The mobile codebase is uniformly Zustand for global state (auth) and TanStack Query for server state. Introducing a new React Context just for this would be inconsistent and adds re-render gotchas (every consumer re-renders on any contest update unless you split contexts).

A Zustand store with **per-contest selector hooks** is strictly better here:

```ts
// pseudocode — not the final file
const useContestUpdatesStore = create<State>(...)
export const useContestUpdate = (contestId: string | undefined) =>
  useContestUpdatesStore(s => contestId ? s.contests[contestId] : undefined);
```

Only cards whose `contestId` actually changed re-render. The web's Context broadcasts to every consumer on every event; on mobile with a FlatList of 16 picks during a Sunday slate, that's bad. **Recommendation: Zustand store.**

Functional parity with the web is straightforward — same actions, same self-heal logic, same shape.

### Connection lifecycle — mount in an auth-gated subtree

The SignalR connection must not start before there is a Firebase user (otherwise `accessTokenFactory` returns `null` and the negotiate fails 401). Two options:

1. Mount `useSignalRClient` in root `_layout.tsx` and guard inside the hook (`if (!user) return;`).
2. Introduce a small `<SignalRGate>` component that mounts only the hook, rendered from inside `AuthGuard`'s success branch.

Option 2 is cleaner and matches the way `ThemeProvider` is structured. The hook still needs an internal user check because Firebase token refresh can momentarily return null mid-session.

### Mobile lifecycle differences from web — explicit handling

Web's `useSignalRClient` relies on the browser's tab-visibility / WebSocket keepalive behavior; mobile cannot. Two mobile-only behaviors to add:

- **AppState listener**: when the app moves to `background`, call `connection.stop()`. When it returns to `active`, call `connection.start()` again. Reason: iOS aggressively kills sockets that idle in the background and `withAutomaticReconnect` can spin pointless retries while the JS thread is paused. Use `AppState` from `react-native`.
- **NetInfo listener** (optional v1.5): subscribe to `@react-native-community/netinfo` and force a reconnect on network type change. `@microsoft/signalr.withAutomaticReconnect()` covers the basic cases but is slow to detect Wi-Fi ↔ cellular handoffs; this is a nice-to-have.

Reconnect strategy: `.withAutomaticReconnect([0, 2000, 10000, 30000, null])` so we don't hammer on prolonged outages.

### Auth flow

`accessTokenFactory` is async and called per-connection-attempt by `@microsoft/signalr`. Implementation:

```ts
accessTokenFactory: async () => {
  const user = getAuth().currentUser;
  if (!user) return null;
  try {
    return await user.getIdToken(); // Firebase auto-refreshes when within 5 min of expiry
  } catch (err) {
    console.warn('[useSignalRClient] token fetch failed', err);
    return null;
  }
}
```

Identical to the web hook, with React Native–friendly error handling (no `console.error` which alarms Expo dev menu).

Token refresh story: a Firebase ID token lives 1 hour. `@microsoft/signalr` calls `accessTokenFactory` on the initial negotiate and on each reconnect. While the WebSocket is alive across the hour boundary, the server keeps trusting the connection (the JWT was validated at handshake). If the server later disconnects (e.g., during reconnect), the factory is called again and a fresh token flows. **No explicit refresh timer needed.**

### Admin route placement

```text
app/
  (auth)/
  (tabs)/
  admin/                ← new route group (no parentheses — visible in URL bar)
    _layout.tsx         ← AdminGuard wraps Stack
    index.tsx           ← the single sport-agnostic page
  create-league.tsx
```

`admin/_layout.tsx` reads `useCurrentUser().data?.isAdmin` and `router.replace('/(tabs)')` if false. Same pattern as the existing `AuthGuard` in root `_layout.tsx`. Reached by a hidden "Admin" link in `profile.tsx` only visible when `me.isAdmin === true` (matches existing admin gate in `create-league.tsx`).

### Sport dropdown UX

`SegmentedControl` already exists at `src/components/ui/SegmentedControl.tsx` (used in profile.tsx). Three options fit cleanly: `NFL` / `NCAAF` / `MLB`. This is idiomatic for the rest of the app and avoids pulling in a new picker library. Internally the segment maps to the API's sport enum (`FootballNfl` / `FootballNcaa` / `BaseballMlb`) and the replay endpoint (football endpoint expects `league=ncaa|nfl` query param; baseball endpoint takes no league qualifier).

### Debug panel design

A `FlatList` (or simple `ScrollView` with `inverted`) showing the most recent 50 events:

```text
12:04:31.847  FootballPlayCompleted  contest=abc…  per=Q3 clk=4:21  away=14 home=21
12:04:30.214  FootballPlayCompleted  contest=abc…  per=Q3 clk=4:48  away=14 home=21
12:04:21.012  ContestStatusChanged   contest=abc…  status=InProgress
```

Lives below the MatchupCard, dismissable via a header chevron (collapsed by default once a test is satisfying). Each event is appended via a separate small Zustand store (`adminDebugStore`) so the debug log doesn't slow down the main updates store.

## 6. Phase-by-phase plan

Each phase is its own PR.

### Phase 1 — SignalR plumbing + contest updates store (no UI changes)

Goal: stand up the live data pipeline behind the existing app surface. After this phase, `MatchupCard` is still static, but events flow into a store nothing yet reads. Easy to revert.

Files created:
- `src/UI/sd-mobile/src/services/signalR/connection.ts` — exports `createSignalRConnection({ url, accessTokenFactory, logger })`. Pure factory, no React.
- `src/UI/sd-mobile/src/hooks/useSignalRClient.ts` — owns the connection lifecycle. Accepts handler callbacks as props (matches web shape). Adds AppState listener.
- `src/UI/sd-mobile/src/stores/contestUpdatesStore.ts` — Zustand store, three action handlers + selector hooks (`useContestUpdate(contestId)`, `useHasLiveUpdate(contestId)`).
- `src/UI/sd-mobile/src/types/signalR.ts` — TS types for `ContestStatusChangedPayload`, `FootballPlayCompletedPayload`, `BaseballPlayCompletedPayload` (mirror the web's untyped object shapes). Generated by reading the C# event classes (`SportsData.Core` IntegrationEvents) so we don't drift.
- `src/UI/sd-mobile/__tests__/contestUpdatesStore.test.ts` — unit tests for the three handlers + self-heal behavior.
- `src/UI/sd-mobile/__tests__/useSignalRClient.test.ts` — mocked-connection tests for handler dispatch.

Files modified:
- `src/UI/sd-mobile/package.json` — add `"@microsoft/signalr": "^9.0.6"`. Audit `transformIgnorePatterns` in the Jest config block — likely needs `@microsoft/signalr` added.
- `src/UI/sd-mobile/app/_layout.tsx` — render a new `<SignalRGate>` inside the authenticated branch. Likely the simplest path: extract `<SignalRGate>` from `useAuth().isAuthenticated`.

Env: introduce `EXPO_PUBLIC_SIGNALR_URL` (falls back to `EXPO_PUBLIC_API_BASE_URL`). Same precedence as the web's `REACT_APP_SIGNALR_URL` / `REACT_APP_API_BASE_URL`.

CI: `npm install` in mobile CI will pull `@microsoft/signalr` — no native modules so EAS prebuild is untouched. Verify the GitHub Actions mobile workflow's `npm ci` step still passes; expected.

PR criteria: app builds, jest tests pass (including new ones), running on dev-client shows `[SignalR] connected` in Metro logs once a user signs in.

### Phase 2 — MatchupCard consumes live updates

Goal: existing screens light up live. No admin yet.

Files modified:
- `src/UI/sd-mobile/src/components/features/games/MatchupCard.tsx` — call `useContestUpdate(matchup.contestId)`, compute `enrichedMatchup` via `useMemo` with nullish fallbacks, pass enriched object down through `TeamRow`, `OddsRow`, `GameStatus`. Mirror exactly the web's `enrichedMatchup` shape from `AdminBaseballPage.jsx` lines 39–67 and `AdminFootballPage.jsx` lines 59–75 — both football and baseball fields, because the same MatchupCard renders all sports.
- `src/UI/sd-mobile/src/types/models.ts` — extend `Matchup` with the baseball live fields (`inning`, `halfInning`, `balls`, `strikes`, `outs`, `runnerOnFirst/Second/Third`, `atBatShortName`, `atBatPositionAbbreviation`, `atBatHeadshotUrl`, `pitchingShortName`, `pitchingPositionAbbreviation`, `pitchingHeadshotUrl`, `lastPlayDescription`, `lastPlayId`, `ballOnYardLine`). Football live fields already exist (`period`, `clock`, `possessionFranchiseSeasonId`, `isScoringPlay`).
- `src/UI/sd-mobile/src/components/features/games/GameStatus.tsx` — add a `BaseballGameStatusInProgress` branch (mirror web `BaseballGameStatusInProgress` already shipped on web). The football InProgress branch already exists per the file header read.
- `src/UI/sd-mobile/__tests__/MatchupCard.live.test.tsx` — new test: render MatchupCard, push an event into the store, assert the rendered text updates.

PR criteria: with the staging API and a running real MLB game (or admin replay from Phase 3 used out-of-order from the web's admin page), `picks.tsx` shows live scoreboard ticks on the in-progress card. No regression on Scheduled / Final cards.

### Phase 3 — Sport-agnostic admin page + replay

Goal: ship the dev tool.

Files created:
- `src/UI/sd-mobile/app/admin/_layout.tsx` — Stack + `AdminGuard` redirecting to `/(tabs)` when `!me.isAdmin`.
- `src/UI/sd-mobile/app/admin/index.tsx` — the single sport-agnostic page. Sport segmented control, contestId TextInput, Load button, Trigger replay button, MatchupCard, collapsible debug log.
- `src/UI/sd-mobile/src/services/api/adminApi.ts` — typed client for `/admin/{sport}/contests/{id}/matchup` (GET) and `/admin/{sport}/contests/{id}/replay` (POST). Endpoint shape:

  ```text
  sport=baseball       → /admin/baseball/contests/{id}/{matchup|replay}
  sport=football, league=ncaa|nfl → /admin/football/contests/{id}/{matchup|replay}?league={league}
  ```
- `src/UI/sd-mobile/src/stores/adminDebugStore.ts` — bounded ring buffer (50 events) Zustand store + `useAdminDebugLog()` selector. Separate from `contestUpdatesStore` so debug rendering doesn't trigger main UI re-renders.
- `src/UI/sd-mobile/src/hooks/useAdminEventTap.ts` — small hook that subscribes to the live SignalR handlers and forwards every event into `adminDebugStore`. Mounted only by the admin screen.
- `src/UI/sd-mobile/__tests__/admin/index.test.tsx` — basic render + interaction tests.

Files modified:
- `src/UI/sd-mobile/app/(tabs)/profile.tsx` — append an "Admin" link visible only when `me?.isAdmin === true`. `router.push('/admin')`.
- `src/UI/sd-mobile/src/hooks/useSignalRClient.ts` — extend to optionally take a `onAny` callback so the admin screen can attach its event tap without re-registering the per-event handlers. Alternative: route every handler through the debug store always (cheap; 50-event ring buffer). Pick the always-on route — simpler, no extra subscription plumbing, and an Admin user is the only one whose screen reads the buffer.

PR criteria: admin user signs in, taps Profile → Admin → MLB → pastes a recently-Final contest GUID → Load → MatchupCard renders → Trigger replay → debug log fills with `BaseballPlayCompleted` rows + the MatchupCard scoreboard ticks. Same flow works for NFL and NCAAF.

## 7. Open questions and decisions to make at PR time

1. **AsyncStorage persistence of `contestId` in admin page.** Web uses `localStorage`. Mobile equivalent is `@react-native-async-storage/async-storage` (already a dep). Worth doing for v1 ergonomics, but not load-bearing. Recommend yes.
2. **Should `useSignalRClient` live in the root layout or inside `(tabs)`?** Argument for root: notifications and live updates apply across the whole authenticated app, including future admin screen. Argument for `(tabs)`: the admin screen is the only place outside tabs that needs it, and that's an unauthenticated edge case we don't have today. Recommend root + auth gate.
3. **Crash on stale token.** `getIdToken()` can throw if Firebase decides the user is signed out. The web's hook returns null on error and lets SignalR fail. Mobile likely should do the same but emit a single toast (we have no toast lib installed — likely add one in a follow-up). Decision deferred.
4. **Should the admin screen poll `useMatchups`-style or use `getMatchupForContest`?** The web admin pages use a one-shot `getBaseballMatchupForContest`/`getFootballMatchupForContest`. Mirror that — no React Query needed for a one-shot. Recommend `useState` + `useEffect`.
5. **Phase 2.5 wedge — should `useContestOverview` (game detail screen) also consume live updates?** It currently polls every 30s. The merge logic would be identical to MatchupCard. Recommend ship in Phase 2 same PR if scope allows, otherwise defer to Phase 4 (not in this plan).
6. **Self-heal write loop on `*PlayCompleted`.** The web sets `setTimeout(() => clearScoringFlag, 2000)` on football scoring plays. On mobile, replicate as a per-contest debounced cleaner inside the store. Verify no leak when the screen unmounts mid-timeout. Recommend a single shared `setTimeout` registry keyed by contestId.
7. **What's the production SignalR URL?** Need to confirm — likely `https://api.sportdeets.com` (same host as REST). The web treats them as separable via `REACT_APP_SIGNALR_URL`. Mirror that with `EXPO_PUBLIC_SIGNALR_URL` even if it ends up unused.

## 8. Test plan

### Unit
- `contestUpdatesStore`: each of three handlers writes the expected fields; status self-heal promotes `Scheduled` → `InProgress` on first play event; scoring flash auto-clears.
- `useSignalRClient`: with a mocked `HubConnection` (DI'd via the factory), `.on(...)` handlers are registered for all three events; calling them dispatches to the store; AppState `background` triggers `stop`; `active` triggers `start`.

### Integration (dev-client on Android emulator / iOS simulator)
- Sign in as the dev test admin user.
- Go to picks; pick a league/week containing an in-progress game (or use admin replay).
- Verify in Metro logs: `[SignalR] connected`. Verify Seq shows the connection on the API side (filter by user).
- From the admin screen, trigger a baseball replay; expect 1 `ContestStatusChanged` + N `BaseballPlayCompleted` over ~N seconds, MatchupCard scoreboard ticks per event, debug log shows the full event stream.
- Background the app for >60 seconds; foreground; verify reconnect and a fresh `ContestStatusChanged` is requested (n/a — SignalR has no replay; this is just confirming the connection re-establishes).
- Toggle airplane mode for 10s; verify the reconnect kicks in within the configured backoff (`[0, 2000, 10000, 30000]`).

### Staging vs local
- Local Producer + local API + local broker is overkill for the mobile-side validation; staging API against a real MLB game is more representative of production behavior (TLS, real auth, real rate limits). Use staging.

### MLB-live caveat
- Don't deploy to TestFlight / internal Play track during a live MLB game window without verifying staging first; this is real production traffic going through the same hub.

## 9. Risks

- **SignalR being deprecated mid-flight by the SSE migration.** Mitigation: the abstraction surface (`createSignalRConnection`, `useSignalRClient`, three handler callbacks, one Zustand store) is small. If SSE replaces the hub in 3–6 months, only `useSignalRClient.ts` and `services/signalR/connection.ts` need to change — the store, the selector hooks, and `MatchupCard` enrichment stay identical. Document this in the file headers.
- **Hermes incompatibility with `@microsoft/signalr` internals.** Low probability — the library is widely used in RN — but worth a smoke test on day 1 of Phase 1.
- **Re-render storm during a high-scoring NFL game** (think 30+ events in a 90s window). Mitigation: the per-contest selector hook means only the in-view cards re-render. `FlatList` virtualization further limits scope.
- **Token refresh edge during a long-lived connection.** As noted in §5, the server doesn't re-validate the JWT mid-connection. If we deploy a backend change that adds per-message JWT validation, mobile clients will silently drop messages until reconnect. This is a backend-side concern, but worth flagging.
- **Admin page on Android keyboard.** The TextInput for contestId is GUID-length; on a small Android device the keyboard occluding both buttons is likely. Use `KeyboardAvoidingView` like `create-league.tsx` does.
- **Self-inflicted PicksScreen scope creep.** Phase 2 is "MatchupCard consumes live updates." The temptation will be to also fix the game-detail screen, the home screen, etc. Resist; ship them in follow-ups.
- **Debug log perf.** Don't append to React state on every event — that's a render per event. Append to the Zustand store with a stable selector; the FlatList re-renders only when the slice changes.

## 10. Out-of-scope follow-ups (forced surface area)

- `PreviewGenerated` toast — needs a mobile toast library (likely `react-native-toast-message` or roll a minimal one).
- Live updates on game-detail screen (`app/(tabs)/(details)/sport/[sport]/[league]/game/[id].tsx`).
- Push notifications (`docs/mobile/notifications-and-live-updates.md` Pillar C).
- SSE migration alignment.

## 11. Critical Files for Implementation

- `src/UI/sd-mobile/package.json` — add `@microsoft/signalr`, update jest `transformIgnorePatterns`
- `src/UI/sd-mobile/src/hooks/useSignalRClient.ts` — new — connection lifecycle hook
- `src/UI/sd-mobile/src/stores/contestUpdatesStore.ts` — new — Zustand store w/ per-contest selector hooks + self-heal
- `src/UI/sd-mobile/src/components/features/games/MatchupCard.tsx` — modified — `enrichedMatchup` merge
- `src/UI/sd-mobile/app/admin/index.tsx` — new — sport-agnostic admin page
