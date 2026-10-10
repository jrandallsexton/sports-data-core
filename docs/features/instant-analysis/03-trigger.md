# 03. Trigger

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 1 (proof of concept) · **Status:** not started

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
