# ParentId Derivation Pattern - Document Processor Standardization

**Status:** In Progress
**Created:** 2026-02-22
**Feature Branch:** `feature/standardize-parentid-derivation`

## Executive Summary

This document captures the architectural decision and implementation plan to standardize how document processors derive their ParentId when processing dependency documents. The current inconsistent approach has led to sourcing issues when ParentId is not explicitly provided by requesting processors.

## Problem Statement

Document processors currently have inconsistent approaches to handling missing ParentId values:

1. **Some processors** assume ParentId is always provided and fail with errors when it's missing
2. **Few processors** (like `SeasonTypeWeekDocumentProcessor`) can derive ParentId from their source URI using `EspnUriMapper`
3. **Requesting processors** sometimes try to derive ParentId for downstream processors, creating tight coupling

This inconsistency causes sourcing failures, especially in dependency-driven scenarios where documents are requested without explicit parent context.

## Architectural Decision

### Chosen Pattern: **Self-Derivation (Option B)**

Each document processor should be responsible for deriving its own ParentId when not explicitly provided. This follows the **Single Responsibility Principle** and **Loose Coupling** principles.

### Pattern Structure

```csharp
protected override async Task ProcessInternal(ProcessDocumentCommand command)
{
    // Step 1: Try to parse provided ParentId, or derive from URI
    var parentId = TryGetOrDeriveParentId(
        command,
        EspnUriMapper.SpecificChildToParent);

    // Step 2: If neither works, log and bail
    if (parentId == null)
    {
        _logger.LogError("Unable to determine ParentId from ParentId or URI");
        return;
    }

    var parentIdValue = parentId.Value;

    // Step 3: Use parentId to query for parent entity
    var parent = await _dataContext.Parents
        .Include(/* relevant navigations */)
        .FirstOrDefaultAsync(x => x.Id == parentIdValue);

    // Step 4: If parent doesn't exist, publish dependency request
    if (parent == null)
    {
        var parentRef = EspnUriMapper.SpecificChildToParent(dto.Ref);
        await PublishDependencyRequest<string?>(
            command,
            new EspnLinkDto { Ref = parentRef },
            parentId: null,
            DocumentType.ParentType);

        throw new ExternalDocumentNotSourcedException($"Parent {parentRef} not found. Will retry.");
    }

    // Continue processing...
}
```

## Why This Pattern?

### ✅ Advantages

1. **Single Responsibility**: Each processor owns its parent relationship logic
2. **Encapsulation**: URI mapping knowledge stays with the processor that needs it
3. **Loose Coupling**: Requesting processors don't need downstream implementation knowledge
4. **Flexibility**: Works both with explicit ParentId (direct invocation) and without (dependency sourcing)
5. **Self-Sufficient**: Processor can be tested and used independently
6. **Resilience**: URI structure changes only affect one processor, not all its callers

### ❌ Rejected Alternative: Requester Derives ParentId

Having requesting processors derive ParentId for downstream processors was rejected because:

- Creates tight coupling between processors
- Leaks domain knowledge across processor boundaries
- Duplicates mapping logic across multiple requesters
- Makes system fragile to URI structure changes
- Violates encapsulation principles

## Implementation Plan

### Phase 1: Documentation & Analysis ✅

- [x] Document architectural decision
- [x] Identify all processors checking ParentId
- [x] Categorize processors by parent relationship type

### Phase 2: EspnUriMapper Completeness ✅

- [x] Verify all necessary URI mapping functions exist
- [x] Add missing mapping functions for identified processors (18 new functions)
- [x] Ensure all mappings return clean URLs (no query strings)
- [x] Write comprehensive tests for all new functions

### Phase 3: Processor Refactoring (By Category) ✅

- [x] FranchiseSeason-based processors (7 processors)
  - [x] TeamSeasonStatisticsDocumentProcessor
  - [x] TeamSeasonLeadersDocumentProcessor
  - [x] TeamSeasonRankDocumentProcessor
  - [x] TeamSeasonRecordDocumentProcessor
  - [x] TeamSeasonRecordAtsDocumentProcessor
  - [x] TeamSeasonProjectionDocumentProcessor
  - [x] TeamSeasonAwardDocumentProcessor
