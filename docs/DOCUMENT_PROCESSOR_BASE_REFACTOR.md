# DocumentProcessorBase - Refactoring Plan

**Date:** December 26, 2025 - January 2026  
**Status:** ?? **IN PROGRESS** (6 of ~50 processors refactored)

---

## Goal

Create `DocumentProcessorBase<TDataContext>` to eliminate duplicate child document request publishing code across 20+ document processors.

---

## Current Problem

**Duplicate pattern across processors:**
```csharp
// EventCompetitionCompetitorDocumentProcessor
private async Task ProcessScores(...)
{
    if (externalProviderDto.Score?.Ref is null)
        return;
    
    var identity = _externalRefIdentityGenerator.Generate(externalProviderDto.Score.Ref);
    
    await _publishEndpoint.Publish(new DocumentRequested(
        Id: identity.UrlHash,
        ParentId: competitionCompetitorId.ToString(),
        Uri: new Uri(identity.CleanUrl),
        Sport: Sport.FootballNcaa,
        SeasonYear: command.Season,
        DocumentType: DocumentType.EventCompetitionCompetitorScore,
        SourceDataProvider: SourceDataProvider.Espn,
        CorrelationId: command.CorrelationId,
        CausationId: CausationId.Producer.EventCompetitionCompetitorDocumentProcessor
    ));
}

// Same pattern in ProcessLineScores, and 15+ other processors
```

**Code smell:** 10 lines of boilerplate per child document × 20+ processors = 200+ duplicate lines

---

## Proposed Solution

### Base Class
```csharp
public abstract class DocumentProcessorBase<TDataContext> : IProcessDocuments
    where TDataContext : BaseDataContext
{
    protected readonly ILogger _logger;
    protected readonly TDataContext _dataContext;
    protected readonly IEventBus _publishEndpoint;
    protected readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

    protected DocumentProcessorBase(
        ILogger logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator)
    {
        _logger = logger;
        _dataContext = dataContext;
        _publishEndpoint = publishEndpoint;
        _externalRefIdentityGenerator = externalRefIdentityGenerator;
    }

    // Each processor implements its own logic
    public abstract Task ProcessAsync(ProcessDocumentCommand command);

    /// <summary>
    /// Helper to publish child document requests.
    /// Eliminates 10 lines of boilerplate per child document.
    /// Supports both EspnLinkDto and EspnResourceIndexItem via IHasRef interface.
    /// </summary>
    protected async Task PublishChildDocumentRequest<TParentId>(
        ProcessDocumentCommand command,
        IHasRef? linkDto,
        TParentId parentId,
        DocumentType documentType,
        Guid causationId)
    {
        if (linkDto?.Ref is null)
        {
            _logger.LogDebug("?? SKIP_CHILD_DOCUMENT: No reference found...");
            return;
        }

        ExternalRefIdentity identity;
        Uri uri;

        try
        {
            identity = _externalRefIdentityGenerator.Generate(linkDto.Ref);
            uri = new Uri(identity.CleanUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "? INVALID_CHILD_URI or IDENTITY_GENERATION_FAILED...");
            return;
        }

        _logger.LogInformation("?? PUBLISH_CHILD_REQUEST: Publishing DocumentRequested...");

        await _publishEndpoint.Publish(new DocumentRequested(
            Id: identity.UrlHash,
            ParentId: parentId?.ToString() ?? string.Empty,
            Uri: uri,
            Ref: null,
            Sport: command.Sport,
            SeasonYear: command.Season,
            DocumentType: documentType,
            SourceDataProvider: command.SourceDataProvider,
            CorrelationId: command.CorrelationId,
            CausationId: causationId
        ));

        _logger.LogDebug("? CHILD_REQUEST_PUBLISHED...");
    }
}
```

