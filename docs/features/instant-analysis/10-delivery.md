# 10. Delivery

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 1 (live) · **Status:** not started

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

- [ ] On the local stack, an opted-in game's play produces a `PlayAnalysis` SignalR message with the right `PlayId`.
- [ ] The shovel audit confirms the exchange routes from each sport broker to the API broker.

## Notes

- Consumer class name is the queue name; check for collisions with any same-named consumer.
