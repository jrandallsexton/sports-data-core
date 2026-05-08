# CompetitionCompetitor — sport-specific subtype split

Captured 2026-05-08. Plan to split the shared `CompetitionCompetitor`
canonical entity into a base + per-sport subclasses, mirroring the
pattern already used for `CompetitionBase` →
`BaseballCompetition` / `FootballCompetition`. Scoped to Phase 1 of a
multi-PR effort; downstream phases noted at the end.

## Why

Two pressure points have arrived at the same time:

1. **`CuratedRankCurrent` is on the shared base** — that column is
   NCAAFB-specific (poll ranks). It violates the
   [sport-specific subtype split feedback memory](../memory/feedback_sport_specific_subtype_split.md):
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

## What changes (Phase 1 scope)

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

## Migration plan

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

Per the [test-migrations-locally feedback](../memory/feedback_test_migrations_locally.md),
both migrations validated against a local prod copy before push.

## Tests

`CompetitionCompetitor` is referenced in:
- DTO projection / extension tests
- `EventCompetitionCompetitorDocumentProcessor` unit tests (if any)
- Any query handler tests in API that read competitors

Mechanical update: change references from `CompetitionCompetitor` to
`CompetitionCompetitorBase` (or to the concrete sport subclass where
construction is involved). No new test scenarios in Phase 1 — the
split is a refactor, not a behavior change.

## Out of scope (deferred phases)

**Phase 2 — MLB Probables ingestion (separate PR):**
- New entity `CompetitionCompetitorProbable` (Baseball-only):
  `Name`, `DisplayName`, `ShortDisplayName`, `Abbreviation`,
  `EspnPlayerId`, `BaseballAthleteId` (FK to BaseballAthlete), audit.
- 1:N collection on `BaseballCompetitionCompetitor` (today only the SP,
  but ESPN's array shape suggests room for more roles).
- Processor extends to read `probables[]`, resolves
  `athlete.$ref` to `BaseballAthlete.Id`. **Per the established
  not-sourced pattern, if the athlete cannot be resolved, request
  athlete sourcing and throw `NotSourcedException` so Hangfire
  retries — do not persist Probable rows with null athlete FKs.
  An empty Probable is worthless on the matchup card.**

**Phase 3 — Probable pitcher stats snapshot (separate PR, separate doc):**
- Pitcher season stats (ERA, WHIP, K/9, etc.) are mutable and drift
  all season — same time-travel hazard as the
  [series snapshot redesign](series-snapshot-redesign.md).
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

- [`docs/series-snapshot-redesign.md`](series-snapshot-redesign.md) —
  same general shape (sport-specific data on a sport-specific entity,
  snapshot for time-travel correctness) we'll mirror in Phase 3.
- [`memory/feedback_sport_specific_subtype_split.md`](../memory/feedback_sport_specific_subtype_split.md) —
  the guardrail this PR honors.
- [`memory/feedback_test_migrations_locally.md`](../memory/feedback_test_migrations_locally.md) —
  applies to both migrations.
- [`memory/feedback_design_doc_first.md`](../memory/feedback_design_doc_first.md) —
  why this doc exists.