### After Refactor (Actual Current Pattern)
```csharp
// TeamSeasonDocumentProcessor : DocumentProcessorBase<TeamSportDataContext>
private async Task ProcessDependents(
    FranchiseSeason canonicalEntity,
    EspnTeamSeasonDto dto,
    ProcessDocumentCommand command)
{
    // Logos - special handling (uses EventFactory and PublishBatch)
    await ProcessLogos(canonicalEntity.Id, dto, command);

    // All other child documents - one line each using base class helper
    await PublishChildDocumentRequest(command, dto.Record, canonicalEntity.Id, DocumentType.TeamSeasonRecord, CausationId.Producer.TeamSeasonDocumentProcessor);
    await PublishChildDocumentRequest(command, dto.Ranks, canonicalEntity.Id, DocumentType.TeamSeasonRank, CausationId.Producer.TeamSeasonDocumentProcessor);
    await PublishChildDocumentRequest(command, dto.Statistics, canonicalEntity.Id, DocumentType.TeamSeasonStatistics, CausationId.Producer.TeamSeasonDocumentProcessor);
    await PublishChildDocumentRequest(command, dto.Athletes, canonicalEntity.Id, DocumentType.AthleteSeason, CausationId.Producer.TeamSeasonDocumentProcessor);
    await PublishChildDocumentRequest(command, dto.Leaders, canonicalEntity.Id, DocumentType.TeamSeasonLeaders, CausationId.Producer.TeamSeasonDocumentProcessor);
    await PublishChildDocumentRequest(command, dto.Injuries, canonicalEntity.Id, DocumentType.TeamSeasonInjuries, CausationId.Producer.TeamSeasonDocumentProcessor);
    await PublishChildDocumentRequest(command, dto.AgainstTheSpreadRecords, canonicalEntity.Id, DocumentType.TeamSeasonRecordAts, CausationId.Producer.TeamSeasonDocumentProcessor);
    await PublishChildDocumentRequest(command, dto.Awards, canonicalEntity.Id, DocumentType.TeamSeasonAward, CausationId.Producer.TeamSeasonDocumentProcessor);
    await PublishChildDocumentRequest(command, dto.Projection, canonicalEntity.Id, DocumentType.TeamSeasonProjection, CausationId.Producer.TeamSeasonDocumentProcessor);
    await PublishChildDocumentRequest(command, dto.Events, canonicalEntity.Id, DocumentType.Event, CausationId.Producer.TeamSeasonDocumentProcessor);
    await PublishChildDocumentRequest(command, dto.Coaches, canonicalEntity.Id, DocumentType.TeamSeasonCoach, CausationId.Producer.TeamSeasonDocumentProcessor);
}

// 11 child documents: 11 lines vs 264 lines! 96% reduction
```

---

## Benefits

### 1. DRY Principle ?
- Single source of truth for child document publishing
- Future changes in one place

### 2. Consistency ?
- All processors use same pattern
- Correlation ID propagation uniform
- CausationId pattern enforced

### 3. Maintainability ?
- ~408 lines of code eliminated (so far, from 3 processors)
- Easier to understand (less boilerplate)

### 4. Type Safety ?
- Generic `TParentId` allows `Guid`, `string`, etc.
- `IHasRef` interface supports both `EspnLinkDto` and `EspnResourceIndexItem`
- Compiler ensures correct types

### 5. Enhanced Error Handling ?
- URI parsing errors caught and logged
- Identity generation failures handled gracefully
- Emoji-based logging for easy visual scanning

---

## Key Enhancement: IHasRef Interface Support

**Challenge:** ESPN DTOs use different types for references:
- `EspnLinkDto` - Standard link with metadata
- `EspnResourceIndexItem` - Simpler reference with just $ref and Id

**Solution:** Use `IHasRef` interface instead of concrete type:

```csharp
// Both types implement IHasRef
public class EspnLinkDto : IHasRef { public Uri Ref { get; set; } ... }
public class EspnResourceIndexItem : IHasRef { public Uri Ref { get; set; } ... }

public interface IHasRef { Uri Ref { get; } }
```

**Result:** One helper method supports all ESPN reference types (current and future)

---

## Impact Scope

**Processors that benefit:**
1. EventCompetitionDocumentProcessor (already refactored internally)
2. EventCompetitionCompetitorDocumentProcessor (`ProcessScores`, `ProcessLineScores`)
3. EventCompetitionCompetitorLineScoreDocumentProcessor
4. ~15+ other processors with similar patterns

**Estimated reduction:** 500-1000 lines of duplicate code

---

## Why Inheritance Here?

**This is a valid use case because:**
1. ? **Shallow hierarchy** - Single base class, not a deep tree
2. ? **Utility methods only** - Not enforcing behavior
3. ? **Significant DRY benefits** - Eliminates 500+ duplicate lines
4. ? **Maintains flexibility** - Processors can override or ignore methods

**Alternative considered:** Extension methods
- ? Can't access protected fields (`_publishEndpoint`, etc.)
- ? Would need to pass dependencies to every call (verbose)

---

## Implementation Plan

1. **Create base class** in `SportsData.Producer/Application/Documents/Processors/`
2. **Update EventCompetitionDocumentProcessor** to inherit and use helper
3. **Update EventCompetitionCompetitorDocumentProcessor** to inherit and use helper
4. **Verify build** and run tests
5. **Systematically migrate** remaining processors
6. **Remove duplicate code**

---

## Migration Strategy

### Phase 1: Create Base Class ? **COMPLETED**
- [x] Create `DocumentProcessorBase<TDataContext>`
- [x] Add `PublishChildDocumentRequest` helper methods
- [x] Verify compilation
- [x] Add `IHasRef` interface support
- [x] Add comprehensive error handling and logging

