# DocumentProcessorBase - Refactoring Plan

**Date:** December 26, 2025  
**Status:** ? **COMPLETED**

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
    /// </summary>
    protected async Task PublishChildDocumentRequest(
        ProcessDocumentCommand command,
        EspnLinkDto? linkDto,
        string parentId,
        DocumentType documentType,
        string causationId)
    {
        if (linkDto?.Ref is null)
            return;

        await _publishEndpoint.Publish(new DocumentRequested(
            Id: HashProvider.GenerateHashFromUri(linkDto.Ref),
            ParentId: parentId,
            Uri: linkDto.Ref.ToCleanUri(),
            Sport: command.Sport,
            SeasonYear: command.Season,
            DocumentType: documentType,
            SourceDataProvider: command.SourceDataProvider,
            CorrelationId: command.CorrelationId,
            CausationId: causationId
        ));
    }

    // Overload for typed parent IDs
    protected async Task PublishChildDocumentRequest<TParentId>(
        ProcessDocumentCommand command,
        EspnLinkDto? linkDto,
        TParentId parentId,
        DocumentType documentType,
        string causationId)
    {
        await PublishChildDocumentRequest(
            command,
            linkDto,
            parentId?.ToString() ?? string.Empty,
            documentType,
            causationId);
    }
}
```

### After Refactor
```csharp
// EventCompetitionCompetitorDocumentProcessor : DocumentProcessorBase<TeamSportDataContext>
private async Task ProcessScores(...)
{
    await PublishChildDocumentRequest(
        command,
        externalProviderDto.Score,
        competitionCompetitorId,
        DocumentType.EventCompetitionCompetitorScore,
        CausationId.Producer.EventCompetitionCompetitorDocumentProcessor);
}

// 1 line vs 10 lines! 90% reduction
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
- ~500-1000 lines of code eliminated
- Easier to understand (less boilerplate)

### 4. Type Safety ?
- Generic `TParentId` allows `Guid`, `string`, etc.
- Compiler ensures correct types

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

### Phase 1: Create Base Class
- [x] Create `DocumentProcessorBase<TDataContext>`
- [x] Add `PublishChildDocumentRequest` helper methods
- [x] Verify compilation

### Phase 2: Pilot Migration (2-3 processors)
- [x] EventCompetitionDocumentProcessor
- [x] EventCompetitionCompetitorDocumentProcessor
- [x] Verify functionality
- [x] Gather feedback

### Phase 3: Full Migration (remaining processors)
- [x] Systematic migration of remaining 15+ processors
- [x] Remove duplicate code
- [x] Update tests if needed

---

## Success Criteria

- [x] All document processors inherit from `DocumentProcessorBase<TDataContext>`
- [x] All child document requests use `PublishChildDocumentRequest` helper
- [x] ~500-1000 lines of duplicate code removed
- [x] All tests pass
- [x] Build successful

---

## Timeline

**Completed:** January 2025  
**Branch:** `feature/document-processor-base-refactor`  
**Actual Effort:** 4 hours

---

**Status:** ? **COMPLETED**  
**Result:** All 45 active document processors successfully migrated
