# Document Processor Refactoring - Batch Update Task

## What's Already Done ?
- `DocumentProcessorBase<TDataContext>` now implements template method pattern
- `ProcessDocumentCommand.ToLogScope()` method added for standardized logging
- Base class `ProcessAsync()` handles all boilerplate (scope, entry/exit logging, error handling)
- Base class defines `ProcessInternal()` as abstract for processor-specific logic
- **2 processors already updated as examples:**
  - `EventCompetitionDocumentProcessor.cs`
  - `AthletePositionDocumentProcessor.cs`

## What Needs To Be Done ??
Update ~50 remaining document processors to remove boilerplate.

## The Simple Pattern (90% of files)

**DELETE the entire `ProcessAsync` method**

**CHANGE:**
```csharp
private async Task ProcessInternal(ProcessDocumentCommand command)
```

**TO:**
```csharp
protected override async Task ProcessInternal(ProcessDocumentCommand command)
```

That's it!

## Files to Update ??
All `*DocumentProcessor.cs` files in `src/SportsData.Producer/Application/Documents/Processors/Providers/` **EXCEPT:**
- `EventCompetitionDocumentProcessor.cs` (already done)
- `AthletePositionDocumentProcessor.cs` (already done)
- `DocumentProcessorBase.cs` (the base class itself)

## Special Case: TeamSeasonDocumentProcessor ??
This one has custom retry logic. Don't delete `ProcessAsync`, just update it to use:
- `command.ToLogScope()` instead of manual dictionary
- `GetType().Name` for processor name
- `command.ToSafeLogObject()` in error logs

## Verification ??
Run `dotnet build` after completion to ensure no errors.

## Expected Impact ??
- ~1,500-2,000 lines removed
- Consistent logging across all 50+ processors
- Single source of truth for logging behavior

## Reference Examples

### EventCompetitionDocumentProcessor.cs (Already Updated)
```csharp
protected override async Task ProcessInternal(ProcessDocumentCommand command)
{
    // actual processing logic - no boilerplate!
    var externalDto = command.Document.FromJson<EspnEventCompetitionDto>();
    // ... rest of logic
}
```

### AthletePositionDocumentProcessor.cs (Already Updated)
```csharp
protected override async Task ProcessInternal(ProcessDocumentCommand command)
{
    // actual processing logic - no boilerplate!
    var externalProviderDto = command.Document.FromJson<EspnAthletePositionDto>();
    // ... rest of logic
}
```
