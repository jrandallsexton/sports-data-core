# 16. Depth

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 3 · **Status:** not started

**Depends on:** Phase 2 done

**Feature doc:** [Out of reach for now](instant-analysis.md#out-of-reach-for-now) · [Corrections](instant-analysis.md#corrections) · [Phases](instant-analysis.md#phases)

## Goal

Broaden and deepen once the core is trusted.

## Scope

- Career facts and highs, derived from plays (max `StatYardage` per athlete via `AthleteSeason.AthleteId`), valid only where participant rows exist.
- NFL, then MLB (its participants are lenient; its event already carries batter and pitcher).
- Corrections: retract facts for reversed plays (`PlayAnalysisRetracted`) once play-correction handling exists.
- Optional asynchronous LLM rephrasing, never on the live path.
- Per-contest SignalR groups.

## Deliverables

- Separate tasks per item when Phase 3 starts.

## Acceptance criteria

- [ ] Each item gets its own task with its own acceptance criteria.

## Notes

- Placeholder: to be broken down when Phase 2 is done.
