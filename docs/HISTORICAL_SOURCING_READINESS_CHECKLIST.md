# Historical Sourcing Readiness Checklist

**Last Updated:** February 8, 2026

**Status:** ✅ **READY FOR EXECUTION** - MongoDB migration complete, all blockers resolved

## Overview

This document tracks readiness for full historical season sourcing runs. Before investing in backfilling 2020-2024 seasons, we need **complete** ESPN data capture and infrastructure ready for bulk ingestion.

**Current State (February 8, 2026):**

- ✅ **MongoDB Migration COMPLETE** - Bare metal deployment on 192.168.0.250
- ✅ **Collection Structure Fixed** - DocumentType collections in sport-specific databases
- ✅ **Read/Write Path Bug Fixed** - Documents correctly written and retrieved
- ✅ **Production Validated** - Smoke tests passed, Compass verification complete
- ✅ 240+ document processor tests passing
- ✅ **EventCompetitionAthleteStatisticsDocumentProcessor** implemented and tested
- ✅ **TeamSeasonLeadersDocumentProcessor** implemented and tested (3 tests passing)
- ✅ **TeamSeasonAwardDocumentProcessor** implemented and tested (3 tests passing)
- ✅ **TeamSeasonCoachDocumentProcessor** implemented and tested (3 tests passing)
- ✅ **EventCompetitionCompetitorRosterDocumentProcessor** - persists roster data (6 tests passing)
- ❌ 2 document processors unimplemented (TeamSeasonInjuries, TeamSeasonProjection - LOW priority)

**Infrastructure Status:**
- ✅ **MongoDB**: 900 GB storage (809 GB free), SCRAM-SHA-256 auth, production-ready
- ✅ **Database Pattern**: Sport-specific databases (FootballNcaa) with DocumentType collections
- ✅ **Cost Savings**: ~$600-2,400/year (Cosmos DB eliminated)
- ✅ **No Throttling**: Unlimited throughput for bulk historical import
- 🔄 **Cosmos DB Deletion**: Ready to execute (validated, pending cleanup)

## Critical Gaps (BLOCKERS)

### 1. EventCompetitionAthleteStatisticsDocumentProcessor

**Status:** ✅ COMPLETE (January 28, 2026)

**Impact:** HIGH - Player-level game statistics captured

**File:** `EventCompetitionAthleteStatisticsDocumentProcessor.cs`

**ESPN URL Pattern:** `/events/{eventId}/competitions/{compId}/competitors/{teamId}/roster/{athleteId}/statistics/0`

**Test File:** `EventCompetitionAthleteStatisticsDocumentProcessorTests.cs`

**Implementation Details:**

- ✅ Deserialize `EspnEventCompetitionAthleteStatisticsDto`
- ✅ Resolve AthleteSeason directly from `dto.Athlete.Ref` using canonical ID lookup
- ✅ Resolve Competition from `dto.Competition.Ref` using canonical ID lookup
- ✅ Map ESPN stat categories using `AthleteCompetitionStatisticExtensions.AsEntity()`
- ✅ Store AthleteCompetitionStatistic entities (with Categories → Stats nested structure)
- ✅ Handle stat updates using "remove existing + insert new" pattern (ESPN wholesale replacement)
- ✅ Unit tests created (inherits from ProducerTestBase)
- ✅ DbSet properties added to TeamSportDataContext
- ✅ EntityConfiguration registered

**Key Design Decision:** Uses direct canonical ID lookup for AthleteSeason resolution:

```csharp
// Resolve AthleteSeason directly from dto.Athlete.Ref
var athleteSeasonIdentity = _externalRefIdentityGenerator.Generate(dto.Athlete.Ref);

var athleteSeason = await _dataContext.AthleteSeasons
    .Where(x => x.Id == athleteSeasonIdentity.CanonicalId)
    .FirstOrDefaultAsync();
```

This approach leverages ESPN's 1:1 mapping between athlete season refs and canonical IDs, avoiding complex joins.

---

