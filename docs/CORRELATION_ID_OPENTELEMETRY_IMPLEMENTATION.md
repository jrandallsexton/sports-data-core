# Correlation ID Logging - Implementation Guide

**Date:** December 26, 2025  
**Status:** ? **IN PROGRESS**

---

## Summary

Implementing consistent correlation ID logging across all document processors using OpenTelemetry W3C Trace Context propagation.

**Key Pattern:**
- Extract `Activity.Current.TraceId` as correlation ID
- `DocumentProcessorBase.ProcessAsync()` calls `_logger.BeginScope(command.ToLogScope())` — individual processors no longer implement BeginScope
- `ToLogScope()` returns 12 properties: `AttemptCount`, `CorrelationId`, `DocumentType`, `MessageId`, `NotifyOnCompletion`, `ParentId`, `Ref`, `SeasonYear`, `SourceDataProvider`, `SourceUri`, `Sport`, `UrlHash`
- Log messages are generic: `"Processing started."`, `"Processing completed."`, `"Processing failed."` — no processor-specific names
- No redundant values in individual log statements (rely on scope)

---

## Established Pattern

### ProcessAsync Template (in DocumentProcessorBase)
```csharp
public virtual async Task ProcessAsync(ProcessDocumentCommand command)
{
    using (_logger.BeginScope(command.ToLogScope()))
    {
        _logger.LogInformation("Processing started.");

        try
        {
            await ProcessInternal(command);
            _logger.LogInformation("Processing completed.");
        }
        catch (ExternalDocumentNotSourcedException retryEx)
        {
            _logger.LogWarning(retryEx,
                "Dependency not ready (attempt {Attempt}). Will retry later.",
                command.AttemptCount + 1);
            // Retry logic — republishes with incremented AttemptCount
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing failed.");
            throw;
        }
    }
}
```
Concrete processors only implement `ProcessInternal()` — they do not need their own BeginScope or start/completion logging.

### Key Rules
1. ? **BeginScope handled by base class** - `command.ToLogScope()` provides 12 standardized fields (SeasonYear, ParentId, CorrelationId, etc.)
2. ? **No {@Command}** - replaced with safe `ToLogScope()` to avoid logging full JSON documents
3. ? **Generic log messages** - `"Processing started."` / `"Processing completed."` / `"Processing failed."` — no processor-specific names
4. ? **No redundant values** - Don't repeat scope fields in individual logs
5. ? **Correct logger type** - `ILogger<ActualProcessorName<TDataContext>>`
6. ? **Processors only implement ProcessInternal()** - base class handles scope, entry/exit/error logging

---

## Processors Status

### ? Completed - EventCompetition Family (COMPLETE!)
1. **EventDocumentProcessor** - Reference implementation
2. **EventCompetitionDocumentProcessor** - Fixed logger type, enhanced scope
3. **EventCompetitionCompetitorDocumentProcessor** - Enhanced BeginScope, fixed logging messages, added completion logging
4. **EventCompetitionCompetitorScoreDocumentProcessor** - Enhanced BeginScope, added start/completion logging, contextual new vs update logging
5. **EventCompetitionCompetitorLineScoreDocumentProcessor** - Enhanced BeginScope, fixed start message, added completion logging, contextual Period logging
6. **EventCompetitionOddsDocumentProcessor** - Enhanced BeginScope, added start/completion logging, content hash deduplication logging
7. **EventCompetitionStatusDocumentProcessor** - Enhanced BeginScope, added start/completion logging, status change tracking, event publishing logging
8. **EventCompetitionSituationDocumentProcessor** - Enhanced BeginScope, added start/completion logging, down/distance/yardLine contextual logging
9. **EventCompetitionBroadcastDocumentProcessor** - Enhanced BeginScope, added start/completion logging, broadcast deduplication and count logging
10. **EventCompetitionPlayDocumentProcessor** - Enhanced BeginScope, added start/completion logging, play-by-play detail logging, event publishing for live games
11. **EventCompetitionLeadersDocumentProcessor** - Enhanced BeginScope, added start/completion logging, leader category tracking, dependency resolution caching, stats count logging
12. **EventCompetitionPredictionDocumentProcessor** - Enhanced BeginScope, added start/completion logging, metric discovery tracking, hard replace operations logging
13. **EventCompetitionProbabilityDocumentProcessor** - Enhanced BeginScope, added start/completion logging, change detection logging, win probability tracking, event publishing
14. **EventCompetitionPowerIndexDocumentProcessor** - Enhanced BeginScope, added start/completion logging, power index discovery tracking, stats count logging
15. **EventCompetitionDriveDocumentProcessor** - Enhanced BeginScope, added start/completion logging, drive/play relationship tracking, play linking vs requesting counts

