# Web SignalR Surface

Snapshot of the web app's real-time pipeline as of post-PR #292 merge — which SignalR events flow today, where they're consumed, what the user actually sees on a push, where the chain is broken, and the admin debug harness that lets us trigger arbitrary events on demand. Combines `web-signalr-surface-audit.md` (the audit) and `signalr-debug-harness-plan.md` (the harness companion plan), previously separate root-level docs.

The audit captures the surface as it stood on 2026-05-03; verify against the live code paths (Producer emitters, API SignalR rebroadcasters, web client listeners) before relying on any specific claim since the surface drifts as features land.

Source for the audit section: parallel exploration agents over `src/UI/sd-ui` and `src/SportsData.Api/Application/Events`. Verified against repo HEAD on `main` at the time of capture.

## Connection & hub

- Single hub: `NotificationHub` at `/hubs/notifications` (`src/SportsData.Api/Program.cs:356`).
- Auth: Firebase ID token via `accessTokenFactory` (`src/UI/sd-ui/src/hooks/useSignalRClient.js:22-35`).
- No groups — every broadcast is `.All`. No `JoinLeagueGroup`, `JoinContestGroup`, or per-user fan-out implemented today.
- Single subscriber in the SPA: `MainApp.jsx` calls `useSignalRClient` exactly once.

## Events flowing today

| SignalR event name | Producer / API source | Web wired? | Visible UI effect |
|---|---|---|---|
| `ContestStatusChanged` | `*EventCompetitionStatusDocumentProcessor` (FB+MLB), `ContestReplayService` | Yes — `handleStatusUpdate` -> context `status` field | Drives the lifecycle branch in `GameStatus` (Final / InProgress / Scheduled) on `/picks` and `/map` |
| `FootballContestStateChanged` | `EventCompetitionPlayDocumentProcessor` (FB), `ContestReplayService` | Yes — `handleFootballStateUpdate` -> period / clock / score / possession / scoring | `/picks`: `MatchupCard` -> `GameStatus` shows period+clock, possession indicator, 2s gold pulse + "TOUCHDOWN!" on scoring play. `/map`: marker glow 5s |
| `BaseballContestStateChanged` | **no Producer emitter yet** | Yes — `handleBaseballStateUpdate` -> context state | **DEAD** — `PicksPage` enrich step never reads inning/halfInning/balls/strikes/outs; even if events arrived, no MLB UI exists to render them |
| `ContestScoreChanged` | `CompetitorScoreUpdatedConsumerHandler` (Producer Worker) | Broadcast by API but **no web listener** | Score tiles on `/picks` only update via the football tick path, not via this dedicated event |
| `ContestOddsUpdated` | `EventCompetitionOddsDocumentProcessor` | Broadcast by API but **no web listener** | Nothing |
| `ContestRecapArticlePublished` | `ContestRecapProcessor` (API) | Broadcast by API but **no web listener** | Nothing |
| `PreviewGenerated` | `PreviewGeneratedHandler` (API) | Yes — `onPreviewCompleted` | Toast only — `refreshMatchups()` is commented out (`MainApp.jsx:101`) |

## Routes that actually light up on a push

- `/app/picks/:leagueId?` — `PicksPage` -> `MatchupList` -> `MatchupCard` -> `GameStatus`
- `/app/map` — `GameMap` markers + scoring-play glow

## Routes that look "live" but aren't

- `/leaderboard` — static fetch; no `useContestUpdates` consumer.
- `/messageboard` — static.
- `/warroom` — static.
- `/sport/:sport/:league/contest/:contestId` (`ContestOverview`) — fetches once on mount; **no live wiring at all**. Game progresses unseen until the user manually refreshes.
- `MatchupCard` flips to "FINAL" branch the moment `status === 'Final'` and stops rendering live fields, even if the context still holds them (`GameStatus.jsx:53-81`).

## Hook & context contract (cheat sheet)

`useSignalRClient` (`src/UI/sd-ui/src/hooks/useSignalRClient.js`) subscribes to:

- `PreviewGenerated` -> `onPreviewCompleted`
- `ContestStatusChanged` -> `onContestStatusChanged` (conditional)
- `FootballContestStateChanged` -> `onFootballContestStateChanged` (conditional)
- `BaseballContestStateChanged` -> `onBaseballContestStateChanged` (conditional)