### 2. TeamSeasonLeadersDocumentProcessor
**Status:** ✅ COMPLETE (February 1, 2026)  
**Impact:** MEDIUM - Season statistical leaders captured  
**File:** `TeamSeasonLeadersDocumentProcessor.cs`  
**Test File:** `TeamSeasonLeadersDocumentProcessorTests.cs` (3 tests passing)

**Implementation Details:**
- ✅ Deserialize `EspnLeadersDto`
- ✅ Link to FranchiseSeason
- ✅ Store leader data with wholesale replacement pattern
- ✅ Handle isNew flag to prevent child document re-spawning
- ✅ Preflight dependency resolution
- ✅ Category auto-creation with race condition handling
- ✅ Comprehensive null guards for malformed ESPN data
- ✅ Unit tests (3 passing)

---

### 3. TeamSeasonInjuriesDocumentProcessor
**Status:** ❌ Unimplemented (skeleton only)  
**Impact:** LOW - Injury reports not captured (nice-to-have)  
**File:** `TeamSeasonInjuriesDocumentProcessor.cs`  
**Test File:** `TeamSeasonInjuriesDocumentProcessorTests.cs` (exists, skipped with "TBD")

**Requirements:**
- [ ] Deserialize DTO
- [ ] Link to FranchiseSeason and Athletes
- [ ] Store injury data
- [ ] Unit tests

**Note:** May not be worth implementing if data freshness is poor

---

### 4. TeamSeasonCoachDocumentProcessor
**Status:** ✅ Complete  
**Impact:** MEDIUM - Coaching staff data captured via wholesale replacement  
**File:** `TeamSeasonCoachDocumentProcessor.cs`  
**Test File:** `TeamSeasonCoachDocumentProcessorTests.cs`  
**Estimated Effort:** 4-6 hours  
**Actual Effort:** ~3 hours

**Implementation Details:**
- Processes ESPN resource index of coaches for a team season
- Wholesale replacement pattern: deletes existing CoachSeason entries, spawns child documents
- No inline processing - spawns DocumentType.CoachSeason for each coach ref
- CoachBySeasonDocumentProcessor handles individual coach season data
- Uses `CausationId.Producer.TeamSeasonCoachDocumentProcessor`

**Files Modified:**
- ✅ `TeamSeasonCoachDocumentProcessor.cs` - wholesale replacement implementation
- ✅ `TeamSeasonCoachDocumentProcessorTests.cs` - 3 passing tests
- ✅ `CausationId.cs` - added TeamSeasonCoachDocumentProcessor GUID

**Unit Tests:**
- ✅ `ProcessAsync_DeletesExistingCoachSeasons_WhenProcessingResourceIndex` - validates wholesale replacement
- ✅ `ProcessAsync_SpawnsChildDocuments_WhenResourceIndexContainsCoaches` - validates child document spawning
- ✅ `ProcessAsync_ReplacesExistingCoachSeasons_WhenProcessedTwice` - validates idempotency

**Key Pattern:** Simple resource index processor - deserializes `EspnResourceIndexDto`, deletes existing `CoachSeason` entries for FranchiseSeason, spawns child `DocumentType.CoachSeason` documents. No inline data processing.

---

### 5. TeamSeasonAwardDocumentProcessor
**Status:** ✅ COMPLETE (February 1, 2026)  
**Impact:** MEDIUM - Team/player awards captured (Heisman, All-American, etc.)  
**File:** `TeamSeasonAwardDocumentProcessor.cs`  
**Test File:** `TeamSeasonAwardDocumentProcessorTests.cs` (3 tests passing)

**Implementation Details:**
- ✅ Deserialize `EspnAwardDto`
- ✅ Link to FranchiseSeason and AthleteSeason
- ✅ Store Award (normalized definition) + FranchiseSeasonAward (season instance) + FranchiseSeasonAwardWinner entities
- ✅ Uses FranchiseSeasonAwardExtensions for pre-computed canonical IDs
- ✅ Unit tests (3 passing)

---

### 6. AwardDocumentProcessor (Football)
**Status:** ⚠️ Skeleton Only - NOT NEEDED  
**Impact:** NONE - Awards handled by TeamSeasonAwardDocumentProcessor  
**File:** `Football/AwardDocumentProcessor.cs`

