# 01. Data spike: findings

**Task:** [01 Data spike](01-data-spike.md) · **Run:** 2026-10-10 against local `sdProducer.FootballNcaa`
(plays ingested through 2026-10-10) · **SQL:** [`sql/01a`–`01f`](sql/)

## Summary

Local data is a restore of the production backup taken around 2am on 2026-10-10, with no games in progress,
so these numbers reflect production.

1. **Roles are reliable for every Phase 1 kind except plain interceptions (type 63) and safeties.**
   Type 63 never names the interceptor (only the passer); that is 37% of 2026 interceptions.
2. **Season source: `AthleteSeasonStatistic`, used as-is (decided 2026-10-10).** Its live behaviour is checked
   with the post-game audit (tasks 08 and 09), not predicted here.
3. **Known risk to watch in that audit:** 2.4% of season documents written mid-game already include part of the
   live game, so "season document + this game's plays" can double-count.
4. **`AthleteCompetitionStatistic` is not usable live**: present for half of finalized games, written days later.
5. **Counting the season from plays undercounts** (type 63 plus 5% of athlete-games with no plays stored).
6. **The example works**: "as of" Cooley's interception = 1 earlier + 1 this game = **2**.

## 1. Participant roles ([`01a`](sql/01a-participant-roles-by-play-type.sql))

Map by the participant's `Type`, never by `Order`: the order varies (e.g. `scorer, patScorer` vs `patScorer, scorer`).

| Kind (TypeId) | 2026 plays | Actor role (named on) | Victim role | Phase 1 |
|---|---|---|---|---|
| Interception return (26) | 551 | `passDefender` (545, 99%), also `returner` | `passer` | yes |
| Interception return TD (36) | 104 | `passDefender` (97, 93%), also `scorer` | `passer` | yes |
| Interception (63) | 390 | **never named**; only `passer`. Interceptor is in `Text` ("intercepted by #23 C.Williams-Dalton") | `passer` | victim-side facts only (see below) |
| Sack (7) | 2,345 | `sackedBy` (2,255, 96%); two `sackedBy` on split sacks | `passer` | yes |
| Fumble lost (29) | 533 | `recoverer` (511, 96%); `forcedBy` often | `fumbler` | yes |
| Passing TD (67) | 2,095 | `passer`, `receiver` (+ `scorer`) | — | yes |
| Rushing TD (68) | 2,255 | `rusher` (+ `scorer`) | — | yes |
| Rush (5) | 43,601 | `rusher` (99%) | — | yes (explosive) |
| Pass reception (24) | 23,234 | `passer`, `receiver` | — | yes (explosive) |
| Safety (20) | 48 | inconsistent (`sackedBy`, `tackler`, `rusher`…); 16 with no participants | — | **no** |

Participant coverage of 2026 plays is ~99% for every kind above except safeties.

**Consequences for task 04:**

- Type 63 can still produce facts about the **passer** ("his 3rd interception thrown this season"), but not the
  defender. Parsing the interceptor from `Text` is possible (jersey + name, team from field position) but out of
  scope for Phase 1.
- Split sacks: decide credit (each `sackedBy` gets a sack fact; ESPN's season stat counts halves). Phrase as
  "shares a sack" or skip season counts for split sacks.
- Safeties drop from Phase 1.

## 2. Double counting ([`01b`](sql/01b-season-stats-freshness.sql), [`01c`](sql/01c-mid-game-season-docs.sql))

Of 13,438 season documents for 2026 athletes, **3,427 (26%) were written while one of the athlete's games was
in progress**. Of the 2,267 of those with an interception stat:

- 2,213 exclude the live game (equal to the sum of earlier games);
- **54 include part of the live game**, and 50 of those are athletes who had an interception in that game.

So "newest season document + this game's plays" double-counts exactly the facts we care about.
**Rule: use the newest season document written before this game's kickoff.** By construction it cannot include
the live game.

## 3. Per-game stats ([`01d`](sql/01d-competition-stats-coverage.sql))

| Measure | Value |
|---|---|
| Finalized 2026 contests | 1,242 |
| With any `AthleteCompetitionStatistic` | 628 (51%) |
| With plays but no per-game stats | 71 |
| Median / p90 delay, final → first per-game stat write | ~7 days / ~28 days |
| Athlete-games in plays with a per-game stat document | 41,960 of 53,486 (78%) |

Not usable as the season-to-date source for a live game.

## 4. "As of this play" prototype ([`01e`](sql/01e-as-of-this-play-prototype.sql))

The worked example returns **2** (1 interception in earlier games + 1 in this game up to this play).

Counting the whole season from plays agrees with the newest season document for only 238 of 432 athletes with
interceptions; it is lower for 188. Causes: type 63 interceptions (defender not named) and 5,301 of 110,373
finalized athlete-games (5%) with no plays stored. Plays are the right source for **the current game**, not for
earlier games.

## 5. Pre-kickoff timing ([`01f`](sql/01f-pre-kickoff-season-doc-freshness.sql))

Of 28,849 athlete-games (2nd game onward), 1,904 had a season document written between the previous final and
this kickoff; 24,196 had none written before kickoff (season documents are mostly written during or after games).
Kept for reference; it doesn't change the decision below.

## Decision for task 06 (2026-10-10)

Season count as of this play =

1. **Earlier games:** the newest `AthleteSeasonStatistic` (`SplitId = '0'`), as-is.
2. **This game:** qualifying plays from `CompetitionPlay` + `CompetitionPlayParticipant` up to this play's
   sequence, matched by role.
3. **Guard:** for interceptions, if the defense already had a type 63 interception earlier in this game, the
   in-game count for its defenders is unknowable; drop the in-game and season counts for that play.

Each fact's `Facts` provenance records which season document was used (id and `CreatedUtc`), so the post-game
audit can spot double counts (a document written after kickoff that already includes the game).
