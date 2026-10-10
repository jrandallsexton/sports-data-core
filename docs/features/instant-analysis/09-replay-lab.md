# 09. Admin replay lab

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 1 (proof of concept) · **Status:** not started

**Depends on:** [08 Persist and audit](08-persist-and-audit.md)

**Feature doc:** [Ship dark and the lab](instant-analysis.md#ship-dark-and-the-lab) · [Learning what's weak](instant-analysis.md#learning-whats-weak)

## Goal

Review real games before anything is user-facing, and start collecting the data that tells us what's weak.

## Scope

- Run the pipeline over a completed game's plays with a new `AnalysisRunId` (lab rows never mix with live).
- Compute "as of" the replayed game: season sums restricted to competitions that started before it.
- Bypass the per-game flag (completed games may have no stream row).
- An admin page: per play, the generated facts, the dropped facts with reasons, and 0–4 ratings (SmackBot Lab pattern).

**Out of scope:**

- Live delivery (task 10).

## Deliverables

- The replay endpoint/command, admin-only.
- The admin page (web).
- Rating storage.

## Acceptance criteria

- [ ] Replaying the example game shows the interception's facts as they would have been at that moment.
- [ ] Two runs of the same game don't collide.
- [ ] Ratings are stored per fact and run.

## Notes

- The existing contest replay services re-publish stored plays; the lab may reuse them or drive the job directly.