`ContestUpdatesContext` (`src/UI/sd-ui/src/contexts/ContestUpdatesContext.jsx`) handlers:

| Handler | State Update |
|---|---|
| `handleStatusUpdate` | Sets `status` + `lastUpdated` per `contestId` |
| `handleFootballStateUpdate` | Merges `period, clock, awayScore, homeScore, possessionFranchiseSeasonId, isScoringPlay`; auto-clears scoring flag after 2s |
| `handleBaseballStateUpdate` | Merges `inning, halfInning, awayScore, homeScore, balls, strikes, outs, runners, atBatAthleteId, pitchingAthleteId` |
| `getContestUpdate(contestId)` | Lookup |
| `hasLiveUpdate(contestId)` | Boolean |
| `clearContestUpdate(contestId)` | Remove single |
| `clearAllUpdates()` | Wipe all |

Two component consumers:

- `src/components/picks/PicksPage.jsx:55` — merges live updates into `enrichedMatchups` useMemo passed to `MatchupList`
- `src/components/map/GameMap.jsx:70` — drives marker color + 5s scoring-play glow

## API -> SignalR fan-out (server side)

All handlers under `src/SportsData.Api/Application/Events/`:

| Event consumed | Handler | SignalR event name | Audience | Payload |
|---|---|---|---|---|
| `ContestOddsUpdated` | `ContestOddsUpdatedHandler.cs:10` | `ContestOddsUpdated` | `.All` | Projected — `{ContestId, Message, CorrelationId, CausationId}` |
| `ContestScoreChanged` | `ContestScoreChangedHandler.cs:13` | `ContestScoreChanged` | `.All` | Full message |
| `ContestStatusChanged` | `ContestStatusChangedHandler.cs:17` | `ContestStatusChanged` | `.All` | Full message |
| `FootballContestStateChanged` | `FootballContestStateChangedHandler.cs:16` | `FootballContestStateChanged` | `.All` | Full message |
| `BaseballContestStateChanged` | `BaseballContestStateChangedHandler.cs:16` | `BaseballContestStateChanged` | `.All` | Full message |
| `ContestRecapArticlePublished` | `ContestRecapArticlePublishedHandler.cs:10` | `ContestRecapArticlePublished` | `.All` | Projected — `{ContestId, Title, ArticleId}` |
| `PreviewGenerated` | `PreviewGeneratedHandler.cs:10` | `PreviewGenerated` | `.All` | Projected — `{ContestId, Message, CorrelationId, CausationId}` |

API consumers registered in `Program.cs:228-240` that **do not** broadcast (DB-only side effects):

- `PickemGroupCreated`
- `PickemGroupMatchupAdded`
- `PickemGroupWeekMatchupsGenerated`
- `ContestStartTimeUpdated`

## Notable gaps the next pass needs to consider

