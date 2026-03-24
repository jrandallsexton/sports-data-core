# Plan: Remove Denormalized SeasonYear Fields

**Created:** 2026-03-24
**Status:** Deferred (plan complete, execution postponed until after historical sourcing)
**Estimated effort:** 7-10 working days across 3-4 PRs
**Risk level:** Medium (database schema change, high file count, but mechanically straightforward)
**Timing recommendation:** Execute after off-season historical sourcing is complete. See Section 12 for rationale.

---

## 1. Problem Statement

`SeasonYear` was denormalized from `Season.Year` into many entity tables to avoid JOINs. This has caused data corruption **twice**:

- **March 2, 2026:** 12,995+ records corrupted across GroupSeason, FranchiseSeason, Contest, Rankings, and Records. Root cause: GroupSeason processor set `SeasonYear = 2023` (wrong variable) while `SeasonId` FK was correct.
- **March 24, 2026:** 8,609 records — FranchiseSeason and downstream entities drifted from the canonical `GroupSeason.SeasonYear`. All wrong values were `2023` across 7 actual seasons (2015-2022).

Both incidents were fixable only because FK relationships were intact and the denormalized column could be recomputed from JOINs. Fix scripts: `sql/pgsql/data-fix-seasonyear-2026-03-02/` and `sql/pgsql/data-fix-seasonyear-2026-03-24/`.

**Decision:** `Season.Year` is the **sole source of truth** for season year. Remove `SeasonYear` from ALL entities that copied it. Derive via JOINs through FK relationships.

---

## 2. What to KEEP (Not In Scope)

These uses of SeasonYear are **canonical, structural, or impractical to remove** and will NOT be changed:

| Location | Reason |
|----------|--------|
| `Season.Year` | THE sole source of truth |
| `SeasonPoll.SeasonYear` | No FK to Season, GroupSeason, or any entity with a derivation path; truly standalone |
| `ProcessDocumentCommand.SeasonYear` | Pipeline parameter, not a database field |
| `EventBase.SeasonYear` and all ~30 event subclasses | Messaging contract across ALL services; changing requires coordinated deployment |
| All API route parameters (`/rankings/{seasonYear}`, etc.) | User-facing URL contracts |
| All UI code (sd-ui: 18 files, sd-mobile: 4 files) | Route params and API call parameters — no changes |
| All Core DTOs (7 classes) | API response contracts needed by UI |
| `EspnUriMapper.TryExtractSeasonYear` | URI parsing utility |
| `GetExternalDocumentQuery.SeasonYear`, `GetExternalImageQuery.SeasonYear` | Provider query parameters |

---

## 3. What to REMOVE

### 3.1 Producer Entities (6 columns to drop)

| Entity | Property | Derivation Path | Notes |
|--------|----------|-----------------|-------|
| `GroupSeason` | `SeasonYear` | `SeasonId` -> `Season.Year` | 1 JOIN; has composite index `(SeasonYear, Slug)` to replace |
| `FranchiseSeason` | `SeasonYear` | `GroupSeasonId` -> `GroupSeason.SeasonId` -> `Season.Year` | 2 JOINs; corrupted in both incidents |
| `Contest` | `SeasonYear` | `SeasonWeekId` -> `SeasonWeek.SeasonId` -> `Season.Year` | 2 JOINs |
| `FranchiseSeasonRanking` | `SeasonYear` | `FranchiseSeasonId` -> `FranchiseSeason` -> `GroupSeason` -> `Season` | 3 JOINs |
| `FranchiseSeasonRecord` | `SeasonYear` | `FranchiseSeasonId` -> `FranchiseSeason` -> `GroupSeason` -> `Season` | 3 JOINs; marked "denormalized for convenience" |
| `FranchiseSeasonProjection` | `SeasonYear` | `FranchiseSeasonId` -> `FranchiseSeason` -> `GroupSeason` -> `Season` | 3 JOINs; marked "denormalized for convenience" |

