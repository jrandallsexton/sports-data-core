# 06. Season facts

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 1 (proof of concept) · **Status:** done 2026-10-10 (uncommitted)

**Depends on:** [01 Data spike](01-data-spike.md), [04 Classifier](04-classifier.md)

**Feature doc:** ["As of this play"](instant-analysis.md#as-of-this-play-how-a-season-count-is-computed) · [Stage 3: dig](instant-analysis.md#stage-3-dig-the-data) · [Out of reach for now](instant-analysis.md#out-of-reach-for-now)

## Goal

Season-to-date facts that are correct at the instant of the play.

## Scope

- Season count as of this play = season-to-date from games **before** the current competition + qualifying plays in the current competition up to and including this play.
- The season-to-date source chosen in task 01 (`AthleteSeasonStatistic` or Σ `AthleteCompetitionStatistic`), never double-counting part of the current game.
- Provenance for each number in the `Facts` jsonb.

**Out of scope:**

- Conference and national analysis (task 14).
- Season milestones (e.g. crossing 1,000 rushing yards): moved to Phase 2, task 14 (decided 2026-10-10). They need a yardage event for every run and catch, not just explosive ones.
- Career facts (task 16).

## Deliverables

- The as-of query (or queries) per stat family.
- Unit tests, including the example interception.

## Acceptance criteria

- [x] The example interception yields "second of the season" (unit test; the real document has since been overwritten, see Outcome).
- [x] Dig time stays within the timing budget in the feature doc (season-document load: 0.2 ms on the example).

## Notes

- Can run in parallel with task 05.

## Outcome (2026-10-10)

`src/SportsData.Producer/Application/Competitions/InstantAnalysis/SeasonFacts.cs` (pure), fed by the analysis job,
which loads the game's kickoff and the season documents of the play's actors and victims (only the stats used).

| Fact type | Kinds | Season stat (`category.stat`) |
|---|---|---|
| `SeasonCount` | interception | `defensiveInterceptions.interceptions` |
| | sack | `defensive.sacks` |
| | fumble lost (recoverer) | `general.fumblesRecovered` |
| | passing / receiving / rushing TD | `passing.passingTouchdowns`, `receiving.receivingTouchdowns`, `rushing.rushingTouchdowns` |
| `SeasonCountAgainst` | interception, sack, fumble lost | `passing.interceptions`, `passing.sacks`, `general.fumblesLost` |
| `SeasonLongest` | explosive run; explosive pass (passer and receiver each) | `rushing.longRushing`; `passing.longPassing`, `receiving.longReception` |

- Season count = the season document's value + this game's qualifying plays up to and including this one.
  `SeasonLongest` must be strictly longer than both the document's long and the athlete's earlier plays today.
- **Which document (decided 2026-10-10):** live uses the newest; a lab replay uses only a document written before
  the game's kickoff. No qualifying document drops the fact, with the reason. The cutoff is a parameter of the
  loader; the live job passes none, and task 09 passes the kickoff.
- **Season documents have no history.** `AthleteSeasonStatisticsDocumentProcessor` deletes and re-inserts the row
  under the same id on every refresh, so `CreatedUtc` is the last rewrite. (Each athlete-season has two split-0
  rows, from two ESPN refs, written seconds apart with the same values.) Consequence, measured with
  [`06a`](sql/06a-pre-kickoff-season-doc-coverage.sql) across 53,486 athlete-games in finalized 2026 games:
  26,123 (49%) have any season document, and only **4,538 (8.5%) have one written before kickoff**. Lab replays of
  past games will therefore drop most season facts.
- **The worked example** ([`06b`](sql/06b-example-interception-season-count.sql)): the interceptor's documents
  were rewritten 2026-10-04, the day after the game, and already include it (value 2). Live on today's data would
  say "3rd" (a double count); the lab correctly drops it; at game time the document said 1, so live would have said
  "2nd". The old value is gone, so "second of the season" is shown by the unit test, not from data.
- Split sacks credit a half to each sacker, matching ESPN's season stat (the game count in task 05 counts 1 each).
- Fumble recoveries: ESPN's `fumblesRecovered` includes own-team recoveries; the in-game part counts opponent
  recoveries (type 29) only.
- Type 63 guard: drops the interceptor's `SeasonCount` too, as decided in task 01.
- Provenance: `seasonStatisticId`, `seasonStatisticCreatedUtc`, `documentRule` (`newest` / `beforeKickoff`),
  `writtenAfterKickoff` (a live document that may already include part of this game, for the post-game audit),
  `stat`, `seasonValue`, `gameValue`, `countedThroughSequence`; `previousGameLongest` on `SeasonLongest`.
- Tests: `SeasonFactsTests`.