1. **`ContestScoreChanged` is broadcast but not consumed.** The dedicated score event the live-pipeline PR (#289) wired up is a no-op on the client today — score updates only happen as a side effect of `FootballContestStateChanged`. Either drop the broadcast or add the listener.
2. **MLB scoreboard surface doesn't exist.** Context handler is wired; emitter and UI both missing. Need both a `BaseballEventCompetitionPlayDocumentProcessor` emitter and a baseball-shaped MLB matchup card.
3. **`ContestOverview` is the obvious "live game" page and gets zero updates.** Most likely the right place to add a focused per-contest subscription (joining a `contest:{id}` group would replace the current `.All` broadcast).
4. **No groups -> every client receives every broadcast.** Fine at current scale; will need `JoinLeagueGroup` / `JoinContestGroup` when traffic grows.
5. **`PreviewGenerated` toast doesn't trigger a refetch** — preview content stays stale until manual reload (`MainApp.jsx:101` has the commented-out `refreshMatchups()`).
6. **Odds ticker / recap toast surfaces don't exist** even though the events are flying.

---

## Debug harness

Plan captured 2026-05-03 for an admin-page tool that lets us trigger arbitrary SignalR events and watch a sport-specific widget re-render. Built on top of the surface mapped in the audit above. Living section — update as decisions land.

### Why we're building this

In-game UI updates are the next major surface. To iterate confidently on the live-game UX we need a way to inject known-shape events on demand and watch the chain react. Real games won't be available all the time, and even when they are, we can't synthesize edge cases (TD with no time on clock, runner thrown out at home, lead-changing FG, etc.) at will.

The audit above identifies the gaps the harness will make visible:

- Producer published `ContestStatusChanged` (lifecycle) + `FootballContestStateChanged` (per-play scoreboard tick) at audit time.
- At audit time, the API rebroadcast both via SignalR.
- At audit time, `/picks` reflected football updates via `GameStatus` and `/map` showed a marker glow.
- At audit time, baseball was silent end-to-end (no Producer emitter; no UI surface).
- At audit time, several SignalR events were broadcast by API but had no client listener (`ContestScoreChanged`, `ContestOddsUpdated`, `ContestRecapArticlePublished`).

Re-verify each bullet by inspecting the actual code (Producer document processors, API `Application/Events/*Handler.cs` consumers, web `useSignalRClient.js` `connection.on(...)` registrations) before treating any of the above as current.

### What this debug harness has to do

1. **Render** a sport-specific visual that exercises the full payload shape for `FootballContestStateChanged` / `BaseballContestStateChanged`. Football: a field with yard markers + possession indicator + score + last-play caption. Baseball: a diamond with bases lit up + score + at-bat + count.
2. **Broadcast on demand** via an admin endpoint, so we can drive the widget without needing a real live game.
3. **Stay attached to the same SignalR hub the production UI uses** so a working test here == working production pipeline.
4. **Be mountable on `/app/admin`** behind the existing `AdminApiToken` so we don't accidentally fan fake states to real users.

### Reference: hand-drawn target

Saved at `src/about/public/Screenshot 2026-05-03 113759.png`. Two panels:

- **Football**: end-zones labeled `AWAY` / `HOME`, yard lines 10-20-30-40-50-40-30-20-10. Header line "Home 1st and 10 at own 40". Score `Away 7 / Home 10`. Possession arrow on the team with the ball. Caption "Last Play: Smith 20-yard pass to Jones".
- **Baseball**: diamond with home plate at bottom, 1st/2nd/3rd as small squares; runners shown as filled squares. Header "Bottom 3rd, 2 men on". Score `Away 0 / Home 2`. Caption "At Bat: T. Smith (1-2) <- Current Count".

### The four design decisions

#### 1. Where the broadcast originates

| Option | Pro | Con |
|---|---|---|
| (a) Admin endpoint -> `IHubContext.SendAsync(...)` direct | Fast dev loop; ms-latency | Bypasses the consumer handler — we'd test transport, not the projection step |
| (b) Admin endpoint -> `_publishEndpoint.Publish(...)` | End-to-end fidelity; hits the real consumer + handler + SendAsync chain | Adds 50-500ms of jitter via outbox + RabbitMQ |
| (c) Both, behind a switch | Default to (b); flip to (a) for UI iteration | Two paths to maintain |

**Default decision: (b).** The whole point is to validate the chain. If transport-only testing is needed later, we can add the toggle.

#### 2. Production card vs. debug-only widget

| Option | Pro | Con |
|---|---|---|
| (a) New components in `components/admin/signalr-debug/` | Debug widget can be maximalist; production cards stay tight | Two rendering paths for same data |
| (b) Extend `GameStatus` / `MatchupCard` with debug branch | One render path | Bloats production with debug-only branches |

**Default decision: (a).** Debug widgets get to be busy. Both consume the same `useContestUpdates` context, so a working debug widget ~= working production widget.

#### 3. Picking the contest under test

| Option | Pro | Con |
|---|---|---|
| (a) Hardcoded sandbox `ContestId` per sport | Simple; debug events never appear in real picks pages | Not "real" |
| (b) Dropdown of in-progress contests | Realistic | Risks broadcasting fake state for a contest real users are watching |

**Default decision: (a).** One fake contest id per sport. Real production cards never see it because it's not in any league/matchup.

#### 4. Event types & presets

Per-sport list, with default-payload presets so the user can click "TOUCHDOWN" and get a fully-formed event.

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
- Inning change — `Top` <-> `Bottom`, `inning++`, reset count/outs/bases
- Custom — raw form

**Lifecycle (`ContestStatusChanged`)** — sport-neutral:

- Scheduled -> InProgress
- InProgress -> Final

**Per-play log (`ContestPlayCompleted`)** — sport-neutral:

- "Fire play log" preset on both football and baseball debug widgets publishes `ContestPlayCompleted` with a synthesized `PlayDescription` (e.g. `Mock play @ HH:MM:SS`).
- API's `ContestPlayCompletedHandler` fans out as `"ContestPlayCompleted"` over SignalR.
- `useSignalRClient` -> `MainApp.onContestPlayCompleted` -> `ContestUpdatesContext.handlePlayCompleted` -> both widgets pick it up via `live.lastPlayDescription` and render the "Last play" line below their scoreboard. Confirms the sport-neutral consumer path runs alongside the per-sport scoreboard tick.

The debug widget itself should render whatever is in the context for the debug ContestId, regardless of which event drove it there.

### Order of operations (working build)

1. **Backend admin endpoint(s)** in `AdminController`:
   - `POST /admin/signalr-debug/football-state` — body `FootballContestStateChanged` (or a slimmer DTO).
   - `POST /admin/signalr-debug/baseball-state` — body `BaseballContestStateChanged`.
   - `POST /admin/signalr-debug/contest-status` — body `ContestStatusChanged`.
   - `POST /admin/signalr-debug/play-completed` — body `DebugContestPlayCompletedRequest { Sport, PlayDescription }`, publishes sport-neutral `ContestPlayCompleted`. Server picks the sandbox `ContestId` based on `Sport` (whitelist: `BaseballMlb`, `FootballNcaa`, `FootballNfl`).
   - All publish via `_publishEndpoint.Publish(...)`.
2. **Web admin page rework**: add a "SignalR Debug" tab/section on `/app/admin`.
3. **Football debug widget** + preset buttons + raw form. Broadcasts from form -> admin endpoint -> MassTransit -> SignalR -> context -> widget re-renders.
4. **Baseball debug widget** + presets + raw form.
5. (Stretch) Event log panel — appends every event the page receives so we can see ordering, drops, lag.

Rough size: ~2-3 hours for a working v1 (admin endpoints + both widgets + presets, no fancy event log). Most of the work is the widgets themselves (rendering a clean football field, a baseball diamond with active bases lit up).

### Locked decisions (2026-05-03)

1. **Broadcast path:** (b) — admin endpoint on API publishes via `_publishEndpoint.Publish(...)` onto MassTransit; API's own registered consumer picks it up and fans out via `IHubContext.SendAsync(...)`. Functionally identical to a real Producer-originated event from the consumer's perspective; the consumer doesn't inspect publisher identity.
2. **Components:** (a) — new debug widgets under `components/admin/signalr-debug/`, separate from production `MatchupCard` / `GameStatus`. Both consume the same `useContestUpdates` context.
3. **ContestId:** hardcoded sandbox id per sport. Goal is simulation, not real data.

### Still open

1. Event log panel in v1, or follow-up?
2. Fix the existing admin "errors" panels (canonical-data connection issue) in the same PR, or park?

### Adjacent gaps surfaced during the audit (not v1 scope)

These exist today and the debug harness will make them visible. Each is its own follow-up:

- **MLB live emitter is missing.** `BaseballContestStateChanged` event + API consumer exist; no Producer emitter wired. Debug harness can prove the consumer-side wiring works ahead of the emitter landing.
- **`ContestScoreChanged` is broadcast but has no web listener.** Either drop the broadcast or add the listener.
- **`ContestOverview` page (`/sport/:sport/:league/contest/:contestId`) has zero live wiring** — fetches once on mount. Likely the right place for a `JoinContestGroup`-style focused subscription once groups are added.
- **No SignalR groups** — every broadcast is `.All`. Fine at current scale; needed when traffic grows.
- **`PreviewGenerated` toast doesn't trigger a refetch** (`MainApp.jsx:101`).
- **Admin "errors" panel canonical-data queries hit `sdProducer.FootballNcaa` directly** but `Local` AppConfig points at `localhost` / `sdApi.All`. Needs `host.docker.internal` + `sdProducer.FootballNcaa` in the canonical connection string.

### Decisions log

| Date | Decision | Rationale |
|---|---|---|
| 2026-05-03 | Plan captured | Hand-drawing + audit drove this; ongoing focus |
| 2026-05-03 | Locked: API publishes via MassTransit; debug widgets separate from production cards; hardcoded ContestId per sport | Functionally identical to Producer-originated event for the SignalR-pipeline test; debug widgets stay maximalist without bloating production |
