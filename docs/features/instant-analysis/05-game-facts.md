# 05. Game facts

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 1 (proof of concept) · **Status:** not started

**Depends on:** [04 Classifier](04-classifier.md)

**Feature doc:** [Stage 3: dig](instant-analysis.md#stage-3-dig-the-data) · [Stage 4: select](instant-analysis.md#stage-4-select)

## Goal

Facts about the current game, from its own play rows: always fresh, no history needed.

## Scope

- Actor in-game counts as of this play ("his second interception today").
- Team in-game counts ("the defense's third takeaway today").
- Game longest ("longest run of the game").
- Each fact returned as a typed value with provenance (plays counted, sequence cut-off) for the `Facts` column.

**Out of scope:**

- Season and history facts (task 06).
- Phrasing (task 07).

## Deliverables

- Set-based queries over `CompetitionPlay` + `CompetitionPlayParticipant` for the competition.
- Unit tests.

## Acceptance criteria

- [ ] Counts include the triggering play and nothing after it in sequence order.
- [ ] Repeated facts are produced every time they apply (repeats are wanted).

## Notes

- Can run in parallel with task 06.
