# 03. Trigger

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 1 (proof of concept) · **Status:** done 2026-10-10 (uncommitted)

**Depends on:** [02 Enablement and schema](02-enablement-and-schema.md)

**Feature doc:** [Stage 1: trigger](instant-analysis.md#stage-1-trigger) · [How the pipeline works today](instant-analysis.md#how-the-pipeline-works-today-the-parts-we-hook-into) · [Timing](instant-analysis.md#timing) · [Decisions](instant-analysis.md#decisions-2026-10-10)

## Goal

A live play reaches the analysis job, on the priority queue, only for games opted in.

## Scope

- A Producer consumer of `FootballPlayCompleted`, kept a thin shim (ingest-consumer convention), named for the event it consumes (consumer naming convention). It checks the kill switch and `CompetitionStream.LiveAnalysisEnabled`, then enqueues the analysis job on **`HangfireQueues.Live` (`"00-live"`)**.
- The analysis job skeleton: load the play and its participants by `PlayId`; hand off to the classifier (task 04) and the rest of the pipeline as they land.
- Idempotency: a redelivered event must not produce a second set of facts for the same play (enforced with task 02's uniqueness).

**Out of scope:**

- Classification, digging, phrasing (tasks 04–07).
- Publishing `PlayAnalysisGenerated` (task 10).

## Deliverables

- The consumer and job, registered in Producer.
- Unit tests for the gating (switch off, flag off, both on).

## Acceptance criteria

- [ ] With the switch on and one game's flag on, only that game's plays enqueue analysis jobs, and they run on `00-live`.
- [ ] A consumer or job failure never affects play ingestion (separate consumer; verify).
- [ ] Redelivering the same `FootballPlayCompleted` produces no duplicate work.

## Notes

- Only new plays in in-progress games fire `FootballPlayCompleted`; edits to existing plays don't (see Corrections).

## Outcome (2026-10-10)

Code: `src/SportsData.Producer/Application/Competitions/InstantAnalysis/`.

- **`FootballPlayCompletedConsumer`** (thin shim, Ingest): returns immediately when the kill switch is off;
  otherwise enqueues `IFootballPlayCompletedConsumerHandler.Process`, which `[Queue(HangfireQueues.Live)]` on the
  interface method routes to `00-live`. Registered in `Program.cs` for football modes only.
- **`FootballPlayCompletedConsumerDefinition`**: endpoint name `producer-football-play-completed`. Without it the
  kebab-case formatter names the queue `football-play-completed`, the same as the API's live last-play consumer, and
  on the shared local broker the two would compete (the API would miss plays even with analysis off). Prod brokers
  are separate, so prod is unaffected either way; no shovel change is needed (the consumer is on the sport broker
  where the event is published).
- **`FootballPlayCompletedConsumerHandler`** (Hangfire job): checks `CompetitionStream.LiveAnalysisEnabled`, skips
  plays that already have a live fact set, then loads the play and participants into an `AnalysedPlay` record
  (projected, no tracking). Tasks 04-08 plug in after the load.
- The per-game check lives in the job rather than the consumer, keeping the consumer free of database work.

Verified:

- Unit tests (`FootballPlayCompletedConsumerTests`, `FootballPlayCompletedConsumerHandlerTests`): switch off/on,
  live queue, flag off/on, redelivery skip, play + participants load.
- Local boot of `producer-ncaa`: healthy with no DI errors; queue `producer-football-play-completed` has 1
  consumer and is bound to the `FootballPlayCompleted` exchange, separate from the API's queue.

- End to end on the local stack (kill switch on via a compose override, the example game opted in, the contest
  replayed through `POST /api/contests/{id}/replay`): every play reached the job, which succeeded in ~5 ms with
  ~25 ms queue latency, on `00-live`.

**Finding (pre-existing, not fixed here): `Enqueue(queue, ...)` doesn't route.** The first end-to-end run used
`IProvideBackgroundJobs.Enqueue(HangfireQueues.Live, ...)`, and Hangfire recorded every job as enqueued to
`default`. Our Hangfire.PostgreSql storage (1.20.13) has no per-job queue column, so the queue-targeted overload
falls back to `default`; the `[Queue]` attribute is honoured (as for the daemon jobs). After switching to the
attribute, all jobs landed on `00-live`. The same overload is how `DocumentCreatedHandler` routes priority
(streamer) documents to `00-live`, so that routing most likely never takes effect either; none of the retained
`DocumentCreatedProcessor` jobs were priority, so it couldn't be confirmed from history.

**Finding for task 09:** `POST /api/contests/{id}/replay` republishes `FootballPlayCompleted` for every play, so
replaying a game whose stream has `LiveAnalysisEnabled = true` runs live analysis and would write live
(`AnalysisRunId` null) facts. The lab must drive the job with an `AnalysisRunId` instead of using the replay
endpoint, or the flag must be off when replaying.
