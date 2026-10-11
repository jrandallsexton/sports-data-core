# 05. Game facts

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 1 (proof of concept) · **Status:** done 2026-10-10 (uncommitted)

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

## Outcome (2026-10-10)

`src/SportsData.Producer/Application/Competitions/InstantAnalysis/GameFacts.cs` (pure), fed by the analysis job,
which loads the game's plays (with participants) and its two competitors in two queries.

| Fact type | Kinds | Meaning |
|---|---|---|
| `GameCount` | interception, sack, fumble lost, passing/receiving/rushing TD | the actor's Nth today |
| `GameCountAgainst` | interception, sack, fumble lost | the victim's Nth today (interceptions thrown, times sacked, fumbles lost) |
| `TeamGameCount` | interception, fumble lost (takeaways together); sack | the defense's Nth today |
| `GameLongest` | explosive run, explosive pass | strictly longer than an earlier explosive run (10+ yards) or catch (20+ yards) in the game; the game's first explosive play never qualifies (decision 2026-10-11) |

- "As of this play": plays with sequence ≤ this play's; later plays are ignored (needed for lab replays).
- Earlier plays are classified with the same `PlayClassifier`, so counts and the play use one definition.
- **Team identity comes from the play's start team** (`StartFranchiseSeasonId` = offense; the defense is the other
  competitor). Checked on 2026 data: the start team is always one of the two competitors and is the defense in only
  4 of ~3,000 takeaway/sack plays. The athlete's own `AthleteSeason.FranchiseSeasonId` disagreed in 181 cases where
  that team isn't even in the game, so it is never used for team facts.
- Type 63 guard (from task 01): if this defense already had a plain interception today, the interceptor's
  `GameCount` is dropped with a reason; victim and team counts still flow.
- Each fact carries provenance (`source`, `countedThroughSequence`, and `previousLongest` for `GameLongest`), and
  dropped facts carry a reason, for task 08's logging.
- Tests: `GameFactsTests`.
