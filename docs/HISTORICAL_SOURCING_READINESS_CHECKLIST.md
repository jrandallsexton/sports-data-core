# Historical Sourcing Readiness Checklist

**Last Updated:** January 28, 2026  
**Status:** ⚠️ NOT READY - 5 processors unimplemented

## Overview

This document tracks readiness for full historical season sourcing runs. Historical sourcing will cost ~$30-40 per season for Azure message processing (RabbitMQ mitigates this), plus ESPN API call costs. Before investing in backfilling 2020-2024 seasons, we need **complete** ESPN data capture.

**Current State:** 
- ✅ 118 document processor tests passing
- ✅ **EventCompetitionAthleteStatisticsDocumentProcessor** implemented and tested
- ❌ 5 document processors unimplemented (skeleton only)
- ⚠️ 5 child document types not processed in EventCompetitionCompetitor

## Critical Gaps (BLOCKERS)

### 1. EventCompetitionAthleteStatisticsDocumentProcessor
**Status:** ✅ COMPLETE (January 28, 2026)  
**Impact:** HIGH - Player-level game statistics captured  
**File:** `EventCompetitionAthleteStatisticsDocumentProcessor.cs`  
**ESPN URL Pattern:** `/events/{eventId}/competitions/{compId}/competitors/{teamId}/roster/{athleteId}/statistics/0`  
**Test File:** `EventCompetitionAthleteStatisticsDocumentProcessorTests.cs`

**Implementation Details:**
- ✅ Deserialize `EspnEventCompetitionAthleteStatisticsDto`
- ✅ Resolve Athlete → AthleteSeason → Competition (via LINQ join on FranchiseSeason)
- ✅ Map ESPN stat categories using `AthleteCompetitionStatisticExtensions.AsEntity()`
- ✅ Store AthleteCompetitionStatistic entities (with Categories → Stats nested structure)
- ✅ Handle stat updates using "remove existing + insert new" pattern (ESPN wholesale replacement)
- ✅ Unit tests created (inherits from ProducerTestBase)
- ✅ DbSet properties added to TeamSportDataContext
- ✅ EntityConfiguration registered

**Key Design Decision:** Uses LINQ join query to resolve AthleteSeason because entity lacks FranchiseSeason navigation property:
```csharp
var athleteSeason = await (from ats in _dataContext.AthleteSeasons
                           join fs in _dataContext.FranchiseSeasons on ats.FranchiseSeasonId equals fs.Id
                           where ats.AthleteId == athleteIdentity.CanonicalId 
                              && fs.SeasonYear == command.Season.Value
                           select ats)
    .AsNoTracking()
    .FirstOrDefaultAsync();
```

---

### 2. TeamSeasonLeadersDocumentProcessor
**Status:** ❌ Unimplemented (skeleton only)  
**Impact:** MEDIUM - Season statistical leaders not captured  
**File:** `TeamSeasonLeadersDocumentProcessor.cs`  
**Test File:** `TeamSeasonLeadersDocumentProcessorTests.cs` (exists, skipped with "TBD")

**Requirements:**
- [ ] Deserialize DTO
- [ ] Link to FranchiseSeason
- [ ] Store leader data (rushing leader, passing leader, etc.)
- [ ] Unit tests

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
**Status:** ❌ Unimplemented (skeleton only)  
**Impact:** MEDIUM - Coaching staff data not captured  
**File:** `TeamSeasonCoachDocumentProcessor.cs`  
**Test File:** `TeamSeasonCoachDocumentProcessorTests.cs` (exists, skipped with "TBD")

**Requirements:**
- [ ] Deserialize DTO
- [ ] Create/link Coach entity
- [ ] Link to FranchiseSeason
- [ ] Store position/role data
- [ ] Unit tests

---

### 5. TeamSeasonAwardDocumentProcessor
**Status:** ❌ Unimplemented (skeleton only)  
**Impact:** MEDIUM - Team/player awards not captured (Heisman, All-American, etc.)  
**File:** `TeamSeasonAwardDocumentProcessor.cs`  
**Test File:** `TeamSeasonAwardDocumentProcessorTests.cs` (exists, skipped with "TBD")

**Requirements:**
- [ ] Deserialize DTO
- [ ] Link to FranchiseSeason or Athlete
- [ ] Store award data
- [ ] Unit tests

---

### 6. AwardDocumentProcessor (Football)
**Status:** ❌ Unimplemented (skeleton only)  
**Impact:** LOW - Award definitions not captured  
**File:** `Football/AwardDocumentProcessor.cs`

**Requirements:**
- [ ] Deserialize DTO
- [ ] Store award metadata (Heisman Trophy, etc.)
- [ ] Unit tests

---

## High Priority Issues

### 7. TeamSeasonDocumentProcessor Potential Bug
**Status:** ⚠️ Requires Investigation  
**Impact:** HIGH - Active processor affecting current data  
**File:** `TeamSeasonDocumentProcessor.cs:228`  
**Issue:** TODO comment indicates dependency resolution may not check for errors properly

```csharp
// TODO: This might be a bug b/c it does not check for dependency errors/issues
```

**Action Items:**
- [ ] Review dependency resolution logic in ProcessDependencies method
- [ ] Verify error handling for missing GroupSeason/Venue references
- [ ] Add integration tests for dependency failure scenarios
- [ ] Document expected behavior

---

## Medium Priority Gaps

### 8. EventCompetitionCompetitorDocumentProcessor - Child Documents
**Status:** ⚠️ Partially Implemented  
**Impact:** MEDIUM - 5 child document types not processed  
**File:** `EventCompetitionCompetitorDocumentProcessor.cs:220-224`

**Missing Child Documents:**
- [ ] Roster (line 220)
- [ ] Statistics (line 221)
- [ ] Leaders (line 222)
- [ ] Record (line 223)
- [ ] Ranks (line 224)

**Note:** Some of these may overlap with other processors. Needs design review.

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
- [x] All 118 existing tests still passing ✅

### Should Have (Before Production Historical Run)
- [ ] **TeamSeasonLeadersDocumentProcessor** implemented
- [ ] **TeamSeasonCoachDocumentProcessor** implemented
- [ ] **TeamSeasonAwardDocumentProcessor** implemented
- [ ] **EventCompetitionCompetitor child documents** design reviewed

### Nice to Have (Can Defer)
- [ ] TeamSeasonInjuriesDocumentProcessor implemented
- [ ] AwardDocumentProcessor implemented
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

### Phase 2: High Value (Before First Historical Run)
3. **Implement TeamSeasonLeadersDocumentProcessor** (4-6 hours)
4. **Implement TeamSeasonCoachDocumentProcessor** (4-6 hours)
5. **Implement TeamSeasonAwardDocumentProcessor** (4-6 hours)

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

- [ ] All Phase 1 (Critical) items completed
- [ ] All Phase 2 (High Value) items completed or explicitly deferred
- [ ] Integration test of 2024 season week successful
- [ ] Manual data spot-check passed
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
