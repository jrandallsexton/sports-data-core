# Auth — Spurious Logout on Inactive Tab (Investigation)

**Status:** Investigation, not yet fixed.
**Reporter:** randall (solo dev / production user).
**Related:** PR #317 (removed `visibilitychange` force-refresh — partial fix, longer time-to-logout, but bug persists).

## Symptom

A signed-in browser tab on `sportdeets.com` shows `Auth state changed: No user` in the console after a period of inactivity, with no user interaction on that tab. The user is then logged out (routed back to `/`). Reproduces specifically on **inactive** tabs after some time. PR #317 made the time-to-failure longer; it did not eliminate the bug.

## Investigation Method

Three parallel investigators:

1. **Frontend code paths** — every callsite of `auth.signOut()`, every path that fires `onAuthStateChanged(null)`, every background timer/poll/SignalR-error path that runs without user interaction.
2. **Backend code paths** — JWT validation config (especially clock skew + lifetime), Firebase Admin verification, SignalR hub auth, every code path that returns 401.
3. **Firebase JS SDK behavior** — what the SDK does in hidden/frozen/discarded tabs, what conditions cause `onAuthStateChanged(null)` without an explicit `signOut()` call, known issues in this space.

> **Reading note on citations.** Code references below use file paths plus stable symbol names (function / handler / interceptor / middleware names) instead of hard-coded line numbers. Line numbers drift quickly — the doc was already invalidated within its own PR by the instrumentation changes — so symbol references are the durable form. Future edits should grep for the symbol, not trust historical line ranges.

## Frontend Auth Surface (verified)