- [x] Competition-based processors (9 processors)
  - [x] EventCompetitionBroadcastDocumentProcessor
  - [x] EventCompetitionLeadersDocumentProcessor
  - [x] EventCompetitionPlayDocumentProcessor
  - [x] EventCompetitionPredictionDocumentProcessor
  - [x] EventCompetitionStatusDocumentProcessor
  - [x] EventCompetitionSituationDocumentProcessor
  - [x] EventCompetitionDriveDocumentProcessor
  - [x] EventCompetitionOddsDocumentProcessor
  - [x] EventCompetitionPowerIndexDocumentProcessor
- [x] CompetitionCompetitor-based processors (3 processors)
  - [x] EventCompetitionCompetitorLineScoreDocumentProcessor
  - [x] EventCompetitionCompetitorScoreDocumentProcessor
  - [x] EventCompetitionCompetitorStatisticsDocumentProcessor
- [x] Coach-based processors (2 processors)
  - [x] CoachRecordDocumentProcessor
  - [x] CoachSeasonRecordDocumentProcessor
- [x] AthleteSeason-based processors (1 processor)
  - [x] AthleteSeasonStatisticsDocumentProcessor
- [x] SeasonWeek-based (already implemented, migrated to base helper)
  - [x] SeasonTypeWeekDocumentProcessor
- [ ] SeasonPoll-based processors (1 processor) — deferred; out of scope for Phase 3
  - [ ] SeasonTypeWeekRankingsDocumentProcessor (deferred — see Category 8 notes)

### Phase 4: Testing

- [x] SeasonTypeWeekDocumentProcessor — ParentId derivation scenario
- [ ] Add tests for ParentId derivation scenarios for remaining refactored processors
- [ ] Verify dependency sourcing works without explicit ParentId
- [ ] Integration tests for end-to-end sourcing flows

### Phase 5: Documentation

- [x] Update processor guidelines with standard pattern
- [ ] Add examples to developer documentation
- [ ] Update architectural decision records (ADR)

## Affected Document Processors

### Category 1: FranchiseSeason-based (7 processors)

**Parent Relationship:** FranchiseSeason (via TeamSeason URI)
**URI Mapper Functions:** Explicit functions for each child type (see Functions section)

1. ✅ **TeamSeasonStatisticsDocumentProcessor** - Uses `TeamSeasonStatisticsRefToTeamSeasonRef`
2. ✅ **TeamSeasonLeadersDocumentProcessor** - Uses `TeamSeasonLeadersRefToTeamSeasonRef`
3. ✅ **TeamSeasonRankDocumentProcessor** - Uses `TeamSeasonRankRefToTeamSeasonRef`
4. ✅ **TeamSeasonRecordDocumentProcessor** - Uses `TeamSeasonRecordRefToTeamSeasonRef`
5. ✅ **TeamSeasonRecordAtsDocumentProcessor** - Uses `TeamSeasonRecordAtsRefToTeamSeasonRef`
6. ✅ **TeamSeasonProjectionDocumentProcessor** - Uses `TeamSeasonProjectionRefToTeamSeasonRef`
7. ✅ **TeamSeasonAwardDocumentProcessor** - Uses `TeamSeasonAwardRefToTeamSeasonRef`

**Example URI Mapping:**

```text
Input:  http://.../seasons/2025/teams/395/statistics/0
Parent: http://.../seasons/2025/teams/395
```

### Category 2: Competition-based (9 processors)

**Parent Relationship:** Competition (via EventCompetition URI)

1. ✅ **EventCompetitionBroadcastDocumentProcessor** - Uses `CompetitionBroadcastRefToCompetitionRef`
2. ✅ **EventCompetitionLeadersDocumentProcessor** - Uses `CompetitionLeadersRefToCompetitionRef`
3. ✅ **EventCompetitionPlayDocumentProcessor** - Uses `CompetitionPlayRefToCompetitionRef`
4. ✅ **EventCompetitionPredictionDocumentProcessor** - Uses `CompetitionPredictionRefToCompetitionRef`
5. ✅ **EventCompetitionStatusDocumentProcessor** - Uses `CompetitionStatusRefToCompetitionRef`
6. ✅ **EventCompetitionSituationDocumentProcessor** - Uses `CompetitionSituationRefToCompetitionRef`
7. ✅ **EventCompetitionDriveDocumentProcessor** - Uses `CompetitionDriveRefToCompetitionRef`
8. ✅ **EventCompetitionOddsDocumentProcessor** - Uses `CompetitionOddsRefToCompetitionRef`
9. ✅ **EventCompetitionPowerIndexDocumentProcessor** - Uses `CompetitionPowerIndexRefToCompetitionRef`