### 3.2 API Entities — Tier 1 (have SeasonWeekId FK, straightforward)

| Entity | Property | Derivation Path |
|--------|----------|-----------------|
| `Contest` | `SeasonYear` | Synced from Producer; derive via SeasonWeek at sync time |
| `Article` | `SeasonYear` | Has `SeasonWeekId`; derive via JOIN |
| `PickemGroupWeek` | `SeasonYear` | Has `SeasonWeekId`; has index on `(SeasonYear, SeasonWeek)` to update |
| `PickemGroupMatchup` | `SeasonYear` | Has `SeasonWeekId` + FK to `PickemGroupWeek` |

### 3.3 API Entities — Tier 2 (no SeasonWeekId FK, harder)

| Entity | Property | Constraint Using SeasonYear | Challenge |
|--------|----------|----------------------------|-----------|
| `ContestResult` | `SeasonYear` | Index on `(Sport, SeasonYear)` | Has `ContestId` FK — derive via Contest |
| `PickemGroupUserStanding` | `SeasonYear` | Unique `(PickemGroupId, UserId, SeasonYear, SeasonWeek)` | No SeasonWeekId FK — must add one |
| `PickemGroupWeekResult` | `SeasonYear` | Unique `(PickemGroupId, SeasonYear, SeasonWeek, UserId)` | No SeasonWeekId FK — must add one |

---

## 4. Derivation Chain Reference

```
Season.Year  (sole source of truth)
  |
  +-- SeasonWeek.SeasonId -> Season.Year
  |     |
  |     +-- Contest.SeasonWeekId -> SeasonWeek -> Season        (2 JOINs)
  |
  +-- GroupSeason.SeasonId -> Season.Year                       (1 JOIN, REMOVE SeasonYear)
        |
        +-- FranchiseSeason.GroupSeasonId -> GroupSeason -> Season  (2 JOINs, REMOVE SeasonYear)
              |
              +-- FranchiseSeasonRanking.FranchiseSeasonId         (3 JOINs)
              +-- FranchiseSeasonRecord.FranchiseSeasonId          (3 JOINs)
              +-- FranchiseSeasonProjection.FranchiseSeasonId      (3 JOINs)
```

---

## 5. Phased Execution Plan

### Phase 1: Producer Entities (PR 1)

**Goal:** Remove `SeasonYear` from GroupSeason, FranchiseSeason, Contest, FranchiseSeasonRanking, FranchiseSeasonRecord, FranchiseSeasonProjection in Producer.

**Estimated files changed:** ~90 (6 entities, 6 extensions, 5 processors, 10 handlers/services, 3 commands, ~60 tests)

#### Step 1a: Update Entity Classes

Remove `SeasonYear` property and its EF configuration from:

| File | Lines |
|------|-------|
| `src/SportsData.Producer/Infrastructure/Data/Entities/GroupSeason.cs` | Property (line 20), EF config, composite index `(SeasonYear, Slug)` (line 94) |
| `src/SportsData.Producer/Infrastructure/Data/Entities/FranchiseSeason.cs` | Property (line 24) |
| `src/SportsData.Producer/Infrastructure/Data/Entities/Contest.cs` | Property (line 36), EF config (lines 107-108) |
| `src/SportsData.Producer/Infrastructure/Data/Entities/FranchiseSeasonRanking.cs` | Property (line 24), EF config (lines 60-61) |
| `src/SportsData.Producer/Infrastructure/Data/Entities/FranchiseSeasonRecord.cs` | Property (line 16), EF config (lines 80-81) |
| `src/SportsData.Producer/Infrastructure/Data/Entities/FranchiseSeasonProjection.cs` | Property (line 16), EF config (line 37) |

**Index replacement for GroupSeason:**

| Current | New | Rationale |
|---------|-----|-----------|
| `HasIndex(x => new { x.SeasonYear, x.Slug }).IsUnique(false)` | `HasIndex(x => new { x.SeasonId, x.Slug }).IsUnique(false)` | `SeasonId` FK is already present and indexed; same selectivity |

