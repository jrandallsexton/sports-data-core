# Point-in-Time (As-Of-Week) Team Records

Status: **Design / discussion** (not yet scheduled)
Last updated: 2026-07-20

## Problem

On the picks / matchup surfaces, each team shows a win-loss record (e.g. "3-0",
"6-2 (3-1)"). When viewing a **historical week** — now reachable via the
read-only past-league picks view (PR #533) — the records render as
**end-of-season** values instead of the record the team carried **into** that
week.

Example: LSU 2024, Week 10 vs Alabama should show LSU entering at **6-2 (3-1)**.
Today it shows the final-season record.

## Root cause

`src/SportsData.Producer/Infrastructure/Sql/GetMatchupsByContestIds.sql` sources
team records from the **mutable, season-level `FranchiseSeason` row** (aliases
`fsAway` / `fsHome`):

```sql
fsAway."Wins", fsAway."Losses", fsAway."ConferenceWins", fsAway."ConferenceLosses"
fsHome."Wins", fsHome."Losses", fsHome."ConferenceWins", fsHome."ConferenceLosses"
```

That row is overwritten as the season progresses, so every past week renders the
same (latest) numbers. A point-in-time record must be **frozen in the game's
context**, not derived from a season-level row that moves.

## Key finding: ESPN's per-game record is POST-game

ESPN embeds each competitor's record inside every competition document
(`EspnEventCompetitionCompetitorRecordDto`: a `Type` — `total` / `vsconf` /
`home` / `road` — a `Summary` like "3-0", and `Stats` for wins/losses/ties). We
already model this as **`CompetitionCompetitorRecord` + `CompetitionCompetitorRecordStat`**
(Producer), fed by `EventCompetitionCompetitorRecordDocumentProcessor`, keyed on
`CompetitionCompetitorId`.

**These records are post-game — they include that game's result.** Verified for
LSU 2024 vs Alabama (`Competition` `55505ccc-…`):

| Team | ESPN `total` | ESPN `vsconf` | Meaning |
|------|-------------|---------------|---------|
| LSU (home)   | 6-3 | 3-2 | **includes** the Alabama loss |
| Alabama (away) | 7-2 | 4-2 | **includes** the LSU win |

Cross-checked against ESPN's LSU 2024 schedule page: the Nov 9 (Alabama) row
reads `6-3 (3-2)`; the record LSU *carried in* is the prior row (Oct 26, Texas
A&M): `6-2 (3-1)`. So the DB and ESPN agree — the stored record is the record
**through** the game, not entering it.

Implication: we cannot display ESPN's per-game record directly; we need the
**entering** record.

## Deriving the entering record

**Approach B (recommended): entering(game N) = the same team's *previous* game's
ESPN post-game record.**
- LSU-Alabama entering = LSU's Texas A&M record = 6-2 (3-1). Exact, no arithmetic.
- The prior game's record already carries the correct conf / non-conf split, so
  it can't drift from what users see on ESPN.
- Implementation: a lag over each `FranchiseSeason`'s competitions ordered by
  date. Season opener → 0-0 (0-0) seed.

**Approach A (alternative): entering = this game's post-game record minus this
game's result.**
- Self-contained per row: `Winner` flag is on the competitor, conference slugs
  decide whether the game counts toward `vsconf`.
- More surface to get wrong (independents, FCS opponents, neutral-site
  conference games), because we do the conference-classification + arithmetic
  ourselves.
- Naturally fits the live pipeline (a finalizing game has everything), but B
  also works live (game N-1 is already final).

## Approach: read N-1's existing record (no new storage)

Decided 2026-07-20: **persist nothing new.** The record a team carried INTO
game N is its record THROUGH its prior game (N-1), and that already exists in
`CompetitionCompetitorRecord`. So no new columns, no migration, no backfill, no
processor change — the matchup query derives the entering record on the fly by
lagging to the team's prior competition's record. This is the leanest fix: zero
duplication, and the existing `EventCompetitionCompetitorRecordDocumentProcessor`
already keeps the source current (it lands each game's `total`/`vsconf`
post-game), so "keeping it current" isn't a design concern — reads always reflect
the latest prior record and ESPN corrections flow through automatically.

Tradeoff: the read is heavier — a lateral lag per competitor plus parsing the EAV
`Summary` string (`"6-2"`, or `"8-7-1"` for an NFL tie, split on `-`). Acceptable
for a per-page-load query. If it ever proves too slow, flattening the post-game
record into columns on `FootballCompetitionCompetitor` is the fallback
optimization — not the starting point.

## Read-path change (the whole implementation)

`GetMatchupsByContestIds.sql`: replace the `fsAway`/`fsHome` record columns
(mutable `FranchiseSeason`) with an entering record derived per competitor:

- Lag to the team's **most-recent prior competition that has a record** (same
  `FranchiseSeason`, same season, game date `<` this game's) and read that
  competitor's `CompetitionCompetitorRecord` — `total` (overall W-L[-T]) and
  `vsconf` (conference W-L[-T]).
- Parse the `Summary` on `-`; non-conference = overall − conference if ever
  needed (the current card shows overall + conference).
- `COALESCE` to 0 when there's no prior record — the season opener, or (rarely) a
  gap where the immediately-prior record isn't sourced.

The API matchup DTO field names (`AwayConferenceWins`, etc.) stay; only the
source moves. No other layer changes.

## Sourcing (the only "backfill")

Nothing to backfill in the schema sense — the data already lives in
`CompetitionCompetitorRecord`. The only gap is **sourcing coverage** (below):
2024 is ~98% there; 2025's record refs are largely un-sourced and need an
ingestion pass. Per the happy-path decision, ship the read path first and use the
UI itself to spot which real (FBS/NFL) games are missing data, rather than
auditing every un-sourced row up front.

The inherent latency gap — entering(N) can't show until N-1 is played *and* its
record has landed — is short (quick-turnaround games) and shared by any approach.

## Outstanding questions

1. **Coverage — measured 2026-07-20** (`sql/pgsql/_debug_point_in_time_coverage.sql`).
   2024: 3737/3802 competitions fully covered (98.3%), **0 missing competitors**,
   65 missing records. 2025: 5/3833 records, again **0 missing competitors**. So
   the model/processor is proven (2024) and the competitor layer is complete both
   years — remaining work is **sourcing the record refs** (all of 2025, a
   finished + fully-sourceable season, plus the 65 in 2024), not a design change.
   Follow-ups: (a) characterize the 65 (2024) gaps — likely lower-division / FCS
   games outside pick'em slates; scope the coverage query to FBS + NFL for the
   number that actually matters; (b) source 2025 and re-run.
1a. **Lag robustness for residual gaps.** entering(N) = the prior game's record,
   so a missing record at game G blanks the *next* game's record for that team
   (gaps propagate one hop). Lag to the **most-recent prior game that has a
   record**, not strictly the immediately-prior competition, so a rare gap leaves
   the record stale by one result rather than blank.
2. **Read-lag cost.** The matchup query gains a lateral lag per competitor over
   `CompetitionCompetitorRecord` (indexed by `CompetitionCompetitorId`; the lag
   ordered by `FranchiseSeason` + game date). Confirm it stays cheap in
   `GetMatchupsByContestIds.sql`; flattening into competitor columns is the
   fallback only if it isn't.
3. **`Summary` parsing.** Read the entering W/L(/T) by splitting the `Summary`
   string (`"6-2"` / `"8-7-1"`) on `-`, or by joining the `CompetitionCompetitorRecordStat`
   children for typed int values? Confirm which is reliably populated.
4. **Non-football sports.** MLB is divisions, not conferences; some sports have
   neither; ties apply to few. Football reads `total`/`vsconf`; what are the
   equivalent record `Type`s per sport, and does the same lag pattern apply?
5. **Edge cases.** Season opener → 0-0 (coalesce); bye weeks (chronologically-
   prior game is fine); postseason/bowl games (entering = end of regular season);
   quick-turnaround games where N-1's record lands close to kickoff.
6. **ATS / other record types.** There is `FranchiseSeasonRecordAts`. Do ATS
   leagues want a point-in-time ATS record too, or is that out of scope for v1?
