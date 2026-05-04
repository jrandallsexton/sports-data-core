# Web SignalR Surface — Audit (2026-05-03)

Snapshot of the web app's real-time pipeline as of post-PR #292 merge:
which SignalR events flow today, where they're consumed, what the user
actually sees on a push, and where the chain is broken.

Source: parallel exploration agents over `src/UI/sd-ui` and
`src/SportsData.Api/Application/Events`. Verified against repo HEAD on
`main` at the time of capture.

## Connection & hub

- Single hub: `NotificationHub` at `/hubs/notifications`
  (`src/SportsData.Api/Program.cs:356`).
- Auth: Firebase ID token via `accessTokenFactory`
  (`src/UI/sd-ui/src/hooks/useSignalRClient.js:22-35`).
- No groups — every broadcast is `.All`. No `JoinLeagueGroup`,
  `JoinContestGroup`, or per-user fan-out implemented today.
- Single subscriber in the SPA: `MainApp.jsx` calls `useSignalRClient`
  exactly once.

## Events flowing today

| SignalR event name | Producer / API source | Web wired? | Visible UI effect |
|---|---|---|---|
| `ContestStatusChanged` | `*EventCompetitionStatusDocumentProcessor` (FB+MLB), `ContestReplayService` | ✅ `handleStatusUpdate` → context `status` field | Drives the lifecycle branch in `GameStatus` (Final / InProgress / Scheduled) on `/picks` and `/map` |
| `FootballContestStateChanged` | `EventCompetitionPlayDocumentProcessor` (FB), `ContestReplayService` | ✅ `handleFootballStateUpdate` → period / clock / score / possession / scoring | `/picks`: `MatchupCard` → `GameStatus` shows period+clock, possession 🏈, 2s gold pulse + "🎉 TOUCHDOWN!" on scoring play. `/map`: marker glow 5s |
| `BaseballContestStateChanged` | **no Producer emitter yet** | ✅ `handleBaseballStateUpdate` → context state | **DEAD** — `PicksPage` enrich step never reads inning/halfInning/balls/strikes/outs; even if events arrived, no MLB UI exists to render them |
| `ContestScoreChanged` | `CompetitorScoreUpdatedConsumerHandler` (Producer Worker) | ⚠️ broadcast by API but **no web listener** | Score tiles on `/picks` only update via the football tick path, not via this dedicated event |
| `ContestOddsUpdated` | `EventCompetitionOddsDocumentProcessor` | ⚠️ broadcast by API but **no web listener** | Nothing |
| `ContestRecapArticlePublished` | `ContestRecapProcessor` (API) | ⚠️ broadcast by API but **no web listener** | Nothing |
| `PreviewGenerated` | `PreviewGeneratedHandler` (API) | ✅ `onPreviewCompleted` | Toast only — `refreshMatchups()` is commented out (`MainApp.jsx:101`) |

## Routes that actually light up on a push

- `/app/picks/:leagueId?` — `PicksPage` → `MatchupList` → `MatchupCard`
  → `GameStatus`
- `/app/map` — `GameMap` markers + scoring-play glow

## Routes that look "live" but aren't

- `/leaderboard` — static fetch; no `useContestUpdates` consumer.
- `/messageboard` — static.
- `/warroom` — static.
- `/sport/:sport/:league/contest/:contestId` (`ContestOverview`) —
  fetches once on mount; **no live wiring at all**. Game progresses
  unseen until the user manually refreshes.
- `MatchupCard` flips to "FINAL" branch the moment `status === 'Final'`
  and stops rendering live fields, even if the context still holds them
  (`GameStatus.jsx:53-81`).

## Hook & context contract (cheat sheet)

`useSignalRClient` (`src/UI/sd-ui/src/hooks/useSignalRClient.js`)
subscribes to:

- `PreviewGenerated` → `onPreviewCompleted`
- `ContestStatusChanged` → `onContestStatusChanged` (conditional)
- `FootballContestStateChanged` → `onFootballContestStateChanged` (conditional)
- `BaseballContestStateChanged` → `onBaseballContestStateChanged` (conditional)

`ContestUpdatesContext`
(`src/UI/sd-ui/src/contexts/ContestUpdatesContext.jsx`) handlers:

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
- `src/components/picks/PicksPage.jsx:55` — merges live updates into
  `enrichedMatchups` useMemo passed to `MatchupList`
- `src/components/map/GameMap.jsx:70` — drives marker color + 5s
  scoring-play glow

## API → SignalR fan-out (server side)

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

API consumers registered in `Program.cs:228-240` that **do not**
broadcast (DB-only side effects):

- `PickemGroupCreated`
- `PickemGroupMatchupAdded`
- `PickemGroupWeekMatchupsGenerated`
- `ContestStartTimeUpdated`

## Notable gaps the next pass needs to consider

1. **`ContestScoreChanged` is broadcast but not consumed.** The
   dedicated score event the live-pipeline PR (#289) wired up is a
   no-op on the client today — score updates only happen as a side
   effect of `FootballContestStateChanged`. Either drop the broadcast
   or add the listener.
2. **MLB scoreboard surface doesn't exist.** Context handler is wired;
   emitter and UI both missing. Need both a
   `BaseballEventCompetitionPlayDocumentProcessor` emitter and a
   baseball-shaped MLB matchup card.
3. **`ContestOverview` is the obvious "live game" page and gets zero
   updates.** Most likely the right place to add a focused per-contest
   subscription (joining a `contest:{id}` group would replace the
   current `.All` broadcast).
4. **No groups → every client receives every broadcast.** Fine at
   current scale; will need `JoinLeagueGroup` / `JoinContestGroup`
   when traffic grows.
5. **`PreviewGenerated` toast doesn't trigger a refetch** — preview
   content stays stale until manual reload (`MainApp.jsx:101` has the
   commented-out `refreshMatchups()`).
6. **Odds ticker / recap toast surfaces don't exist** even though the
   events are flying.