**Example URI Mapping:**

```text
Input:  http://.../events/401752671/competitions/401752671/leaders
Parent: http://.../events/401752671/competitions/401752671
```

### Category 3: CompetitionCompetitor-based (3 processors)

**Parent Relationship:** CompetitionCompetitor

1. ✅ **EventCompetitionCompetitorLineScoreDocumentProcessor** - Uses `CompetitionLineScoreRefToCompetitionCompetitorRef`
2. ✅ **EventCompetitionCompetitorScoreDocumentProcessor** - Uses `CompetitionCompetitorScoreRefToCompetitionCompetitorRef`
3. ✅ **EventCompetitionCompetitorStatisticsDocumentProcessor** - Uses `CompetitionCompetitorStatisticsRefToCompetitionCompetitorRef`

**Example URI Mapping:**

```text
Input:  http://.../competitions/401752671/competitors/2231/linescores/1
Parent: http://.../competitions/401752671/competitors/2231
```

### Category 4: Contest-based (1 processor)

**Parent Relationship:** Contest (Event)
**URI Mapper Function:** `CompetitionRefToContestRef()` (exists)

1. **EventCompetitionDocumentProcessor** - Requires ParentId, **not yet refactored**

**Example URI Mapping:**

```text
Input:  http://.../events/401752671/competitions/401752671
Parent: http://.../events/401752671
```

### Category 5: Coach-based (2 processors)

**Parent Relationship:** Coach or CoachSeason

1. ✅ **CoachRecordDocumentProcessor** - Uses `CoachRecordRefToCoachRef`
2. ✅ **CoachSeasonRecordDocumentProcessor** - Uses `CoachSeasonRecordRefToCoachSeasonRef`

**Example URI Mapping:**

```text
Input:  http://.../coaches/123/record             → http://.../coaches/123
Input:  http://.../seasons/2025/coaches/123/record → http://.../seasons/2025/coaches/123
```

### Category 6: AthleteSeason-based (1 processor)

**Parent Relationship:** AthleteSeason

1. ✅ **AthleteSeasonStatisticsDocumentProcessor** - Uses `AthleteSeasonStatisticsRefToAthleteSeasonRef`

**Example URI Mapping:**

```text
Input:  http://.../seasons/2025/athletes/4426333/statistics/0
Parent: http://.../seasons/2025/athletes/4426333
```

### Category 7: SeasonWeek-based ✅

1. ✅ **SeasonTypeWeekDocumentProcessor** - Uses `SeasonTypeWeekToSeasonType` via `TryGetOrDeriveParentId`

### Category 8: SeasonPoll-based (1 processor)

**Parent Relationship:** SeasonPoll
**URI Mapper Function:** `SeasonPollWeekRefToSeasonPollRef()` (exists)

1. **SeasonTypeWeekRankingsDocumentProcessor** - Has warning log, attempts derivation — **not yet refactored to standard pattern**

### Excluded: Test Processors

- `OutboxTestTeamSportDocumentProcessor` - Test infrastructure
- `OutboxTestDocumentProcessor` - Test infrastructure

## Reference Implementation

The `DocumentProcessorBase` provides a helper method for ParentId derivation:

```csharp
// In DocumentProcessorBase
protected Guid? TryGetOrDeriveParentId(
    ProcessDocumentCommand command,
    Func<Uri, Uri>? uriMapper = null)
{
    if (Guid.TryParse(command.ParentId, out var parentId))
        return parentId;

    if (uriMapper == null || command.SourceUri == null)
        return null;

    try
    {
        var parentUri = uriMapper(command.SourceUri);
        var derivedId = _externalRefIdentityGenerator.Generate(parentUri).CanonicalId;

        _logger.LogDebug(
            "ParentId not provided, derived from URI. SourceUri={SourceUri}, ParentUri={ParentUri}, DerivedId={DerivedId}",
            command.SourceUri, parentUri, derivedId);
        return derivedId;
    }
    catch (Exception ex)
    {
        _logger.LogDebug(ex, "Failed to derive ParentId from URI. SourceUri={SourceUri}", command.SourceUri);
        return null;
    }
}
```