| Path | File · Symbol | Triggers `signOut()` without user interaction? |
| --- | --- | --- |
| `apiClient` response interceptor 401-retry → refresh fails → `auth.signOut()` | `src/UI/sd-ui/src/api/apiClient.js` — response interceptor 401 retry branch | **YES** — fires on any failed `getIdToken(true)` after a 401, including transient network blips, Firebase rate-limit, etc. |
| `apiClient` request interceptor → no `currentUser` → `isAuthError` → `window.location.href = '/'` | `src/UI/sd-ui/src/api/apiClient.js` — request interceptor (no-user branch) and response interceptor (`isAuthError` branch) | Indirectly — does **not** call `signOut()` (so won't produce the "No user" log), but does redirect to `/`, which looks like a logout. |
| `AuthContext.handleSignOut` | `src/UI/sd-ui/src/contexts/AuthContext.jsx` — `handleSignOut` | No — only wired to a user-initiated button. |
| `MainApp.jsx` sign-out dialog | `src/UI/sd-ui/src/MainApp.jsx` — sign-out confirmation dialog | No — user confirms a modal. |
| `firebase.js` visibilitychange handler | (removed in PR #317) | N/A — gone. |

The single Firebase listener lives in `AuthContext.jsx` — the `AuthProvider` `useEffect` that subscribes to `onAuthStateChanged`. So `Auth state changed: No user` in the console means Firebase Auth itself emitted `null` — i.e., either *this* tab observed local persistence cleared, or a `signOut()` from another tab was broadcast cross-tab.

**Non-suspects (verified):**
- No service worker. No Workbox. CRA default not invoked.
- No React Query / SWR. No `refetchOnWindowFocus` / `refetchInterval` anywhere.
- No `window.addEventListener('storage', ...)` outside Firebase's own internals.
- No code references Firebase persistence keys (`firebase:authUser:*`) directly.
- No `localStorage.clear()` anywhere. The three `localStorage.removeItem` callsites all touch app-owned keys, never Firebase keys.
- Two `setInterval` calls (15 s ticks in `usePickLocking.js` and `PicksPage.jsx`) update display state only — no API calls.
- `useSignalRClient.js` token factory returns `null` on `getIdToken()` error; it does **not** call `signOut()` or hit `apiClient`. Server may eventually 401-disconnect, but the disconnect alone does nothing user-visible.

## Backend Auth Surface (verified)

| Path | File · Symbol | Notes |
| --- | --- | --- |
| JWT bearer scheme registration | `src/SportsData.Api/Program.cs` — `AddAuthentication`/`AddJwtBearer` block | `Authority = https://securetoken.google.com/sportdeets-dev`. Validates lifetime, audience, issuer. **`ClockSkew` not overridden — default 5 min.** `RequireSignedTokens` not set. |
| `OnMessageReceived` JWT event | `src/SportsData.Api/Program.cs` — `JwtBearerEvents.OnMessageReceived` | **Reads token from `authToken` cookie only.** Ignores the `Authorization` header. SignalR's `access_token` query param is **not** read. |
| `FirebaseAuthenticationMiddleware` | `src/SportsData.Api/Middleware/FirebaseAuthenticationMiddleware.cs` — `Invoke` (path that calls `FirebaseAuth.DefaultInstance.VerifyIdTokenAsync`) | Runs when JwtBearer didn't authenticate. Reads token from `Authorization` header OR `authToken` cookie. **Returns 401 on ANY exception** from `VerifyIdTokenAsync` — including transient Firebase network errors, JWKS-fetch hiccups, and expired-token rejection. |
| SignalR hub | `src/SportsData.Api/Infrastructure/Notifications/NotificationHub.cs` — class itself | **No `[Authorize]` attribute.** No `Context.Abort()`. No token re-validation on the long-lived connection. A connection survives token expiry indefinitely; the server does not kick stale-token clients. **The hub is NOT a logout source.** |
| Health endpoints | `src/SportsData.Api/Program.cs` — `/health` MapGet; `src/SportsData.Api/AppConfiguration.cs` — `/api/health` and `/api/health/live` mappings | Mapped AFTER `UseAuthentication`. `FirebaseAuthenticationMiddleware` only 401s **if a token is present and fails**. Anonymous health pollers are fine; a stale-cookie request to `/health` will **401**. |
| `RevokeRefreshTokens` | (not present anywhere) | Not called. Server is not invalidating tokens. |
| Middleware order | `src/SportsData.Api/Program.cs` — pipeline assembly | Correct: `UseRouting → UseAuthentication → FirebaseAuthenticationMiddleware → UseAuthorization`. |

**Clock-skew race:** JwtBearer's default 5-min skew applies on the JwtBearer path, but most browser calls go through `FirebaseAuthenticationMiddleware` (cookie-only JwtBearer means `Authorization`-header-only browser calls bypass it). The middleware delegates to `FirebaseAdmin.VerifyIdTokenAsync`, which uses Firebase's own skew. The frontend's apiClient refresh threshold is also 5 min. Small window for a request to leave the browser with a token the frontend thinks is good but the server rejects on `exp`.

## Firebase JS SDK Behavior in Inactive Tabs (research)

Selected findings, full report saved separately:

- **Firebase internally suspends its token-refresh timer when the tab is hidden** (visibility-aware). The active refresh resumes on tab visibility, not while hidden. Chrome's intensive throttling (1 wake/min after 5 min hidden, plus full freeze under Energy Saver after ~5 min silent) compounds this.
- **Failed transient token refreshes do NOT call `signOut()`** inside the SDK. The SDK leaves the user signed in with a stale token and retries on next visibility/online event.
- **Firebase fires `onAuthStateChanged(null)` *without* an explicit `signOut()` call when:** the refresh endpoint returns a hard auth error (`TOKEN_EXPIRED`, `USER_DISABLED`, `USER_NOT_FOUND`, `INVALID_REFRESH_TOKEN`), the user was deleted/disabled server-side, `revokeRefreshTokens` was called, or local persistence was wiped (browser eviction, Safari ITP, "clear cookies on close").
- **Tab discard + reload** also briefly fires `onAuthStateChanged(null)` once during rehydration before re-emitting the persisted user. This is a transient glitch, not a real sign-out — but a frontend that routes-to-`/` on the null fire will misread it as a logout.
- **No widely reported Firebase JS SDK bug** of spurious sign-outs purely from Chrome desktop inactivity. Strongly suggests the symptom is either a real refresh failure (hard auth error) or a cross-tab broadcast from another origin tab — not a Firebase-internal misfire.

> Note on persistence backend: Firebase's published `.d.ts` for `browserLocalPersistence` (v11.6.1) says "An implementation of Persistence of type LOCAL using `localStorage` for the underlying storage." The investigation also surfaced claims (firebase-js-sdk issue #631 thread) that modern Auth SDKs may use IndexedDB primarily with localStorage as the cross-tab signal channel. **Either way, the cross-tab broadcast mechanism uses the localStorage `storage` event** — the corollary that "one tab's `signOut()` clears state for all same-origin tabs" is unchanged.

## Vectors That Can Produce the Symptom (ranked)

### 1. `apiClient` response-interceptor 401-retry → another tab's failed force-refresh → cross-tab broadcast `signOut()` (HIGH)

This is the leading suspect. The user can have multiple sportdeets tabs open (or even just one — a background-active tab still makes requests if SignalR reconnects and triggers anything that hits apiClient). When any tab gets a 401, the response interceptor force-refreshes the token. If that refresh throws **for any reason** (network blip, Firebase rate-limit hitting two-tabs racing the same refresh, transient JWKS fetch failure on the backend, etc.), the catch block calls `auth.signOut()`. Firebase clears persistence and broadcasts the change via the `storage` event to all same-origin tabs. The inactive tab receives the broadcast and logs `Auth state changed: No user`.

The inactive tab is a **passive victim**. PR #317 removed one source of force-refreshes (visibilitychange). This source (401 retry) is still live.

### 2. `FirebaseAuthenticationMiddleware.Invoke` 401-on-any-`VerifyIdTokenAsync`-exception (HIGH, supplies the 401 that feeds vector #1)

The server's middleware returns 401 on **any** exception from `VerifyIdTokenAsync`. That includes transient Firebase network failures, JWKS cache misses, and expired tokens. So even a fully-valid client with one in-flight stale request can get a 401, feeding vector #1. The frontend treats all 401s identically.

### 3. Tab discard + reload → momentary `onAuthStateChanged(null)` (MEDIUM)

If Chrome discards the tab (Memory Saver / Energy Saver after long inactivity), the page reloads on visit. During Firebase rehydration the listener briefly fires with `null` before emitting the persisted user. If the app routes to `/` on the null fire (which `AuthContext.jsx` doesn't do directly — it relies on whatever guard component reads `user`), the user is bounced to login before the rehydration completes. Console would show `Auth state changed: No user` once.

The user's symptom is on an **inactive** tab — discard is a plausible match.

### 4. Storage eviction / Safari ITP (LOW for Chrome desktop, HIGH if Safari/iPhone)

If the browser evicts site data under storage pressure (Chrome) or 7-day-no-interaction rule (Safari/iOS), Firebase's local persistence is wiped and the next emit is `null`. Symptom matches. Likely not Chrome desktop; very likely on iOS/Safari.

### 5. Real server-side refresh-token invalidation (LOW)

User disabled, deleted, password changed externally, etc. Implausible given solo-dev and reported reproducibility.

## Most Likely Root Cause

**Vector #1 (apiClient signOut on 401-retry failure), fed by Vector #2 (backend 401-on-any-exception).** The flow:

```text
[any tab, any background request]
   ↓ stale token, server returns 401
apiClient response interceptor — 401 retry branch
   ↓ user.getIdToken(true)  ← throws on transient failure
apiClient response interceptor — refresh-failure catch
   ↓ auth.signOut()         ← clears local persistence
Firebase storage event broadcast
   ↓
[every other tab, including the inactive one]
   onAuthStateChanged(null)
   "Auth state changed: No user"
```

The bug is symmetric with the one we just removed: the frontend reacts to a *transient* failure as if it were *terminal*, and the consequence broadcasts to every tab.

## Recommended Next Steps

### Step 1 — Instrument before fixing (HIGH PRIORITY)

We are guessing between four plausible vectors. Cheap instrumentation answers it definitively:

- In `AuthContext.jsx` `AuthProvider` `useEffect`, on `onAuthStateChanged`, log: `document.visibilityState`, `document.wasDiscarded`, `performance.getEntriesByType('navigation')[0]?.type`, time since page load, and the *previous* user value.
- Use `onIdTokenChanged` alongside `onAuthStateChanged` and log the last `getIdToken()` error code captured globally.
- In `apiClient.js` response interceptor refresh-failure catch, log the refresh error's `code`/`message` and the URL of the failing request, **before** calling `signOut()`.
- In `apiClient.js` request interceptor "no authenticated user" reject branch, log the URL — tells us which call is triggering the redirect.

With one production occurrence captured, we'll know if it's a tab-discard rehydration, a cross-tab broadcast, or storage eviction.

### Step 2 — Make `signOut()` decisions explicit (after instrumentation)

The fix shape, once we have data, is almost certainly:

- **Stop signing out on transient refresh failures.** In the `apiClient` response-interceptor refresh-failure catch, distinguish between Firebase error codes that *mean* the session is dead (`auth/user-token-expired`, `auth/user-disabled`, `auth/invalid-refresh-token`) and everything else. Sign out only on the former; on the latter, retry the original request or surface a transient-error UI.
- **Backend: stop 401ing on any Firebase exception.** In `FirebaseAuthenticationMiddleware`, distinguish hard auth failures (signed but expired, invalid signature, wrong audience) from transient infrastructure failures (JWKS fetch error, Firebase Admin SDK unreachable). Hard failures → 401. Transient → 503 + Retry-After. The frontend can retry a 503 without losing the session.
- **Guard against the cross-tab broadcast.** Even with the above, a real session-end on any tab logs everyone out — which is correct. But add a single-tab `BroadcastChannel` heartbeat so the cross-tab `signOut()` only fires when the *originating* tab confirms the failure (not on a single transient request).

### Step 3 — Optional, defensive

- Add a `document.wasDiscarded` check in the `AuthContext` listener: if `wasDiscarded` is true and the emit is `null`, wait one tick before routing — Firebase's rehydration will overwrite with the persisted user.
- Set `ClockSkew` explicitly to 30s (not 5 min — too forgiving) on the backend and document the value.

## Open Questions

- **Which tab originates the failure?** Instrumentation answers this. If it's the *inactive* tab itself (not a cross-tab broadcast), discard/rehydration moves up the ranking.
- **What Firebase error code appears on the failed refresh?** A `auth/network-request-failed` says transient; `auth/user-token-expired` says real session end.
- **Multi-tab usage at time of incident?** User report only mentions the inactive tab — but the bug may originate elsewhere. Worth asking next time.
- **Mobile/Safari reproduction?** ITP storage eviction is much more aggressive there. If the report is desktop Chrome only, eviction is less likely.

## Verified Non-Causes

Listed so we don't re-investigate:

- The visibilitychange handler (removed in PR #317).
- SignalR hub authentication (hub is anonymous; not a logout source).
- Any service worker (none present).
- Background API polling via React Query / SWR / etc. (not in the codebase).
- Direct `localStorage.clear()` or Firebase-key manipulation (none in app code).
- Long-running `setInterval` calls (only 15 s display-tick timers, no API calls).
- `RevokeRefreshTokens` server-side (not called anywhere).

## Files of Interest

```text
src/UI/sd-ui/src/api/apiClient.js                # vectors #1, #2 (frontend)
src/UI/sd-ui/src/contexts/AuthContext.jsx        # the only onAuthStateChanged listener
src/UI/sd-ui/src/firebase.js                     # persistence + (removed) visibilitychange handler
src/UI/sd-ui/src/hooks/useSignalRClient.js       # SignalR token factory
src/SportsData.Api/Middleware/FirebaseAuthenticationMiddleware.cs   # backend 401-on-any-exception
src/SportsData.Api/Program.cs                    # JWT scheme registration + middleware order
src/SportsData.Api/Infrastructure/Notifications/NotificationHub.cs  # (anonymous; rule out)
```