#### Step 1b: Update Extension Methods

| File | Change |
|------|--------|
| `GroupSeasonExtensions.cs` | Remove `seasonYear` param from `AsEntity`, remove `SeasonYear = seasonYear` assignment. `ToCanonicalModel` needs no change (does not map SeasonYear; `ConferenceSeasonDto` is currently empty) |
| `FranchiseSeasonExtensions.cs` | Remove `seasonYear` param from `AsEntity` (line 10), remove assignment (line 30). In `ToCanonicalModel` (line 61), change `SeasonYear` mapping (line 68) to derive via `entity.GroupSeason.Season!.Year` |
| `ContestExtensions.cs` | Remove `seasonYear` param from `AsEntity`. In `ToCanonicalModel`, derive via `entity.SeasonWeek.Season!.Year` (requires `.Include()` at call site) |
| `FranchiseSeasonRankingExtensions.cs` | Remove `seasonYear` param from `AsEntity` (line 15), remove assignment (line 29) |
| `FranchiseSeasonRecordExtensions.cs` | Remove `seasonYear` param from `AsEntity` (line 12), remove assignment (line 20). In `AsCanonical`, derive via `entity.FranchiseSeason.GroupSeason.Season!.Year` |
| `FranchiseSeasonProjectionExtensions.cs` | Remove `seasonYear` param from `AsEntity` (line 13), remove assignment (line 20) |

#### Step 1c: Update Document Processors

| File | Change |
|------|--------|
| `GroupSeasonDocumentProcessor.cs` | Remove `seasonYear` argument from `AsEntity()` call; SeasonYear extraction logic (lines 61-89) still needed for `ProcessDocumentCommand` pipeline param and event publishing, but no longer stored on entity |
| `TeamSeasonDocumentProcessor.cs` | Remove `seasonYear` argument from `AsEntity()` call for FranchiseSeason |
| `EventDocumentProcessor.cs` | Remove `seasonYear` argument from `AsEntity()` call for Contest |
| `TeamSeasonRankDocumentProcessor.cs` | Stop reading `franchiseSeason.SeasonYear` (line 73); no longer needed since child entity doesn't store it |
| `TeamSeasonRecordDocumentProcessor.cs` | Stop reading `franchiseSeason.SeasonYear` (lines 80, 90); derive from nav prop chain for event publishing |
| `TeamSeasonProjectionDocumentProcessor.cs` | Stop reading `franchiseSeason.SeasonYear` (line 90); derive from nav prop chain for event publishing |
| `FootballSeasonRankingDocumentProcessor.cs` | Change `.Where(x => x.SeasonYear == ...)` to JOIN via FranchiseSeason -> GroupSeason -> Season |
| `SeasonFutureDocumentProcessor.cs` | Change `.Where(fs => fs.SeasonYear == season.Year)` (line 110) to `.Where(fs => fs.GroupSeason.Season!.Year == season.Year)` |

#### Step 1d: Update LINQ Queries

