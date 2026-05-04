# SignalR Debug Harness — Plan (2026-05-03)

Captures the design discussion for an admin-page tool that lets us
trigger arbitrary SignalR events and watch a sport-specific widget
re-render. Companion to `docs/web-signalr-surface-audit.md`. Living
doc — update as decisions land.

## Why we're building this

In-game UI updates are the next major surface. **Audit-time snapshot
(2026-05-03)** — verify against the live code paths (Producer emitters,
API SignalR rebroadcasters, web client listeners) before relying on any
of these claims; the surface drifts as features land:

- As of the 2026-05-03 audit, Producer published `ContestStatusChanged`
  (lifecycle) + `FootballContestStateChanged` (per-play scoreboard tick).
- At audit time, the API rebroadcast both via SignalR.
- At audit time, `/picks` reflected football updates via `GameStatus`
  and `/map` showed a marker glow.
- At audit time, baseball was silent end-to-end (no Producer emitter;
  no UI surface).
- At audit time, several SignalR events were broadcast by API but had
  no client listener (`ContestScoreChanged`, `ContestOddsUpdated`,
  `ContestRecapArticlePublished`).

Re-verify each bullet by inspecting the actual code (Producer document
processors, API `Application/Events/*Handler.cs` consumers, web
`useSignalRClient.js` `connection.on(...)` registrations) before
treating any of the above as current.

To iterate confidently on the live-game UX we need a way to inject
known-shape events on demand and watch the chain react. Real games
won't be available all the time, and even when they are, we can't
synthesize edge cases (TD with no time on clock, runner thrown out at
home, lead-changing FG, etc.) at will.

## What this debug harness has to do

1. **Render** a sport-specific visual that exercises the full payload
   shape for `FootballContestStateChanged` /
   `BaseballContestStateChanged`. Football: a field with yard markers
   + possession indicator + score + last-play caption. Baseball: a
   diamond with bases lit up + score + at-bat + count.
2. **Broadcast on demand** via an admin endpoint, so we can drive the
   widget without needing a real live game.
3. **Stay attached to the same SignalR hub the production UI uses** so
   a working test here ≡ working production pipeline.
4. **Be mountable on `/app/admin`** behind the existing
   `AdminApiToken` so we don't accidentally fan fake states to real
   users.

## Reference: hand-drawn target

Saved at `src/about/public/Screenshot 2026-05-03 113759.png`. Two
panels:

- **Football**: end-zones labeled `AWAY` / `HOME`, yard lines
  10-20-30-40-50-40-30-20-10. Header line "Home 1st and 10 at own 40".
  Score `Away 7 / Home 10`. Possession `🏈` arrow on the team with
  the ball. Caption "Last Play: Smith 20-yard pass to Jones".
- **Baseball**: diamond with home plate at bottom, 1st/2nd/3rd as
  small squares; runners shown as filled squares. Header "Bottom 3rd,
  2 men on". Score `Away 0 / Home 2`. Caption "At Bat: T. Smith (1-2)
  ← Current Count".

## The four design decisions

### 1. Where the broadcast originates

| Option | Pro | Con |
|---|---|---|
| (a) Admin endpoint → `IHubContext.SendAsync(...)` direct | Fast dev loop; ms-latency | Bypasses the consumer handler — we'd test transport, not the projection step |
| (b) Admin endpoint → `_publishEndpoint.Publish(...)` | End-to-end fidelity; hits the real consumer + handler + SendAsync chain | Adds 50–500ms of jitter via outbox + RabbitMQ |
| (c) Both, behind a switch | Default to (b); flip to (a) for UI iteration | Two paths to maintain |

**Default decision: (b).** The whole point is to validate the chain.
If transport-only testing is needed later, we can add the toggle.

### 2. Production card vs. debug-only widget

| Option | Pro | Con |
|---|---|---|
| (a) New components in `components/admin/signalr-debug/` | Debug widget can be maximalist; production cards stay tight | Two rendering paths for same data |
| (b) Extend `GameStatus` / `MatchupCard` with debug branch | One render path | Bloats production with debug-only branches |

**Default decision: (a).** Debug widgets get to be busy. Both consume
the same `useContestUpdates` context, so a working debug widget ≈
working production widget.

### 3. Picking the contest under test

| Option | Pro | Con |
|---|---|---|
| (a) Hardcoded sandbox `ContestId` per sport | Simple; debug events never appear in real picks pages | Not "real" |
| (b) Dropdown of in-progress contests | Realistic | Risks broadcasting fake state for a contest real users are watching |

**Default decision: (a).** One fake contest id per sport. Real
production cards never see it because it's not in any league/matchup.

### 4. Event types & presets

Per-sport list, with default-payload presets so the user can click
"TOUCHDOWN" and get a fully-formed event.

**Football (`FootballContestStateChanged`)** quick presets:

- Game start — Q1, 15:00, 0-0, away possession, no score
- Field goal — bump score +3, `isScoringPlay=true`
- Touchdown — bump score +6, `isScoringPlay=true`
- Possession change — flip `possessionFranchiseSeasonId`
- Custom — raw form for all fields

**Baseball (`BaseballContestStateChanged`)** quick presets:

- Pitch (ball) — increment `balls`
- Pitch (strike) — increment `strikes`
- Out — increment `outs`, reset count, maybe rotate at-bat
- Single — runner on first, batter advances
- Home run — bump score, clear bases, scoring-play analog
- Inning change — `Top` ↔ `Bottom`, `inning++`, reset count/outs/bases
- Custom — raw form

**Lifecycle (`ContestStatusChanged`)** — sport-neutral:

- Scheduled → InProgress
- InProgress → Final

**Per-play log (`ContestPlayCompleted`)** — sport-neutral:

- "Fire play log" preset on both football and baseball debug widgets
  publishes `ContestPlayCompleted` with a synthesized `PlayDescription`
  (e.g. `Mock play @ HH:MM:SS`).
- API's `ContestPlayCompletedHandler` fans out as
  `"ContestPlayCompleted"` over SignalR.
- `useSignalRClient` → `MainApp.onContestPlayCompleted` →
  `ContestUpdatesContext.handlePlayCompleted` → both widgets pick it up
  via `live.lastPlayDescription` and render the "Last play" line below
  their scoreboard. Confirms the sport-neutral consumer path runs
  alongside the per-sport scoreboard tick.

The debug widget itself should render whatever is in the context for
the debug ContestId, regardless of which event drove it there.

## Order of operations (working build)

1. **Backend admin endpoint(s)** in `AdminController`:
   - `POST /admin/signalr-debug/football-state` — body
     `FootballContestStateChanged` (or a slimmer DTO).
   - `POST /admin/signalr-debug/baseball-state` — body
     `BaseballContestStateChanged`.
   - `POST /admin/signalr-debug/contest-status` — body
     `ContestStatusChanged`.
   - `POST /admin/signalr-debug/play-completed` — body
     `DebugContestPlayCompletedRequest { Sport, PlayDescription }`,
     publishes sport-neutral `ContestPlayCompleted`. Server picks the
     sandbox `ContestId` based on `Sport` (whitelist:
     `BaseballMlb`, `FootballNcaa`, `FootballNfl`).
   - All publish via `_publishEndpoint.Publish(...)`.
2. **Web admin page rework**: add a "SignalR Debug" tab/section on
   `/app/admin`.
3. **Football debug widget** + preset buttons + raw form. Broadcasts
   from form → admin endpoint → MassTransit → SignalR → context →
   widget re-renders.
4. **Baseball debug widget** + presets + raw form.
5. (Stretch) Event log panel — appends every event the page receives
   so we can see ordering, drops, lag.

Rough size: ~2–3 hours for a working v1 (admin endpoints + both
widgets + presets, no fancy event log). Most of the work is the
widgets themselves (rendering a clean football field, a baseball
diamond with active bases lit up).

## Locked decisions (2026-05-03)

1. **Broadcast path:** (b) — admin endpoint on API publishes via
   `_publishEndpoint.Publish(...)` onto MassTransit; API's own
   registered consumer picks it up and fans out via
   `IHubContext.SendAsync(...)`. Functionally identical to a real
   Producer-originated event from the consumer's perspective; the
   consumer doesn't inspect publisher identity.
2. **Components:** (a) — new debug widgets under
   `components/admin/signalr-debug/`, separate from production
   `MatchupCard` / `GameStatus`. Both consume the same
   `useContestUpdates` context.
3. **ContestId:** hardcoded sandbox id per sport. Goal is simulation,
   not real data.

## Still open

1. Event log panel in v1, or follow-up?
2. Fix the existing admin "errors" panels (canonical-data connection
   issue) in the same PR, or park?

## Adjacent gaps surfaced during the audit (not v1 scope)

These exist today and the debug harness will make them visible. Each
is its own follow-up:

- **MLB live emitter is missing.** `BaseballContestStateChanged` event
  + API consumer exist; no Producer emitter wired. Debug harness can
  prove the consumer-side wiring works ahead of the emitter landing.
- **`ContestScoreChanged` is broadcast but has no web listener.**
  Either drop the broadcast or add the listener.
- **`ContestOverview` page (`/sport/:sport/:league/contest/:contestId`)
  has zero live wiring** — fetches once on mount. Likely the right
  place for a `JoinContestGroup`-style focused subscription once
  groups are added.
- **No SignalR groups** — every broadcast is `.All`. Fine at current
  scale; needed when traffic grows.
- **`PreviewGenerated` toast doesn't trigger a refetch**
  (`MainApp.jsx:101`).
- **Admin "errors" panel canonical-data queries hit
  `sdProducer.FootballNcaa` directly** but `Local` AppConfig points at
  `localhost` / `sdApi.All`. Needs `host.docker.internal` +
  `sdProducer.FootballNcaa` in the canonical connection string.

## Decisions log

| Date | Decision | Rationale |
|---|---|---|
| 2026-05-03 | Plan captured | Hand-drawing + audit drove this; ongoing focus |
| 2026-05-03 | Locked: API publishes via MassTransit; debug widgets separate from production cards; hardcoded ContestId per sport | Functionally identical to Producer-originated event for the SignalR-pipeline test; debug widgets stay maximalist without bloating production |