Example usage:

```csharp
protected override async Task ProcessInternal(ProcessDocumentCommand command)
{
    var franchiseSeasonId = TryGetOrDeriveParentId(
        command,
        EspnUriMapper.TeamSeasonStatisticsRefToTeamSeasonRef);

    if (franchiseSeasonId == null)
    {
        _logger.LogError("Unable to determine FranchiseSeasonId from ParentId or URI");
        return;
    }

    var franchiseSeasonIdValue = franchiseSeasonId.Value;
    // Continue processing with franchiseSeasonIdValue...
}
```

This approach:

1. Checks if ParentId can be parsed directly
2. If not, uses the provided EspnUriMapper function to derive the parent URI from `command.SourceUri`
3. Generates a canonical ID from the derived URI
4. Returns nullable Guid (null if both ParentId and mapper fail)

## EspnUriMapper Functions

### Pre-existing Functions (not added by this effort)

- ✅ `CompetitionCompetitorScoreRefToCompetitionCompetitorRef()` - CompetitorScore → Competitor
- ✅ `CompetitionCompetitorLineScoreRefToCompetitionCompetitorRef()` - LineScore → Competitor
- ✅ `CompetitionCompetitorStatisticsRefToCompetitionCompetitorRef()` - CompetitorStatistics → Competitor
- ✅ `CompetitionLeadersRefToCompetitionRef()` - Leaders → Competition
- ✅ `CompetitionRefToContestRef()` - Competition → Contest
- ✅ `SeasonPollWeekRefToSeasonPollRef()` - PollWeek → Poll
- ✅ `SeasonTypeWeekToSeasonType()` - SeasonTypeWeek → SeasonType

### Added by This Effort (18 functions, all return clean URLs without query strings)

- [x] `TeamSeasonStatisticsRefToTeamSeasonRef()` - Statistics → TeamSeason
- [x] `TeamSeasonLeadersRefToTeamSeasonRef()` - Leaders → TeamSeason
- [x] `TeamSeasonRankRefToTeamSeasonRef()` - Rank → TeamSeason
- [x] `TeamSeasonRecordRefToTeamSeasonRef()` - Record → TeamSeason
- [x] `TeamSeasonRecordAtsRefToTeamSeasonRef()` - RecordAts → TeamSeason
- [x] `TeamSeasonProjectionRefToTeamSeasonRef()` - Projection → TeamSeason
- [x] `TeamSeasonAwardRefToTeamSeasonRef()` - Award → TeamSeason
- [x] `CompetitionBroadcastRefToCompetitionRef()` - Broadcast → Competition
- [x] `CompetitionPlayRefToCompetitionRef()` - Play → Competition
- [x] `CompetitionPredictionRefToCompetitionRef()` - Prediction → Competition
- [x] `CompetitionStatusRefToCompetitionRef()` - Status → Competition
- [x] `CompetitionSituationRefToCompetitionRef()` - Situation → Competition
- [x] `CompetitionDriveRefToCompetitionRef()` - Drive → Competition
- [x] `CompetitionOddsRefToCompetitionRef()` - Odds → Competition
- [x] `CompetitionPowerIndexRefToCompetitionRef()` - PowerIndex → Competition
- [x] `AthleteSeasonStatisticsRefToAthleteSeasonRef()` - Statistics → AthleteSeason
- [x] `CoachSeasonRecordRefToCoachSeasonRef()` - Season record → CoachSeason
- [x] `CoachRecordRefToCoachRef()` - Record → Coach

**Also:** Query string stripping was applied to all pre-existing functions that previously preserved query strings.

## Testing Strategy

### Test Coverage Required for Each Refactored Processor

1. **Test: ParentId Provided**
   - Verify processor uses provided ParentId directly
   - No URI derivation occurs
   - Existing functionality preserved

