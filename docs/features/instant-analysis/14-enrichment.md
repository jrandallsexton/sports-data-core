# 14. Enrichment: conference, national, team-rank and milestone facts

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 2 · **Status:** not started

**Depends on:** Phase 1 done

**Feature doc:** ["Leads the SEC"](instant-analysis.md#leads-the-sec-phase-2) · [Stage 3: dig](instant-analysis.md#stage-3-dig-the-data) · [Decisions](instant-analysis.md#decisions-2026-10-10)

## Goal

Once facts are flowing, add the comparative facts: leads the conference, ranks nationally, team rank.

## Scope

- "Leads the SEC": option A first (`SeasonTypeLeader` filtered by `FranchiseSeason.GroupSeasonMap`, phrased honestly, e.g. "entered today leading the SEC"), then option B (our own live aggregation with a per-conference cache).
- National rank facts from `SeasonTypeLeader`.
- Team-rank facts from `FranchiseSeasonStatistic.Rank`.
- Season milestones crossed on this play (e.g. 1,000 rushing yards). Moved here from task 06 (2026-10-10). Needs the classifier to emit a yardage event for every run and catch, since a milestone can be crossed on a 3-yard run.

**Out of scope:**

- Career facts (task 16).

## Deliverables

- The new fact families, wired through select, phrase, persist and delivery.

## Acceptance criteria

- [ ] Lab replays show correct conference and national facts on known examples.
- [ ] Option B's cache stays within the dig budget.
- [ ] A run that crosses a milestone yields the milestone fact; the next run doesn't repeat "crossed".

## Notes

- To be split into smaller tasks when Phase 2 starts.
