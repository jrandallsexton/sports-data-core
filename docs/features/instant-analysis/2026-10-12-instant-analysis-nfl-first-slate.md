# Instant analysis: NFL first live slate (2026-10-11)

**Feature:** [Instant Analysis](instant-analysis.md)
**Data:** local restore of prod `sdProducer.FootballNfl`, taken about 05:00 ET on 2026-10-12. Drop reasons are from prod Seq.
**SQL:** [`audit-a-live-facts-integrity-and-recount.sql`](sql/audit-a-live-facts-integrity-and-recount.sql)

## The slate

| | |
|---|---|
| Games opted in | 13 (12 finished; the Monday game had no plays yet) |
| Live facts | 389 on 170 plays (about 7% of 2,317 plays) |
| Lab runs, ratings | none |
| Fact types written | GameCount 183, GameCountAgainst 86, TeamGameCount 86, GameLongest 34 |
| Season facts written | **0** (484 dropped) |

## Findings

### 1. No season facts: NFL 2026 season stats were never sourced

All 484 season-fact drops had the same reason: "No season document written before kickoff." None of the 162 athletes
in the facts has an `AthleteSeasonStatistic` row for 2026. The newest NFL season stats are 2025, last written
2026-08-15.

This is a sourcing gap, not an instant-analysis bug. Until it's fixed, NFL can only produce game facts.

NCAA does have 2026 rows, written in weekly batches (2,004 on 09-27, 8,663 on 10-04). Nothing was written for 10-11 as of
the restore. The selector drops a season fact when the athlete's document predates the team's previous game being
finalized. So stale NCAA documents cost season facts rather than producing wrong numbers. Check when the NCAA refresh
runs before the NCAA debut.

### 2. Classifier ignores type 80 and type 39 (our bug, 7 wrong facts)

- **Type 80** is NFL only: a sack where the defense recovers the fumble (34 plays in the 2026 NFL season, against 489
  plain sacks).
- **Type 39** is a fumble return touchdown (3 NFL plays and 27 NCAA plays in 2026).

`PlayClassifier` produces nothing for either type, with three effects:
- The play gets no sack or takeaway facts of its own.
- Every later count in that game is short by one: sacks by that defense, times that quarterback was sacked, and
  takeaways.
- Of 43 later sack and fumble facts in affected games, 7 were wrong when published. Examples:
  - "C. Keenum has been sacked once this game" (it was twice).
  - "Broncos' third sack of the game" (it was the fourth).
  - "49ers' third takeaway of the game" (it was the fourth).

Both types carry the roles needed: `sackedBy`, `passer`, `recoverer` and `fumbler`. **Fix:** classify 80 as a Sack
plus a FumbleLost, and 39 as a FumbleLost. Then rerun the type-id survey (`01a`) on NFL, since the task 01 spike only
covered NCAA.

### 3. ESPN changed six facts after they were published (correct at the time)

| Change | Facts |
|---|---|
| A fumble reversed on replay (Bengals at Dolphins; the play became a completed pass) | 3: the recoverer's first recovery, the fumbler's first lost fumble, "Dolphins' first takeaway" |
| A sack re-credited (Browns): first credited to M. Collins, later to A. Huntington | 1: Collins was told "first sack of the game" twice, on two different plays |
| Yardage revised by 1 yard | 2 "longest of the game" facts (27 to 28, 24 to 23) |

That is 6 of 389 facts (1.5%) wrong by morning, none of them our bug. We need a policy (see the plan below). Also, a
play can be reclassified to an unknown type while keeping its facts: the Willis sack-fumble was type 29 live and 80 by
morning.

### 4. Integrity is clean

None of the following occurred:
- Duplicate ordinals or ordinal gaps.
- Orphan plays, or facts attached to the wrong competition or contest.
- Empty text.
- Facts in games that weren't opted in.

## Low-value facts

Cards show two facts (`PLAY_ANALYSIS_MAX_SHOWN = 2`, web and mobile). Facts are ranked by fact type alone.

| Pattern | Volume |
|---|---|
| Plays where every fact is a first or a once | 74 of 170 plays (169 facts, 43% of all facts) |
| A player's first of the game (`GameCount` = 1) | 146 of 183 |
| A victim's first (`GameCountAgainst` = 1): "sacked once", "first lost fumble" | 48 of 86 |
| A team's first (`TeamGameCount` = 1) | 39 of 86 |
| Three facts restating one sack (sacker, team, quarterback) | 57 of 58 sack plays |
| Higher counts ranked out of the top two by a first | 28 facts |
| "Longest of the game" won by 2 yards or less | 11 of 34; up to 7 claims in one game |

Play text below is shortened. "Play N" is how many plays into the game it came.

### Plays where every fact is a first or a once (74 plays)

A "first" is true on every one of these plays. Whether it says anything depends on **when** it happens and **what
else** is going on. Phase 2 exists for exactly that: situation filters and season context. The same sentence can be
obvious, informative, or beside the point:

| Quarter | Plays | Facts |
|---|---|---|
| 1st | 28 | 67 |
| 2nd | 17 | 39 |
| 3rd | 15 | 34 |
| 4th | 11 | 22 |
| OT | 3 | 7 |

7 came within the game's first 10 plays, 12 within the first 20. 45 of 74 (61%) came in the first half.

**Obvious from the clock: the game has barely started.**

| Game | When | Play | Facts |
|---|---|---|---|
| CIN @ MIA | Q1 14:03, play 5 | Burrow pass to Gesicki for 4 yards, TOUCHDOWN | "J. Burrow's first touchdown pass of the game." "M. Gesicki's first touchdown catch of the game." |
| NYG @ WSH | Q1 13:00, play 6 | Croskey-Merritt up the middle for 33 yards, TOUCHDOWN | "J. Croskey-Merritt's first rushing touchdown of the game." |
| CHI @ GB | Q1 13:27, play 6 | Bagent sacked for -11 (Wyatt) | "D. Wyatt's first sack of the game." "Packers' first sack of the game." "T. Bagent has been sacked once this game." |
| DET @ ARI | Q1 13:39, play 5 | Goff sacked for -12 (Baker) | "B. Baker's first sack of the game." "Cardinals' first sack of the game." "J. Goff has been sacked once this game." |
| SF @ SEA | Q1 12:40, play 9 | Darnold pass INTERCEPTED by Green | "R. Green's first interception of the game." "49ers' first takeaway of the game." "S. Darnold's first interception thrown this game." |
| DEN @ LAC | Q1 6:53, play 21 | Dobbins left end for 2 yards, TOUCHDOWN | "J.K. Dobbins' first rushing touchdown of the game." |

**A first that does say something: the absence until now is the story.**

| Game | When | Play | Facts | What it's really saying |
|---|---|---|---|---|
| IND @ PIT | Q4 6:00, 22-31 | D. Jones sacked for -1 (Heyward) | "Steelers' first sack of the game." | Pittsburgh hadn't sacked Jones in three and a half quarters. |
| MIN @ NO | Q4 3:53, 23-13 | Shough sacked for -9 (Van Ginkel) | "Vikings' first sack of the game." | First sack with under four minutes left. |
| CLE @ NYJ | Q4 6:15, 12-10 | Watson pass to Boston for 29 yards, TOUCHDOWN | "D. Watson's first touchdown pass of the game." | No touchdown pass until the 4th quarter of a 12-10 game. |
| HOU @ TEN | Q4 14:09, 26-3 | Stroud pass to Noel for 9 yards, TOUCHDOWN | "C.J. Stroud's first touchdown pass of the game." | Up 26-3, and his first touchdown pass only now. |
| CLE @ NYJ | Q4 4:01, 12-10 | Punter Bojorquez fumbles (aborted), Jets recover | "Jets' first takeaway of the game." | A late takeaway in a two-point game. |

**A first that misses the story.**

| Game | When | Play | Facts | The real story |
|---|---|---|---|---|
| BAL @ ATL | OT 2:57, 30-23 | Huntley up the middle for 1 yard, TOUCHDOWN | "T. Huntley's first rushing touchdown of the game." | The go-ahead touchdown in overtime. |
| BAL @ ATL | OT 2:11, 30-23 | Tagovailoa pass INTERCEPTED by Hamilton | "K. Hamilton's first interception of the game." "Ravens' first takeaway of the game." "T. Tagovailoa's first interception thrown this game." | An interception with Atlanta trailing in overtime. |
| CIN @ MIA | Q4 0:45, 31-24 | Willis up the middle for 1 yard, TOUCHDOWN | "M. Willis' first rushing touchdown of the game." | A late score that cut it to a one-score game. |

**Volume on a single play.** A split sack makes four facts out of one sack: LV @ NE, Q2 0:48, Maye sacked for -3
(split by Laulu and Koonce): "J. Laulu's first sack of the game." "M. Koonce's first sack of the game." "Raiders' first
sack of the game." "D. Maye has been sacked once this game."

**What this means for Phase 2.**
- **Game time:** a first in the 1st quarter, or within the first ~20 plays, is near certain and should be suppressed.
- **Team firsts:** a team's first sack or takeaway late in the game (say the 4th quarter) carries information.
- **Individual firsts:** a player's first late in the game rarely does.
- **Score and situation:** go-ahead, overtime and late one-score plays need their own facts (task 14 or later); a
  "first" is the wrong frame for them.
- **Season context** turns a first into something worth saying ("his 6th rushing touchdown of the season"). NFL has
  none until 2026 season stats are sourced (finding 1).

### A player's first of the game (146 of 183)

Every touchdown on the list above produces one or two of these. Fumble recoveries are always 1 (13 of 13): nobody
recovered two in a game. A count is informative at 2 or more: "D. Maye's fourth touchdown pass of the game.", "Jets'
sixth sack of the game.", "Packers' fourth takeaway of the game."