| File | Current | New |
|------|---------|-----|
| `GroupSeasonsService.cs:24` | `gs.SeasonYear == seasonYear` | `gs.Season!.Year == seasonYear` |
| `FranchiseSeasonEnrichmentJob.cs:31` | `x.SeasonYear == effectiveSeasonYear` | `x.GroupSeason.Season!.Year == effectiveSeasonYear` |
| `EnqueueFranchiseSeasonEnrichmentCommandHandler.cs:76` | `fs.SeasonYear == command.SeasonYear` | `fs.GroupSeason.Season!.Year == command.SeasonYear` |
| `EnqueueFranchiseSeasonMetricsGenerationCommandHandler.cs:52` | `fs.SeasonYear == command.SeasonYear` | `fs.GroupSeason.Season!.Year == command.SeasonYear` |
| `GetFranchiseSeasonByIdQueryHandler.cs:29` | `fs.SeasonYear == query.SeasonYear` | `fs.GroupSeason.Season!.Year == query.SeasonYear` |
| `GetFranchiseSeasonByIdQueryHandler.cs:34` | Projects `.SeasonYear` | Project `fs.GroupSeason.Season!.Year` |
| `GetFranchiseSeasonsQueryHandler.cs:35` | `.OrderByDescending(fs => fs.SeasonYear)` | `.OrderByDescending(fs => fs.GroupSeason.Season!.Year)` |
| `GetFranchiseSeasonsQueryHandler.cs:40` | Projects `.SeasonYear` | Project `fs.GroupSeason.Season!.Year` |
| `GetFranchiseSeasonMetricsBySeasonYearQueryHandler.cs:42` | `fsm.Season == query.SeasonYear` | No change needed — `FranchiseSeasonMetric.Season` is a plain `int` field, not a FK |
| `ContestController.cs:244` | `c.SeasonYear == seasonYear && c.SeasonWeekId == ...` | `c.SeasonWeekId == ...` (SeasonWeekId already constrains to season) |
| `RefreshCompetitionMetricsCommandHandler.cs:39` | `c.SeasonYear == command.SeasonYear` | `c.SeasonWeek.Season!.Year == command.SeasonYear` |
| `RefreshAllCompetitionMediaCommandHandler.cs:46,63` | `c.Contest.SeasonYear == command.SeasonYear` | `c.Contest.SeasonWeek.Season!.Year == command.SeasonYear` |
| `GetSeasonContestsQueryHandler.cs:26` | `c.SeasonYear == query.SeasonYear` | `c.SeasonWeek.Season!.Year == query.SeasonYear` |
| `GetCurrentPollsQueryHandler.cs:118` | `x.SeasonYear == seasonYear` (on FranchiseSeasonRanking) | `x.FranchiseSeason.GroupSeason.Season!.Year == seasonYear` |
| `GetPollBySeasonWeekIdQueryHandler.cs:70` | `x.SeasonYear == seasonYear` (on FranchiseSeasonRanking) | `x.FranchiseSeason.GroupSeason.Season!.Year == seasonYear` |

#### Step 1e: EF Migration

```
dotnet ef migrations add RemoveSeasonYearDenorm_GroupSeason_FranchiseSeason_Contest_Rankings_Records_Projections
```

Drops 6 columns, replaces GroupSeason `(SeasonYear, Slug)` index with `(SeasonId, Slug)`. **Deploy code first** (stop writing), then run migration.

#### Step 1f: Update Tests (~60 files)

Most changes are mechanical: remove `.With(x => x.SeasonYear, ...)` from AutoFixture builders, remove `seasonYear` parameter from `AsEntity()` calls, add `.Include()` setup where navigation property chains are tested.

**Highest-impact test files:**
- `GroupSeasonDocumentProcessorTests.cs` — assertions on `group.SeasonYear` must change to verify via Season FK
- `TeamSeasonDocumentProcessorTests.cs` — FranchiseSeason creation assertions
- `EventDocumentProcessorTests.cs`
- `FinalizeContestsBySeasonYearHandlerTests.cs`
- `RefreshCompetitionMetricsCommandHandlerTests.cs`
- `TeamSeasonRankDocumentProcessorTests.cs`
- `TeamSeasonRecordDocumentProcessorTests.cs`
- `TeamSeasonProjectionDocumentProcessorTests.cs`
- `GetCurrentPollsQueryHandlerTests.cs`
- `GetFranchiseSeasonByIdQueryHandlerTests.cs`
- `GetFranchiseSeasonsQueryHandlerTests.cs`
- `FranchiseSeasonEnrichmentJobTests.cs`
- `EnqueueFranchiseSeasonEnrichmentCommandHandlerTests.cs`
- `EnqueueFranchiseSeasonMetricsGenerationCommandHandlerTests.cs`
- `GetFranchiseSeasonMetricsBySeasonYearQueryHandlerTests.cs`
- `GoldenRules.cs` (integration tests: lines 34, 58)

