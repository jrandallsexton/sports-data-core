# 11. Web display

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 1 (live) · **Status:** not started

**Depends on:** [10 Delivery](10-delivery.md)

**Feature doc:** [Stage 8: display](instant-analysis.md#stage-8-display) · [Decisions](instant-analysis.md#decisions-2026-10-10)

## Goal

Web MatchupCards show the facts for the current play, for everyone.

## Scope

- Register `"PlayAnalysis"` in `useSignalRClient.js`; store `lastPlayAnalysis` per contest in `ContestUpdatesContext`, keyed by `playId`.
- MatchupCard shows the facts under the last-play line only while `lastPlayAnalysis.playId === lastPlayId`, ordered by priority.
- A distinct style so facts read as commentary.
- On for everyone (decision 4).

**Out of scope:**

- Cold-load (task 13).

## Deliverables

- Context and card changes.
- Vitest tests (attach by playId, replaced when the next play lands).

## Acceptance criteria

- [ ] A fact arriving after its play attaches to it; a fact for an older play is ignored.
- [ ] ESLint clean; local end-to-end check on an opted-in game before PR.

## Notes

- Can run in parallel with tasks 12 and 13.