### ?? To Review - Other Document Types
16. TeamSeasonDocumentProcessor
17. AthleteSeasonDocumentProcessor
18. SeasonDocumentProcessor
19. VenueDocumentProcessor
20. ... (many more processors across different document types)

---

## ?? MAJOR MILESTONE ACHIEVED! ??

### EventCompetition Family - 100% Complete! ?

All 15 processors in the Event ? EventCompetition hierarchy now have consistent, correlation-ID-aware logging!

**Complete Processing Flow:**
```
Event (Contest)                           ? #1
  ?? EventCompetition                     ? #2
      ?? Competitor (x2)                  ? #3
      ?   ?? CompetitorScore              ? #4
      ?   ?? CompetitorLineScore          ? #5
      ?? Odds                             ? #6
      ?? Status                           ? #7
      ?? Situation                        ? #8
      ?? Broadcast                        ? #9
      ?? Play (100-200 per game)          ? #10
      ?? Leaders                          ? #11
      ?? Prediction                       ? #12
      ?? Probability                      ? #13
      ?? PowerIndex                       ? #14
      ?? Drive (20-30 per game)           ? #15
          ?? Contains Plays (linked back)
```

---

## Progress Summary

**Completed:** 15 of 50+ total processors ??  
**Build Status:** ? Successful  
**Pattern:** ? 100% Consistent across EventCompetition family  

**Estimated Total Processors:** ~50-60 across all document types  
**EventCompetition Coverage:** 100% ?  
**Overall Progress:** ~30% of all processors

---

## Key Achievements

### 1. Consistency Across All 15 Processors ?
Every processor in the EventCompetition family now inherits from `DocumentProcessorBase`:
- ? BeginScope with `command.ToLogScope()` (12 fields including SeasonYear, ParentId, CorrelationId, etc.) handled by base class
- ? Generic start/completion/error logging in base class: `"Processing started."`, `"Processing completed."`, `"Processing failed."`
- ? No `{@Command}` — safe logging only
- ? Contextual logging specific to each processor's business logic in `ProcessInternal()`
- ? Correct logger types (`ILogger<ActualProcessorName<TDataContext>>`)

### 2. Complete Document Flow Coverage ?
Successfully standardized logging for the entire game event processing pipeline:
- **Foundation:** Event ? Competition
- **Teams:** Competitor ? Score, LineScore
- **Game State:** Status, Situation, Broadcast
- **Analytics:** Odds, Probability, Prediction, PowerIndex, Leaders
- **Play-by-Play:** Play, Drive (with bidirectional linking)

### 3. Business Value Delivered ?
- **Seq Queries**: Easy filtering by CorrelationId, DocumentType, Season
- **Debugging**: Clear start/end markers for each processor
- **Performance**: Can track processor duration with start/completion logs
- **Business Visibility**: Context-specific data (scores, probabilities, status changes, drives, plays)
- **Distributed Tracing**: OpenTelemetry TraceId propagation across entire processing pipeline
- **Real-Time Tracking**: Win probabilities, live game status, play-by-play updates

---

## Next Steps

**Ready to expand beyond EventCompetition!**

Remaining processor families to standardize:
1. **TeamSeason Family** - Team rosters, statistics, records, rankings
2. **AthleteSeason Family** - Player statistics, injuries, awards
3. **Season Family** - Season configuration, weeks, phases
4. **Venue Family** - Stadium/field information
5. **GroupSeason Family** - Conference standings
6. **Coach Family** - Coaching staff
7. **Franchise Family** - Team reference data

**Estimated remaining:** ~35-45 processors

---

## Pattern Template (Reference)

The base class `DocumentProcessorBase<TDataContext>` handles all of this automatically.
Concrete processors only implement `ProcessInternal(ProcessDocumentCommand command)`.

```csharp
// In DocumentProcessorBase.ProcessAsync() — DO NOT duplicate in processors:
using (_logger.BeginScope(command.ToLogScope()))
{
    _logger.LogInformation("Processing started.");
    try
    {
        await ProcessInternal(command);
        _logger.LogInformation("Processing completed.");
    }
    catch (ExternalDocumentNotSourcedException retryEx)
    {
        _logger.LogWarning(retryEx,
            "Dependency not ready (attempt {Attempt}). Will retry later.",
            command.AttemptCount + 1);
        // Republish with incremented AttemptCount
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Processing failed.");
        throw;
    }
}