---

### Phase 2: API Entities — Tier 1 (PR 2)

**Goal:** Remove `SeasonYear` from API entities that have a `SeasonWeekId` FK.

**Target:** Contest, Article, PickemGroupWeek, PickemGroupMatchup

**Estimated files changed:** ~45

#### Key Changes

| Area | Files | Change |
|------|-------|--------|
| Entity classes | 4 | Remove property, update EF config, update indexes |
| Event handlers | 2 | `PickemGroupCreatedHandler`, `PickemGroupWeekMatchupsGeneratedHandler` — remove SeasonYear assignments, filter by SeasonWeekId |
| Scoring services | 4 | `LeagueWeekScoringJob`, `LeagueWeekScoringService`, `BackfillLeagueScoresCommandHandler`, `ContestScoringProcessor` — rewrite SeasonYear filters |
| SQL queries | 1 | `GetTeamCardSchedule.sql` — change `C."SeasonYear"` to JOIN (other SQL files already JOIN to `Season.Year`) |
| Tests | ~24 | Remove SeasonYear from fixtures |

#### Index Updates

| Current | New |
|---------|-----|
| `PickemGroupWeek(SeasonYear, SeasonWeek)` | `PickemGroupWeek(SeasonWeekId)` |
| `PickemGroupMatchup(GroupId, SeasonYear, SeasonWeek)` | `PickemGroupMatchup(GroupId, SeasonWeekId)` |

---

### Phase 3: API Entities — Tier 2 (PR 3)

**Goal:** Remove `SeasonYear` from entities without `SeasonWeekId` FK.

**Target:** ContestResult, PickemGroupUserStanding, PickemGroupWeekResult

**This is the hardest phase.** These entities use SeasonYear in unique constraints but lack the FK needed to derive it.

#### Strategy

1. **Migration 1:** Add nullable `SeasonWeekId` FK column to PickemGroupUserStanding and PickemGroupWeekResult
2. **Backfill script:** Populate `SeasonWeekId` from existing SeasonYear+SeasonWeek matched against canonical SeasonWeek table
3. **Migration 2:** Make non-nullable, create new unique indexes, drop SeasonYear column and old indexes

For `ContestResult`: derive via `ContestId` FK -> Contest -> SeasonWeek (no new column needed).

#### Alternative

If the blast radius is too high, these three entities can be **deferred indefinitely**. The corruption risk is lower here because they're written by API scoring logic (not the Provider->Producer pipeline where corruption originated).

---

### Phase 4: Cleanup (PR 4, optional)

- Archive `sql/pgsql/data-fix-seasonyear-*` directories
- Update ad-hoc SQL scripts (~10 files)
- Remove debug queries referencing dropped columns

---

## 6. Performance Considerations

| Query Pattern | Before | After | Impact |
|---------------|--------|-------|--------|
| GroupSeason by season year | `WHERE gs.SeasonYear = @year` | `JOIN Season s ... WHERE s.Year = @year` | 1 extra JOIN; SeasonId FK indexed |
| FranchiseSeason by year | `WHERE fs.SeasonYear = @year` | `JOIN GroupSeason gs ... JOIN Season s ... WHERE s.Year = @year` | 2 extra JOINs; all FKs indexed |
| Contest by season year | `WHERE c.SeasonYear = @year` | `JOIN SeasonWeek sw ... JOIN Season s ... WHERE s.Year = @year` | 2 extra JOINs; FK indexes exist |
| Ranking by year | `WHERE fsr.SeasonYear = @year` | `JOIN FranchiseSeason fs ... JOIN GroupSeason gs ... JOIN Season s ... WHERE s.Year = @year` | 3 extra JOINs |
| Record by year | Same as Ranking | Same as Ranking | 3 extra JOINs |
| FranchiseSeason sort by year | `ORDER BY fs.SeasonYear` | `ORDER BY s.Year` (via JOIN) | 2 extra JOINs for ORDER BY |