**Reason Not Implemented:**
TeamSeasonAwardDocumentProcessor (section 5) already handles award processing by:
- Creating Award entities (normalized definitions) from season-specific award URLs
- Converting season-specific URLs to canonical award URLs
- Storing FranchiseSeasonAward (season instance) and FranchiseSeasonAwardWinner entities

This standalone processor would be redundant. The skeleton can remain for potential future use if ESPN exposes a separate award definition endpoint.

---

## High Priority Issues

### 7. TeamSeasonDocumentProcessor Potential Bug

**Status:** ✅ RESOLVED  
**Impact:** N/A - Misleading TODO comment removed  
**File:** `TeamSeasonDocumentProcessor.cs`

**Resolution:**
The TODO comment "This might be a bug b/c it does not check for dependency errors/issues" has been removed. Code review confirms dependency resolution is working correctly:
- `ProcessDependencies` method properly validates GroupSeason and Venue dependencies
- Throws `ExternalDocumentNotSourcedException` when dependencies aren't ready
- Proper error handling with retry logic via DocumentCreated event

No action required - processor is working as designed.

---

## Medium Priority Gaps

### 8. EventCompetitionCompetitorRosterDocumentProcessor - Roster Data NOT Persisted

**Status:** ✅ **COMPLETE (February 1, 2026)**  
**Impact:** HIGH - Game roster data captured, enables Games Played calculation  
**File:** `EventCompetitionCompetitorRosterDocumentProcessor.cs`
**Test File:** `EventCompetitionCompetitorRosterDocumentProcessorTests.cs` (6 tests passing)
**Estimated Effort:** 6-8 hours  
**Actual Effort:** ~4 hours

**Implementation Details:**
- ✅ Created `AthleteCompetition` entity with composite unique index (CompetitionId, AthleteSeasonId)
- ✅ Wholesale replacement pattern implemented (delete existing + insert new per competition)
- ✅ Position resolution via `_externalRefIdentityGenerator.Generate(entry.Position.Ref).CanonicalId`
- ✅ Jersey number, DidNotPlay flag persisted
- ✅ Gracefully handles missing AthleteSeasons (skips with debug log)
- ✅ Still spawns child documents for EventCompetitionAthleteStatistics
- ✅ Migration generated: 20260201100623_01FebV2_AthleteCompetition
- ✅ DbSet and EntityConfiguration registered in TeamSportDataContext

**Unit Tests:**
- ✅ `WhenJsonIsValid_DtoDeserializes` - validates ESPN data structure
- ✅ `WhenProcessingRoster_PublishesChildDocumentRequestsForAthleteStatistics` - ensures stats spawning still works (39 requests)
- ✅ `WhenProcessingRoster_PersistsAthleteCompetitionEntries` - validates roster entry creation
- ✅ `WhenProcessingRosterTwice_ReplacesExistingEntries` - validates wholesale replacement idempotency
- ✅ `WhenRosterEntryHasJerseyNumber_PersistsJerseyNumber` - validates jersey number mapping
- ✅ `WhenAthleteDidNotPlay_PersistsDidNotPlayFlag` - validates DidNotPlay flag handling

**AthleteCompetition Entity:**

```csharp
public class AthleteCompetition : CanonicalEntityBase<Guid>
{
    public Guid CompetitionId { get; set; }
    public Guid AthleteSeasonId { get; set; }
    public Guid? PositionId { get; set; }  // FK via ESPN ref canonical ID
    public string? JerseyNumber { get; set; }
    public bool DidNotPlay { get; set; }
    
    // Navigation properties
    public Competition Competition { get; set; }
    public AthleteSeason AthleteSeason { get; set; }
    public AthletePosition? Position { get; set; }
}
```

**Key Pattern:** Processor now persists roster entries BEFORE spawning child documents, enabling Games Played calculation via:

```sql
SELECT COUNT(DISTINCT CompetitionId) 
FROM AthleteCompetition 
WHERE AthleteSeasonId = @athleteSeasonId AND DidNotPlay = false
```

---

### 9. TeamSeasonDocumentProcessor - Season Notes
**Status:** ⚠️ Data Availability Issue  
**Impact:** LOW  
**File:** `TeamSeasonDocumentProcessor.cs:300`

