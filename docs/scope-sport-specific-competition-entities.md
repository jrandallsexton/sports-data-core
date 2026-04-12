# Scope: Sport-Specific Competition Entity Refactor

## Objective

Split `CompetitionPlay` and `CompetitionSituation` into sport-specific entity hierarchies using TPH (table-per-hierarchy), following the proven `Athlete` → `TeamAthlete` → `FootballAthlete`/`BaseballAthlete` pattern. This enables baseball-specific pitch tracking, at-bat grouping, and situation modeling without polluting the football schema.

`Contest` remains shared — it is structurally identical across sports. `Competition` is ~95% shared but has sport-specific differences (e.g., football has Drives; baseball has series data, probables). Competition will follow the same TPH split pattern: `CompetitionBase` (shared) → `FootballCompetition` / `BaseballCompetition`.

## Timing

**Execute during the off-season (now through August 2026).** Both NCAAFB and NFL will be active by September — any schema changes to the football tables must be stable before then.

## Entity Changes

### Phase 1: CompetitionPlay (highest impact, ~25 files)

#### New Entity Hierarchy

```
CompetitionPlay (base — shared fields, stays in TeamSportDataContext)
├── FootballCompetitionPlay (Football-specific: drives, downs, yards, clock)
└── BaseballCompetitionPlay (Baseball-specific: pitches, at-bats, coordinates)
```

#### Base Entity: CompetitionPlay (modify existing)

**Keep on base:**
- Id, CompetitionId, EspnId, SequenceNumber, Ordinal
- Type (PlayType enum — needs sport-scoping, see below), TypeId
- Text, ShortText, AlternativeText, ShortAlternativeText
- AwayScore, HomeScore, PeriodNumber
- ScoringPlay, Priority, ScoreValue
- Modified, Wallclock
- StartFranchiseSeasonId (team on play — used by both sports)
- ExternalIds, Probabilities, SituationsAsLastPlay (navigation collections)

**Move to FootballCompetitionPlay:**
- DriveId (FK to CompetitionDrive)
- EndFranchiseSeasonId (football has start/end team, baseball only has team)
- StartDown, StartDistance, StartYardLine, StartYardsToEndzone
- EndDown, EndDistance, EndYardLine, EndYardsToEndzone
- StatYardage
- ClockValue, ClockDisplayValue
- ScoringType (touchdown, field goal, etc.) — currently on the DTO but not the entity