All FK columns are already indexed. PostgreSQL will use index nested loop joins. The Season table is tiny (dozens of rows). GroupSeason and SeasonWeek are also small. **Impact should be negligible** for this dataset size.

The worst case is FranchiseSeasonRanking/Record/Projection queries which go through 3 JOINs, but each JOIN is on an indexed FK and the intermediate tables (FranchiseSeason, GroupSeason) are modest in size.

---

## 7. Deployment Strategy

Each phase follows this sequence:

1. **Code PR** — remove reads/writes, make column unused
2. **Deploy code** to all pods
3. **Migration PR** — drop column(s)
4. **Run migration** (via normal startup)

For Phase 3 (adding SeasonWeekId): deploy add-column migration first, run backfill, then deploy drop-column migration.

---

## 8. File Impact Summary

| Area | Files Changed | Tests |
|------|--------------|-------|
| Producer entities + extensions | 12 | - |
| Producer processors | 8 | - |
| Producer handlers/services/controllers | 10 | - |
| Producer commands/queries | 5 | - |
| Producer EF migration | 1 new | - |
| Producer tests | ~60 | Yes |
| API entities | 7 | - |
| API handlers/services | 8 | - |
| API SQL queries | 1 | - |
| API EF migrations | 1-3 new | - |
| API tests | 24 | Yes |
| Core (DTOs, events, clients) | **0** | - |
| UI (sd-ui, sd-mobile) | **0** | - |
| Ad-hoc SQL scripts | ~10 | - |
| **Total** | **~150** | **~84 are tests** |

---

## 9. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Query performance regression from added JOINs | Low | Low | All FKs indexed; dataset is small; Season table is tiny |
| 3-JOIN chain too slow for Ranking/Record queries | Very Low | Low | Can add computed column or materialized view if needed (unlikely) |
| Missed a SeasonYear reference | Medium | Low | Compiler catches removed properties; grep before each PR |
| EF migration ordering issue | Low | Medium | Deploy code before migration; phases are independent |
| GroupSeason index change breaks query plans | Low | Low | Replacing `(SeasonYear, Slug)` with `(SeasonId, Slug)` — same cardinality via FK |
| API pickem scoring breaks | Medium | Medium | Phase 3 is isolated; can defer |
| Breaking change in event contracts | N/A | N/A | EventBase.SeasonYear is explicitly KEPT |
| GroupSeasonDocumentProcessor regression | Medium | Medium | Processor still extracts year from URL for events/commands; test thoroughly |

---

## 10. SeasonPoll.SeasonYear — Why It Stays

`SeasonPoll` has **no FK to Season, GroupSeason, or any entity** in the derivation chain. Its `SeasonYear` is set directly from `ProcessDocumentCommand.SeasonYear` in `SeasonPollDocumentProcessor`. There is no JOIN path to derive it. Adding a `SeasonId` FK to SeasonPoll is possible but would be a schema addition (new column + backfill), not a simplification. Since SeasonPoll was not involved in either corruption incident, the risk/reward doesn't justify the change.

---

## 11. Definition of Done

- [ ] No entity in Producer or API reads or writes `SeasonYear` on the removed columns
- [ ] GroupSeason `(SeasonYear, Slug)` index replaced with `(SeasonId, Slug)`
- [ ] All LINQ queries derive season year via JOINs or navigation properties
- [ ] All unit tests pass with updated entity construction
- [ ] Integration tests (`GoldenRules.cs`) pass with updated queries
- [ ] EF migrations generated and tested locally
- [ ] SQL scripts updated to remove references to dropped columns
- [ ] `dotnet build` passes for all affected projects
- [ ] `dotnet test` passes for all affected test projects
- [ ] Production data verification queries return expected counts

---

## 12. Review and Timing Decision (2026-03-24)