### Phase 2: Pilot Migration ? **COMPLETED**
- [x] EventCompetitionDocumentProcessor (10 child documents)
- [x] SeasonTypeDocumentProcessor (2 child documents)
- [x] AthleteSeasonDocumentProcessor (1 child document)
- [x] Verify functionality
- [x] Pattern validated

### Phase 3: Large Processor Refactoring ? **COMPLETED**
- [x] TeamSeasonDocumentProcessor (~343 lines removed)
- [x] SeasonDocumentProcessor (~50 lines removed)
- [x] GroupSeasonDocumentProcessor (~15 lines removed)
- [x] All tests pass
- [x] Build successful

### Phase 4: Remaining Processors ?? **IN PROGRESS**
**Next candidates:**
- [ ] AthleteSeasonStatisticsDocumentProcessor
- [ ] TeamSeasonStatisticsDocumentProcessor
- [ ] EventCompetitionCompetitorStatisticsDocumentProcessor
- [ ] TeamSeasonRecordDocumentProcessor
- [ ] TeamSeasonRecordAtsDocumentProcessor
- [ ] CoachRecordDocumentProcessor

**Then evaluate remaining ~44 processors:**
- [ ] Systematic scan of all processors
- [ ] Identify refactoring opportunities
- [ ] Update processors one by one
- [ ] Remove duplicate code
- [ ] Update tests if needed

---

## Success Criteria

- [x] Base class created with `PublishChildDocumentRequest` helper
- [x] Base class enhanced to support `IHasRef` interface (both `EspnLinkDto` and `EspnResourceIndexItem`)
- [x] Base class includes comprehensive error handling and emoji-based logging
- [ ] All document processors inherit from `DocumentProcessorBase<TDataContext>` (6 of ~50 done)
- [ ] All child document requests use `PublishChildDocumentRequest` helper (partial)
- [x] ~408 lines removed so far (from 3 processors)
- [x] All tests pass
- [x] Build successful

---

## Current Progress (January 2026)

### ? **Refactored Processors**
1. **TeamSeasonDocumentProcessor** - ~343 lines removed (11 ProcessX() methods eliminated)
2. **SeasonDocumentProcessor** - ~50 lines removed
3. **GroupSeasonDocumentProcessor** - ~15 lines removed
4. **EventCompetitionDocumentProcessor** - Already using helper (10 child documents)
5. **SeasonTypeDocumentProcessor** - Already using helper
6. **AthleteSeasonDocumentProcessor** - Already using helper

**Total: 6 processors refactored, ~408 lines removed**

### ?? **Candidates for Refactoring** (Identified but not yet refactored)
1. AthleteSeasonStatisticsDocumentProcessor
2. TeamSeasonStatisticsDocumentProcessor
3. EventCompetitionCompetitorStatisticsDocumentProcessor
4. TeamSeasonRecordDocumentProcessor
5. TeamSeasonRecordAtsDocumentProcessor
6. CoachRecordDocumentProcessor

### ?? **Total Processor Count**
- **~50 document processors** across the codebase
- **~44 remaining** to be evaluated for refactoring potential
- Many may not need refactoring (simple processors without child documents)

---

## Timeline

**Started:** December 26, 2025  
**Current Status:** In Progress (January 2026)  
**Branch:** `feature/refactor-docProcessors`  
**Effort So Far:** ~6 hours (6 processors refactored, ~408 lines removed)

---

## Lessons Learned

### 1. **Interface Polymorphism Was Key**
- Initial implementation used `EspnLinkDto?` concrete type
- Hit compilation errors with `EspnResourceIndexItem` in `TeamSeasonDocumentProcessor`
- Solution: Use `IHasRef` interface - supports both types seamlessly
- **Takeaway:** Design for abstractions, not concrete types

### 2. **Emoji Logging Improves Observability**
- Added emoji-prefixed log messages (`?? PUBLISH_CHILD_REQUEST`, `?? SKIP_CHILD_DOCUMENT`, `? INVALID_CHILD_URI`)
- Makes log scanning much easier in Seq/Loki
- Helps identify patterns quickly during troubleshooting

### 3. **Error Handling Catches Edge Cases**
- Base class includes try/catch for URI parsing failures
- Prevents processor crashes from malformed ESPN data
- Graceful degradation: Log error and skip child, don't fail parent processing

### 4. **Massive LOC Reduction Possible**
- TeamSeasonDocumentProcessor alone: **658 lines ? 315 lines** (52% reduction)
- 11 methods × 24 lines each = 264 lines ? 11 lines (inline calls)
- Pattern scales: More child documents = bigger savings

### 5. **One-by-One Refactoring Is Safer**
- Systematic, file-by-file approach prevents breaking changes
- Build verification after each processor ensures stability
- Easy to identify and fix issues immediately

---

**Status:** ?? **IN PROGRESS**  
**Next Session:** Continue with the 6 identified candidate processors
