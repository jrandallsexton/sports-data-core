# Document Processor Code Review

**Date:** 2025-12-29
**Reviewer:** Claude (AI Assistant)
**Scope:** All document processors excluding Golf (~50 files)

---

## Executive Summary

This review analyzed 50+ document processor files across the following categories:
- **Football processors** (28 files)
- **TeamSports processors** (13 files)
- **Common processors** (1 file - VenueDocumentProcessor)
- **Infrastructure** (7 files - base classes, factories, registry)

### Issue Summary by Severity

| Severity | Count | Description |
|----------|-------|-------------|
| **Critical** | 15 | Bugs that could cause data corruption, silent failures, or incorrect tracing |
| **Medium** | 35+ | Missing functionality, incomplete implementations, potential null references |
| **Low** | 50+ | Code style, inconsistencies, minor improvements |

---

## Critical Issues

### 1. Silent Failures in DocumentCreatedProcessor (INFRASTRUCTURE)
**File:** `DocumentCreatedProcessor.cs` (lines 96-114)
**Impact:** When document content is null or empty, the processor returns silently. Hangfire considers the job successful, preventing retry mechanisms from working.

**Recommendation:** Throw an exception to trigger retry logic.

---

### 2. Missing Immutable Field Preservation with SetValues()

The following processors use `SetValues()` pattern but do NOT preserve `CreatedBy` and `CreatedUtc`:

| File | Line(s) | Status |
|------|---------|--------|
| `EventCompetitionDocumentProcessor.cs` | 207-209 | **FIXED** (during this session) |
| `VenueDocumentProcessor.cs` | 137-150 | **FIXED** (during this session) |

**Pattern to follow:**
```csharp
// Preserve immutable fields before SetValues overwrites them
var originalCreatedBy = entity.CreatedBy;
var originalCreatedUtc = entity.CreatedUtc;

entry.CurrentValues.SetValues(updatedEntity);

// Restore immutable fields
entity.CreatedBy = originalCreatedBy;
entity.CreatedUtc = originalCreatedUtc;
```

---

### 3. Delete-and-Replace Patterns Losing Audit Trail

These processors delete existing records and create new ones, losing `CreatedBy`/`CreatedUtc`:

| File | Line(s) | Description |
|------|---------|-------------|
| `AthleteSeasonStatisticsDocumentProcessor.cs` | 88-99 | Removes existing, creates new |
| `EventCompetitionStatusDocumentProcessor.cs` | 99-107 | Hard replace loses audit info |
| `EventCompetitionLeadersDocumentProcessor.cs` | 108-111 | Delete-then-insert pattern |
| `EventCompetitionOddsDocumentProcessor.cs` | 135-143 | Delete-then-insert pattern |
| `TeamSeasonRecordDocumentProcessor.cs` | 80-93 | Also passes `Guid.Empty` as correlationId! |

**Recommendation:** Preserve and restore `CreatedBy`/`CreatedUtc` from the deleted entity.

---

### 4. Wrong CausationIds

| File | Line | Wrong CausationId | Should Be |
|------|------|-------------------|-----------|
| `EventCompetitionDriveDocumentProcessor.cs` | 216 | `GroupSeasonDocumentProcessor` | `EventCompetitionDriveDocumentProcessor` (missing) |
| `EventCompetitionOddsDocumentProcessor.cs` | 156, 161 | `EventDocumentProcessor` | `EventCompetitionOddsDocumentProcessor` (missing) |
| `SeasonPollDocumentProcessor.cs` | 109 | `SeasonTypeWeekRankingsDocumentProcessor` | `SeasonPollDocumentProcessor` |

---

### 5. CausationId GUID Collision

**File:** `CausationId.cs`

```csharp
// Line 21 - Both have the same GUID!
ContestUpdateProcessor = new Guid("10000000-0000-0000-0000-00000000000F")
// Line 30
EventDocumentProcessor = new Guid("10000000-0000-0000-0000-00000000000F")
```

**Impact:** Cannot distinguish between these processors in traces/logs.

---

### 6. Stub/Unimplemented Processors

These processors have TODO stubs with no actual processing logic:

| File | Line(s) |
|------|---------|
| `AwardDocumentProcessor.cs` | 22-27 |
| `StandingsDocumentProcessor.cs` | 22-27 |
| `TeamSeasonAwardDocumentProcessor.cs` | 42-46 |
| `TeamSeasonCoachDocumentProcessor.cs` | 54-56 |
| `TeamSeasonInjuriesDocumentProcessor.cs` | 54-56 |
| `TeamSeasonLeadersDocumentProcessor.cs` | 62-64 |

