# CompetitionCompetitor — sport-specific subtype split

Captured 2026-05-08. Combines `competition-competitor-split.md`
(Phase 1, the subtype split) and `competition-competitor-probables.md`
(Phase 2, MLB probables ingestion), previously separate root-level docs.
Together they document the multi-PR effort to retire the shared
`CompetitionCompetitor` canonical entity in favor of sport-specific
subclasses, then hang MLB's `probables[]` collection off the new
baseball subtype.

## Overall effort

The canonical `CompetitionCompetitor` entity was originally shared
across all sports. Two pressure points arrived at the same time and
forced a split:

1. **`CuratedRankCurrent` is on the shared base** — that column is
   NCAAFB-specific (poll ranks). It violates the
   [sport-specific subtype split feedback memory](../../memory/feedback_sport_specific_subtype_split.md):
   "when a sport adds fields to a `*Base`-hanging entity, model a
   sport-specific subclass; do NOT pile nullable columns on the
   shared parent."
2. **MLB's `probables[]`** — ESPN's MLB EventCompetitionCompetitor
   payload carries a `probables[]` array (probable starting pitcher
   today; possibly more roles in the future). Football has no
   equivalent. Adding it to the shared base would compound the smell;
   adding it to a sport-specific subclass needs that subclass to
   exist first.

ESPN payload shape comparison (live fixtures in the test data):

| Field | NCAAFB | MLB | Where it should live |
|---|---|---|---|
| `id`, `uid`, `type`, `order`, `homeAway` | ✓ | ✓ | base |
| `team.$ref` (FranchiseSeasonId) | ✓ | ✓ | base |
| `score.$ref` | ✓ | ✓ | base |
| `record.$ref` | ✓ | ✓ | base |
| `winner` | ✓ | (implicit) | base |
| `statistics.$ref` | ✓ | ✓ | base |
| `linescores.$ref` | ✓ (quarters) | ✓ (innings) | base |
| `leaders.$ref` | ✓ | ✓ | not yet ingested either way |
| `roster.$ref` | ✓ | – | football subtype (not yet ingested) |
| `ranks.$ref` | ✓ | – | football subtype (not yet ingested) |
| `curatedRank.current` | ✓ | – | **moves: base → football subtype** |
| `probables[]` | – | ✓ | **adds: baseball subtype** (Phase 2) |

The effort is sequenced as four phases. Phase 1 (the structural split)
and Phase 2 (MLB probables ingestion) are documented in detail below.
Phases 3 and 4 are scoped at the end.

## Phase 1 — Sport-specific subtype split

Phase 1 scope: rename the existing canonical entity to
`CompetitionCompetitorBase`, make it abstract, and introduce
sport-specific TPH subclasses, mirroring the pattern already used for
`CompetitionBase` → `BaseballCompetition` / `FootballCompetition`.

### Entity model

Rename the existing `CompetitionCompetitor` to `CompetitionCompetitorBase`
and make it abstract. The DB table name (`CompetitionCompetitor`)
stays. The renamed type lives in the same path:
`src/SportsData.Producer/Infrastructure/Data/Entities/CompetitionCompetitorBase.cs`.

Add two sport-specific subclasses:

- `src/SportsData.Producer/Infrastructure/Data/Baseball/Entities/BaseballCompetitionCompetitor.cs`
- `src/SportsData.Producer/Infrastructure/Data/Football/Entities/FootballCompetitionCompetitor.cs`

Move `CuratedRankCurrent` off the base, onto
`FootballCompetitionCompetitor`.

`BaseballCompetitionCompetitor` carries no new columns in Phase 1
(probables come in Phase 2).

### Context registrations

Each sport context exposes a typed `DbSet` on the concrete subclass:

- `BaseballDataContext.CompetitionCompetitors`: `DbSet<BaseballCompetitionCompetitor>`
- `FootballDataContext.CompetitionCompetitors`: `DbSet<FootballCompetitionCompetitor>`

The base type is abstract, so EF will use TPH with a `Discriminator`
column. Each DB is single-sport, so the discriminator is cosmetic —
every row in Baseball DB has `Discriminator = "BaseballCompetitionCompetitor"`,
every row in Football DB has `Discriminator = "FootballCompetitionCompetitor"`.
This matches how `CompetitionBase` was split previously.

### Processor wiring