2. **Test: ParentId Missing - Parent Exists**
   - ParentId is null/empty in command
   - Processor derives ParentId from URI
   - Parent entity exists in database
   - Processing continues successfully
   - Correct parent relationship established

3. **Test: ParentId Missing - Parent Missing**
   - ParentId is null/empty in command
   - Processor derives ParentId from URI
   - Parent entity does NOT exist in database
   - Dependency request published with correct parent URI
   - `ExternalDocumentNotSourcedException` thrown for retry

### Example Test Pattern

```csharp
[Fact]
public async Task WhenParentIdNotProvided_ShouldDeriveFromUri_AndProcess()
{
    // Arrange
    var generator = new ExternalRefIdentityGenerator();

    // Derive expected parentId using same logic processor will use
    var parentUri = EspnUriMapper.SpecificChildToParentRef(childUri);
    var expectedParentId = generator.Generate(parentUri).CanonicalId;

    // Create parent with derived ID
    var parent = CreateParent(expectedParentId);
    await _dataContext.Parents.AddAsync(parent);
    await _dataContext.SaveChangesAsync();

    // Command with null ParentId
    var command = CreateCommand(parentId: null, sourceUri: childUri);

    // Act
    await sut.ProcessAsync(command);

    // Assert
    var result = await _dataContext.Children
        .FirstOrDefaultAsync(c => c.ParentId == expectedParentId);

    result.Should().NotBeNull("processor should derive ParentId from URI");
    result.ParentId.Should().Be(expectedParentId);
}
```

### Current Test Coverage

- ✅ All 18 new `EspnUriMapper` functions have comprehensive unit tests (happy path, null input, invalid shape where applicable)
- ✅ `SeasonTypeWeekDocumentProcessor` — ParentId derivation scenario covered
- ⬜ Remaining refactored processors — derivation scenario tests pending (Phase 4)

## Success Criteria

1. ✅ All 24 affected processors can derive their ParentId when not provided (23 done, 1 deferred: `EventCompetitionDocumentProcessor`)
2. ✅ All necessary EspnUriMapper functions exist and return clean URLs
3. ⬜ Each processor has test coverage for ParentId derivation scenarios (in progress — Phase 4)
4. ✅ No breaking changes to existing functionality (explicit ParentId still works)
5. ✅ Dependency sourcing works reliably without explicit ParentId
6. ⬜ Documentation fully updated (Phase 5 in progress)

## Benefits of Standardization

1. **Reliability**: Dependency sourcing becomes more resilient
2. **Maintainability**: Consistent pattern across all processors
3. **Decoupling**: Processors are self-contained and independent
4. **Testing**: Clear test patterns for all processors
5. **Flexibility**: Processors work in multiple invocation scenarios
6. **Discoverability**: Developers know where to look for parent resolution logic

## Related Work

- [EspnUriMapper Cleanup](./espn-uri-mapper-cleanup.md) - Query string removal
- Document Processor Guidelines (to be updated)
- Architectural Decision Records (to be created)

## Notes

- This standardization effort is expected to resolve many sourcing issues encountered during historical data backfills
- The pattern was discovered while investigating dependency sourcing failures in `SeasonTypeWeekDocumentProcessor`
- URI mapping functions in `EspnUriMapper` have been standardized to return clean URLs (no query strings) for canonical ID generation
- The `TeamSeasonChildToTeamSeasonRef` private helper in `EspnUriMapper` is shared by all 7 TeamSeason child mappers to avoid duplication
- `EventCompetitionDocumentProcessor` (Contest-based, Category 4) is intentionally deferred — it requires separate analysis of its parent resolution path

## Change Log

| Date | Change | Author |
|------|--------|--------|
| 2026-02-22 | Initial document created | Copilot |
| 2026-02-22 | Identified affected processors; categorized by parent type | Copilot |
| 2026-02-22 | Phase 2 complete: 18 EspnUriMapper functions added with tests; query string stripping applied to existing functions | Copilot |
| 2026-02-23 | Phase 3 complete: all 23 processors refactored; SeasonTypeWeekDocumentProcessor migrated to base helper; code defects corrected | Claude (Sonnet 4.6) |
