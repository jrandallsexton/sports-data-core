# MLB series — snapshot redesign

Captured 2026-05-07. Walks back the entity-shape decisions in
`docs/mlb-series-ingestion-plan.md` and replaces them with a flatter
snapshot-on-Competition model. Authored before implementation per
the design-doc-first guardrail.

## Why

Two problems, identified in conversation after the original design
shipped (commit f1e74576):

### 1. Time-travel hazard

Series state is **mutable**. A 3-game series in May reads "tied 1-1"
mid-series, then "NYM win 2-1" after the third game. Season head-to-head
records grow all season. The current `Series` and `SeasonSeries` rows
always reflect *now*.

That's wrong for the matchup page. When a user in September clicks back
to a game played in May, the matchup header should display what was true
*at kickoff of that game*, not the current rolled-up state. NCAAFB hit
the same problem last season with team rankings — display showed
end-of-season rank instead of rank at game time.

### 2. The entities don't carry information that isn't elsewhere

Walking through what `Series` / `SeasonSeries` actually contribute:

- `Summary`, `Completed`, `TotalCompetitions`, `StartDate` — all present
  in every game's EventCompetition payload. The latest game's payload
  IS the current state.
- Per-team `Wins`/`Ties` (in the `*Competitor` join rows) — also in
  every game's payload.
- `EspnSeriesId` — could be a column on `BaseballCompetition` instead.
- `SeasonSeries` — fully derivable from the season's `BaseballCompetition`
  rows, even without ESPN providing it.

The tables are a **convenience shape** — code aesthetics for
`Series.Competitions` navigation and a relational join for per-team
records. They don't carry data not derivable from the per-game records,
and once we add per-game snapshots, every read-query they could serve is
served as well or better by querying `BaseballCompetition` directly.

The original design was over-modeled for what we actually need.

## What changes

Drop four tables, drop two FK columns, add ~16 nullable columns to
`BaseballCompetition`. Net: code reduction.

### Drops

Tables:
- `Series`
- `SeriesCompetitor`
- `SeasonSeries`
- `SeasonSeriesCompetitor`

Columns on `BaseballCompetition`:
- `CurrentSeriesId` (FK → `Series.Id`)
- `SeasonSeriesId` (FK → `SeasonSeries.Id`)

### Adds (all on `BaseballCompetition`)

Grouping key:

| Column | Type | Notes |
|---|---|---|
| `EspnSeriesId` | `string?` | Raw ESPN series id ("600056007"). Indexed (non-unique). Enables "all games in series X" queries. |

Current-series snapshot (state going into this game):

| Column | Type |
|---|---|
| `CurrentSeriesSummary` | `string?` |
| `CurrentSeriesTotalCompetitions` | `int?` |
| `CurrentSeriesCompleted` | `bool?` |
| `CurrentSeriesStartDate` | `DateTimeOffset?` |
| `CurrentSeriesHomeWins` | `int?` |
| `CurrentSeriesHomeTies` | `int?` |
| `CurrentSeriesAwayWins` | `int?` |
| `CurrentSeriesAwayTies` | `int?` |

Season-series snapshot (state going into this game):

| Column | Type |
|---|---|
| `SeasonSeriesSummary` | `string?` |
| `SeasonSeriesTotalCompetitions` | `int?` |
| `SeasonSeriesCompleted` | `bool?` |
| `SeasonSeriesHomeWins` | `int?` |
| `SeasonSeriesHomeTies` | `int?` |
| `SeasonSeriesAwayWins` | `int?` |
| `SeasonSeriesAwayTies` | `int?` |

All nullable because (a) historical games predate this work,
(b) ESPN occasionally omits series for postponed/preseason games,
(c) sport-specific MLB-only state on the sport-specific entity.

The columns carry **at-game-start semantics by convention**, enforced
by the lock-on-first-write rule below. No `_AtKickoff` suffix —
"kickoff" is football vocabulary and doesn't belong on a baseball
entity. The semantics are conveyed by the lock rule and an entity
comment.

## Write semantics

### Lock-on-first-write

The processor writes the snapshot columns **only when they're currently
null**. Once any column in a snapshot section is non-null, the whole
section is treated as locked and never overwritten on subsequent
processing.

