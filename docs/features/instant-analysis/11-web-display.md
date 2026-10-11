# 11. Web display

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 1 (live) · **Status:** done 2026-10-11 (browser check passed)

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

- [x] A fact arriving after its play attaches to it; a fact for an older play is ignored.
- [ ] ESLint clean; local end-to-end check on an opted-in game before PR. (ESLint clean; the browser check needs a signed-in admin, see Outcome.)

## Notes

- Can run in parallel with tasks 12 and 13.

## Outcome (2026-10-10)

- `useSignalRClient.js` registers `"PlayAnalysis"` (`onPlayAnalysis`), wired in `MainApp.jsx` to
  `ContestUpdatesContext.handlePlayAnalysis`, which stores `lastPlayAnalysis: { playId, items }` per contest.
  A late analysis for an older play never replaces the one for the current play.
- `utils/playAnalysis.js`: `playAnalysisForLastPlay(liveUpdate)` returns the items only while
  `lastPlayAnalysis.playId === lastPlayId` (so facts that arrive before their play wait for it, and facts for an
  older play never show). Used by the `PicksPage` merge and the admin Football page's observer card.
- `MatchupCard` → `GameStatus` → `FootballGameStatusInProgress` renders the first `PLAY_ANALYSIS_MAX_SHOWN` (2)
  items under the last-play line, in the order delivered (priority order), styled `.live-state-analysis`: an accent
  rule on the left, upright and bolder than the italic play text, so they read as commentary. All items are still
  delivered and stored; only the card limits what it shows. Football only (baseball has no analysis).
- Tests (Vitest): `utils/playAnalysis.test.js`, `contexts/ContestUpdatesContext.test.jsx` (after the play, before
  the play, dropped on the next play, late older analysis ignored), `FootballGameStatusInProgress.test.jsx`. Full
  web suite: 153 passed. ESLint clean.

### Replay interval (added 2026-10-11)

The admin Football page's replay sent a play every second, too fast to read a play and its facts. The page now has a
"Seconds between plays" field (1-60, default 1, remembered per browser), sent as `?intervalSeconds=` through the API
and `ContestClient.ReplayContest` to Producer's replay endpoint, which rejects values outside 1-60 with a 400 and
passes the interval into `FootballContestReplayService`. Bulk replays (league week, season week) keep 1 second;
baseball is unchanged. Tests: `ReplayContestIntervalTests` (Producer), `ContestClientTests` (Core),
`AdminFootballPage.test.jsx` (web).

### Browser check (passed 2026-10-11)

Facts appeared under the last-play line during a replay on `/app/admin/football` and cleared on the next play.
Steps, for re-running it:


1. Local stack: `producer-ncaa` with the kill switch on and the example game's `LiveAnalysisEnabled = true`, plus
   `api`; clear the game's live `PlayAnalysis` rows so the replay re-analyses (or use another opted-in game).
2. Web: `npm start` in `src/UI/sd-ui`, sign in as an admin, open `/app/admin/football`, choose the contest, start a
   replay, and watch the card: facts appear under the last-play line on plays like the interception (sequence 8)
   and disappear when the next play lands.
