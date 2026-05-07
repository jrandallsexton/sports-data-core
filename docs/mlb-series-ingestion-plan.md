# MLB Series ingestion — as built

---

**SUPERSEDED — DO NOT IMPLEMENT FROM THIS DOC.**

The entity-based design captured below was walked back the same day it
shipped. The live implementation follows
**[`docs/series-snapshot-redesign.md`](series-snapshot-redesign.md)**.

The remainder of this file (~290 lines) is preserved as decision
history only. Two issues drove the redesign: historical matchup pages
would render current series state instead of at-game-start state
(time-travel hazard), and the `Series` / `SeasonSeries` entity tables
didn't carry information not derivable from per-competition data.

---

Captured 2026-05-07; implemented in commit f1e74576 ("feat(producer):
MLB series + season-series ingestion") on branch
`feat/mlb-series-ingestion`. ESPN's MLB EventCompetition documents
carry two flavors of series context — a current multi-game series
and a season-long head-to-head — that we now capture as canonical
entities. This doc records the shape, the decisions taken, and the
delivered implementation. Sections that read as deliberation
("modeling options", "what I'd lean toward", "open question") are
preserved as historical context for the choice; the locked outcome
is in **Decisions (locked 2026-05-07)** and the delivered shape is
in **Implementation (as built)**.

## What ESPN sends

Sample: `test/unit/SportsData.Producer.Tests.Unit/Data/EspnBaseballMlb/Event.json`
(competition 401815224, NYM @ COL, 2026-05-06).

Series data is **inline within a competition object** — not a separate
`$ref` you fetch. The shape:

```jsonc
"competitions": [{
  "id": "401815224",
  // …other competition fields…
  "seriesId": "600056136",
  "series": [
    {
      "type": "current",
      "title": "Current Series",
      "summary": "NYM lead series 1-0",
      "completed": false,
      "totalCompetitions": 3,
      "competitors": [
        { "id": "27", "uid": "s:1~l:10~t:27", "wins": 0, "ties": 0, "team": { "$ref": ".../teams/27" } },
        { "id": "21", "uid": "s:1~l:10~t:21", "wins": 1, "ties": 0, "team": { "$ref": ".../teams/21" } }
      ],
      "events": [
        { "$ref": ".../events/401815210" },
        { "$ref": ".../events/401815224" },
        { "$ref": ".../events/401815239" },
        { "$ref": ".../events/401871498" }
      ],
      "startDate": "2026-05-04T04:00Z"
    },
    {
      "type": "season",
      "title": "Regular Season Series",
      "summary": "COL leads series 3-1",
      "completed": false,
      "totalCompetitions": 6,
      "competitors": [
        { "id": "27", "wins": 3, "ties": 0, "team": { "$ref": ".../teams/27" } },
        { "id": "21", "wins": 1, "ties": 0, "team": { "$ref": ".../teams/21" } }
      ],
      "events": [ /* 8 event refs */ ],
      "startDate": "2026-04-24T23:10Z"
    }
  ],
  // …
}]
```

Two kinds:

1. **`type: "current"`** — the active multi-game series (3-game set in
   the sample). Backed by a real ESPN identity: `seriesId` ("600056136"
   in the sample) lives at the *competition* level. Multiple competitions
   in the same series will all carry the same `seriesId` and an
   identical `series[type=current]` payload.
2. **`type: "season"`** — the season-long head-to-head between the two
   teams. **No ID.** Synthetic, derived from the season's body of
   competitions between this team-pair. The sample's "season" entry
   says `totalCompetitions: 6` but lists 8 event refs — discrepancy
   is likely makeup/postponed games being included in `events` but
   not in the count. (Worth verifying empirically before assuming.)

Both entries share the same per-team `competitors` shape: id,
wins, ties, and a team `$ref` (FranchiseSeason).

## Why this matters (UI hooks — historical, not in this PR)

Per the top-down feedback memory
(`memory/feedback_top_down_ui_first.md`) we should fix the UI shape
before locking the data model. Likely surfaces:

- **Contest Overview page** — "NYM lead series 1-0" prominent on the
  matchup header; "COL leads season series 3-1" as secondary stat.
- **Series strip** — small inline list of the 3 (or 4) games in the
  current series with each game's outcome and date.
- **Command Center wallboard** ([memory/project_command_center_vision](.))
   — series state is a candidate for the live tile (e.g. "Game 2 of 3,
  series tied 1-1").

**Open question for product:** what's the minimum UI surface for v1?
Just a "summary" string per type? Or the full per-team wins/ties +
the events strip? The data model should follow the answer.

## Modeling options (historical — Option C selected)

Three options were considered, with trade-offs. The choice turned
on (a) how much of the series view we wanted to render in the UI,
and (b) how we felt about persisting derived data. The locked
outcome (Option C, with both kinds persisted) is captured in
**Decisions (locked 2026-05-07)** below.

### Option A — Two persisted entities, one new DocumentType

- Add `DocumentType.EventCompetitionSeries` (next free integer in
  `DocumentType.cs`, currently 71).
- New canonical entity `Series` keyed by `seriesId` for the "current"
  variant; `SeriesCompetitor` join row holding `(SeriesId, FranchiseSeasonId, Wins, Ties)`.
- `Contest` (or `Competition`) gets a nullable `CurrentSeriesId` FK.
- A separate `SeasonSeries` entity keyed by a synthetic
  `(SeasonYear, FranchiseSeasonIdLow, FranchiseSeasonIdHigh)`
  composite — captures the head-to-head record. `SeasonSeriesCompetitor`
  same shape as above.
- New `EventCompetitionSeriesDocumentProcessor` consumes the series
  payload. Since ESPN doesn't publish series at a separate `$ref`,
  the seed publisher is `EventCompetitionDocumentProcessor`: when
  it processes a competition with a `series` field, it synthesizes
  per-series `DocumentRequested` events with `Uri` set to a synthetic
  but stable URL (e.g. `urn:sportdeets:mlb:series:current:600056136`
  and `urn:sportdeets:mlb:series:season:2026:21-27`).

Trade-offs:
- ✅ Slots cleanly into the existing DocumentProcessor cascade — same
  patterns, retry semantics, idempotency.
- ✅ Persisted state means UI queries are cheap and consistent.
- ✅ Series data can be reasoned about independently — e.g. a future
  "playoff series" surface reuses the same model.
- ❌ Synthesizing URIs and DocumentRequested events for inline data is
  novel here. Sets a precedent worth thinking about — when else might
  we want to do this for other inline-only ESPN sub-objects?
- ❌ Extra writes per Competition refresh (small, but multiplied by the
  league).
- ❌ "Season series" is a derived synthesis. Persisting it means we
  carry update-staleness risk unless we re-derive on every contest
  finalize.

### Option B — Persist "current" only; derive "season" at query time

- Same as Option A for the *current* series: new `DocumentType`,
  new `Series` + `SeriesCompetitor` entities, synthesized seed.
- "Season series" is **not** persisted as an entity. Instead, an
  API query handler computes it from existing canonical data:
  filter `Competition` rows by `(SeasonYear, team-pair)`, aggregate
  the results, return the head-to-head record.

Trade-offs:
- ✅ Avoids persisting derived state — no staleness drift risk on the
  season head-to-head.
- ✅ Smaller schema delta.
- ✅ Honest: the source of truth for "season series" is already our
  Competition + outcome data; ESPN's `series[type=season]` is just a
  pre-rolled view of that.
- ❌ Per-request aggregation cost on the UI's hot path (Contest
  Overview). Probably fine for MLB scale, but worth measuring.
- ❌ Two different code paths for the same conceptual thing (current =
  persisted entity, season = projection). UI doesn't see this, but
  the team has to remember.

### Option C — No new DocumentType; extend `EventCompetition` processor inline

- No new `DocumentType`. The `EventCompetition` processor reads the
  inline `series` payload during normal processing and writes
  `Series` / `SeriesCompetitor` rows directly.
- Optional: still persist `current` only and derive `season`, OR
  persist both.

Trade-offs:
- ✅ Minimum machinery — no synthetic URIs, no new processor, no new
  DocumentType.
- ✅ Series state is updated atomically with the EventCompetition that
  carries it. No retry-cascade complexity.
- ❌ Mixes concerns — the EventCompetition processor grows. Future
  changes to series logic touch a hot processor.
- ❌ Series data can't be filtered out of the cascade via
  `IncludeLinkedDocumentTypes` — it's bundled with EventCompetition.
- ❌ No retry isolation: if the Series persist step fails, the whole
  EventCompetition processing is in question.

### What I'd lean toward, and why

If forced to pick now, **Option B** — persist the "current" series as
a first-class entity behind a new `DocumentType` (because it has a
real ESPN identity and is referenced across multiple competitions),
but compute the "season" head-to-head at query time (because we already
have the source data and persistence buys nothing but staleness risk).

But I'd want to hear the UI shape decision before committing. If the
UI only ever needs a one-line "summary" string per type, **Option C**
becomes more attractive — capture the strings on Competition and move
on.

## Identity, linking, and cascade

Decisions that depend on which option we pick:

- **`Series` identity (current)**: ESPN `seriesId` (string). Could
  hash to a `Guid` via the same external-id pattern used for refs,
  or store as a string column. Probably hash-to-Guid for consistency
  with the rest of the schema.
- **`Series` identity (season, if persisted)**: synthesized
  `(SeasonYear, FranchiseSeasonId-pair)`. Pair must be canonicalized
  (sort by id) so symmetry doesn't produce two rows.
- **`Contest` linkage**: `Contest.CurrentSeriesId` nullable FK. Don't
  add a SeasonSeriesId — too synthetic to be a stable FK (and Option
  B doesn't persist it anyway).
- **Series → Competitions**: a Series points at multiple Competitions
  (`events[]`). M:N via a `SeriesEvent` join, or store the list as
  an array column? Postgres handles both; M:N is more queryable.
  Could also flip the relationship: store `Competition.CurrentSeriesId`
  and walk the inverse.
- **Refresh Contest filter**: if we go with Option A or B, add
  `EventCompetitionSeries` to `ContestRefreshDocumentTypes` so series
  state stays current on refresh.
- **`isNew` short-circuit**: standard `if (isNew || ShouldSpawn(...))`
  guard at the spawn site.

## Sport scope

The audit-driven [feedback memory on sport-specific subtype
splits](.) suggests we should think about whether series is MLB-only
or should generalize. Series exist in NBA (best-of-7), NHL, NFL
divisional/championship/Super Bowl rounds (single-game "series"),
college tournaments. ESPN's data shape is likely consistent across
these; the semantics differ.

Concrete question: do we model `Series` as a `*Base` shared parent
with sport-specific subclasses, or just one `Series` table that
works for all team sports? Lean toward shared — the fields here
(wins, ties, totalCompetitions) cover everything I can think of.
Revisit if NHL tie semantics or NBA best-of-7 needs sport-specific
fields.

## Data quirks observed (need empirical verification)

1. **`totalCompetitions` vs `events.length` mismatch.** Sample shows
   `totalCompetitions: 6` for season-series with 8 listed events. Likely
   a postponed/makeup-game artifact. Need to look at a few more samples
   to know whether `events[]` is "all attempted" and `totalCompetitions`
   is "all that count toward the head-to-head." Affects whether we
   trust `totalCompetitions` as a stable progress indicator.
2. **`seriesId` only present at competition level (not in either
   series entry).** The "current" entry presumably owns it; the
   "season" entry has no ID. Need to confirm the field is reliably
   present whenever `series[type=current]` is.
3. **`completed: false` even when summary suggests an in-progress
   record.** ESPN flips this to true only when the series has been
   formally decided (final game played, or one team can't catch up
   mathematically?). Need to observe a transition to be sure.
4. **`ties` field is always 0 in the sample.** MLB doesn't tie, but
   the field exists for cross-sport consistency. Keep on the entity.

## Decisions (locked 2026-05-07)

- **UI shape:** deferred. Implementation proceeds against the data
  shape only; UI rendering is out of scope for this PR.
- **Modeling approach:** **Option C — inline extraction.** No new
  `DocumentType`. Series state is read inline from the
  EventCompetition payload during normal processing.
- **Read site:** `BaseballEventCompetitionDocumentProcessor` (the
  sport-specific subclass), not `EventDocumentProcessor`. The series
  data semantically belongs to the competition. The standalone
  `EventCompetition.json` also carries `seriesId` / `series[]`
  (verified — sample has `seriesId: "600055560"`), so reading at the
  competition level covers both Event-cascade and direct-EventCompetition
  refresh paths with one extraction site.
- **"Season series" persistence:** **persist as canonical data.**
  Treated like any other entity, even though ESPN provides no `$ref`.
  Synthesized identity from `(SeasonYear, sorted FranchiseSeasonId
  pair)`.
- **Sport scope:** **MLB-only entities** in `BaseballDataContext`. No
  shared base, no interface. NFL/NCAAFB out of scope (no series
  concept). Refactor when NBA arrives with real data — interface or
  shared base, decided then.

---

**HISTORICAL — DO NOT IMPLEMENT.** The shipped implementation differs
from what's described below. See
[`docs/series-snapshot-redesign.md`](series-snapshot-redesign.md).

---

## Implementation (as built)

### DTOs (already in place at the time of this plan)

`EspnSeriesDto`, `EspnSeriesCompetitorDto`, and the
`SeriesId` / `Series` properties on `EspnBaseballEventCompetitionDto`
are already present at
`src/SportsData.Core/Infrastructure/DataSources/Espn/Dtos/Baseball/EspnBaseballEventCompetitionDto.cs:23–91`.
DTO work is done; only entity/processor/migration/test work remains.

### New entities (delivered in `src/SportsData.Producer/Infrastructure/Data/Baseball/Entities/`)

Class names are unprefixed — the `Baseball/Entities/` namespace
already conveys sport scope, and there is no `*Base` parent the
prefix would distinguish from. Easier rename later if `Series` ever
gets promoted to a shared entity.

**`Series`** — the "current series" entity, identity hashed
from ESPN `seriesId`:

| Column | Type | Notes |
|---|---|---|
| `Id` | `Guid` | `HashProvider.GenerateHashFromString(seriesId)` |
| `EspnSeriesId` | `string` | The raw `seriesId` ("600056136"). Indexed unique. |
| `Title` | `string` | "Current Series" |
| `Description` | `string?` | |
| `Summary` | `string?` | "NYM lead series 1-0" |
| `Completed` | `bool` | |
| `TotalCompetitions` | `int` | |
| `StartDate` | `DateTimeOffset?` | Parsed from ESPN string |
| `CreatedUtc` / `ModifiedUtc` | `DateTime` | Standard audit |
| `CreatedBy` / `ModifiedBy` | `Guid` | Correlation ids |

Navigation: `ICollection<SeriesCompetitor> Competitors`,
`ICollection<BaseballCompetition> Competitions` (inverse via
`BaseballCompetition.CurrentSeriesId`).

**`SeriesCompetitor`** — per-team join row:

| Column | Type | Notes |
|---|---|---|
| `Id` | `Guid` | |
| `SeriesId` | `Guid` | FK |
| `FranchiseSeasonId` | `Guid` | FK |
| `Wins` | `int` | |
| `Ties` | `int` | Always 0 for MLB; column kept for cross-sport future |
| `CreatedUtc` / `ModifiedUtc` / etc. | | |

Unique index: `(SeriesId, FranchiseSeasonId)`.

**`SeasonSeries`** — the head-to-head, identity synthesized:

| Column | Type | Notes |
|---|---|---|
| `Id` | `Guid` | `HashProvider.GenerateHashFromString($"{SeasonYear}:{lowFsId}-{highFsId}")` after sorting the pair |
| `SeasonYear` | `int` | |
| `FranchiseSeasonALowId` | `Guid` | sorted-low team |
| `FranchiseSeasonBHighId` | `Guid` | sorted-high team |
| `Title` | `string?` | "Regular Season Series" |
| `Description` | `string?` | |
| `Summary` | `string?` | "COL leads series 3-1" |
| `Completed` | `bool` | |
| `TotalCompetitions` | `int` | Trust this even when `events.length` differs |
| `StartDate` | `DateTimeOffset?` | |
| `CreatedUtc` / `ModifiedUtc` / etc. | | |

Unique index: `(SeasonYear, FranchiseSeasonALowId, FranchiseSeasonBHighId)`.

**`SeasonSeriesCompetitor`** — same shape as
`SeriesCompetitor`, FKs to `SeasonSeriesId`.

**No `*ExternalId` table** for either — `Series` keeps the raw
`EspnSeriesId` as a column, and `SeasonSeries` has no
external identity at all.

### Schema delta on existing entities (delivered)

`BaseballCompetition` got two new nullable FK columns:

- `CurrentSeriesId` → `Series.Id`
- `SeasonSeriesId` → `SeasonSeries.Id`

Both are nullable because (a) historical games predate this work,
and (b) ESPN occasionally omits series for postponed/preseason
games.

### Processor delta (delivered)

`EventCompetitionDocumentProcessorBase` received a new virtual
hook, defaulted to no-op:

```csharp
protected virtual Task ProcessSportSpecificCompetitionData(
    ProcessDocumentCommand command,
    EspnEventCompetitionDtoBase dto,
    CompetitionBase competition,
    bool isNew) => Task.CompletedTask;
```

It is called from the base after the competition entity is
persisted but before the existing child-document spawning loop (so
series-related data is in place when the cascade fires, even though
no child doc depends on it today).

`BaseballEventCompetitionDocumentProcessor` overrides this hook
(see `BaseballEventCompetitionDocumentProcessor.cs:54`):

```csharp
protected override async Task ProcessSportSpecificCompetitionData(
    ProcessDocumentCommand command,
    EspnEventCompetitionDtoBase dto,
    CompetitionBase competition,
    bool isNew)
{
    if (dto is not EspnBaseballEventCompetitionDto baseball
        || baseball.Series is null
        || competition is not BaseballCompetition baseballCompetition)
    {
        return;
    }

    foreach (var seriesEntry in baseball.Series)
    {
        switch (seriesEntry.Type)
        {
            case "current": await UpsertCurrentSeries(...); break;
            case "season":  await UpsertSeasonSeries(...);  break;
            // unknown types logged + ignored — forward-compat
        }
    }
}
```

Concurrency: multiple competitions in the same series are processed
in parallel by different workers; both will try to upsert the same
`Series` row. Use the existing
`IsUniqueConstraintViolation` retry-as-update pattern at
`ResourceIndexItemProcessor.cs:142–172` for both upsert paths. Same
applies to `SeasonSeries`.

Idempotency: the operation is "upsert by id, replace counter columns"
— last-write-wins is safe because every competition in the series
carries the same payload.

### EF migration (delivered)

One migration on `BaseballDataContext`:
`Migrations/Baseball/20260507145154_AddSeriesAndSeasonSeries.cs`.
Adds tables `Series`, `SeriesCompetitor`, `SeasonSeries`,
`SeasonSeriesCompetitor`, and alters `BaseballCompetition` to add
`CurrentSeriesId` and `SeasonSeriesId` nullable FKs.

Per [feedback_test_migrations_locally](memory), validate the
migration locally against a copy of prod before pushing.

### Tests (delivered)

Unit tests for `BaseballEventCompetitionDocumentProcessor` live in
`test/unit/SportsData.Producer.Tests.Unit/Application/Documents/Processors/Providers/Espn/Baseball/BaseballEventCompetitionDocumentProcessorSeriesTests.cs`,
using the `EventCompetition.json` fixture (carries `seriesId` and
`series[]`). Coverage:

- New current-series creates `Series` + 2 `Competitor` rows.
- Re-processing same competition is idempotent (no row count growth,
  Wins/Ties match latest payload).
- New season-series creates `SeasonSeries` with sorted-pair
  identity.
- Two competitions in the same season-pair (one earlier in the year,
  one later) produce a single `SeasonSeries` row with the
  later wins/ties/summary.
- `BaseballCompetition.CurrentSeriesId` and `SeasonSeriesId` are
  set.
- Unknown `series[].type` is logged and ignored, processing
  continues.

### Refresh Contest filter

No change. Series is not a `DocumentType` (Option C), so
`ContestRefreshDocumentTypes` doesn't need updating. Series state
flows whenever an EventCompetition refresh happens.

### Out of scope for this PR

- API query handlers / DTOs for surfacing series to the UI. Deferred
  until UI shape is decided.
- NBA / NHL / NFL series modeling.
- Backfill of historical games (we don't have the inline series data
  for past seasons; would require a re-fetch of all historical
  EventCompetition docs and that's a separate ESPN-volume question).

## Implementation tasks (completed)

All steps below shipped in commit f1e74576 on branch
`feat/mlb-series-ingestion`.

1. ✅ Branch.
2. ✅ Added four entity classes + EntityConfigurations.
3. ✅ Wired up `BaseballDataContext` `DbSet`s and `OnModelCreating`.
4. ✅ Added `CurrentSeriesId` / `SeasonSeriesId` to
   `BaseballCompetition` + entity configuration.
5. ✅ EF migration `20260507145154_AddSeriesAndSeasonSeries`. Local
   validation done.
6. ✅ Added virtual `ProcessSportSpecificCompetitionData` hook to
   `EventCompetitionDocumentProcessorBase`.
7. ✅ Override implemented in
   `BaseballEventCompetitionDocumentProcessor` with upsert helpers
   using the `IsUniqueConstraintViolation` retry pattern.
8. ✅ Unit tests against the existing `EventCompetition.json`
   fixture.
9. ✅ Build + test.
10. ✅ Staged, committed, pushed, PR opened.

## Related

- `docs/refresh-contest-cascade-narrowing.md` — cascade filter
  pattern this work needs to honor.
- `docs/processor-shouldspawn-audit.md` — pattern compliance for new
  spawn sites.
- `memory/project_command_center_vision.md` — series tile is in
  scope for the wallboard.
- `memory/feedback_top_down_ui_first.md` — UI shape before wire
  contract; currently waiting on UI direction.