```csharp
// TODO: MED: Request sourcing of team season notes (data not available when following link)
```

**Action Items:**
- [ ] Verify if ESPN provides season notes data
- [ ] Implement if available, document if not

---

### 10. FranchiseDocumentProcessor - Missing Venue Handling
**Status:** ⚠️ Error Handling Gap  
**Impact:** MEDIUM  
**File:** `FranchiseDocumentProcessor.cs:151`

```csharp
// TODO: What to do if the venue does not exist?
```

**Action Items:**
- [ ] Define behavior when venue reference is missing
- [ ] Either request venue document or log warning
- [ ] Add test coverage for missing venue scenario

---

### 11. StandingsDocumentProcessor
**Status:** ❌ Unimplemented (skeleton only)  
**Impact:** LOW - Conference standings not captured  
**File:** `Football/StandingsDocumentProcessor.cs`

**Requirements:**
- [ ] Deserialize DTO
- [ ] Store standings data
- [ ] Link to GroupSeason
- [ ] Unit tests

---

## Test Coverage Status

### Passing Tests
- ✅ 118 document processor tests passing
- ✅ All core processors (Season, Event, Competition, etc.) well-tested

### Skipped Tests (12 total)

| Test | Reason | Action Required |
|------|--------|-----------------|
| `TeamSeasonAwardDocumentProcessorTests.NotYetImplemented_Fails` | TBD | Implement processor |
| `TeamSeasonProjectionDocumentProcessorTests.NotYetImplemented_Fails` | TBD | Implement processor |
| `TeamSeasonInjuriesDocumentProcessorTests.NotYetImplemented_Fails` | TBD | Implement processor |
| `TeamSeasonLeadersDocumentProcessorTests.NotYetImplemented_Fails` | TBD | Implement processor |
| `TeamSeasonCoachDocumentProcessorTests.NotYetImplemented_Fails` | TBD | Implement processor |
| `GroupSeasonDocumentProcessorTests.WhenGroupExistsAndSeasonIsNew_Sec2025_IsAppendedToExistingGroup` | Revisit | Review behavior |
| `EventCompetitionProbabilityDocumentProcessorTests.WhenValuesHaveNotChanged_SecondCallIsSkipped` | TODO | Implement optimization |
| `EventCompetitionPlayDocumentProcessorTests.WhenEntityExists_ShouldUpdateExistingPlay` | Updates not yet implemented | Implement updates |
| `EventCompetitionPlayDocumentProcessorTests.WhenParentIdMissing_ThrowsException` | log not throw | Fix error handling |
| `EventCompetitionPlayDocumentProcessorTests.WhenSeasonMissing_ThrowsException` | log not throw | Fix error handling |
| `EventCompetitionPlayDocumentProcessorTests.WhenParentIdInvalid_ThrowsException` | log not throw | Fix error handling |
| `EventCompetitionDriveDocumentProcessorTests.WhenStartTeamNotFound_ShouldThrowException` | No longer valid | Remove or rewrite |

---

## Historical Sourcing Readiness Criteria

### Must Have (Blockers)
- [x] **EventCompetitionAthleteStatisticsDocumentProcessor** implemented and tested ✅ (January 28, 2026)
- [x] **TeamSeasonDocumentProcessor bug** investigated - no bug exists (misleading comment) ✅ (January 28, 2026)
- [x] All existing tests still passing ✅ (240+ tests passing)
- [x] **EventCompetitionCompetitorRosterDocumentProcessor** - roster data persistence complete ✅ (February 1, 2026)

### Should Have (Before Production Historical Run)

- [x] **TeamSeasonLeadersDocumentProcessor** implemented ✅ (February 1, 2026)
- [x] **TeamSeasonCoachDocumentProcessor** implemented ✅ (January 2026)
- [x] **TeamSeasonAwardDocumentProcessor** implemented ✅ (February 1, 2026)
- [ ] **EventCompetitionCompetitor child documents** design reviewed (DEFERRED - can implement post-historical run if needed)

### Nice to Have (Can Defer)

