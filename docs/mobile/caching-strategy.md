# Mobile Caching Strategy

## Overview

SportDeets has a natural data immutability split that makes caching highly effective:

- **Completed games and past picks** — immutable once finalized; never need to be re-fetched
- **Current week's games** — stable until kickoff, then live
- **In-progress games** — never cache; always fresh
- **AI previews / analysis** — expensive to generate; cache aggressively (changes at most daily)

Caching should be implemented at **two layers** that complement each other: HTTP response headers on the API, and a client-side query cache on the mobile app.

---

## Layer 1: HTTP Cache-Control Headers (API)

Set on the .NET API controllers/middleware. The mobile client (and any CDN or proxy in between) honors these automatically — no client-side code changes required.

### Recommended policy per endpoint

| Endpoint / Data type | Header |
|---|---|
| Completed game results (past weeks/seasons) | `Cache-Control: public, max-age=604800, immutable` |
| User's submitted picks (past, finalized) | `Cache-Control: private, max-age=604800, immutable` |
| Current week's games (pre-kickoff) | `Cache-Control: public, max-age=300` + `ETag` |
| Live / in-progress games | `Cache-Control: no-store` |
| AI preview / analysis | `Cache-Control: public, max-age=86400` |
| User profile / account data | `Cache-Control: private, max-age=60` |

### `immutable` flag
Tells the client the response will never change for the duration of `max-age`. The client skips the request entirely — no round trip, no `If-None-Match` check. Only use this when the data is genuinely immutable (completed game from a past week, finalized pick).

### ETags for conditional requests
For data that *might* change (current week games before kickoff), return an `ETag` header with a hash or version token. The client sends `If-None-Match` on subsequent requests; the server returns `304 Not Modified` with no body if unchanged. Saves bandwidth but still incurs a round trip.

### Implementation in .NET
A simple approach is a per-endpoint `[ResponseCache]` attribute or a custom `CacheControlAttribute`. For immutable past-season data, a middleware that inspects the route and sets headers based on whether season+week are in the past is clean and centralized.

### `private` vs `public`
- `public` — response may be stored by shared proxies and CDNs (safe for game data, standings, AI previews)
- `private` — response contains user-specific data; only the individual client may cache it (picks, user record, profile)

---

## Layer 2: React Query (Client-side)

React Query maintains an in-memory logical cache on top of the HTTP cache. Even if the HTTP layer is fast, React Query prevents redundant renders and coordinates multiple components requesting the same data.

### Recommended `staleTime` values

| Data | `staleTime` |
|---|---|
| Completed past games | `Infinity` — never considered stale |
| Finalized user picks | `Infinity` |
| Current week games | `60_000` (60 seconds) |
| AI preview | `3_600_000` (1 hour) |
| Live game scores | `0` (always stale; pair with `refetchInterval`) |

`staleTime: Infinity` means React Query will never trigger a background refetch. The data is served from the in-memory cache until the app is restarted or the cache is explicitly invalidated (e.g. after a user submits a new pick).

### Cache key design

React Query keys are hierarchical. Design them to match your domain's immutability boundaries:

```ts
// Completed season — immutable
['matchups', 'season', 2024, 'week', 3]

// Current week — refetchable
['matchups', 'current']

// User picks for a specific contest — immutable after submission
['picks', 'contest', contestId]
```

This lets you invalidate `['matchups', 'current']` without touching past-season data, and invalidate a specific pick after submission without affecting others.

### Persistence across app restarts

By default React Query's cache is in-memory only — it is lost when the app is closed. For completed game data this means a cold-start always hits the network once per session.

To persist the cache to disk, `@tanstack/query-async-storage-persister` combined with MMKV (fast synchronous storage) can serialize the React Query cache between sessions. This is an optional optimization — implement it if cold-start API calls become noticeable to users.

---

## Decision: When to implement each layer

| Priority | Change | Benefit |
|---|---|---|
| High | `Cache-Control` headers on API completed-game endpoints | All clients (web + mobile) benefit immediately |
| High | `staleTime: Infinity` in React Query for past weeks | Eliminates redundant in-session fetches |
| Medium | `ETag` on current-week endpoints | Reduces bandwidth for unchanged pre-kickoff data |
| Low | React Query persistence to MMKV | Eliminates cold-start fetches for past data |
| Low | SQLite via expo-sqlite | Only needed if querying across cached data locally |

---

## What NOT to cache

- Any endpoint with live score updates (`no-store`)
- Auth tokens / Firebase session (handled by Firebase SDK, not HTTP cache)
- Anything that changes based on the authenticated user's real-time state