---

### 7. DocumentProcessorFactory Missing Error Handling
**File:** `DocumentProcessorFactory.cs` (lines 61-64)
**Issue:** No try-catch around `ActivatorUtilities.CreateInstance`. If constructor parameters don't match, this throws a cryptic reflection exception.

---

## Medium Issues

### Missing Update Logic

Many processors detect existing entities but don't implement updates:

| File | Method | Current Behavior |
|------|--------|------------------|
| `AthleteDocumentProcessor.cs` | `ProcessExisting` | Only handles headshot image |
| `AthletePositionDocumentProcessor.cs` | `ProcessUpdate` | Missing `ModifiedUtc`/`ModifiedBy` |
| `CoachDocumentProcessor.cs` | `ProcessUpdate` | Only updates `Experience` field |
| `CoachBySeasonDocumentProcessor.cs` | `ProcessUpdate` | Logs warning, does nothing |
| `EventCompetitionCompetitorDocumentProcessor.cs` | `ProcessUpdate` | Only processes children, no field updates |
| `EventCompetitionPlayDocumentProcessor.cs` | `ProcessUpdate` | Missing audit fields |
| `EventCompetitionDriveDocumentProcessor.cs` | `ProcessUpdate` | Only processes plays, no field updates |
| `GroupSeasonDocumentProcessor.cs` | `HandleExisting` | Logs warning, does nothing |
| `SeasonDocumentProcessor.cs` | `ProcessUpdateAsync` | Logs error, does nothing |
| `SeasonTypeWeekDocumentProcessor.cs` | `ProcessExistingEntity` | Logs error, does nothing |
| `SeasonTypeWeekRankingsDocumentProcessor.cs` | `ProcessExistingEntity` | Logs error, does nothing |

---

### Missing Retry Handling

These processors don't catch `ExternalDocumentNotSourcedException` for retry:

- `AthletePositionDocumentProcessor.cs`
- `SeasonFutureDocumentProcessor.cs`
- `SeasonPollDocumentProcessor.cs`
- `SeasonTypeDocumentProcessor.cs`
- `EventCompetitionCompetitorStatisticsDocumentProcessor.cs`
- `EventCompetitionDriveDocumentProcessor.cs`

---

### Potential Null Reference Exceptions

| File | Line | Issue |
|------|------|-------|
| `CoachBySeasonDocumentProcessor.cs` | 99 | `dto.Person.Ref` accessed without null check |
| `CoachBySeasonDocumentProcessor.cs` | 141 | `dto.Team.Ref` accessed without null check |
| `EventCompetitionCompetitorStatisticsDocumentProcessor.cs` | 74 | `dto.Team.Ref` without null check |
| `EventCompetitionProbabilityDocumentProcessor.cs` | 141 | `DateTime.Parse(dto.LastModified)` without null/format check |
| `TeamSeasonRecordAtsDocumentProcessor.cs` | 75 | `dto.Items` not null-checked before iteration |
| `CoachRecordDocumentProcessor.cs` | 93 | `.First()` on collection that could be empty |

---

### SaveChangesAsync Issues

| File | Line | Issue |
|------|------|-------|
| `EventCompetitionDriveDocumentProcessor.cs` | 225 | SaveChangesAsync inside loop (performance) |
| `EventDocumentProcessor.cs` | 537-538 | SaveChangesAsync inside loop (performance) |
| `EventCompetitionStatusDocumentProcessor.cs` | 121-130 | Event published BEFORE SaveChanges (inconsistent state if save fails) |

---

### Missing Domain Events

These processors create/update entities without publishing domain events:

- `AthleteSeasonStatisticsDocumentProcessor.cs`
- `CoachBySeasonDocumentProcessor.cs`
- `TeamSeasonRankDocumentProcessor.cs`
- `TeamSeasonRecordAtsDocumentProcessor.cs`
- `TeamSeasonStatisticsDocumentProcessor.cs`

---

### DocumentProcessorRegistry Issues

| Line | Issue |
|------|-------|
| 25-27 | Assembly scanning may miss unloaded assemblies |
| 37-42 | Duplicate processor handling is non-deterministic (first one wins, but order not guaranteed) |
| 55-64 | `ReflectionTypeLoadException` loader exceptions not logged |

---

### Unused Code