- [ ] TeamSeasonInjuriesDocumentProcessor implemented
- [ ] ~~AwardDocumentProcessor~~ (NOT NEEDED - redundant with TeamSeasonAwardDocumentProcessor)
- [ ] StandingsDocumentProcessor implemented
- [ ] TeamSeasonProjectionDocumentProcessor implemented

---

## Decision Points

### 1. ESPN Data Availability Verification

Before implementing processors, verify ESPN actually provides this data for historical seasons:

- [ ] Check sample 2020-2024 season URLs for athlete statistics
- [ ] Check for team season leaders data
- [ ] Check for coach/award data availability
- [ ] Document any data gaps in ESPN's historical archive

### 2. Cost-Benefit Analysis

- **EventCompetitionAthleteStatistics**: HIGH value - game stats are critical
- **TeamSeasonLeaders**: MEDIUM value - enhances season summaries
- **TeamSeasonCoach**: MEDIUM value - useful context
- **TeamSeasonAward**: MEDIUM value - prestige data
- **TeamSeasonInjuries**: LOW value - often stale/unreliable
- **Standings**: LOW value - can derive from game results

---

## Implementation Priority

### Phase 1: Critical (Required for Historical Sourcing) ✅ COMPLETE

1. **Investigate TeamSeasonDocumentProcessor bug** ✅ (2 hours actual)
   - No bug found - misleading TODO comment removed
2. **Implement EventCompetitionAthleteStatisticsDocumentProcessor** ✅ (8 hours actual)
   - Implemented using "remove + replace" pattern from AthleteSeasonStatisticsDocumentProcessor
   - LINQ join query to resolve AthleteSeason via FranchiseSeason
   - Extension methods already existed in AthleteCompetitionStatisticExtensions
   - DbSet properties added to TeamSportDataContext
   - Test stub created, inherits from ProducerTestBase
   - Build succeeds, all 118 document processor tests pass

### Phase 2: High Value (Before First Historical Run) ✅ COMPLETE

1. **Implement TeamSeasonLeadersDocumentProcessor** ✅ COMPLETE (actual: ~6 hours)
   - Implemented with wholesale replacement pattern
   - Preflight dependency resolution to prevent data loss
   - Category auto-creation with race condition handling
   - Comprehensive null guards for malformed ESPN data
   - All 3 unit tests passing
2. ✅ **Complete: TeamSeasonCoachDocumentProcessor** (~3 hours, 3 passing tests)
3. **Implement TeamSeasonAwardDocumentProcessor** ✅ COMPLETE (actual: ~5 hours)
   - Uses FranchiseSeasonAwardExtensions for pre-computed canonical IDs
   - Wholesale replacement pattern
   - All 3 unit tests passing
4. **Refactor child document spawning pattern across all processors** ✅ COMPLETE (actual: ~4 hours)
   - Applied conditional spawn pattern using `ShouldSpawn(DocumentType, command)`
   - Prevents duplicate child document requests when processor re-runs
   - Implemented across EventCompetitionDocumentProcessor, TeamSeasonLeadersDocumentProcessor, TeamSeasonDocumentProcessor
   - Critical for historical sourcing to avoid exponential duplicate spawns

### Phase 3: Optional (Can Run Historical Sourcing Without)
6. TeamSeasonInjuriesDocumentProcessor
7. AwardDocumentProcessor
8. StandingsDocumentProcessor
9. EventCompetitionCompetitor child document review

---

## Validation Plan

Before executing first historical sourcing run (2024 season):

1. **Unit Test Coverage**
   - [ ] All new processors have >80% code coverage
   - [ ] Edge cases covered (missing dependencies, malformed JSON)

2. **Integration Testing**
   - [ ] Process sample 2024 season week in DEV environment
   - [ ] Verify all document types processed successfully
   - [ ] Check database for expected entities (ContestParticipantStatistics, etc.)

3. **Sample Data Verification**
   - [ ] Manually verify a sample game's athlete statistics match ESPN website
   - [ ] Verify team season leaders data accuracy
   - [ ] Check for missing/null fields

4. **Performance Testing**
   - [ ] Monitor processing time for new processors
   - [ ] Check for N+1 query issues
   - [ ] Verify RabbitMQ message throughput

