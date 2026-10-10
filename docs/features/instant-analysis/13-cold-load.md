# 13. Cold-load

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 1 (live) · **Status:** not started

**Depends on:** [08 Persist and audit](08-persist-and-audit.md)

**Feature doc:** [Stage 6: persist](instant-analysis.md#stage-6-persist) · [Stage 8: display](instant-analysis.md#stage-8-display)

## Goal

A card loaded mid-game shows the latest play's facts without waiting for the next play.

## Scope

- Add the latest play's `PlayAnalysis` items to the matchup payload, using the same LATERAL-join pattern `GetMatchupsByContestIds.sql` uses for `LastPlayDescription`.
- Carry them through the API's matchup DTO to web and mobile, merged like the other live fields.

**Out of scope:**

- History of earlier plays' facts.

## Deliverables

- SQL and DTO changes.
- Client merge changes (web and mobile).

## Acceptance criteria

- [ ] Reloading a card mid-game shows the current play's facts.
- [ ] No extra query per card (stays in the existing batch).

## Notes

- Can run in parallel with tasks 11 and 12.