### Three facts restating one sack (57 of 58 sack plays)

Every sack produces the sacker's count, the defense's count and the quarterback's count:

> BAL @ ATL, Q3 2:14: Huntley sacked for -7 (Ebukam).
> "S. Ebukam's first sack of the game." · "Falcons' fourth sack of the game." · "T. Huntley has been sacked 3 times this game."

Only the second and third facts carry information, and they say the same thing from two sides.

### Higher counts ranked out of the top two (28 facts)

The type ranking (GameCount 40, TeamGameCount 50, GameCountAgainst 70) puts a first ahead of a running count. The
`[#n]` is the stored rank; cards show #0 and #1.

| Game | When | Facts |
|---|---|---|
| BAL @ ATL | Q3 2:14 | "S. Ebukam's first sack of the game." [#0] · "Falcons' fourth sack of the game." [#1] · "T. Huntley has been sacked 3 times this game." [#2] |
| CLE @ NYJ | Q2 10:09 | "S. Kamara's first sack of the game." [#0] · "Browns' third sack of the game." [#1] · "G. Smith has been sacked 3 times this game." [#2] |
| CHI @ GB | Q2 0:57 | "B. Cisse's first sack of the game." [#0] · "Packers' second sack of the game." [#1] · "T. Bagent has been sacked 2 times this game." [#2] |

### "Longest of the game" (34 claims)

The title changes hands often, sometimes by a yard. CLE @ NYJ had seven claims:

| When | Fact | Margin |
|---|---|---|
| Q1 9:17 | "G. Wilson's 32-yard catch is the longest pass play of the game." | 11 |
| Q1 8:32 | "Q. Judkins' 14-yard run is the longest of the game." | 4 |
| Q1 0:24 | "B. Allen's 17-yard run is the longest of the game." | 3 |
| Q2 13:20 | "J. Jeudy's 52-yard catch is the longest pass play of the game." | 20 |
| Q3 7:17 | "B. Allen's 18-yard run is the longest of the game." | 1 |
| Q4 7:13 | "D. Watson's 20-yard run is the longest of the game." | 2 |
| Q4 3:01 | "B. Allen's 22-yard run is the longest of the game." | 2 |

Other one-yard margins: "R. Stevenson's 12-yard run" (LV @ NE, Q2), "A. Jones Sr.'s 19-yard run" (MIN @ NO, Q3), "J.
Williams' 39-yard catch" (DET @ ARI, Q3). By contrast, "J. Warren's 50-yard run" (IND @ PIT, Q3, by 40 yards) and "J.
Jeudy's 52-yard catch" (by 20) are worth saying.

The strongest facts in this slate are the running counts once they reach 2 or more ("S. Darnold has been sacked 5
times this game.", "J. Burrow's fourth touchdown pass of the game."). Season facts, once NFL sources them, should
lead.

## Audit plan (repeatable after each slate)

1. **Integrity (SQL 1)**: every count must be zero. Any non-zero result is a bug.
2. **Coverage (SQL 1b plus Seq)**:
   - Plays and facts per game.
   - Drop reasons tallied from Seq by `FactType`, `Kind` and `Reason`.
   - A whole family dropping for one reason (finding 1) is a sourcing or rule problem, not noise.
3. **Recount (SQL 2a to 2c)**: recompute every game fact from the final play data, with types 80 and 39 counted.
   Sort each disagreement into one of two buckets:
   - **Our bug**: the final data and the data at publish time agree, and we were wrong.
   - **ESPN changed it**: the play's type, participants or yardage changed after publish.

   Today this is a judgment from the play text. To make it mechanical, store a snapshot in the fact's provenance at
   analysis time: the play type id, yardage and the counted actor ids. Then the audit compares the snapshot with the
   final data instead of guessing.
4. **Season facts (once sourced)**: for each season fact, check that the season document's value plus the game value
   equals the stated total. After the game, check that it matches the next season document minus the rest of that
   game. `writtenAfterKickoff` must always be false under the pre-kickoff rule.
5. **Phrasing**: the number in the text matches `value`: ordinal words (first, second, third) and "once" / "N times".
6. **Value review (SQL 3)**:
   - Track the low-value measures above per slate.
   - Use lab ratings once there are some.
   - Each filter added in task 15 cites the measure it moves.

## Proposed next steps

1. Classify types 80 and 39. Small and contained; fixes finding 2.
2. Decide the ESPN-correction policy: accept it, re-check at final, or supersede a fact when its play changes. At
   minimum, log the snapshot from plan step 3.
3. Raise NFL 2026 season-stat sourcing (finding 1) separately from this feature. Confirm the NCAA weekly refresh time.
4. Low-value rules for task 15, in order of likely impact:
   - Suppress first/once facts when they only repeat the play.
   - Rank by type and value together, so higher counts beat firsts.
   - Keep one fact per sack.
   - Require a minimum margin or a game-time floor for "longest of the game".