---

## Historical Sourcing Execution Plan (Post-Readiness)

### First Run: 2024 Season (Validation)
- **Purpose:** Verify all processors work correctly before investing in full backfill
- **Cost:** ~$30-40 for message processing
- **ESPN API calls:** ~13,260 (1 season + 130 venues + 130 team seasons + ~13,000 athlete seasons)
- **Timeline:** 4-6 hours with tier delays (0/30/60/240 minutes)

### Full Backfill: 2020-2024 (5 seasons)
- **Purpose:** Complete historical data capture
- **Cost:** ~$150-200 for message processing
- **ESPN API calls:** ~66,300 total
- **Timeline:** 20-30 hours (can parallelize seasons)

---

## Sign-Off Checklist

Before authorizing first historical sourcing run:

- [x] All Phase 1 (Critical) items completed
- [x] All Phase 2 (High Value) items completed
- [x] **MongoDB migration completed** - Bare metal on 192.168.0.250
- [x] **900 GB storage available** - Ready for massive data import
- [x] **Production validation passed** - Smoke tests and Compass verification
- [x] **Read/write path bug fixed** - Collection naming aligned
- [x] **Migration baseline squash completed** - Single baseline migration
- [x] **Cosmos DB deleted** - $600-2,400/year savings realized
- [ ] Integration test of 2024 season week successful (NEXT STEP)
- [ ] Manual data spot-check passed (AFTER integration test)

---

## Next Steps to Execute

### 1. Integration Test (2024 Season Week) ⏭️ NEXT
**Purpose**: Validate all processors work correctly before full backfill

**Scope**: Single week of 2024 season (10-20 games)

**Cost**: Minimal (test run)

**Timeline**: 30-60 minutes

**Validation**: Check for EventCompetitionAthleteStatistics, TeamSeasonLeaders, etc. in database

### 2. First Full Season Run (2024)
**Purpose**: Complete validation before investing in multi-season backfill

**Cost**: ~$30-40 for message processing (RabbitMQ mitigates)

**ESPN API calls**: ~13,260 total

**Timeline**: 4-6 hours with tier delays (0/30/60/240 minutes)

**MongoDB Impact**: ~10-50 GB data growth (depends on ESPN response sizes)

### 3. Full Backfill (2020-2024)
**Purpose**: Complete historical data capture

**Seasons**: 5 seasons total

**Cost**: ~$150-200 for message processing

**ESPN API calls**: ~66,300 total

**Timeline**: 20-30 hours (can parallelize seasons)

**MongoDB Impact**: 50-250 GB data growth

**Storage Headroom**: 809 GB free → 559-759 GB after backfill (plenty of room)

---

## Completed Infrastructure Milestones ✅

- ✅ **MongoDB Migration** (Feb 7-8, 2026) - Bare metal on 192.168.0.250
- ✅ **Storage Expansion** - 900 GB across all NUCs
- ✅ **Collection Structure Fix** - DocumentType pattern implemented
- ✅ **Read/Write Path Bug Fix** - Collection naming aligned
- ✅ **Production Validation** - Smoke tests passed
- ✅ **Migration Baseline Squash** - 54 migrations → 1 baseline
- ✅ **Cosmos DB Deletion** - $600-2,400/year savings realized
- ✅ **All Critical Processors** - 240+ tests passing

---

## Pre-Historical Sourcing: Migration Baseline Reset

**Status:** ✅ **READY TO EXECUTE** (February 2, 2026)

**Impact:** HIGH - Improves pod startup performance for KEDA autoscaling during historical runs

**Current State:**
- **54 migrations** spanning August 2025 - February 2026 (Initial → 01FebV3)
- EF Core processes all migrations on every pod startup
- Startup overhead: ~100-500ms per pod
- **With KEDA scaling to 20-50 pods, this compounds significantly**

**⚠️ CRITICAL SAFETY RULES:**
1. **NEVER delete migrations before creating baseline** - EF needs them to generate proper baseline
2. **Test on local database FIRST** - Verify baseline works before touching production
3. **Backup production database** - Safety net for rollback
4. **Verify schema match** - Local and production must be identical before squash

---

