# 12. Mobile display

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 1 (live) · **Status:** done 2026-10-11 (uncommitted); on-device check pending, see Outcome

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

- [x] Same attach/replace behaviour as web.
- [x] tsc clean.
- [ ] On-device check on an opted-in game.

## Notes

- JavaScript only, so it can ship over the air. Can run in parallel with tasks 11 and 13.

## Outcome (2026-10-11)

- `src/types/signalR.ts`: `PlayAnalysisItem` and `PlayAnalysisPayload`.
- `contestUpdatesStore.handlePlayAnalysis` stores `lastPlayAnalysis: { playId, items }` per contest, with the web
  rule: a late analysis for an older play never replaces the one for the current play.
- `useSignalRClient` registers `"PlayAnalysis"`.
- `src/utils/playAnalysis.ts`: `playAnalysisForLastPlay(live)` returns the items only while
  `lastPlayAnalysis.playId === lastPlayId`; `PLAY_ANALYSIS_MAX_SHOWN` = 2. `MatchupCard` merges it as
  `playAnalysis` (new optional field on `Matchup`).
- `GameStatus` (football in progress) renders the first two facts under the last-play line: centered, 12pt,
  weight 600, primary text colour, against the 11pt italic muted play text. Baseball unchanged.
- Tests (Jest): store (after/before the play, dropped on the next play, late older analysis ignored, missing ids),
  `playAnalysis.test.ts`, `MatchupCard.live.test.tsx` (first two shown, cleared on the next play), and the hook
  registers `PlayAnalysis`. Full suite: 374 passed, 33 suites. tsc clean.

### On-device check (pending)

Run the local stack (`producer-ncaa` with the kill switch on, `api-all`), point the dev build at it, open a league
whose week includes the example game (`98035135-...`), and replay it from `/app/admin/football` with a few seconds
between plays. Facts should appear under the last play (e.g. the interception) and clear on the next play.
JavaScript only, so it ships over the air.
