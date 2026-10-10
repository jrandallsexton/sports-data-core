# 10. Delivery

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 1 (live) · **Status:** done 2026-10-10 (uncommitted); the shovels deploy with sports-data-config

**Depends on:** [08 Persist and audit](08-persist-and-audit.md)

**Feature doc:** [Stage 7: deliver](instant-analysis.md#stage-7-deliver) · [Timing](instant-analysis.md#timing) · [How the pipeline works today](instant-analysis.md#how-the-pipeline-works-today-the-parts-we-hook-into)

## Goal

Facts reach every connected client within a moment of the play.

## Scope

- The `PlayAnalysisGenerated { ContestId, CompetitionId, PlayId, Items[] }` event (Core), published from the analysis job.
- Exchange pre-declaration and a shovel per Producer broker in sports-data-config (new API consumer rule).
- An API consumer named for the event, sending `Clients.All.SendAsync("PlayAnalysis", …)` on `NotificationHub`.

**Out of scope:**

- Per-contest SignalR groups (task 16).
- Client display (tasks 11, 12).

## Deliverables

- The event contract and doc under `docs/events/`.
- The API consumer.
- The sports-data-config change (separate repo PR).

## Acceptance criteria

- [x] On the local stack, an opted-in game's play produces a `PlayAnalysis` SignalR message with the right `PlayId`.
- [ ] The shovel audit confirms the exchange routes from each sport broker to the API broker. (Manifests added; verify after deploy, and pre-declare the exchange on both football brokers first.)

## Notes

- Consumer class name is the queue name; check for collisions with any same-named consumer.

## Outcome (2026-10-10)

- **Event:** `PlayAnalysisGenerated { ContestId, CompetitionId, PlayId, Items[{ Id, FactType, Text }] }` in
  `Core/Eventing/Events/Contests/PlayAnalysisGenerated.cs`, `CausationId.Producer.PlayAnalyser`. Doc:
  [docs/events/contests/PlayAnalysisGenerated.md](../../events/contests/PlayAnalysisGenerated.md); event overview
  updated.
- **Publish:** `PlayAnalyser` publishes through `IEventBus` (the `FootballDataContext` outbox) in the same save as
  the `PlayAnalysis` rows, **live only** (lab runs never publish). If a racing duplicate job loses on the unique
  index, its staged outbox message is detached along with its rows, so a play is pushed once.
- **API:** `PlayAnalysisGeneratedConsumer` (queue `play-analysis-generated`; no name collision) sends
  `Clients.All.SendAsync("PlayAnalysis", message)`.
- **sports-data-config (uncommitted in that repo):** `shovel-play-analysis-generated-ncaa-to-api.yaml` and
  `-nfl-to-api.yaml`, added to `app/base/rabbitmq/shovels/kustomization.yaml`. Before they reconcile, pre-declare
  `SportsData.Core.Eventing.Events.Contests:PlayAnalysisGenerated` (fanout, durable) on `rabbitmq-football-ncaa`
  and `rabbitmq-football-nfl` (see that directory's README), or wait for the first live publish.

### Local end-to-end (example game, flag on, Producer + API)

- A SignalR client on `/hubs/notifications` received `PlayAnalysis` messages for the contest, each **after** its
  play's `FootballPlayCompleted`, with the right `PlayId`, e.g. "T. Cooley's first interception of the game." on
  the interception play.
- Two replays ran concurrently (a stale queued one plus a new one), so plays were analysed twice at once: the
  duplicate guard fired twice, and Seq shows **22 SignalR sends for the 22 plays with live facts, one each**.
- Seq (local) shows "Fact dropped" events with `PlayId`, which also closes task 08's Seq check.
- The exchange on the broker is `SportsData.Core.Eventing.Events.Contests:PlayAnalysisGenerated` (fanout), bound to
  `play-analysis-generated`, matching the shovels' `srcExchange`.
- Local-only finding: Hangfire.PostgreSql fetches queues alphabetically, so `daemon` beats `default`; ~160
  restored-from-prod `ICompetitionBroadcastingJob`s on `daemon` held every worker and starved the replay job on
  `default`. Worked around locally with `SportsData.Producer__BackgroundProcessor__MinWorkers=250` in a scratch
  compose override (not committed).