### CORRECTED PROCEDURE (Safe Order)

### Step 1: Backup Production Database
```powershell
# On Bender or local machine with kubectl access
cd C:\Projects\sports-data-provision\util

# Create timestamped backup
.\21_CopyProdToLocal.ps1 -Force
# Backup saved to d:\sdprod-backups\
```

### Step 2: Verify Production Migration State
```sql
-- Connect to PRODUCTION database
SELECT COUNT(*) FROM "__EFMigrationsHistory";
-- Expected: 54 migrations

-- Verify last migration
SELECT "MigrationId" FROM "__EFMigrationsHistory" 
ORDER BY "MigrationId" DESC LIMIT 1;
-- Expected: 20260201105014_01FebV3_AthleteCompetitionCompetitorFK
```

### Step 3: Verify Local Database Matches Production
```powershell
# Check local database has same migrations
cd C:\Projects\sports-data\src\SportsData.Producer

# This should show 54 migrations applied
dotnet ef migrations list --context FootballDataContext
```

### Step 4: Create Baseline Migration (WHILE old migrations still exist)
```powershell
# Still in Producer directory
# DO NOT delete migrations yet - EF needs them to create baseline!

dotnet ef migrations add 02FebV1_Baseline --context FootballDataContext

# This generates:
# - Migrations\20260202XXXXXX_02FebV1_Baseline.cs
# - Migrations\20260202XXXXXX_02FebV1_Baseline.Designer.cs
# - Updates TeamSportDataContextModelSnapshot.cs
```

**IMPORTANT**: Note the exact timestamp in the generated filename (e.g., `20260202103045`)

### Step 5: Test Baseline on LOCAL Database First
```powershell
# Option A: Test with fresh local database
cd C:\Projects\sports-data\src\SportsData.Producer

# Drop local database
dotnet ef database drop --context FootballDataContext --force

# Apply ONLY the new baseline
dotnet ef database update --context FootballDataContext

# Verify schema looks correct (check a few key tables exist)
```

```sql
-- Connect to LOCAL database
SELECT table_name FROM information_schema.tables 
WHERE table_schema = 'public' 
ORDER BY table_name;

-- Should see: AthleteSeason, Competition, Franchise, etc.
-- Verify AthleteCompetition exists (from latest migration)
SELECT column_name FROM information_schema.columns 
WHERE table_name = 'AthleteCompetition';
```

### Step 6: Run Tests Against Local Baseline
```powershell
# Build solution
dotnet build C:\Projects\sports-data\sports-data.sln

# Run unit tests (should all pass with baseline)
dotnet test C:\Projects\sports-data\test\unit\SportsData.Producer.Tests.Unit\SportsData.Producer.Tests.Unit.csproj

# Expected: 240+ tests passing
```

### Step 7: Delete Old Migration Files (AFTER baseline tested)
```powershell
# Navigate to Producer project
cd C:\Projects\sports-data\src\SportsData.Producer\Migrations

# BACKUP: Copy old migrations to archive folder first
New-Item -Path ".\archive_pre_squash" -ItemType Directory -Force
Copy-Item "20*.cs" ".\archive_pre_squash\" -Exclude "20260202*"
Copy-Item "20*.Designer.cs" ".\archive_pre_squash\" -Exclude "20260202*"

# Delete old migration files (NOT the new baseline!)
Remove-Item "2025*.cs"
Remove-Item "2025*.Designer.cs"
Remove-Item "202601*.cs" -Exclude "20260202*"
Remove-Item "202601*.Designer.cs" -Exclude "20260202*"
Remove-Item "20260201*.cs"
Remove-Item "20260201*.Designer.cs"

# Verify only baseline remains
Get-ChildItem "20*.cs"
# Should show ONLY: 20260202XXXXXX_02FebV1_Baseline.cs and .Designer.cs
```

### Step 8: Update Production Migration History
```sql
-- Connect to PRODUCTION database
-- ⚠️ POINT OF NO RETURN - Make sure backup exists!

BEGIN;

-- Clear old migration history
DELETE FROM "__EFMigrationsHistory";

-- Add the new baseline (replace XXXXXX with actual timestamp from Step 4)
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") 
VALUES ('20260202XXXXXX_02FebV1_Baseline', '10.0.2');

-- Verify
SELECT * FROM "__EFMigrationsHistory";
-- Should show ONLY the baseline migration

COMMIT;
```