`EventCompetitionCompetitorDocumentProcessor` currently constructs
`CompetitionCompetitor` directly. After the split it can't — it would
have to construct an abstract base. Two options:

1. **Mirror the `EventCompetitionDocumentProcessorBase` pattern**:
   abstract `CreateEntity(...)` on the base processor, sport-specific
   subclasses (`BaseballEventCompetitionCompetitorDocumentProcessor`,
   `FootballEventCompetitionCompetitorDocumentProcessor`) override it.
2. Keep one processor, dispatch on `Sport` from the command.

Option 1 matches the existing pattern in this codebase and keeps each
sport's logic isolated. Going with (1).

### Extensions

`CompetitionCompetitorExtensions.cs` has projection helpers that
reference the canonical type. The shared projections (e.g. mapping
DTO → canonical entity for the fields on the base) stay; sport-specific
projections move into per-sport extension classes living in the
sport entity folders.

### CompetitionBase navigation

`CompetitionBase.Competitors` is currently
`ICollection<CompetitionCompetitor>`. After the rename it becomes
`ICollection<CompetitionCompetitorBase>`. Sport-specific code that
needs to read sport-specific fields (e.g. `CuratedRankCurrent` for
NCAAFB) does so via `OfType<FootballCompetitionCompetitor>()` or by
reading from `FootballDataContext.CompetitionCompetitors` directly.

### Children entities

`CompetitionCompetitorStatistic`, `CompetitionCompetitorLineScore`,
`CompetitionCompetitorScore`, `CompetitionCompetitorRecord`, and
`CompetitionCompetitorExternalId` all FK to
`CompetitionCompetitor.Id`. After rename, the FK target is
`CompetitionCompetitorBase.Id`. EF's TPH stores all subclasses in the
same table, so the FK relationship doesn't change at the DB level —
no schema change for the child tables. Child C# code references the
base type, which is fine since the children don't care about
sport-specific fields.

### Migration plan

Two migrations, one per sport context.

**Baseball migration:**
- Add `Discriminator` column (string) to `CompetitionCompetitor`,
  default value `"BaseballCompetitionCompetitor"`, backfill all
  existing rows.
- Drop `CuratedRankCurrent` column (it's NCAAFB-only; Baseball never
  had meaningful values).

**Football migration:**
- Add `Discriminator` column (string) to `CompetitionCompetitor`,
  default value `"FootballCompetitionCompetitor"`, backfill all
  existing rows.
- `CuratedRankCurrent` stays — but its conceptual home is now the
  football subtype. No DDL change for that column.

Per the [test-migrations-locally feedback](../../memory/feedback_test_migrations_locally.md),
both migrations validated against a local prod copy before push.

### Tests

`CompetitionCompetitor` is referenced in:
- DTO projection / extension tests
- `EventCompetitionCompetitorDocumentProcessor` unit tests (if any)
- Any query handler tests in API that read competitors

Mechanical update: change references from `CompetitionCompetitor` to
`CompetitionCompetitorBase` (or to the concrete sport subclass where
construction is involved). No new test scenarios in Phase 1 — the
split is a refactor, not a behavior change.

## Phase 2 — MLB probables ingestion

Phase 2 hangs MLB's `probables[]` collection off the
`BaseballCompetitionCompetitor` subtype created in Phase 1, and wires
inline ingestion into the EventCompetitionCompetitor processor.

### What ESPN ships

The MLB EventCompetitionCompetitor payload carries an inline `probables`
array. Today it contains a single entry — the probable starting pitcher
— but the array shape leaves room for future roles (closer, etc.).

```json
"probables": [
  {
    "name": "probableStartingPitcher",
    "displayName": "Probable Starting Pitcher",
    "shortDisplayName": "Starter",
    "abbreviation": "SP",
    "playerId": 4311625,
    "athlete": { "$ref": ".../seasons/2026/athletes/4311625?lang=en&region=us" },
    "statistics": { "$ref": ".../seasons/2026/types/2/athletes/4311625/statistics/0?..." }
  }
]
```

Football has no analogue, so this lives entirely on the baseball side
of the split.

### Schema

New canonical entity: `CompetitionCompetitorProbable` — a 1:N child of
`BaseballCompetitionCompetitor`.