### 12.1 Cross-Agent Review

This plan was independently reviewed by GitHub Copilot CLI and Claude Code. Key findings and resolutions:

#### API Contest Entity (Copilot flagged as "critical")

**Finding:** API Contest has `SeasonWeek` (int) and `SeasonYear` (int) but NO `SeasonWeekId` FK. Plan assumed FK existed.

**Resolution:** Not critical. API Contest is **not EF-managed** — its `IEntityTypeConfiguration` is commented out, there is no `DbSet<Contest>` in `AppDataContext`. The API fetches contest data from Producer via HTTP clients (`IContestClientFactory`) and receives real-time updates via MassTransit consumers that forward to SignalR. No API database column to drop. Phase 2 scope for API Contest is about the data flow (DTOs/events), not a schema migration.

#### GroupSeasonExtensions.ToCanonicalModel (Copilot correct)

`ToCanonicalModel` maps only `Id` and `CreatedUtc` — does NOT map SeasonYear. `ConferenceSeasonDto` is currently empty. No changes needed to this method. Plan Section 5 Step 1b updated.

#### FranchiseSeasonMetric.Season (Copilot correct)

Verified: `FranchiseSeasonMetric.Season` is a plain `int` field, not a FK. `GetFranchiseSeasonMetricsBySeasonYearQueryHandler` needs no changes. Uncertainty note removed from plan.

#### PickemGroupMatchup Dual-Storage (Copilot valid detail)

Entity has both `SeasonWeekId` (Guid FK) and denormalized `SeasonYear`/`SeasonWeek` (int). Composite index `(GroupId, SeasonYear, SeasonWeek)` uses denormalized columns. Already accounted for in Phase 2 index replacement table, but noted as more involved than "straightforward."

#### EventBase Subclass Count

Actual count is ~30 (not ~24). Updated in Section 2.

#### Minor line number discrepancy

`FranchiseSeasonExtensions.ToCanonicalModel` starts at line 61, not 68. Updated in Section 5 Step 1b.

#### Items from Copilot review 12.9 that are non-issues

1. **Stored procedures/views** — None exist. This is EF Code First + Dapper raw SQL only.
2. **External dependencies** — No reporting tools or analytics pipelines query the database directly.
3. **Migration rollback** — Re-add column + backfill from FK chain (same pattern as the corruption fix scripts already written).
4. **API-Producer sync** — HTTP clients + MassTransit events. Verified above.
5. **Performance baselines** — Nice-to-have but not blocking. Dataset is small, all FKs indexed, Season table is tiny.

---

### 12.2 Timing Decision: Defer Execution

**Status: Plan complete. Execution deferred until after off-season historical sourcing.**

**Rationale:**

1. **Historical sourcing is in progress.** The Provider->Producer pipeline is actively ingesting years of historical data. This is the exact pipeline that would be disrupted by removing SeasonYear from 6 Producer entities, 8 processors, and 10+ handlers.

2. **The corruption risk is understood and contained.** Both incidents had the same root cause (processor setting SeasonYear from the wrong variable) and the same fix (JOIN to FK chain). The fix scripts exist and can be re-run. The GroupSeasonDocumentProcessor regression test (line 81-136 in tests) now guards against the specific bug.

3. **~150 files across ~90 non-test files is a large blast radius** for a system that is "mostly stable" and actively doing useful work. Introducing subtle query regressions during historical sourcing could corrupt or lose data that took hours to ingest.

4. **The off-season window is finite.** Time spent on this refactor is time not spent sourcing historical data. The denormalization removal will be just as valid (and safer) once sourcing is complete and the system is idle.

5. **No new corruption vectors are being introduced.** The pipeline code is stable; the risk is in the existing code, not in new features being added.

**When to execute:**
- After historical sourcing is complete
- Before the next regular season begins (when the pipeline will be under real-time load)
- Phase 1 (Producer) first, verify stability, then Phase 2 (API) in a separate PR cycle