### Step 9: Update Local Development Database
```sql
-- Connect to LOCAL database
-- Apply same migration history update

BEGIN;

DELETE FROM "__EFMigrationsHistory";

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") 
VALUES ('20260202XXXXXX_02FebV1_Baseline', '10.0.2');

COMMIT;
```

### Step 10: Verify Baseline Works in Both Environments
```powershell
# Test local
cd C:\Projects\sports-data\src\SportsData.Producer
dotnet ef migrations list --context FootballDataContext
# Should show: 20260202XXXXXX_02FebV1_Baseline (Applied)

# Run tests again
dotnet test C:\Projects\sports-data\test\unit\SportsData.Producer.Tests.Unit\SportsData.Producer.Tests.Unit.csproj
# Expected: 240+ tests passing
```

### Step 11: Commit and Deploy to Production
```powershell
cd C:\Projects\sports-data

git status
# Should show:
# - Deleted: 53 old migration files (2025*, 202601*, 20260201*)
# - Added: 1 new baseline migration (20260202*)
# - Modified: TeamSportDataContextModelSnapshot.cs

git add .
git commit -m "chore: squash 54 migrations to baseline for historical sourcing performance

- Reduces pod startup time by ~100-500ms per pod
- Critical for KEDA autoscaling (20-50 concurrent pods)
- Production migration history manually updated
- Baseline tested on local database before deployment"

git push

# Deploy via Azure DevOps pipeline
# Pods will restart with new baseline, no schema changes
```

### Step 12: Monitor Production Deployment
```powershell
# Watch pods restart
kubectl get pods -n default -l app=producer-football-ncaa -w

# Check pod logs for migration application
kubectl logs -n default -l app=producer-football-ncaa --tail=50 | grep -i migration

# Should see: "Applying migration '20260202XXXXXX_02FebV1_Baseline'"
# Should NOT see errors
```

---

### Rollback Plan

**If baseline migration fails in production:**

```powershell
# 1. Restore old migrations from git
git revert <commit-hash>
git push

# 2. Restore production migration history from backup
# (Backup taken in Step 1 contains __EFMigrationsHistory data)
```

**If you need to restore production database entirely:**
```sql
-- Use backup from Step 1 (d:\sdprod-backups\)
-- Standard PostgreSQL restore process
```

---

### Benefits of Squash

- ✅ **Faster pod startup**: 100-500ms saved per pod (54 migrations → 1)
- ✅ **Reduced memory**: Single baseline vs 54 migrations
- ✅ **Cleaner logs**: Less EF Core migration scanning
- ✅ **Better KEDA scaling**: Critical for 20-50 concurrent pods during historical runs
- ✅ **Easier debugging**: Simpler to reason about schema state
- ✅ **Smaller Docker images**: Fewer migration files in container

---

### Post-Squash Verification Checklist

- [ ] Production migration history shows only baseline
- [ ] Local migration history shows only baseline  
- [ ] All 240+ unit tests passing
- [ ] Pods restart successfully in production
- [ ] No migration errors in pod logs
- [ ] Sample document processing works (smoke test)
- [ ] Old migrations archived in `Migrations/archive_pre_squash/`
- [ ] Git history contains squash commit

---
- [ ] RabbitMQ cluster stable and scaled appropriately
- [ ] Monitoring/alerting in place (Seq, Grafana)
- [ ] Budget approved for full backfill
- [ ] Rollback plan documented (if data quality issues found)

---

## References

- [Historical Season Sourcing Design](./HISTORICAL_SEASON_SOURCING.md)
- [Historical Sourcing Code Review](./HISTORICAL_SOURCING_CODE_REVIEW_BRIEF.md)
- [Load Test Results](../README.md) - 3.2M jobs processed successfully

---

**Next Steps:**
1. Prioritize Phase 1 work (bug investigation + athlete stats processor)
2. Schedule implementation sprint
3. Re-evaluate after Phase 1 completion