- `DocumentProcessorFactory.cs` line 6-9: `DocumentAction` enum defined but never used

---

## Low Issues

### Inconsistent Logging Patterns

| Pattern | Files Using It |
|---------|----------------|
| `{@Command}` (may expose sensitive data) | TeamSeasonProjectionDocumentProcessor, TeamSeasonRankDocumentProcessor, TeamSeasonRecordAtsDocumentProcessor, etc. |
| `command.ToSafeLogObject()` (preferred) | Most other processors |

---

### Missing CorrelationId Scope

- `AwardDocumentProcessor.cs`
- `StandingsDocumentProcessor.cs`

---

### Hardcoded Values

| File | Line | Issue |
|------|------|-------|
| `SeasonTypeWeekRankingsDocumentProcessor.cs` | 143, 223 | Hardcoded `Sport.FootballNcaa` instead of `command.Sport` |
| `EventCompetitionCompetitorScoreDocumentProcessor.cs` | 125 | Hardcoded `SourceDataProvider.Espn` |
| `EventCompetitionDriveDocumentProcessor.cs` | 214 | Hardcoded `SourceDataProvider.Espn` |

---

### Code Style

- Emojis in log messages (`DocumentProcessorBase.cs` lines 60-64)
- String interpolation in logging instead of structured logging (`FranchiseDocumentProcessor.cs` line 115)
- Protected fields instead of properties (`DocumentProcessorBase.cs` lines 19-22)
- `ProcessInternal` is public instead of private (`TeamSeasonDocumentProcessor.cs` line 96)

---

## Missing CausationIds

The following processors need CausationId entries in `CausationId.cs`:

### Football
- `EventCompetitionDriveDocumentProcessor`
- `EventCompetitionOddsDocumentProcessor`
- `EventCompetitionBroadcastDocumentProcessor`
- `EventCompetitionCompetitorStatisticsDocumentProcessor`
- `EventCompetitionPredictionDocumentProcessor`

### TeamSports
- `TeamSeasonProjectionDocumentProcessor`
- `TeamSeasonRankDocumentProcessor`
- `TeamSeasonRecordAtsDocumentProcessor`
- `TeamSeasonStatisticsDocumentProcessor`
- `TeamSeasonAwardDocumentProcessor`
- `TeamSeasonCoachDocumentProcessor`
- `TeamSeasonInjuriesDocumentProcessor`
- `TeamSeasonLeadersDocumentProcessor`
- `CoachRecordDocumentProcessor`

---

## Code Duplication Opportunities

### 1. ProcessAsync Boilerplate

Almost every processor has identical structure:
```csharp
public async Task ProcessAsync(ProcessDocumentCommand command)
{
    using (_logger.BeginScope(new Dictionary<string, object>
    {
        ["CorrelationId"] = command.CorrelationId
    }))
    {
        _logger.LogInformation("Began with {@command}", command);
        await ProcessInternal(command);
    }
}
```

**Recommendation:** Move to base class with template method pattern.

---

### 2. DTO Validation Pattern

Repeated in every processor:
```csharp
var dto = command.Document.FromJson<SomeDto>();
if (dto is null) { _logger.LogError(...); return; }
if (string.IsNullOrEmpty(dto.Ref?.ToString())) { _logger.LogError(...); return; }
```

**Recommendation:** Create base class helper: `TryDeserializeAndValidate<T>(command, out T dto)`

---

### 3. ParentId Validation

Repeated across many processors:
```csharp
if (!Guid.TryParse(command.ParentId, out var parentId))
{
    _logger.LogError("Invalid ParentId: {ParentId}", command.ParentId);
    return;
}
```

**Recommendation:** Create base class helper: `TryParseParentId(command, out Guid parentId)`

---

### 4. Dependency Resolution

FranchiseSeason resolution pattern is duplicated across:
- `TeamSeasonDocumentProcessor`
- `TeamSeasonProjectionDocumentProcessor`
- `TeamSeasonRankDocumentProcessor`
- `TeamSeasonRecordAtsDocumentProcessor`
- `EventCompetitionLeadersDocumentProcessor`
- Many others

**Recommendation:** Move to `DocumentProcessorBase.ResolveFranchiseSeasonAsync()`

---

### 5. Retry Exception Handling

```csharp
catch (ExternalDocumentNotSourcedException ex)
{
    _logger.LogError(ex, "...");
    await _dataContext.SaveChangesAsync();
    throw;
}
```

