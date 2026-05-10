# Matchup Card — live-state enrichment

Working notes for the matchup card's live-state work. Updated in place
as decisions land. Use as context in follow-up sessions.

## Status

**MLB live rows landed** in `feat/matchup-card-mlb-enrichment` (PR
forthcoming): score line picks up the live status, score, and a
last-play description on every `BaseballPlayCompleted`. Inning/count/
outs and runner rows render when populated; suppressed when the
canonical play data emits defaults (which is most of the time today
— see "Next blocker" below).

**Football left untouched.** Existing FB block was lifted verbatim
into `FootballGameStatusInProgress.jsx`; class names, behavior, and
visual output are unchanged. The deferred decisions (last-play row on
FB, status-neutral class renames on the FB markup) live in
`Deferred / Open` below.

## Architecture as it landed

```
MatchupCard.jsx
  └─ GameStatus.jsx                       ← thin dispatcher
       ├─ Final / Scheduled branches      ← shared markup, unchanged
       └─ InProgress branch:
            ├─ BaseballGameStatusInProgress.jsx  ← NEW (status-neutral classes)
            └─ FootballGameStatusInProgress.jsx  ← FB block, lifted verbatim
```

Routing key: `leagueSport` (backend Sport enum name, e.g.
`"BaseballMlb"`). When `leagueSport` is missing or unrecognized, the
dispatcher falls back to football rendering — preserves prior behavior
on routes that haven't been re-plumbed.

### File map

| File | Role |
|---|---|
| `components/matchups/GameStatus.jsx` | Dispatcher. Owns Scheduled / Final markup. |
| `components/matchups/FootballGameStatusInProgress.jsx` | FB live block (period, clock, score with 🏈, scoring flash). |
| `components/matchups/BaseballGameStatusInProgress.jsx` | MLB live block (LIVE label, score with ⚾ on batting team, inning+count+outs row, runners row, last-play row — each suppressed at defaults). |
| `components/matchups/MatchupCard.jsx` | Threads `leagueSport` + the union of FB/MLB live fields into `GameStatus`. |
| `components/matchups/MatchupCard.css` | New status-neutral wrappers (`.game-status-block`, `.game-status-row`, `.game-status-link`, `.status-label`) + MLB row styles. Old FB classes (`.game-result`, `.final-score`, etc.) still in use; rename deferred. |

### Class names (current state)

| Sport | Wrapper / inner | Class |
|---|---|---|
| Football InProgress / Final / Scheduled | outer / inner / link / label | `.game-result` / `.final-score` / `.final-score-link` / `.result-label` |
| Baseball InProgress | outer / inner / link / label | `.game-status-block` / `.game-status-row` / `.game-status-link` / `.status-label` |
| Both | score line | `.score-display` (already neutral) |

The neutral names cover only baseball today. Renaming the football
markup is intentionally deferred — see `Deferred / Open` below.

## Reference: live-state field shapes

From `ContestUpdatesContext.jsx` handlers:

**`handleFootballPlayCompleted`**: `period`, `clock`, `awayScore`,
`homeScore`, `possessionFranchiseSeasonId`, `isScoringPlay`,
`ballOnYardLine`, `lastPlayId`, `lastPlayDescription`, `lastPlayAt`,
`lastUpdated`.

**`handleBaseballPlayCompleted`**: `inning`, `halfInning`, `awayScore`,
`homeScore`, `balls`, `strikes`, `outs`, `runnerOnFirst`,
`runnerOnSecond`, `runnerOnThird`, `atBatAthleteId`, `pitchingAthleteId`,
`lastPlayId`, `lastPlayDescription`, `lastPlayAt`, `lastUpdated`.

**`handleStatusUpdate`**: `status`, `lastUpdated`.

## Parent-merge gotcha (important)

`MatchupCard` is a "dumb" component — props in, render out. It does
NOT call `useContestUpdates()` itself. Each *parent* that renders the
card and wants live updates is responsible for:

1. Calling `useContestUpdates()` and reading `getContestUpdate(contestId)`.
2. Merging the returned `live` fields onto the matchup before passing
   to `MatchupCard`.

Two parents do this today, **and both must be kept in sync** when a
new live field is introduced:

- `components/picks/PicksPage.jsx` — `enrichedMatchups` useMemo.
- `components/admin/AdminBaseballPage.jsx` — `enrichedMatchup` useMemo.

If one is updated and the other isn't, `MatchupCard` silently gets
undefined values for the new fields and the new rows will fail to
render on whichever page was missed. (This bit me on the admin page
when the picks-page enrichment was extended for MLB.)

Future cleanup options (not for this PR):
- Move the subscription into `MatchupCard` itself (one place to
  update, but couples the card to the context — bad for snapshots /
  tests / non-live views).
- Extract a `useEnrichedMatchup(matchup)` hook so callers can't
  forget to merge a new field.

## Next blocker — canonical play data

The MLB live emission (`BaseballEventCompetitionPlayDocumentProcessor`
and `BaseballContestReplayService`) currently sends defaults for
fields that aren't materialized on `BaseballCompetitionPlay`:

| Wire field | Source today | What gets emitted |
|---|---|---|
| `HalfInning` | not on the play entity (lives on `BaseballCompetitionStatus`) | empty string |
| `Outs` | not on the play entity | `0` |
| `RunnerOnFirst` / `Second` / `Third` | not on the play entity | `false` |
| `AtBatAthleteId` | not on the play entity | `null` |
| `PitchingAthleteId` | not on the play entity | `null` |

Visible consequence on `MatchupCard`:
- ⚾ batting-team indicator never appears (derived from `halfInning`
  — empty string fails the Top/Bottom check).
- Inning row would render with bare `inning` value but is suppressed
  whenever `halfInning` is also empty (current behavior — see
  `hasInningRow` guard in `BaseballGameStatusInProgress.jsx`).
- Runners row never appears (all three booleans false).

The fix is sourcing-side: capture the missing fields onto
`BaseballCompetitionPlay` (or read from `BaseballCompetitionStatus`
during the play emission). That's the next PR per user's call.

## Deferred / Open

1. **FB last-play row** — `FootballPlayCompleted` carries
   `playDescription`, but FB rendering wasn't touched in this pass.
   Decision deferred until FB matchup card is revisited.
2. **FB class renames** (`.game-result` → `.game-status-block` etc.)
   — left for a follow-up; would touch CSS + JSX in
   `FootballGameStatusInProgress`. Not blocking anything.
3. **`BaseballLiveStatePanel`** in `AdminBaseballPage` — kept on
   purpose because it renders defaulted fields with `?? '—'`
   placeholders, useful while the canonical-data gap above persists.
   Delete when sourcing fills the fields for real.
4. **Subscription pattern** — see "Parent-merge gotcha" above. Both
   parents must be updated when adding a new live field.

## Caveats

- `BaseballDebugCard` (right column of `/admin/baseball`) subscribes
  to `BASEBALL_DEBUG_CONTEST_ID` (sandbox), separate from the
  contestId entered in the input form. So the diamond on the right
  reacts to synthetic events; the matchup card on the left reacts to
  real replay events. Same code path, different keys. Mixing the two
  for end-to-end visual proof requires firing synthetic events at the
  real contestId or extending `BaseballDebugCard` to accept the
  page-level contestId — not done here.
- `MatchupCard` is rendered on both `/picks` and `/admin/baseball`.
  See "Parent-merge gotcha" above for the implication.
