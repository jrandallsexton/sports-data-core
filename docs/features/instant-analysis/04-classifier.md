# 04. Classifier

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 1 (proof of concept) · **Status:** done 2026-10-10 (uncommitted)

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

## Outcome (2026-10-10)

`src/SportsData.Producer/Application/Competitions/InstantAnalysis/PlayClassifier.cs`, called by the analysis job
after the play loads (logged as "Play classified" with kinds, actors and victims).

- A play yields zero or more `PlayEvent(Kind, Actors, Victim)`; a long touchdown pass yields three (passing TD,
  receiving TD, explosive pass).
- Kinds and roles (from the task 01 findings): Interception (26, 36, 63; actor `passDefender`, victim `passer`),
  Sack (7; every `sackedBy`, so split sacks credit both), FumbleLost (29; actor `recoverer`, victim `fumbler`),
  PassingTouchdown + ReceivingTouchdown (67), RushingTouchdown (68), ExplosiveRun (5, 68), ExplosivePass (24, 67;
  credits passer and receiver).
- Thresholds in code: explosive run ≥ 10 yards, explosive pass ≥ 20 yards (set 2026-10-10; Phase 2 moves them
  to the database).
- Classifies on the stored ESPN `TypeId`, not the mapped enum column. Matches roles by name, never by order.
- Type 63 interceptions keep the event with no actor, so victim facts (passer's interceptions thrown) still work.
  Touchdowns and explosive plays with nobody credited are dropped. Unresolved athletes are never credited.
- Out of Phase 1: safeties (20), field goals, return TDs, fumble return TD (39).
- Tests: `PlayClassifierTests` (each kind, thresholds at the boundary, split sacks, type 63, unresolved athletes,
  unknown and out-of-scope types).