**Recommendation:** Move to base class as virtual method.

---

## Recommendations Summary

### Immediate Actions (Critical)
1. Fix CausationId GUID collision (`ContestUpdateProcessor` vs `EventDocumentProcessor`)
2. Fix wrong CausationIds in `EventCompetitionDriveDocumentProcessor`, `EventCompetitionOddsDocumentProcessor`, `SeasonPollDocumentProcessor`
3. Fix `DocumentCreatedProcessor` to throw on null/empty documents
4. Add error handling to `DocumentProcessorFactory.CreateInstance`
5. Fix `TeamSeasonRecordDocumentProcessor` passing `Guid.Empty` as correlationId

### Short-Term (Medium)
1. Add missing CausationId entries for all processors
2. Implement immutable field preservation pattern in all processors using delete-and-replace
3. Add missing `ModifiedUtc`/`ModifiedBy` updates to all ProcessUpdate methods
4. Add null checks for potential null reference exceptions
5. Move SaveChangesAsync outside of loops
6. Ensure events are published AFTER SaveChangesAsync

### Long-Term (Low/Refactoring)
1. Extract common patterns to `DocumentProcessorBase`
2. Standardize on one update pattern (prefer SetValues with immutable field preservation)
3. Complete stub implementations or remove unused processors
4. Standardize logging patterns across all processors
5. Add `CancellationToken` support throughout

---

## Appendix: Files Reviewed

### Football Processors (28 files)
- AthleteDocumentProcessor.cs
- AthletePositionDocumentProcessor.cs
- AthleteSeasonDocumentProcessor.cs
- AthleteSeasonStatisticsDocumentProcessor.cs
- AwardDocumentProcessor.cs
- CoachBySeasonDocumentProcessor.cs
- EventCompetitionAthleteStatisticsDocumentProcessor.cs
- EventCompetitionBroadcastDocumentProcessor.cs
- EventCompetitionCompetitorDocumentProcessor.cs
- EventCompetitionCompetitorLineScoreDocumentProcessor.cs
- EventCompetitionCompetitorScoreDocumentProcessor.cs
- EventCompetitionCompetitorStatisticsDocumentProcessor.cs
- EventCompetitionDocumentProcessor.cs
- EventCompetitionDriveDocumentProcessor.cs
- EventCompetitionLeadersDocumentProcessor.cs
- EventCompetitionOddsDocumentProcessor.cs
- EventCompetitionPlayDocumentProcessor.cs
- EventCompetitionPowerIndexDocumentProcessor.cs
- EventCompetitionPredictionDocumentProcessor.cs
- EventCompetitionProbabilityDocumentProcessor.cs
- EventCompetitionSituationDocumentProcessor.cs
- EventCompetitionStatusDocumentProcessor.cs
- EventDocumentProcessor.cs
- FootballSeasonRankingDocumentProcessor.cs
- GroupSeasonDocumentProcessor.cs
- SeasonDocumentProcessor.cs
- SeasonFutureDocumentProcessor.cs
- SeasonPollDocumentProcessor.cs
- SeasonTypeDocumentProcessor.cs
- SeasonTypeWeekDocumentProcessor.cs
- SeasonTypeWeekRankingsDocumentProcessor.cs
- StandingsDocumentProcessor.cs

### TeamSports Processors (13 files)
- CoachDocumentProcessor.cs
- CoachRecordDocumentProcessor.cs
- FranchiseDocumentProcessor.cs
- TeamSeasonAwardDocumentProcessor.cs
- TeamSeasonCoachDocumentProcessor.cs
- TeamSeasonDocumentProcessor.cs
- TeamSeasonInjuriesDocumentProcessor.cs
- TeamSeasonLeadersDocumentProcessor.cs
- TeamSeasonProjectionDocumentProcessor.cs
- TeamSeasonRankDocumentProcessor.cs
- TeamSeasonRecordAtsDocumentProcessor.cs
- TeamSeasonRecordDocumentProcessor.cs
- TeamSeasonStatisticsDocumentProcessor.cs

### Common Processors (1 file)
- VenueDocumentProcessor.cs

### Infrastructure (7 files)
- DocumentProcessorBase.cs
- DocumentProcessorAttribute.cs
- DocumentProcessorRegistry.cs
- DocumentProcessorFactory.cs
- DocumentCreatedProcessor.cs
- OutboxTestDocumentProcessor.cs
- OutboxTestTeamSportDocumentProcessor.cs
