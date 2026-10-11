# 15. Move to the database, first weak-pattern filters

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 2 · **Status:** not started

**Depends on:** [09 Admin replay lab](09-replay-lab.md), live data

**Feature doc:** [Learning what's weak](instant-analysis.md#learning-whats-weak) · [Stage 5: phrase](instant-analysis.md#stage-5-phrase) · [Decisions](instant-analysis.md#decisions-2026-10-10)

## Goal

Tune without deploys, and start filtering what the data shows is weak.

## Scope

- Templates move to a database catalog (decision 2), editable from the lab.
- Explosive-play thresholds and fact-type priorities move to the database (decision 5).
- First weak-pattern filters (by fact type, value range, situation or frequency), chosen from lab ratings and the logged volume of generated and dropped facts.

**Out of scope:**

- New fact families.

## Deliverables

- Database catalog and admin editing.
- Filter mechanism with its first rules, each justified by data.

## Acceptance criteria

- [ ] Changing a template or threshold in the lab takes effect without a deploy.
- [ ] Each filter's effect is visible in the logs (dropped with the filter's name).

## Notes

- To be split into smaller tasks when Phase 2 starts.
