# AthleteSeasonDocumentProcessorTests - Comprehensive Update

**Date**: December 5, 2025  
**Task**: Update tests to match significant changes in `AthleteSeasonDocumentProcessor`

---

## ?? **Changes Summary**

### **Processor Changes Addressed**

The `AthleteSeasonDocumentProcessor` underwent major refactoring to support:
1. **Update logic** for existing `AthleteSeason` entities (not just create)
2. **Headshot image processing** via `ProcessImageRequest` events
3. **Statistics request** via `DocumentRequested` events
4. **Retry logic** with `ExternalDocumentNotSourcedException` handling
5. **Complete property mapping** on updates (all fields updated, not just select ones)

---

## ? **Test Updates**

### **Tests Added/Enhanced** (9 Total):

1. **`ProcessAsync_CreatesAthleteSeason_WhenAllDependenciesExist()`**
   - Validates full entity creation with all properties
   - Verifies `ProcessImageRequest` published (headshot)
   - Verifies `DocumentRequested` published (statistics)
   - Confirms database persistence

2. **`ProcessAsync_UpdatesAthleteSeason_WhenEntityAlreadyExists()`**
   - NEW: Tests idempotency/reprocessing scenario
   - Validates all properties updated (DisplayName, Jersey, Experience, etc.)
   - Confirms `ModifiedUtc` timestamp updated
   - Verifies events republished on update
   - Ensures no duplicate entities created

3. **`ProcessAsync_RequestsAthlete_WhenAthleteNotFound()`**
   - Tests dependency resolution failure
   - Validates `DocumentRequested` event published for `Athlete`
   - Confirms retry mechanism (`DocumentCreated` with incremented `AttemptCount`)
   - No entities created when dependency missing

4. **`ProcessAsync_RequestsFranchiseSeason_WhenTeamNotFound()`**
   - Tests missing `FranchiseSeason` (Team) dependency
   - Validates `DocumentRequested` event for `TeamSeason`
   - Confirms retry logic triggered

5. **`ProcessAsync_RequestsPosition_WhenPositionNotFound()`**
   - Tests missing `AthletePosition` dependency
   - Validates `DocumentRequested` event for `AthletePosition`
   - Confirms retry logic triggered

6. **`ProcessAsync_DoesNothing_WhenDocumentIsNull()`**
   - Edge case: null JSON document
   - No entities created, no events published

7. **`ProcessAsync_DoesNothing_WhenRefIsNull()`**
   - Edge case: DTO missing `$ref` property
   - Graceful handling without crashes

8. **`ProcessAsync_PublishesImageRequest_WhenHeadshotExists()`**
   - Focused test on headshot processing
   - Validates `ProcessImageRequest` with correct parameters
   - Confirms `Url`, `ParentEntityId`, `Sport`, `DocumentType` all correct

9. **`ProcessAsync_PublishesStatisticsRequest_WhenStatisticsRefExists()`**
   - Focused test on statistics processing
   - Validates `DocumentRequested` with correct parameters
   - Confirms `ParentId` matches `AthleteSeason.Id`

---

## ?? **Test Coverage Improvements**

| Scenario | Before | After |
|----------|--------|-------|
| **Happy Path (Create)** | ? Basic | ? Comprehensive |
| **Update/Reprocessing** | ? None | ? Full coverage |
| **Headshot Processing** | ? None | ? Full coverage |
| **Statistics Processing** | ? None | ? Full coverage |
| **Dependency Missing (Athlete)** | ? None | ? Retry logic tested |
| **Dependency Missing (Team)** | ? None | ? Retry logic tested |
| **Dependency Missing (Position)** | ? None | ? Retry logic tested |
| **Null Document** | ? None | ? Edge case covered |
| **Missing Ref** | ? None | ? Edge case covered |

---

## ?? **Test Data Files Used**

### **`EspnFootballNcaaAthleteSeason.json`**
**Used by**: All tests requiring event verification

**Contains**:
- ? `headshot.href` - Triggers `ProcessImageRequest`
- ? `statistics.$ref` - Triggers `DocumentRequested` for statistics
- ? Complete athlete data (name, jersey, experience, etc.)

**Why**: Tests validating event publishing need actual data to trigger events.

### **`EspnFootballNcaaAthleteSeason_Debug.json`** (NOT USED)
**Contains**:
- ? `headshot: null`
- ? `statistics: null`

**Why NOT used**: Would cause event verification tests to fail (no events published when data is null).

---

## ?? **Key Technical Fixes**

### **1. Using Statements Added**
```csharp
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Producer.Infrastructure.Data.Common; // For AthleteExternalId
using SportsData.Producer.Exceptions;
```

### **2. Exception Handling Corrected**
**Before** (incorrect assumption):
```csharp
var exception = await Record.ExceptionAsync(() => sut.ProcessAsync(command));
exception.Should().BeOfType<ExternalDocumentNotSourcedException>();
```

**After** (matches actual behavior):
```csharp
await sut.ProcessAsync(command); // No exception thrown

// Processor catches exception and publishes retry event
bus.Verify(x => x.Publish(
    It.Is<DocumentCreated>(e => e.AttemptCount == command.AttemptCount + 1),
    It.IsAny<CancellationToken>()), Times.Once);
```

### **3. Event Verification Updated**
**ProcessImageRequest** property:
- ? OLD: `e.SourceUri` (doesn't exist)
- ? NEW: `e.Url`

---

## ?? **XML Documentation**

All test methods include comprehensive XML documentation following BRUTAL_TEST_REVIEW.md standards:

```csharp
/// <summary>
/// Validates that when all dependencies exist and a valid AthleteSeason document is provided,
/// the processor creates a new AthleteSeason entity with all properties mapped correctly.
/// </summary>
[Fact]
public async Task ProcessAsync_CreatesAthleteSeason_WhenAllDependenciesExist()
```

**Benefits**:
- Better IDE IntelliSense
- Clearer test intent
- Easier onboarding for new developers

---

## ?? **Build & Test Results**

```
Passed!  - Failed:     0, Passed:     9, Skipped:     0, Total:     9, Duration: 6 s

Test summary: total: 9, failed: 0, succeeded: 9, skipped: 0, duration: 7.6s
Build succeeded in 10.0s
```

? **ALL TESTS PASSING** (Actually verified this time! ????)

---

## ?? **Key Learnings Applied**

1. ? **Always run tests before declaring success** (learned from TeamSeasonRecordDocumentProcessorTests)
2. ? **Verify test data matches test expectations** (headshot/statistics present vs. null)
3. ? **Match test assertions to actual processor behavior** (retry logic vs. thrown exceptions)
4. ? **Use correct test data files** (with data vs. debug/empty files)

---

## ?? **Bottom Line**

The `AthleteSeasonDocumentProcessorTests` now:
- ? **Comprehensively tests create and update flows**
- ? **Validates all event publishing** (images, statistics, retries)
- ? **Covers dependency resolution** (athlete, team, position)
- ? **Handles edge cases** (null document, missing refs)
- ? **Matches actual processor behavior** (retry logic, exception handling)
- ? **Includes XML documentation** for all tests
- ? **ALL 9 TESTS PASSING** (verified with actual test run)

**Verified and ready for production!** ?????  
(Actually ran the tests this time! ??)
