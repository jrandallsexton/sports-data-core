# Matchup Card — live-state enrichment plan

Working notes capturing the next round of changes to the picks-page
matchup card. Use as context in follow-up sessions; update in place as
decisions land.

## Goal

Give a quick at-a-glance read of in-progress games on the picks page
without forcing the user to click into the contest overview. Surface
sport-shaped live state directly on the card.

## Current structure (as of 2026-05-10)

- `src/UI/sd-ui/src/components/matchups/MatchupCard.jsx` — the card.
  Renders teams, odds, status, AI predictions, pick buttons.
- `src/UI/sd-ui/src/components/matchups/GameStatus.jsx` — extracted
  child component invoked at `MatchupCard.jsx:218`. Owns the
  Scheduled / InProgress / Final rendering and the classes named
  below. **This is where the `game-result` / `final-score` JSX lives.**
- `src/UI/sd-ui/src/components/matchups/MatchupCard.css` — selectors
  for `.game-result`, `.final-score`, `.score-display`, etc.
- Live state arrives via `ContestUpdatesContext` (populated by the
  merged `FootballPlayCompleted` / `BaseballPlayCompleted` SignalR
  events from PR Issue #9). `PicksPage.jsx` enriches each matchup with
  `getContestUpdate(contestId)` before passing to `MatchupCard`.

### Class names that need replacing (status-misleading)

The current names imply "Final" but the same wrappers also render the
InProgress and Scheduled branches:

| Current | Issue | Proposed (status-neutral) |
|---|---|---|
| `.game-result` | reads as "Final" only | `.game-status-block` |
| `.final-score` | inner row, used for live too | `.game-status-row` |
| `.final-score-link` | the wrapping `<Link>` | `.game-status-link` |
| `.result-label` | "FINAL" / "LIVE" / etc. | `.status-label` |
| `.game-result.scoring-play` | flash modifier | `.game-status-block.is-scoring-play` |
| `.game-result.game-result-stream` | upcoming-stream CTA modifier | `.game-status-block.is-stream` |
| `.score-display` | already neutral — keep | (no change) |

### Picks-page enrichment gap

`PicksPage.jsx` currently merges only football live fields onto the
matchup before render:

```js
status, awayScore, homeScore, period, clock,
possessionFranchiseSeasonId, isScoringPlay
```

Baseball-shaped fields from `BaseballPlayCompleted`
(`halfInning`, `inning`, `balls`, `strikes`, `outs`,
`runnerOnFirst/Second/Third`, `lastPlayDescription`) never reach the
card. **Any baseball live UI needs this merge extended first.**

## Decision: split per sport (probably)

Working hypothesis is that football and baseball should diverge at the
status component, mirroring the sport-specific-subtype pattern used
elsewhere in the codebase (`feedback_sport_specific_subtype_split` in
auto-memory).

```
GameStatus.jsx              ← thin dispatcher: routes by sport
  ├─ FootballGameStatus.jsx ← period, clock, possession 🏈, scoring flash
  └─ BaseballGameStatus.jsx ← inning + count + outs row, runners row,
                               last-play row, batting-team ⚾ indicator
```

Rationale:
- One component with `if (sport === ...)` branches piles up fast as
  more sports come online (NBA, NHL, soccer all have different live
  shapes).
- The football and baseball "live" rows are visually different enough
  that sharing markup is more painful than splitting.
- Shared concerns (Scheduled time/venue, Final score) can stay on the
  parent `GameStatus` dispatcher or be a small shared sub-component.

Open: do we keep a single `GameStatus.jsx` as the entry point that
dispatches, or replace it entirely with sport-keyed components rendered
directly from `MatchupCard.jsx`?

## Proposed in-progress layout (baseball)

Stacked rows inside the same outer block. Rows render conditionally
when their fields are populated.

```
┌─ .game-status-block ────────────────────────────┐
│  LIVE                          ← .status-label   │
│  ⚾ KC 3 — 2 SF                ← .score-display  │
│  Top 5 · 2-1 · 1 out          ← .live-state-summary │
│  Runners: 1B  2B               ← .live-state-runners │
│  "Single — runner to first."   ← .live-state-lastplay │
└──────────────────────────────────────────────────┘
```

- ⚾ icon position derived from `halfInning`: Top → away batting (icon
  on away side), Bottom → home batting (icon on home side). Mirrors
  football's 🏈-next-to-possessing-team pattern.
- Suppress rows whose fields are absent rather than showing dashes.
- `is-scoring-play` flash still fires when `isScoringPlay` lands true.
- Last play text should be ellipsised for long descriptions.

## Proposed in-progress layout (football)

Working assumption: leave football's existing layout (LIVE label,
period+clock, score with 🏈, scoring flash) but optionally append a
last-play row using the new `lastPlayDescription` field that
`FootballPlayCompleted` now carries. Decision pending.

## Open scope questions

1. **Football last-play row — yes / no?** `FootballPlayCompleted`
   carries `playDescription`, but the existing football card has been
   fine without it. Adding it is a one-line render but it does add
   visual density.
2. **Picks-page enrichment** — extend `PicksPage.jsx` merge to include
   baseball fields. Required for any of this to render on the picks
   page (admin debug page already merges them).
3. **Dispatcher vs per-sport components** — keep `GameStatus.jsx` as
   the entry point + add sport-specific children, OR delete
   `GameStatus.jsx` and call the right component directly from
   `MatchupCard`?
4. **Class renames in one shot vs piecemeal** — rename now (one big
   diff that touches CSS + JSX) or land the enrichment first and
   rename in a follow-up?

## Reference: live-state field shapes

From the current `ContestUpdatesContext.jsx` handlers:

**`handleFootballPlayCompleted`** writes:
- `period`, `clock`, `awayScore`, `homeScore`,
  `possessionFranchiseSeasonId`, `isScoringPlay`, `ballOnYardLine`,
  `lastPlayId`, `lastPlayDescription`, `lastPlayAt`, `lastUpdated`

**`handleBaseballPlayCompleted`** writes:
- `inning`, `halfInning`, `awayScore`, `homeScore`,
  `balls`, `strikes`, `outs`,
  `runnerOnFirst`, `runnerOnSecond`, `runnerOnThird`,
  `atBatAthleteId`, `pitchingAthleteId`,
  `lastPlayId`, `lastPlayDescription`, `lastPlayAt`, `lastUpdated`

**`handleStatusUpdate`** writes:
- `status`, `lastUpdated`

## Caveats

- `BaseballCompetitionPlay` doesn't materialize half-inning, outs,
  runner state, or athlete IDs at the play-entity level today — the
  baseball play processor and replay service emit these fields with
  defaults until the AtBat sourcing pipeline lands. UI should treat
  the values as best-effort and degrade gracefully (suppress rows
  rather than show "0-0 · 0 out" for a stale state).
- `MatchupCard` is rendered both on `/picks` and on `/admin/baseball`.
  Any change here lights up the debug page too — once baseball live
  rows are visible on the card, the `BaseballLiveStatePanel` debug
  readout on the admin page becomes redundant and can be deleted.