Per-section guard:

```csharp
// Current series
if (competition.CurrentSeriesSummary is null)
{
    // populate all CurrentSeries* columns from the payload
}

// Season series
if (competition.SeasonSeriesSummary is null)
{
    // populate all SeasonSeries* columns from the payload
}
```

The `Summary` column is the section-presence sentinel. ESPN always
supplies it when the section is present.

### Rationale for lock-on-first-write (vs lock at status transition)

The lock fires the first time we see ESPN publish the series payload
for this game. That's typically before kickoff. ESPN updates the
payload mid-game and post-game, but those updates carry the rolled-up
post-game state, not the at-kickoff state we want.

A more precise alternative would be "lock when game status transitions
from `pre` to `in`" — captures the canonical going-into-the-game state
even if our first sight was mid-game. We will start with first-write
and only escalate if we observe drift between first-sight state and
true at-kickoff state.

### Home/away mapping

ESPN's `series[].competitors[]` carries team id but not home/away.
Home/away comes from `EspnEventCompetitionCompetitorDto.HomeAway`
("home" or "away") in the parent EventCompetition payload's
`competitors[]`.

The processor matches series competitor `id` against EventCompetition
competitor `id` to assign the right Wins/Ties to Home vs Away columns.
No FranchiseSeason resolution required — pure id-string match in the
DTO.

If the match fails (one of the team ids isn't in the parent
competitors list), log a warning and skip that snapshot section.
Should be impossible in practice, but defensive.

## Migration plan

One EF migration on `BaseballDataContext`:

1. Drop foreign-key columns from `BaseballCompetition`:
   `CurrentSeriesId`, `SeasonSeriesId`.
2. Drop tables: `SeriesCompetitor`, `SeasonSeriesCompetitor` (children
   first), `Series`, `SeasonSeries`.
3. Add the 16 snapshot columns to `BaseballCompetition`.
4. Add non-unique index on `BaseballCompetition.EspnSeriesId` to
   support "list games in series X" queries.

Per [feedback_test_migrations_locally](memory), validate against a
local copy of prod before pushing.

The current `Series` / `SeasonSeries` rows in prod (one of each from
the test that prompted this redesign) are dropped — they were a single
day's data, not load-bearing. No backfill needed; the snapshot columns
populate as games re-process via normal cascade traffic.

## Processor delta

`BaseballEventCompetitionDocumentProcessor.ProcessSportSpecificCompetitionData`
is rewritten as a flat snapshot writer. The `foreach` over `series[]`
and the `switch` on `entry.Type` (cases `"current"`, `"season"`, `null`,
`default`) are retained from the prior shape — those still make sense.
Everything else from the prior implementation is dropped.

What's gone, vs. what was there before this PR:

- **`IDateTimeProvider` ctor parameter and field** — removed. The
  processor no longer writes `ModifiedUtc` on any sport-specific entity
  (those entities don't exist anymore); the base class still handles
  competition audit fields.
- **Missing-`SeasonYear` `LogError` branch** — removed. With no
  synthesized `(year, low, high)` season-series identity, `SeasonYear`
  is no longer required for snapshot writes. The contract-violation
  check is no longer applicable here.
- **Clear-on-omitted FK logic** — removed. There are no
  `CurrentSeriesId` / `SeasonSeriesId` FKs to clear, and the
  lock-on-first-write rule means an empty payload should *not* blow
  away a previously-locked snapshot anyway.
- **Distinct-FranchiseSeasonId guard** — removed. The flat model has no
  `(low, high)` pair identity, so the case it guarded against is
  unreachable.
- **`ProcessCurrentSeries` / `ProcessSeasonSeries` upsert helpers** —
  replaced by `ApplyCurrentSeriesSnapshot` / `ApplySeasonSeriesSnapshot`,
  both guarded by `competition.{Current,Season}SeriesSummary is null`.
  No `Series` / `SeasonSeries` row upserts, no `SeriesCompetitor` /
  `SeasonSeriesCompetitor` join writes, no `UpsertSeriesCompetitors` /
  `UpsertSeasonSeriesCompetitors` helpers.
- **`FranchiseSeason` resolution via `ResolveIdAsync`** — removed.
  Home/away mapping is a pure DTO id-string match against
  `EspnEventCompetitionDtoBase.Competitors[].HomeAway`, no DB lookup.

What's added:

- **`RestoreSnapshotFromOriginalValues(BaseballCompetition c)`** —
  reads each snapshot column's `OriginalValue` from the EF change
  tracker and restores it onto the entity before the lock check fires.
  Necessary because the base class's
  `_dataContext.Entry(competition).CurrentValues.SetValues(updatedEntity)`
  blanks all snapshot columns on every reprocess (the entity returned
  by `AsBaseballEntity` doesn't carry them). Without this restore, the
  lock would be defeated on every update.
- **`TryMapHomeAway(...)`** — small helper that pulls home/away ids
  from the parent competitor list and matches them against the series
  competitor list. Logs and returns `false` if any matching step fails.
- **`EspnSeriesId` write** — set on every pass when
  `baseballDto.SeriesId` is non-null, deliberately *outside* the lock
  rule. It's the grouping key, not historical state.

## Test rewrites

Existing test file:
`test/unit/SportsData.Producer.Tests.Unit/Application/Documents/Processors/Providers/Espn/Baseball/BaseballEventCompetitionDocumentProcessorSeriesTests.cs`

The four tests in the rewritten file (verified against
`BaseballEventCompetitionDocumentProcessorSeriesTests.cs` on this branch):

- `DTO_Deserializes_Series_Fields_From_Fixture` — sanity-check that
  `EspnBaseballEventCompetitionDto` deserializes `seriesId` and the
  `series[]` array from the fixture, including the unrecognized
  `"preseason"` entry that the processor logs and skips.
- `WhenSeriesPayloadProcessed_SnapshotColumnsAreSet` — processes the
  fixture once and asserts the snapshot columns on
  `BaseballCompetition` are populated (current and season summaries,
  totals, completed flags, home/away wins and ties, plus
  `EspnSeriesId`). Replaces the old "persists `Series` /
  `SeasonSeries` rows" assertion shape from the entity-based design.
- `WhenReprocessed_SnapshotIsLocked_AndDoesNotOverwrite` — processes
  once, then mutates the fixture's `current` series wins/summary and
  reprocesses. Asserts the second run's mutated values are *not*
  reflected on the entity (lock-on-first-write held). This is the
  load-bearing lock-semantics gate.
- `WhenSeriesArrayMissing_DoesNotClearLockedSnapshot` — locks a
  snapshot via a normal first pass, then reprocesses with `series:
  []`. Asserts the locked columns survive the empty-payload
  reprocess. Replaces the old "clears stale FKs" test, which was
  inverted under the new semantics.

Helpers: `SetupAndBuildCommand` (seeds Contest + Competition +
CompetitionExternalId, builds the `ProcessDocumentCommand`) and
`SeedContestAndCompetition`. The prior `SeedFranchiseSeasons` helper
is gone — the flat model doesn't resolve `FranchiseSeason` for
home/away mapping, so no FranchiseSeason seeding is needed.

This list was validated against the test source on the
`feat/mlb-series-snapshot` branch.

## Update to `mlb-series-ingestion-plan.md`

Append a postscript section noting the redesign:

> **Postscript (2026-05-07):** the as-built design described above was
> walked back the same day. Two issues drove the redesign — historical
> matchup pages would render current series state instead of at-kickoff
> state, and the entity tables didn't carry information not derivable
> from per-competition data. See `docs/series-snapshot-redesign.md`.

The original doc stays as-is (decision history) — the postscript points
forward.

## Out of scope

- API query handlers for surfacing snapshot columns to the UI. Deferred
  until UI shape is decided per the original plan.
- "Lock at status transition" upgrade. Will revisit if first-write
  proves too imprecise.
- NBA / NHL / NFL series modeling. Same as before — defer until those
  sports arrive.

## Related

- `docs/mlb-series-ingestion-plan.md` — the original (now superseded)
  design.
- `memory/feedback_design_doc_first.md` — guardrail this doc honors.
- `memory/feedback_test_migrations_locally.md` — applies to the
  drop-tables migration.
- `memory/feedback_sport_specific_subtype_split.md` — `BaseballCompetition`
  is the right place for these MLB-only columns; no `*Base` parent
  pollution.