**New on BaseballCompetitionPlay:**
- AtBatId (string — groups pitches within an at-bat)
- AtBatPitchNumber (int? — pitch number within the at-bat)
- BatOrder (int? — batter's lineup position)
- BatsType, BatsAbbreviation (batter handedness)
- PitchCoordinateX, PitchCoordinateY (double? — strike zone location)
- HitCoordinateX, HitCoordinateY (double? — field location)
- PitchTypeId, PitchTypeText, PitchTypeAbbreviation (pitch classification)
- PitchVelocity (int? — mph)
- PitchCountBalls, PitchCountStrikes (running count)
- ResultCountBalls, ResultCountStrikes (count at result)
- Trajectory (string? — F=fly, G=ground, L=line drive)
- StrikeType (string? — foul, swinging, looking)
- SummaryType (string? — I=inning, A=at-bat, P=pitch, N=result)
- AwayHits, HomeHits (int — running totals)
- AwayErrors, HomeErrors (int — running totals)
- RbiCount (int)
- IsDoublePlay, IsTriplePlay (bool)

#### PlayType Enum

Current `PlayType` enum has only football values. ESPN reuses type IDs across sports with different meanings (e.g., id=59 = "FieldGoalGood" in football, "Start Inning" in baseball).

**Options:**
1. **Sport-scoped enum** — `FootballPlayType` and `BaseballPlayType` on the sport-specific entities, keep `PlayType` on base as `Unknown` for cross-sport queries
2. **Unified enum with sport prefix** — `FootballFieldGoalGood = 1059`, `BaseballStartInning = 2059`. Gets messy.
3. **String-based type** — drop the enum, store TypeId (string) + TypeText (string). Most flexible, least type-safe.

**Recommendation:** Option 1. Sport-specific enums on the subclasses. The base entity keeps `TypeId` (string) for raw storage and drops the `PlayType` enum property.

### Phase 2: Competition (medium-high impact, ~20 files)

#### New Entity Hierarchy

```
Competition (base — ~95% of current fields, stays in TeamSportDataContext)
├── FootballCompetition (Drives collection, football-specific format/overtime)
└── BaseballCompetition (Series data, probables, baseball-specific format)
```

#### Base Entity: Competition (modify existing)

**Keep on base (~95% of current properties):**
- Id, ContestId, Date, Attendance
- All boolean availability flags (IsPlayByPlayAvailable, etc.)
- Type fields (TypeId, TypeText, etc.)
- Source fields (GameSource, BoxscoreSource, etc.)
- Status, Venue, VenueId
- Competitors, Notes, Broadcasts, Links
- Leaders, PowerIndexes, Probabilities, Odds, Situations, Media, Metrics
- Plays (base CompetitionPlay collection)
- Format regulation fields (periods, displayName, slug)
- ExternalIds

**Move to FootballCompetition:**
- Drives (ICollection<CompetitionDrive>) — football-only concept
- FormatOvertimeDisplayName, FormatOvertimePeriods, FormatOvertimeSlug — football overtime structure differs from baseball extras
- HasDefensiveStats — football-specific

**New on BaseballCompetition:**
- SeriesId (string?) — ESPN series identifier
- SeriesSummary (string?) — e.g., "Series tied 1-1"
- SeriesTotalCompetitions (int?) — games in the series
- WasSuspended (bool) — rain delays / suspended games
- Duration (string?) — game duration display (e.g., "2:42")

### Phase 3: CompetitionSituation (medium impact, ~15 files)

#### New Entity Hierarchy

```
CompetitionSituation (base — CompetitionId, LastPlayId)
├── FootballCompetitionSituation (Down, Distance, YardLine, IsRedZone, Timeouts)
└── BaseballCompetitionSituation (Balls, Strikes, Outs, OnFirst/Second/Third, Pitcher, Batter)
```

#### Base Entity: CompetitionSituation (modify existing)

**Keep on base:**
- Id, CompetitionId, LastPlayId

**Move to FootballCompetitionSituation:**
- Down, Distance, YardLine, IsRedZone
- AwayTimeouts, HomeTimeouts
- Check constraints (CK_CompetitionSituation_Down, etc.)

**New on BaseballCompetitionSituation:**
- Balls (int), Strikes (int), Outs (int)
- OnFirstAthleteRef (string? — ref URL or athlete ID)
- OnSecondAthleteRef (string?)
- OnThirdAthleteRef (string?)
- PitcherAthleteRef (string?)
- BatterAthleteRef (string?)
- PitcherPeriod (int? — inning pitcher entered)
- BatterPeriod (int? — inning of at-bat)

### Phase 3: CompetitionDrive (no change needed)

`CompetitionDrive` is football-only. It lives on `TeamSportDataContext` but only football processors create drives. Baseball simply never populates it. No refactor needed — the empty table in the baseball database is harmless.

## Files to Modify

### Entities (7 files to modify, 6 new files)

| File | Action |
|------|--------|
| `Infrastructure/Data/Entities/Competition.cs` | Extract football-specific fields to subclass, keep ~95% shared |
| `Infrastructure/Data/Entities/CompetitionPlay.cs` | Extract football fields to subclass, keep shared fields |
| `Infrastructure/Data/Entities/CompetitionPlayExternalId.cs` | No change (FK to base CompetitionPlay) |
| `Infrastructure/Data/Entities/CompetitionSituation.cs` | Extract football fields to subclass, keep shared fields |
| `Infrastructure/Data/Entities/CompetitionDrive.cs` | Update FK to `FootballCompetition`, `Plays` navigation to `FootballCompetitionPlay` |
| `Infrastructure/Data/Entities/CompetitionProbability.cs` | No change (FK to base CompetitionPlay) |
| `Infrastructure/Data/Entities/Extensions/CompetitionExtensions.cs` | Split `AsEntity` into `AsFootballEntity` / `AsBaseballEntity` |
| **NEW** `Infrastructure/Data/Football/Entities/FootballCompetition.cs` | Football-specific: Drives collection, overtime format, HasDefensiveStats |
| **NEW** `Infrastructure/Data/Football/Entities/FootballCompetitionPlay.cs` | Football-specific play fields |
| **NEW** `Infrastructure/Data/Football/Entities/FootballCompetitionSituation.cs` | Football-specific situation fields |
| **NEW** `Infrastructure/Data/Baseball/Entities/BaseballCompetition.cs` | Baseball-specific: series data, duration, WasSuspended |
| **NEW** `Infrastructure/Data/Baseball/Entities/BaseballCompetitionPlay.cs` | Baseball-specific play fields |
| **NEW** `Infrastructure/Data/Baseball/Entities/BaseballCompetitionSituation.cs` | Baseball-specific situation fields |

### Data Contexts (2 files)

| File | Action |
|------|--------|
| `Infrastructure/Data/Football/FootballDataContext.cs` | Add `new DbSet<FootballCompetition>`, `new DbSet<FootballCompetitionPlay>`, `new DbSet<FootballCompetitionSituation>` + entity configs |
| `Infrastructure/Data/Baseball/BaseballDataContext.cs` | Add `new DbSet<BaseballCompetition>`, `new DbSet<BaseballCompetitionPlay>`, `new DbSet<BaseballCompetitionSituation>` + entity configs |

### Extension Methods (3 files to modify, 2 new files)

| File | Action |
|------|--------|
| `Infrastructure/Data/Entities/Extensions/CompetitionPlayExtensions.cs` | Split `AsEntity` into `AsFootballEntity` / `AsBaseballEntity` (like AthleteExtensions) |
| `Infrastructure/Data/Entities/Extensions/CompetitionSituationExtensions.cs` | Same split |
| `Infrastructure/Data/Entities/Extensions/CompetitionExtensions.cs` | Minor — update if play/situation types change |

### Document Processors (5 files to modify, 1 new file)

| File | Action |
|------|--------|
| `Football/EventCompetitionDocumentProcessor.cs` | Change entity type to `FootballCompetition`, use `AsFootballEntity`, constrain to `FootballDataContext` (restore original constraint) |
| `Football/EventCompetitionPlayDocumentProcessor.cs` | Change entity type to `FootballCompetitionPlay`, use `AsFootballEntity`, constrain to `FootballDataContext` (restore original constraint) |
| `Football/EventCompetitionSituationDocumentProcessor.cs` | Change entity type to `FootballCompetitionSituation`, constrain to `FootballDataContext` (restore original constraint) |
| `Baseball/BaseballEventCompetitionPlayDocumentProcessor.cs` | Change entity type to `BaseballCompetitionPlay`, use `AsBaseballEntity` |
| `Baseball/BaseballEventCompetitionSituationDocumentProcessor.cs` | Change entity type to `BaseballCompetitionSituation` |
| **NEW** `Baseball/BaseballEventCompetitionDocumentProcessor.cs` | Create baseball-specific competition processor for `BaseballCompetition` (series data, duration, probables) |

### Query Handlers (2 files)

| File | Action |
|------|--------|
| `Contests/Queries/GetContestOverview/GetContestOverviewQueryHandler.cs` | Uses CompetitionPlay for play log — may need sport-aware query or base type query |
| `Competitions/Commands/CalculateCompetitionMetrics/CalculateCompetitionMetricsCommandHandler.cs` | Uses CompetitionPlay for football metrics (FG attempts, etc.) — constrain to FootballCompetitionPlay |

### Streaming / Enrichment (2 files)

| File | Action |
|------|--------|
| `Competitions/FootballCompetitionStreamer.cs` | Already football-only, update to use FootballCompetitionPlay if needed |
| `Contests/ContestEnrichmentProcessor.cs` | References CompetitionPlay — verify impact |

### ESPN DTOs (1 new file, 1 modify)

| File | Action |
|------|--------|
| `Core/Infrastructure/DataSources/Espn/Dtos/Common/EspnEventCompetitionPlayDto.cs` | Keep as shared DTO — all fields nullable, deserializes both sports |
| **NEW** `Core/Infrastructure/DataSources/Espn/Dtos/Baseball/EspnBaseballEventCompetitionPlayDto.cs` | Subclass with baseball-specific typed properties (pitchCoordinate, hitCoordinate, pitchType, etc.) |
| `Core/Infrastructure/DataSources/Espn/Dtos/Common/EspnEventCompetitionSituationDto.cs` | Keep as shared — add baseball fields (balls, strikes, outs, runners) as nullable |

### Migrations (2 new migration files)

| Context | Action |
|---------|--------|
| FootballDataContext | Add Discriminator column to CompetitionPlay + CompetitionSituation, add FootballCompetitionPlay columns (no data change — existing rows get 'FootballCompetitionPlay' discriminator), move check constraints to football-only |
| BaseballDataContext | Add Discriminator column, add BaseballCompetitionPlay columns, add BaseballCompetitionSituation columns |

### Tests (7 files to modify/create)

| File | Action |
|------|--------|
| `Football/EventCompetitionPlayDocumentProcessorTests.cs` | Update to use `FootballCompetitionPlay` assertions |
| `Baseball/BaseballEventCompetitionPlayDocumentProcessorTests.cs` | Update to use `BaseballCompetitionPlay` assertions |
| `Baseball/BaseballEventCompetitionSituationDocumentProcessorTests.cs` | Update to use `BaseballCompetitionSituation` assertions |
| `Common/EventCompetitionDocumentProcessorTests.cs` | Verify no impact |
| `Common/EventCompetitionProbabilityDocumentProcessorTests.cs` | Verify no impact |
| `Competitions/CalculateCompetitionMetricsCommandHandlerTests.cs` | Update if metrics handler changes |
| `Contests/ContestEnrichmentProcessorTests.cs` | Verify no impact |

### API Project (7 files — read-only queries, low risk)

| File | Action |
|------|--------|
| `Sql/GetContestOverviewPlayLog.sql` | Verify query works against base CompetitionPlay table (TPH — same physical table) |
| `Sql/Errors/CompetitionsWithoutDrives.sql` | Football-only diagnostic — add sport filter |
| `Sql/Errors/CompetitionsWithoutPlays.sql` | Add sport filter |
| `Admin/Queries/GetCompetitionsWithoutPlays/` | Add sport filter to query handler |
| `Admin/Queries/GetCompetitionsWithoutDrives/` | Football-only — add sport guard |
| `Admin/Queries/GetCompetitionsWithoutCompetitors/` | Verify no impact |
| `Admin/Queries/GetCompetitionsWithoutMetrics/` | Football-only metrics — add sport guard |

### Competition Command Handlers (7 files — verify impact)

| File | Action |
|------|--------|
| `Competitions/Commands/CalculateCompetitionMetrics/` | Uses CompetitionPlay for football metrics — constrain to FootballCompetitionPlay |
| `Competitions/Commands/RefreshCompetitionDrives/` | Football-only — no change |
| `Competitions/Commands/RefreshCompetitionMedia/` | Sport-agnostic — verify no impact |
| `Competitions/Commands/RefreshCompetitionMetrics/` | Football-only metrics — add sport guard |
| `Competitions/Commands/EnqueueCompetitionMediaRefresh/` | Sport-agnostic — verify no impact |
| `Competitions/Commands/EnqueueCompetitionMetricsCalculation/` | Football-only — add sport guard |
| `Competitions/FootballCompetitionMetricsAuditJob.cs` | Already football-only — may need FootballCompetitionPlay type |

### Core PlayType Enum (1 file)

| File | Action |
|------|--------|
| `Core/Common/PlayType.cs` | Either split into `FootballPlayType`/`BaseballPlayType` or deprecate in favor of TypeId string |

## Total File Count

| Category | Files Modified | Files Created |
|----------|---------------|---------------|
| Entities | 7 | 6 |
| Data Contexts | 2 | 0 |
| Extensions | 3 | 0 |
| Processors | 5 | 1 |
| Query Handlers | 2 | 0 |
| Streaming/Enrichment | 2 | 0 |
| Competition Commands | 7 | 0 |
| DTOs | 2 | 1 |
| Migrations | 0 | 2 |
| Tests | 7 | 0 |
| API SQL/Queries | 7 | 0 |
| Enums | 1 | 0-2 |
| **Total** | **~45** | **~9** |

## Migration Strategy

### Data Backfill

Since TPH uses a single physical table with a Discriminator column:

1. Add `Discriminator` column (varchar, not null, default 'CompetitionPlay')
2. `UPDATE "CompetitionPlay" SET "Discriminator" = 'FootballCompetitionPlay'` — all existing rows are football
3. Add new baseball-specific columns (all nullable)
4. Football-specific columns that were non-nullable become nullable on the base table (EF handles via discriminator)

### Check Constraints

Football's `CompetitionSituation` has check constraints (`CK_CompetitionSituation_Down`, etc.). These need to be dropped and recreated as partial constraints filtered by Discriminator, or moved to application-level validation.

### Zero Downtime

- Deploy Producer code first (new entity hierarchy, both processors)
- Run migration (additive — new columns, discriminator)
- Backfill discriminator values
- No data loss, no breaking changes to existing football data

## Phasing Within the Refactor

### PR 1: Entity hierarchy + migrations (no processor changes)
- Create all 6 subclass entities (FootballCompetition, BaseballCompetition, Football/BaseballCompetitionPlay, Football/BaseballCompetitionSituation)
- Update data contexts with sport-specific DbSets
- Generate migrations with discriminator columns
- Backfill script (all existing rows get Football discriminator)
- All existing processors continue to work against base types

### PR 2: Football processor updates
- Update EventCompetitionDocumentProcessor to use FootballCompetition
- Update football play/situation processors to use FootballCompetitionPlay/FootballCompetitionSituation
- Update extension methods (AsFootballEntity)
- Restore FootballDataContext constraints on processors
- Update competition command handlers with sport guards
- Update football tests

### PR 3: Baseball processor updates
- Create BaseballEventCompetitionDocumentProcessor for BaseballCompetition (series data, duration, probables)
- Update baseball play/situation processors to use BaseballCompetitionPlay/BaseballCompetitionSituation
- Create baseball DTO subclass for competition play
- Update baseball extension methods (AsBaseballEntity)
- Update baseball tests

### PR 4: PlayType enum refactor
- Create sport-specific enums or switch to string-based
- Update processors and query handlers
- Clean up any remaining base type references

### PR 5: API + Cleanup
- Add sport filters to API admin query handlers (CompetitionsWithoutDrives, CompetitionsWithoutPlays, etc.)
- Verify API SQL queries work correctly against TPH tables
- Remove nullable football fields from base if no longer needed
- Final test pass

## Estimated Effort

- PR 1: 1-2 days (entity hierarchy + migrations are mechanical)
- PR 2: 1-2 days (football processor + command handler updates)
- PR 3: 1-2 days (baseball processor + new competition processor)
- PR 4: 0.5 days (enum refactor)
- PR 5: 0.5-1 day (API queries + cleanup and verification)

**Total: ~5-7 days of focused work**, spread across a few weeks is fine given the off-season timeline.