| Column | Notes |
|---|---|
| `Id` (PK) | Deterministic Guid: `Combine("competitor-probable", competitorId, name)` |
| `CompetitionCompetitorId` (FK, cascade) | Parent competitor row |
| `AthleteSeasonId` (FK, restrict) | Resolved from `athlete.$ref` |
| `EspnPlayerId` | Convenience copy of ESPN's int playerId |
| `Name` | Role key (`probableStartingPitcher`) |
| `DisplayName` / `ShortDisplayName` / `Abbreviation` | UI copy |

Unique index on `(CompetitionCompetitorId, Name)` so the role-name is
the natural key — reprocessing the same competitor with the same role
upserts the same row.

Cascade vs restrict choice:
- **Cascade from competitor** — a probable row is meaningless without
  its parent competitor; deleting the competitor takes its probables
  with it.
- **Restrict from athlete** — deleting an `AthleteSeason` should NOT
  cascade-cull historical probable rows. The probable is a record of
  what was advertised at game time; we want it to outlive the athlete's
  season row if that ever gets pruned.

### Ingestion path

`BaseballEventCompetitionCompetitorDocumentProcessor` overrides two
virtual hooks added to the base in this PR:

1. **`DeserializeDto`** — returns the sport-specific
   `EspnBaseballEventCompetitionCompetitorDto` so the base pipeline can
   pass the full payload (including `Probables`) through to the
   sport-specific hook.
2. **`ProcessSportSpecificCompetitorData`** — runs after the competitor
   entity is staged on the change tracker but before
   `SaveChangesAsync`, so probable rows commit in the same transaction
   as the competitor.

Per probable:
- Skip with a warning if `Name` or `Athlete.Ref` is missing.
- Resolve `AthleteSeasonId` via `IGenerateExternalRefIdentities`.
- If the AthleteSeason isn't in the DB yet:
  - `PublishDependencyRequest(... DocumentType.AthleteSeason)`
  - throw `ExternalDocumentNotSourcedException` so Hangfire retries
    the competitor document. This is the established not-sourced
    pattern; persisting a probable without a resolved athlete is
    worthless on the matchup card, so we fail loud and retry.
- Upsert by deterministic Id; the `(competitorId, role-name)` key
  collapses repeat runs onto the same row and updates the audit fields.

### Why not...

- **...persist the probable with a null `AthleteSeasonId`?** An empty
  probable is worthless on the matchup card. The not-sourced retry
  pattern is the established convention here.
- **...denormalize the pitcher's statistics onto the competitor?** ESPN
  ships a `statistics.$ref` per probable but those numbers drift through
  the season. Phase 3 will snapshot a small set of stats on the
  probable row at game-start time (ERA, W-L, K, etc.) so the matchup
  card renders the at-game-start view rather than today's rolled-up
  numbers — same time-travel problem we already solved for series
  state in PR #299. Out of scope here.
- **...add a `Role` enum?** Today only `probableStartingPitcher`
  exists. A string column with a unique-index discriminator keeps the
  schema flexible if ESPN starts shipping additional roles. We can
  promote to an enum once a second role appears and the closed set
  stops being speculative.

## Out of scope (later phases)

**Phase 3 — Probable pitcher stats snapshot (separate PR, separate doc):**
- Pitcher season stats (ERA, WHIP, K/9, etc.) are mutable and drift
  all season — same time-travel hazard as the
  [series snapshot redesign](baseball-series-snapshot-redesign.md).
  Matchup cards from May should display May-era ERA, not September-era.
- Likely shape: snapshot stat columns directly on
  `CompetitionCompetitorProbable`, locked on first non-null write.
  Same pattern as series snapshots, just at a different parent.
- Wider column set than series state (~10+ stat columns) — warrants
  its own design doc when we get there.

**Phase 4 — UI:**
- API exposes `Probables` on the matchup payload.
- MatchupCard component renders the SP per side, with athlete
  name/photo and stat snapshot teaser.

## Related

- [`docs/features/baseball-series-snapshot-redesign.md`](baseball-series-snapshot-redesign.md) —
  same general shape (sport-specific data on a sport-specific entity,
  snapshot for time-travel correctness) we'll mirror in Phase 3.
- [`memory/feedback_sport_specific_subtype_split.md`](../../memory/feedback_sport_specific_subtype_split.md) —
  the guardrail this PR honors.
- [`memory/feedback_test_migrations_locally.md`](../../memory/feedback_test_migrations_locally.md) —
  applies to both migrations.
- [`memory/feedback_design_doc_first.md`](../../memory/feedback_design_doc_first.md) —
  why this doc exists.
