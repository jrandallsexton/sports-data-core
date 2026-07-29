# sportDeets Launch-Readiness Assessment

**Date:** 2026-07-29
**Scope:** `src/SportsData.Api`, `src/UI/sd-ui`, `src/UI/sd-mobile`, production logs (Seq, 72h), EF Core data layer, caching/performance, CI/CD, repo hygiene
**Basis:** `main` @ `45601011`+ (PR #570 merged). Full solution builds clean; 1,444 backend unit tests pass; 84 mobile jest tests pass; 10 web vitest tests pass.
**Method:** six parallel read-only audits plus direct verification of every P0 claim. No code was modified.

---

## Verdict

**The application is in good shape. The platform under it is not.**

Two findings would, on their own, make a public launch go badly:

1. **Any authenticated user can read any league's roster, standings, and message board, and post into it,** by knowing a league GUID. GUIDs travel in invite links and URLs.
2. **The production database became unreachable three times in four hours** on 2026-07-28. Everything else in this document is secondary to that.

Behind those: two unauthenticated endpoints that shouldn't exist on a public API, no password reset on either platform, and simulated content visible to users.

Nothing here is architectural. The codebase is disciplined — strict typing, consistent CQRS, real test coverage on the backend, well-documented deliberate decisions. The gaps are the ones a solo operator predictably accumulates: guards applied to write paths but not read paths, a path nobody walks (password reset), and infrastructure you already knew was a stopgap.

Estimated work to clear P0 + P1: **3–4 focused days**, dominated by the IDOR sweep.

> **Correction, 2026-07-29 (post-review).** The first draft listed web email sign-in and email signup as launch blockers. **That was wrong — both work, confirmed empirically by the operator** (existing email user signed in; brand-new email account created with correct data in Settings).
>
> Root cause of the misread, traced afterward: the Dec-2025 `1daad92e0` header-based-auth migration removed cookie auth from `AuthContext` but left **five call sites** behind across `Login.jsx`, `EmailSignupForm.jsx`, and `SignupPage.jsx`. Those fossils describe a login/onboarding flow that no longer exists, and I read them as the live path. Meanwhile the thing that actually provisions users — `FirebaseAuthenticationMiddleware` → `UserService.GetOrCreateUserAsync` on first authenticated request — is invisible from the client code entirely.
>
> Re-filed as **P3: Dead cookie-auth subsystem**, which is now a real (if low-severity) finding in its own right. The analytical error was mine: I verified static facts and asserted a runtime consequence without tracing the flow to its end.

---

## Methodology and limits

**Examined:** every API controller and its authorization posture; representative query/command handlers; all EF entity configurations, migrations, and the model snapshot; every mobile screen and web route; 50 Error-level Seq events spanning 72 hours; CI workflows and Azure pipelines; tracked files for secrets.

**Not examined (blind spots you should close yourself):**
- The `sports-data-config` sibling repo — Flux manifests, k8s ingress rules, replica counts, secret wiring. Several findings below (`/api` proxy routing, rate limiting at ingress, response compression) can only be *confirmed* there.
- Runtime behavior. Nothing was executed against a live environment; findings are static-analysis plus log evidence.
- Cloudflare configuration (WAF, caching, DDoS posture).
- The Producer/Provider/Notification services, except where API depends on them.
- Load testing. The game-day model below is arithmetic, not measurement.

**Confidence:** every P0 was independently verified against source by me, not taken on an agent's word. P1/P2 items carry file:line evidence from the audits; I spot-checked a representative subset.

**A caveat this document earned the hard way:** static verification of a fact is not verification of its *consequence*. Two findings in the first draft were correct about the code and wrong about the outcome, because I confirmed the defect without tracing the surrounding flow (see the Verdict correction). Where a finding below asserts user-visible impact rather than code shape — especially in the P1 mobile and web sections — treat it as a hypothesis worth ten seconds of empirical confirmation before you spend an hour on it.

---

## P0 — Launch blockers

| # | Finding | Area |
|---|---|---|
| P0-1 | IDOR: any authenticated user can read any league's data by GUID | API security |
| P0-2 | IDOR: any authenticated user can write to any league's message board | API security |
| P0-3 | Unauthenticated endpoint publishes bus events and discloses outbox contents | API security |
| P0-4 | Unauthenticated endpoint triggers AI preview generation (cost) | API security |
| P0-5 | PostgreSQL unreachable 3× in 4 hours on 2026-07-28 | Infrastructure |
| P0-6 | "Forgot password?" is a dead button; no password reset exists on any platform | Auth (both) |
| P0-7 | Simulated/placeholder content shown to real users | Product |

### P0-1 — IDOR: league data readable by GUID

No membership verification on by-group reads. **Structurally impossible in at least one case:** `GetLeagueByIdQuery` (`Application/UI/Leagues/Queries/GetLeagueById/GetLeagueByIdQuery.cs`) carries only `LeagueId` — the handler has no caller identity to check against.

Affected: `GetLeagueByIdQueryHandler` (roster, settings), `GetLeaderboardQueryHandler:31` (every member's points/accuracy — checks only that the group *exists*), `GetThreadsQueryHandler:25` and `GetRepliesQueryHandler` (private trash talk), `LeagueController.cs:217/337/382` (matchups, week overview, scores).

League GUIDs are not secret: they appear in invite links, share sheets, screenshots, and logs. A user who receives an invite to League A can enumerate nothing — but anyone who *obtains* a GUID reads that league's full roster and standings.

**Fix:** a shared membership guard applied to every by-group read. **Design constraint you must preserve:** the mobile league-invite preview (`app/league-invite/[leagueId].tsx`) deliberately calls `getLeagueById` as a *non-member* — a naive guard breaks the invite flow shipped in #570. Either scope the guard to exclude a minimal invite-preview projection, or split a public "invite preview" DTO from the full detail read.

Note the asymmetry: the *invite* paths (`SendLeagueInvite`, `InviteUserToLeague`, `GetInviteableUsers`, `CloneLeague`) all verify membership correctly. The read paths were simply never given the same treatment.

### P0-2 — IDOR: message board writes

`CreateThreadCommandHandler.cs:25` and `CreateReplyCommandHandler` persist using the route `GroupId` with no membership check. Any authenticated user can post into any league's board. On a social feature this is a harassment vector, not just a data-integrity one.

**Related, same class:** `SubmitPickCommandHandler` — verified no membership check. A non-member can submit picks into any league whose GUID they know, appearing in its leaderboard.

### P0-3 — `OutboxTestController` is exposed

`src/SportsData.Api/Controllers/OutboxTestController.cs` — **verified:** no `[Authorize]`, no admin gate, no environment gating in `Program.cs`. Routed at `api/test/outbox`.

- `POST publish-no-entity-changes` / `verify-outbox-used` — publish MassTransit events, write outbox rows
- `GET outbox-messages` — returns the last 50 outbox message bodies to anyone

This is precisely the class of unauthenticated ops endpoint that got Producer/Provider pulled off public ingress on 2026-07-23. **Fix:** delete it; it is a development artifact.

### P0-4 — Unauthenticated AI preview generation

`LeagueController.cs:355` — `POST /ui/leagues/{id}/previews/{weekId}/generate` lacks `[Authorize]`. **Verified:** the class has 19 `[Authorize]` attributes across 20 actions — exactly this one was missed. Anonymous callers enqueue AI-generation jobs, driving inference cost and Hangfire queue depth.

### P0-5 — PostgreSQL availability

The Seq analysis is unambiguous: of 50 Error events in 72 hours, **~90% are one infrastructure story**, and the other ~68 hours were error-silent.

Three windows on 2026-07-28 (17:08, 18:47, 21:10 UTC) where `192.168.0.251:5432` was unreachable — 30 health-check failures at ~26s connection timeouts, across **every sport and every service role simultaneously** (Provider NFL/NCAA/MLB, Producer NFL). The 18:47 window dropped *established* connections mid-transaction ("connection reset by peer"), not merely refusing new ones.

This is the Postgres VM on Bender — the documented workstation stopgap. During a live game window, this is total platform outage: no picks submitted, no scores updated, no live events.

**This validates the pre-season data-hardware migration as mandatory, not aspirational.** Before public users: explain 2026-07-28 (host memory pressure? VM restart? backup job? network flap?) or move off that host. Three incidents in one afternoon is not a fluke to launch on.

### P0-6 — No password reset anywhere

Mobile: `app/(auth)/sign-in.tsx:277` — **verified verbatim** — a `TouchableOpacity` styled as "Forgot password?" with **no `onPress`**. The TODO above it (deferred from PR #274) documents this.

Web: `sendPasswordResetEmail` appears **nowhere** in `sd-ui`; `Login.jsx` has no forgot-password link at all.

Email/password is an offered sign-in path on both platforms. Any user who forgets their password is permanently locked out with no self-service recovery. A visibly non-functional button is also App Store rejection bait.

**Fix:** `sendPasswordResetEmail` is ~10 lines of Firebase on both platforms. This is the cheapest P0 on the list.

### P0-7 — Placeholder content visible to users

- **Web:** `SettingsPage.jsx:540` unconditionally renders `BadgesPanel`, titled **"🏅 Your Badges (simulated)"** (`BadgesPanel.tsx:47`), loading static fake data from `public/data/badges.json` (file exists — it renders).
- **Web:** `ContestOverview.jsx:41` renders `No contest data available. (Debug: {JSON.stringify(data)})` to users.
- **Mobile:** `app/(tabs)/profile.tsx:342-344` — **verified verbatim** — `seasonRecord`/`careerRecord` hardcoded to `{0,0,0}`. Every user sees "0-0 / —%" This Season and Career cards on a top-level tab. They look broken, not empty.
- **Web:** `WarRoomPage.jsx:16-25` — "More Widgets Coming Soon" placeholder on a first-class nav item.

---

## P1 — High priority (fix before broad public exposure)

### Security

- **Ops endpoints gated by authentication only, not admin.** `ContestController` — any logged-in user can `POST {id}/refresh`, `{id}/media/refresh`, `{id}/finalize`, mutating canonical results that drive scoring. `PreviewController.cs:25/48` — any authenticated user can approve/reject any AI preview. Move behind `[AdminApiToken]`.
- **`TeamCardController` has no class-level `[Authorize]`**; `PATCH .../logos/{logoId}/dark-bg` (:132) is anonymous and writes shared presentation state.
- **SignalR hub is unauthenticated** (`NotificationHub.cs`, `Program.cs:416`). Client-invocable `SendMessageToUser(userId, message)` lets any socket push arbitrary payloads to any user — spoofing/spam vector. Add `[Authorize]`; remove client-invocable send methods.
- **CORS allows `localhost:3000/3001/8081` with `AllowCredentials()` in the production build** (`Program.cs:318-337`), plus wildcard `*.sportdeets.com`. Environment-scope the origin list.
- **No rate limiting anywhere** (no `AddRateLimiter`) and **no length cap on message-board content** — unbounded posts, no throttling, on a public social feature. Confirm whether Cloudflare provides rate limiting; if not, this is a P0-adjacent abuse vector at launch.
- **`Include Error Detail=true` on the Postgres connection string in all environments** (`SportsData.Core/.../ServiceRegistration.cs:138`) — embeds row/parameter values in exceptions that flow to Seq. Gate to Development.

### Data layer

- **`MatchupPreview` has zero secondary indexes** (`MatchupPreview.cs:49` — the unique index is commented out; snapshot confirms only the PK). Queried by `ContestId` on **every picks-page load**. Sequential scan on a table that grows per contest per week.
- **`PickemGroupMatchup` has no standalone `ContestId` index**; three hot paths query by `ContestId` alone — every pick submission, every scoring pass, and a bus consumer.
- **N+1 in `GetLeagueWeekOverview`** (`:123-143`) — **verified myself**: loops league members calling the per-user picks handler (2 queries each). A 20-member league = ~42 round-trips per page view, on a page users refresh during games.
- **N+1 in `GetLeaderboardWidget`** (`:85-110`) — per league, re-runs the full leaderboard aggregation just to extract the caller's rank. User in 8 leagues ≈ 26 round-trips. Fires on nearly every session start.
- **Unbounded query + correctness bug**: `GetPickAccuracyByWeekQueryHandler.cs:128-131` loads **every scored pick in the database** (`UserPicks.Where(p => p.PointsAwarded != null)`) with no user filter — despite fetching the synthetic user at :114. "Synthetic accuracy" is currently everyone's accuracy.
- **Full connection string (with password) written to stdout at startup** — `SportsData.Core/.../ServiceRegistration.cs:154` `Console.WriteLine($"PostgreSQL: {connString}")`. Shipped to Seq on every boot of every service.

### Performance / caching

- **Contest overview polling is an uncached proxy.** Mobile polls every 30s (`useContest.ts:84`); the handler forwards to Producer per request. N viewers of one game = N identical Producer round-trips every 30s. A 10–15s `IMemoryCache` keyed by contestId collapses this regardless of user count. **Single biggest game-day lever.**
- **Picks-page matchup envelope recomputed per member.** `GetLeagueWeekMatchupsQueryHandler` runs 4 DB queries + a Producer POST per request, and the response has **zero user-specific fields** — cacheable per (leagueId, week) for 30–60s. The SignalR overlay already tolerates staleness.
- **The only `[OutputCache]` in the codebase never fires.** `RankingsController.cs:28` sits under a class-level `[Authorize]`; ASP.NET Core's default output-cache policy refuses authenticated requests, and every UI request authenticates by cookie. `AddOutputCache()`/`UseOutputCache()` are wired and caching nothing.

### Mobile

- **Shared invite links cannot open the app.** `app.json` declares only `"scheme": "sportdeets"` — no iOS `associatedDomains`, no Android `intentFilters` for `sportdeets.com`. Mobile's own Share Invite Link emits `https://www.sportdeets.com/app/join/{id}`; recipients always land in the browser. The in-app join screen is reachable *only* via push notification, which requires already having the app and an account. **The mobile-to-mobile invite loop exits the app** — directly undercuts the viral path for a social product.
- **Team page season hardcoded to 2025** (`team/[slug].tsx:164-165`, with a `// TODO: revert` comment). At 2026 launch, every team page opened without a season param shows last season.
- **Live game detail never updates.** `game/[id].tsx` uses React Query with a 5-minute staleTime, does **not** consume `contestUpdatesStore`/SignalR, has no `refetchInterval`, and no pull-to-refresh. Matchup cards in the list are live; tapping *into* a live game freezes it. Contradicts the weekend "am I winning?" posture.
- **No email/password account creation on mobile.** Google/Apple create accounts transparently; email users must sign up on web first, and nothing on the sign-in screen says so — a wrong password and a nonexistent account produce the same message.

### Web

- **`apiOffline` blast radius.** `App.js:54-59` — *any* axios network error or 10s timeout replaces the entire route tree with `ErrorPage`, which has **no retry button and no state reset**. One slow background call bricks the session until manual reload. (The legal-route allowlist added in #565 is the only escape hatch.)
- **Zero error boundaries** anywhere in `sd-ui` — any render exception white-screens the app.
- **No email verification**; accounts are created with a 6-character password minimum.
- **Auth internals logged to production console.** `EmailSignupForm.jsx:62` logs the full decoded ID-token claims object. 120 `console.*` calls across 35 files ship to prod (CRA does not strip them).

---

## P2 — Medium (soon after launch)

**Security/correctness**
- `PicksController.cs:65` and `MessageboardController.cs:37` lack `[Authorize]` — they call `GetCurrentUserId()`, which throws → **500 instead of 401** for anonymous callers. Data stays caller-scoped, so not an exposure.
- Auth cache staleness: user row (including `IsAdmin`, `IsReadOnly`) cached 15min sliding-5 — admin revocation/bans take up to 15 minutes.
- `AdminApiTokenAttribute.cs:23` uses `==` for token comparison (timing side channel; fails closed on empty config — good).

**Data layer**
- `MessagePost` optimistic concurrency is a **no-op on PostgreSQL** — `byte[] RowVersion` + `IsRowVersion()` maps to plain `bytea`; Npgsql never bumps it, so `DbUpdateConcurrencyException` can never fire. `ToggleReaction`/`CreateReply` do read-modify-write on counters assuming that protection. Concurrent reactions will lose counts. Use `UseXminAsConcurrencyToken()` or atomic SQL increments.
- `PickScoringProcessor` — per-group query and `SaveChangesAsync` **per pick** inside loops (`:143-149`, `:249`), at exactly the Saturday-evening finalization burst.
- Unclamped Dapper pool to the canonical (Producer) DB (`Program.cs:170-174`) — no `Maximum Pool Size`, no `Application Name`. Worst-case API pod footprint is 150 connections, not 50, and invisible in `pg_stat_activity`.
- `GetPublicLeagues` — unbounded, `Include`s full member collections it never uses.
- Message-board cursor pagination uses `DateTime.Ticks` in the predicate (non-sargable, defeats the index), and returns raw entities instead of DTOs.
- `MessagePost.Content` has **no length bound** at entity or validator level.
- `PendingModelChangesWarning` globally suppressed (`AppDataContext.cs:87-90`) — hides "you forgot a migration" until it surfaces as a runtime SQL error in prod, combined with migrate-on-startup.
- Missing unique constraint on `PickemGroupConference (GroupId, ConferenceId)` (deliberately commented out).
- Unhandled unique-violation races → 500s in `SubmitPickCommandHandler:99-142` and `JoinLeagueCommandHandler:71-85` (other handlers do this correctly — pattern exists, just not applied here).

**Performance**
- `includeDeactivated: true` on hot paths (web PicksPage/Leaderboard/Leagues; mobile picks/leagues/standings) — fetches the full historical league list, growing every season forever.
- Web PicksPage import-availability effect depends on `userPicks` → re-fires on **every pick submission** (~15 extra XHR rounds per pick session).
- `ui/analytics/franchise-season/{yr}` unpaginated (~130+ teams); `ui/results/...` loads a full season and is `[AllowAnonymous]` — scrapeable, but also the cheapest OutputCache win in the repo (anonymous requests cache under the default policy today).
- No `[ResponseCache]` on the entire static/slow tier: conferences, venues, franchises, team cards, season overview, rankings history, game dates, matchup previews.

**Mobile**
- Home screen renders the new-user zero-state on API failure (`(tabs)/index.tsx` never checks `isError`) — the exact "looks like data loss" bug the leagues screen explicitly guards against.
- No offline/connectivity handling at all (no NetInfo); web has a global circuit, mobile has none.
- Game-detail selected-pick styling is light-mode-only (`game/[id].tsx:663` hardcodes `#EEF2FF` + navy).
- Team page collapses every failure into "Team not found." with no retry.
- Missing surfaces: Message Board, War Room, Game Map, League Discover. Core loop is intact without them, but that is the entire social/engagement layer plus public-league acquisition.

**Web**
- No public 404 route (`App.js` has no catch-all) — unmatched public URLs render blank, including typo'd invite links.
- SEO/meta effectively absent: `index.html:10-13` has an invalid `<meta name="sportDeets">`, no description, no OG/Twitter tags — shared links render bare. No per-route title management.
- `alert()` used in 4 places (invite-link copy, delete-league failure, signup) where the app otherwise uses toasts.
- Maintenance page has no trigger; two unwired mechanisms exist.
- 401 mid-session uses `window.location.href` hard redirects, losing all state; only `SettingsPage` checks `err.isUnauthorized`.
- Landing header sign-in is fully commented out (`LandingHeader.jsx:25-44`) — the only sign-in path is buried below the signup form.
- Footer lacks an `/account-deletion` link (reachable via Terms/Privacy cross-links; Google Play reviewers prefer one click from chrome).

---

## P3 — Low / informational

### Dead cookie-auth subsystem in sd-ui (re-filed from the first draft's P0 — see the Verdict correction)

**Root cause, traced 2026-07-29:** commit `1daad92e0` — *"auth changes (header-based)"*, 2025-12-08 — migrated the web app from cookie-based auth to a per-request `Authorization: Bearer` header. It removed `setToken` and the cookie machinery from `AuthContext`, but **left every call site standing**. Everything below is a fossil of that migration.

**Nothing here is broken in practice**, because user provisioning never depended on any of it: `FirebaseAuthenticationMiddleware.cs:153` calls `UserService.GetOrCreateUserAsync` on the **first authenticated request**. The API creates the user itself. (Operator-confirmed: a brand-new email account lands in Settings with correct data.)

| Fossil | Status |
|---|---|
| `Login.jsx:16,24` — `setToken` from `useAuth()` | Context member deleted in `1daad92e0`; the call always throws `TypeError` into the `catch` at :27. Harmless: `onAuthStateChanged` has already fired and `App.js:44-52` navigates to `/app`, unmounting `Login` before the error state matters. |
| `EmailSignupForm.jsx:64` — `apiWrapper.Auth.setToken(token)` | Sets an `authToken` cookie that `sd-ui` never sends (`apiClient.js:16` → `withCredentials: false`). |
| `EmailSignupForm.jsx:67` — `fetch("/api/user/me")` | Relative URL; no `/api` proxy in `nginx.conf:14-18`, so the SPA fallback returns 200 and the `404 → onboarding` branch is unreachable. The `.ok` branch it *does* hit is the correct outcome. |
| `SignupPage.jsx:68-78` — onboarding via `UserSummaryCard` | Gated on state only `handleEmailSignupSuccess` sets, which only the dead 404 branch calls. Never renders. |
| `SignupPage.jsx:60-64` — `handleOnboardingSubmit` | `console.log` + two TODOs. |
| `AuthController.SetToken` (API) | Still sets the cookie. The API *can* read it (`Program.cs:80-92` promotes cookie → bearer), but no known client sends it — mobile also uses a bearer header. |

**Why this matters beyond tidiness:** these five artifacts tell a coherent, mutually-reinforcing story about a login flow that has not existed since December. That is exactly what led the first draft of this document to file two launch blockers against working functionality. Dead code doesn't just rot — it actively misinforms whoever reads the codebase next, human or otherwise.

**Fix:** delete the lot as one change — the `setToken` call, the probe, the onboarding branch, `UserSummaryCard`'s signup usage, and the stub handler. Before removing `AuthController.SetToken` server-side, confirm nothing else sends the cookie (SignalR negotiate, JobsDashboard, Bruno collections) — see open question #4. If onboarding is genuinely wanted, build it deliberately rather than reviving this path.

- **`Login.jsx`'s `setToken` call always throws and is always swallowed.** `Login.jsx:16` destructures `setToken` from `useAuth()`; `AuthContext.jsx:103` provides only `{ user, loading, handleSignOut }`, so line 24's `await setToken(...)` raises `TypeError` into the `catch` at line 27.

  **Why it's harmless:** `signInWithEmailAndPassword` has already succeeded, so `onAuthStateChanged` fires and `App.js:44-52` navigates to `/app` — the redirect unmounts `Login` before the error state is meaningfully visible. And the call isn't load-bearing anyway: `apiClient.js:16` sets `withCredentials: false` and line 41 attaches `Authorization: Bearer <firebase-token>` per request, so web auth never depends on the `authToken` cookie that `POST /auth/set-token` sets.

  **Note the contrast:** `EmailSignupForm.jsx:64` calls the *correct* thing — `apiWrapper.Auth.setToken(token)` — which is why signup captures the backend user properly. `Login.jsx` reaches for a context member that doesn't exist.

  **Fix:** delete the call, or replace it with `apiWrapper.Auth.setToken` for symmetry — after answering open question #4 about whether the cookie has any remaining consumer. Leaving a permanently-throwing call in the sign-in path will eventually mask a real error.

- **The signup onboarding branch is unreachable, and its submit handler is a stub.** `SignupPage.jsx:42` / `EmailSignupForm.jsx:67` probe with a relative `fetch("/api/user/me")`; under the container's nginx (`nginx.conf:14-18`, no `/api` proxy) the SPA fallback returns `index.html` with 200, so the `404 → onboarding` branch never fires.

  **Why it's harmless:** the `.ok` branch is the correct outcome — the backend user was already created by `apiWrapper.Auth.setToken` at line 64, so the flow proceeds to `/app` with a fully populated account (operator-verified). `SignupPage.jsx:60-64`'s `handleOnboardingSubmit` is a `console.log` plus TODOs, so the branch would be a dead end even if it fired.

  **Fix:** delete the probe and the stub onboarding path, *or* build the onboarding step deliberately and point the probe at the real API base URL. The current shape is a feature that looks implemented and isn't.

### Other

- **Test asymmetry:** 1,444 backend unit tests vs **3 test files** in `sd-ui`. The web app is the public front door and is effectively untested. Mobile sits at 84 tests, all hooks/lib/api — zero screen tests.
- **CI is healthier than it looks:** Azure Pipelines runs API unit tests and web vitest; GitHub Actions runs mobile jest and deploy smoke tests. The GitHub deploy workflows don't re-run unit tests, but the Azure pipelines gate the builds.
- **`react-scripts` 5.0.1 / CRA is EOL** — no upstream security fixes; the `overrides` block manually patches vulnerable transitive deps. A treadmill, not a fire. TypeScript 4.9.5 on web is also old.
- Orphaned simulated components (`TipWeekWidget`, `PickAccuracyChart`, `AIAccuracyChart`) — not imported anywhere; delete rather than risk future wiring.
- `/gallery` route is an unlinked orphan serving Oct-2025 screenshots.
- `manifest.json` icons cap at 196px (no 192/512 maskable); `background_color` `#ffffff` clashes with theme `#0d1117`.
- Dead API stub with a hardcoded `Task.Delay(100)`: `PicksController.cs:21-31`.
- Dead tables mapped in the model: `ContestResult`, `PickResult`, `PickemGroupUserStanding`.
- `DateTime.UtcNow` used directly (violating the `IDateTimeProvider` rule) in ~8 handlers — testability, not correctness.
- Both `moment-timezone` and `luxon` shipped in web.
- ~15 log calls per request on the hottest API path; Seq cost/noise on game day.
- Duplicate redirect effects in `App.js:38-42` (strict subset of `:44-52`).
- Stale comment: `Program.cs:238-239` says `max_connections` is 500; you run 1200.
- **A11y (mobile):** inconsistent rather than absent — picks header, leagues screen, division chips are well-labeled; `SettingsRow` rows (including Sign Out / Delete Account), the forgot-password link, and game-detail pick buttons lack roles/labels.

---

## Verified clean

Recording these so absence of findings is distinguishable from absence of looking.

**Security:** Firebase JWT validation is correct (issuer, audience `sportdeets`, lifetime all validated — not skipped). No SQL injection: the only raw SQL is static and parameterless. No hardcoded secrets under `src/SportsData.Api`; `.env.prod` verified clean (Maps key pipeline-injected). No stack-trace or developer-exception leakage; `EnableSensitiveDataLogging` is Development-gated. Commissioner authorization **is** enforced server-side for league delete and matchup add. Invite paths verify inviter membership. Pick submission is caller-scoped (a user cannot read or write another user's picks). Nginx security headers are strong: enforcing CSP without `unsafe-inline` scripts, HSTS, XFO, nosniff, Permissions-Policy.

**Data layer:** Zero `timestamp without time zone` columns — all `DateTime` maps to `timestamptz`. The known `UpsertMatchupPreview` ExecutionStrategy conflict **is fixed**; both raw-transaction sites are correctly wrapped. Outbox atomicity verified on three spot-checked handlers (publish before `SaveChangesAsync`). Uniqueness constraints match handler assumptions across five entity pairs. FK delete behaviors are coherent — league delete cascades but is guarded by a Serializable transaction plus a has-picks refusal; user hard-delete is designed away in favor of anonymization. No destructive migrations against populated user-data columns. Hot read handlers (GetMe, GetUserPicksByGroupAndWeek, GetLeaderboard core, GetUserLeagues) are all `AsNoTracking`, DTO-projected, and index-aligned.

**Web:** Legal routing complete and reachable during API outage (the #565 allowlist works). Footer present in both chromes. All `/app/*` behind `PrivateRoute`; `/admin*` additionally behind `AdminRoute`. Account-deletion flow is solid — confirm step, focus management, correct sign-out teardown. No empty catch blocks; fetch effects use mounted/cancelled guards consistently.

**Mobile:** Auth (Google + Apple + email sign-in) with proper Firebase error mapping and cross-provider collision handling. League management complete: create with gates and confirm dialog, clone, delete with server-error surfacing, invite via search/email/share. Picks flow complete including confidence points with in-flight reservation, cross-league import, lock rules, read-only ended leagues. Push: registration/unregistration, per-category preferences, LeagueInvite deep-link with cold-start race handling. Store hygiene: in-app privacy/terms, in-app account deletion, OTA updates, no dev screens reachable by normal users, **only 2 TODO comments in the entire codebase**.

**Correction to one Seq finding:** the 8 MLB `Status=Accepted` scoring errors are **not** a silent scoring gap. `PickScoringProcessor.cs:96-97` *throws* on non-`NotFound` failures (only `NotFound` returns early), so Hangfire retries. It is a mis-leveled Error that should be a Warning, not lost data.

---

## Suggested sequencing

**Day 1 — Stop the bleeding (security)**
1. Delete `OutboxTestController` (P0-3). One file.
2. Add `[Authorize]` to `previews/generate` (P0-4), `TeamCardController`, `PicksController.cs:65`, `MessageboardController.cs:37`.
3. Move contest refresh/finalize and preview approve/reject behind `[AdminApiToken]`.
4. `[Authorize]` on the SignalR hub; delete client-invocable `SendMessageToUser*`.
5. Remove the connection-string `Console.WriteLine`; gate `Include Error Detail` to Development.

**Day 2 — The IDOR sweep (P0-1, P0-2)**
One shared membership guard, applied to every by-group read and the message-board writes. **Design the invite-preview exemption first** — a naive guard breaks the mobile invite flow. Add membership verification to pick submission while you're in there.

**Day 3 — Password reset and placeholder removal (P0-6, P0-7)**
Implement `sendPasswordResetEmail` on both platforms (~10 lines each) and wire mobile's dead button. Remove the simulated badges panel, the debug JSON dump, and the hardcoded mobile profile records. Optionally clear the dead-auth-code cleanup in P3 while in these files.

**Day 4 — Data layer + caching (the game-day set)**
Two index additions (`MatchupPreview.ContestId`, `PickemGroupMatchup.ContestId`) + migration. Rewrite the two N+1 handlers. Add the contest-overview and matchup-envelope caches. Fix the synthetic-accuracy unbounded query.

**Then — before or during a soft launch**
Mobile deep links (the invite loop), the 2025 team-page constant, live game detail, rate limiting, error boundaries, the mobile profile placeholder.

**In parallel, on its own track: P0-5.** Explain or eliminate the 2026-07-28 Postgres incidents. This is the only item that no amount of application work compensates for, and it's the one already on your roadmap.

---

## Open questions only you can answer

1. **Does the k8s ingress proxy `/api`?** Not launch-critical (the signup probe lands on the correct branch either way), but it determines whether the dead onboarding path in P3 should be repaired or deleted.
2. **Does Cloudflare provide rate limiting and response compression** for `api.sportdeets.com`? Two P1 items collapse to non-issues if so.
3. **What happened on 2026-07-28** between 17:00 and 21:15 on the Bender host?
4. **Does anything still depend on the `authToken` cookie set by `POST /auth/set-token`?** The web client sends `withCredentials: false` and authenticates by bearer header, so the cookie appears vestigial for `sd-ui` — confirm SignalR negotiate and any non-browser caller before deleting the dead call in P3.
5. **Are Message Board / War Room / Map / Discover in scope for launch on mobile,** or is web-only acceptable for v1?
6. **What is the launch audience size?** The caching work is optional at 50 users and mandatory at 5,000.
