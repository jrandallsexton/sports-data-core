# 12. Mobile display

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 1 (live) · **Status:** not started

**Depends on:** [10 Delivery](10-delivery.md)

**Feature doc:** [Stage 8: display](instant-analysis.md#stage-8-display) · [Decisions](instant-analysis.md#decisions-2026-10-10)

## Goal

Mobile MatchupCards show the facts for the current play, for everyone.

## Scope

- Subscribe to `"PlayAnalysis"` in the mobile SignalR client; store `lastPlayAnalysis` per contest in `contestUpdatesStore`.
- MatchupCard shows the facts under the last-play line while the `playId` matches, ordered by priority.
- Add the payload type to `src/types/signalR.ts`.

**Out of scope:**

- Cold-load (task 13).

## Deliverables

- Store, client and card changes.
- Jest tests mirroring the web behaviour.

## Acceptance criteria

- [ ] Same attach/replace behaviour as web.
- [ ] tsc clean; on-device check on an opted-in game.

## Notes

- JavaScript only, so it can ship over the air. Can run in parallel with tasks 11 and 13.
