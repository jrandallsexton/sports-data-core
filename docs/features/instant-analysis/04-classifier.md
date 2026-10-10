# 04. Classifier

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 1 (proof of concept) · **Status:** not started

**Depends on:** [01 Data spike](01-data-spike.md), [03 Trigger](03-trigger.md)

**Feature doc:** [Stage 2: classify](instant-analysis.md#stage-2-classify-what-happened-who-did-it) · [Worked example](instant-analysis.md#worked-example-the-interception-in-_debug_instant_analysissql)

## Goal

Turn a play into a kind, an actor (and sometimes a victim), and the fact families worth digging.

## Scope

- Kinds for Phase 1: interception, sack, passing TD, rushing TD, explosive run, explosive pass (plus fumble lost if task 01 confirms its roles).
- Classify on the `PlayType` enum ids (`src/SportsData.Core/Common/PlayType.cs`), not the `lkPlayType` table, which is missing newer types.
- The role mapping from task 01 (e.g. interception actor = `passDefender`).
- Explosive-play thresholds, **in code** for now (decision 5).

**Out of scope:**

- Database-backed thresholds (task 15).
- Kinds beyond Phase 1.

## Deliverables

- One small classifier with its thresholds and role mapping in one place.
- Unit tests per kind, using the real fixture plays.

## Acceptance criteria

- [ ] The example interception classifies as Interception with actor = the intercepting defender, victim = the passer.
- [ ] A run below the threshold isn't classified as explosive; one at or above is.
- [ ] Unknown or unmapped play types classify to nothing, without throwing.

## Notes

- Use `EspnFootballNcaaEventCompetitionPlays.json` (Producer unit test data) for fixture plays.
