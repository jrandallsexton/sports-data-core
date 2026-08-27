# Player Pick'em Scoring

Status: v1 (read-time live scoring; persistence + standings deferred)
Date: 2026-08-27

## Model

Performance points from real statistics (see player-pickem.md — player
props were deliberately rejected). The SCORING MATRIX is data; the
ENGINE is code:

- `PlayerScoringRuleSet` — named, versioned rule set; `IsDefault` marks
  the fallback. Per-league selection is a later nullable FK on
  `PickemGroup` (schema shaped for it now, per the design doc).
- `PlayerScoringRule` — one row per scored stat:
  `{ StatKey, Points, PerUnits }` where
  `points = value * Points / PerUnits` (fractional, rounded to 2dp —
  the modern reading of "1 point per 25 yards"; floor-per-full-unit is
  a one-line engine change if ever wanted).

`StatKey` is `category.statName` from the canonical
AthleteCompetitionStatistic chain (e.g. `passing.passingYards`), OR a
`derived.*` key the engine computes from raw stats before applying the
matrix. Derivations are engine code because they're structural (a
missed kick IS attempts minus made); point VALUES are matrix data.

Derived keys (v1):

| Key | Derivation |
|---|---|
| derived.missedExtraPoints | kicking.extraPointAttempts − extraPointsMade |
| derived.fieldGoalsMade17_39 | made1_19 + made20_29 + made30_39 |
| derived.fieldGoalsMissed17_39 | (att1_19+att20_29+att30_39) − made17_39 |
| derived.fieldGoalsMade40_49 / Missed40_49 | bucket made / att − made |
| derived.fieldGoalsMade50_59 | kicking.fieldGoalsMade50_59 |
| derived.fieldGoalsMade60Plus | kicking.fieldGoalsMade60_99 |

## Default matrix (seeded; source: operator's standard chart)

Position-agnostic — the union of the chart's per-position tables (a
QB's receiving yards score like anyone's; the chart simply never lists
stats a position rarely accrues):

| StatKey | Points | PerUnits |
|---|---|---|
| passing.passingYards | 1 | 25 |
| passing.passingTouchdowns | 6 | 1 |
| passing.interceptions | −2 | 1 |
| rushing.rushingYards | 1 | 10 |
| rushing.rushingTouchdowns | 6 | 1 |
| receiving.receivingYards | 1 | 10 |
| receiving.receivingTouchdowns | 6 | 1 |
| fumbles.fumblesLost | −2 | 1 |
| passing.twoPtPass / rushing.twoPtRush / receiving.twoPtReception | 2 | 1 |
| kicking.extraPointsMade | 1 | 1 |
| derived.missedExtraPoints | −2 | 1 |
| derived.fieldGoalsMade17_39 | 3 | 1 |
| derived.fieldGoalsMissed17_39 | −2 | 1 |
| derived.fieldGoalsMade40_49 | 4 | 1 |
| derived.fieldGoalsMissed40_49 | −1 | 1 |
| derived.fieldGoalsMade50_59 | 5 | 1 |
| derived.fieldGoalsMade60Plus | 6 | 1 |

No receptions rule — the chart is standard (non-PPR). DEF rules
(blocked kick, safety, sacks, points-allowed tiers) wait for the DEF
slot; the tier shape is already representable as derived keys.

## Flow (v1 — live, read-time)

1. Producer: `GetAthleteStatlines(contestIds, athleteSeasonIds)` — for
   each (athleteSeason, contest) pair, the flattened
   `category.statName → value` map for the scoring categories
   (passing/rushing/receiving/fumbles/kicking). Competition resolves
   via Contest 1:1. Live-fresh via the play-driven refresh debounce.
2. API: `GetMyPlayerLineup` batches one statline call for the lineup's
   anchored slots, applies the default rule set, and returns per-slot
   `Points` + a compact `StatLine` display string + lineup `TotalPoints`.
   No persistence — recompute on every read while games run.
3. Web: points on each slot chip + a lineup total.

## Refresh triggers

No polling, no background jobs (operator constraint):

- **Phase 1 (shipped with v1)**: the play-completed SignalR events
  already flowing Producer → API → ContestUpdatesContext stamp
  `contests[id].lastUpdated`. The roster builder watches the timestamps
  of ITS anchored contests and, on activity, silently refetches the
  lineup twice — ~45s after the play (fast feedback) and again at ~4min
  (after the Producer stat-document debounce catches up). Quiet games
  cost zero requests; nobody polls.
- **Phase 2 (next PR, with persistence + standings)**: the stat
  DOCUMENT processor publishes `AthleteCompetitionStatsUpdated
  (contestId, athleteSeasonId)` → shovel to API → consumer matches
  PlayerLineupSlot anchors → recompute/persist → SignalR broadcast to
  the league group. Final results persist on the existing
  contest-finalized event chain (no jobs). Precise where Phase 1 is
  approximate.

## Deferred

- Post-final persistence (slot/lineup results on game finalization,
  mirroring team-pick scoring) + weekly/season standings.
- Per-league rule-set selection UI; rule-set admin editor.
- DEF scoring (with the DEF slot).
- Threshold BONUSES (300-yard game, etc.) — the rule schema gains a
  RuleType when needed; v1 is linear + derived-event only.
