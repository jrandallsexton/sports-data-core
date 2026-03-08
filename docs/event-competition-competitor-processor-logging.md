# EventCompetitionCompetitorDocumentProcessor Enhanced Logging

## Overview

Added comprehensive logging to `EventCompetitionCompetitorDocumentProcessor` to track the complete flow of processing competitor documents and their child documents (Score, LineScore, Roster, Statistics, Record).

## Purpose

The logging was added to diagnose why downstream `DocumentRequested` events for `EventCompetitionCompetitorScore` and `EventCompetitionCompetitorLineScore` might not be published or processed.

## Key Question Being Investigated

**Are the `DocumentRequested` events being published but not reaching the child processors?**

Possible causes:
1. Events not being published at all
2. Events published but not persisted to outbox
3. Events in outbox but not sent to Service Bus
4. Events sent but not consumed by handlers
5. Handlers consuming but child processors failing

## Added Logging

### 1. Entity Creation/Update Flow

**New Entity Created:**
```
?? CREATE_COMPETITOR: Creating new CompetitionCompetitor. 
   CompetitionId={guid}, FranchiseSeasonId={guid}, HomeAway=home/away
? COMPETITOR_CREATED: CompetitionCompetitor entity created. CompetitorId={guid}
```

**Existing Entity Updated:**
```
?? UPDATE_COMPETITOR: Updating existing CompetitionCompetitor. 
   CompetitorId={guid}, HomeAway=home/away
```

### 2. Child Document Processing Flow

**Initiation:**
```
?? PROCESS_CHILD_DOCUMENTS: Processing child documents for competitor.
   CompetitorId={guid}, IsNew={true/false}
```

**Completion:**
```
? CHILD_DOCUMENTS_COMPLETED: Child document processing completed. CompetitorId={guid}
```

### 3. Child Document Publishing (Score, LineScore, Roster, Statistics, Record)

All 5 child document types use the base class `PublishChildDocumentRequest` method, which provides standardized logging:

**Skipping (No Ref):**
```
SKIP_CHILD_DOCUMENT: No reference found.
```
(Logged at Debug level with `ChildDocumentType` and `ParentId` in scope)

**Publishing Request:**
```
? CHILD_REQUEST_PUBLISHED: DocumentRequested published. UrlHash={hash}
```

The processor spawns up to 5 child document types:
1. `EventCompetitionCompetitorScore`
2. `EventCompetitionCompetitorLineScore`
3. `EventCompetitionCompetitorRoster`
4. `EventCompetitionCompetitorStatistics`
5. `EventCompetitionCompetitorRecord`

### 5. Database Save & Outbox Flush

**Before Save:**
```
?? SAVING_CHANGES: About to call SaveChangesAsync to persist CompetitionCompetitor and flush outbox. 
   CompetitionId={guid}, HasPendingChanges=true/false
```

**After Save:**
```
? SAVE_COMPLETED: SaveChangesAsync completed. All outbox messages should now be flushed to service bus. 
   CompetitionId={guid}
```

## Expected Log Flow (Happy Path)

### For New Competitor

```
[Time] Processing started.                                            (from base class)
[Time] Processing new CompetitionCompetitor entity. Ref={url}
[Time] CREATE_COMPETITOR: Creating new CompetitionCompetitor. CompetitionId={guid}, FranchiseSeasonId={guid}, HomeAway=home
[Time] COMPETITOR_CREATED: CompetitionCompetitor entity created. CompetitorId={guid}
[Time] PROCESS_CHILD_DOCUMENTS: Processing child documents for competitor. CompetitorId={guid}, IsNew=True
[Time] CHILD_REQUEST_PUBLISHED: DocumentRequested published. UrlHash={hash}    (Score)
[Time] CHILD_REQUEST_PUBLISHED: DocumentRequested published. UrlHash={hash}    (LineScore)
[Time] CHILD_REQUEST_PUBLISHED: DocumentRequested published. UrlHash={hash}    (Roster)
[Time] CHILD_REQUEST_PUBLISHED: DocumentRequested published. UrlHash={hash}    (Statistics)
[Time] CHILD_REQUEST_PUBLISHED: DocumentRequested published. UrlHash={hash}    (Record)
[Time] CHILD_DOCUMENTS_COMPLETED: Child document processing completed. CompetitorId={guid}
[Time] SAVING_CHANGES: About to call SaveChangesAsync... HasPendingChanges=true
[Time] SAVE_COMPLETED: SaveChangesAsync completed. All outbox messages should now be flushed...
[Time] Processing completed.                                          (from base class)
```

### For Updated Competitor

```
[Time] Processing started.                                            (from base class)
[Time] Processing CompetitionCompetitor update. CompetitorId={guid}, Ref={url}
[Time] UPDATE_COMPETITOR: Updating existing CompetitionCompetitor. CompetitorId={guid}, HomeAway=home
[Time] PROCESS_CHILD_DOCUMENTS: Processing child documents for competitor. CompetitorId={guid}, IsNew=False
[Time] CHILD_REQUEST_PUBLISHED: DocumentRequested published. UrlHash={hash}    (per child type, if ShouldSpawn passes)
[Time] CHILD_DOCUMENTS_COMPLETED: Child document processing completed. CompetitorId={guid}
[Time] SAVING_CHANGES: About to call SaveChangesAsync... HasPendingChanges=true
[Time] SAVE_COMPLETED: SaveChangesAsync completed...
[Time] Processing completed.                                          (from base class)
```

## Diagnostic Queries in Seq

### 1. Check if Child Document Events Are Being Published

```
@m like '%CHILD_REQUEST_PUBLISHED%'
| select ChildDocumentType, ParentId, @t
```

**What This Tells You:**
- ? If you see these logs, events ARE being published
- ? If you don't see these logs, check for SKIP_CHILD_DOCUMENT or PROCESS_CHILD_DOCUMENTS logs

### 2. Check if Refs Are Missing in DTO

```
@m like '%SKIP_CHILD_DOCUMENT%'
| select ChildDocumentType, ParentId, @m
```

**What This Tells You:**
- If you see many SKIP logs, the ESPN DTOs don't have refs for those child types
- This is NORMAL for some competitors (e.g., pre-game)

### 3. Verify Outbox Flush

```
@m like '%SAVING_CHANGES%' OR @m like '%SAVE_COMPLETED%'
| select CompetitionId, HasPendingChanges, @t
```

**What This Tells You:**
- If `HasPendingChanges=false` before SAVING_CHANGES, no outbox messages were added
- If `HasPendingChanges=true`, outbox should have messages

### 4. Track Complete Flow for a Competitor

```
CompetitorId="{specific-guid}"
| select @t, @m
| order by @t asc
```

This shows the complete timeline for a single competitor.

### 5. Find Competitors That Published Child Document Events

```
@m like '%CHILD_DOCUMENTS_COMPLETED%'
| select CompetitorId
```

## Troubleshooting Guide

### Issue: No Child Document Events Published

**Symptoms:**
- See `PROCESS_CHILD_DOCUMENTS`
- See `SKIP_CHILD_DOCUMENT` for all child types
- Never see `CHILD_REQUEST_PUBLISHED`

**Diagnosis:**
The ESPN DTO doesn't have refs for child document types. This might be normal for:
- Pre-game competitors
- Incomplete data from ESPN
- Certain game statuses

**Action:**
Check the source ESPN JSON to confirm refs are present:
```csharp
var dto = command.Document.FromJson<EspnEventCompetitionCompetitorDto>();
// dto.Score?.Ref, dto.Linescores?.Ref, dto.Roster?.Ref, dto.Statistics?.Ref, dto.Record?.Ref
```

### Issue: Events Published But Never Processed

**Symptoms:**
- See `CHILD_REQUEST_PUBLISHED`
- See `SAVE_COMPLETED`
- Never see logs from `EventCompetitionCompetitorScoreDocumentProcessor`

**Diagnosis:**
1. **Check if events reached Service Bus:**
   - Look in Azure Portal ? Service Bus ? Topics/Queues ? Messages
   
2. **Check if DocumentRequestedHandler consumed them:**
   - Search Provider logs for `DocumentRequested received` with the UrlHash

3. **Check if ResourceIndexItemProcessor processed them:**
   - Search Provider logs for `Processing resource index item` with the UrlHash

4. **Check if DocumentCreated was published back:**
   - Search Provider logs for `DocumentCreated event published` with the UrlHash

5. **Check if Producer consumed DocumentCreated:**
   - Search Producer logs for `?? HANDLER_ENTRY` with matching DocumentType

**Action:**
Trace the UrlHash through the complete pipeline:
```
1. Producer: ? SCORE_REQUEST_PUBLISHED (UrlHash=abc123)
2. Provider: DocumentRequested received (UrlHash=abc123)
3. Provider: Processing resource index item (UrlHash=abc123)
4. Provider: DocumentCreated event published (UrlHash=abc123)
5. Producer: ?? HANDLER_ENTRY (DocumentType=EventCompetitionCompetitorScore)
6. Producer: ?? PROCESSOR_ENTRY (EventCompetitionCompetitorScoreDocumentProcessor)
```

### Issue: SaveChangesAsync Has No Changes

**Symptoms:**
- See `?? SAVING_CHANGES: HasPendingChanges=false`

**Diagnosis:**
Either:
1. Competitor entity already existed (update scenario)
2. No downstream events were published (Score/LineScore refs were null)

**Action:**
- Check if this is an update: Look for `?? UPDATE_COMPETITOR`
- Check if refs were skipped: Look for `?? SKIP_SCORES` and `?? SKIP_LINESCORES`

## Key Insights

### 1. Outbox Pattern

The `DocumentRequested` events are published using the outbox pattern:
```csharp
await _publishEndpoint.Publish(new DocumentRequested(...));
// Event added to outbox in-memory

await _dataContext.SaveChangesAsync();
// Outbox interceptor persists messages to OutboxMessage table
// Background worker sends to Service Bus
```

### 2. Event Flow

```
EventCompetitionCompetitorDocumentProcessor
  ? Publish DocumentRequested(Score)
  ? Publish DocumentRequested(LineScore)
  ? Publish DocumentRequested(Roster)
  ? Publish DocumentRequested(Statistics)
  ? Publish DocumentRequested(Record)
  ? SaveChangesAsync (flushes outbox)
  
Provider.DocumentRequestedHandler
  ? Receives DocumentRequested(Score)
  ? Fetches from ESPN
  ? Stores in blob
  ? Publishes DocumentCreated(Score)
  
Producer.DocumentCreatedHandler
  ? Receives DocumentCreated(Score)
  ? Enqueues Hangfire job
  
Hangfire
  ? Executes EventCompetitionCompetitorScoreDocumentProcessor
  ? Persists score data
```

### 3. Why Logging Was Critical

Without this logging, you couldn't tell:
- ? Whether events were published
- ? Whether DTOs had the required refs
- ? Whether SaveChangesAsync was called
- ? Whether outbox had pending changes
- ? Which stage of the pipeline failed

## Expected Counts

For a typical game with 2 competitors:

**Per Competitor:**
- 1x `CREATE_COMPETITOR` or `UPDATE_COMPETITOR`
- 1x `PROCESS_CHILD_DOCUMENTS`
- 0-5x `CHILD_REQUEST_PUBLISHED` (one per child type with a valid ref: Score, LineScore, Roster, Statistics, Record)
- 0-5x `SKIP_CHILD_DOCUMENT` (for child types with no ref)
- 1x `CHILD_DOCUMENTS_COMPLETED`

**Per Processor Run:**
- 1x `SAVING_CHANGES`
- 1x `SAVE_COMPLETED`

## Related Processors

This same logging pattern should be applied to:
- `EventCompetitionDocumentProcessor` (publishes many downstream types)
- `EventDocumentProcessor` (publishes EventCompetition)
- Any processor that publishes `DocumentRequested` events

## Emoji Legend for Filtering

| Emoji | Meaning | Seq Filter |
|-------|---------|------------|
| ?? | New Entity | `@m like '%??%'` |
| ?? | Update Entity | `@m like '%??%'` |
| ?? | Downstream Processing | `@m like '%??%'` |
| ?? | Scores | `@m like '%??%'` |
| ?? | LineScores | `@m like '%??%'` |
| ?? | Publishing | `@m like '%??%'` |
| ? | Success | `@m like '%?%'` |
| ?? | Skipped | `@m like '%??%'` |
| ?? | Saving | `@m like '%??%'` |

## Next Steps

1. **Run a test** with Provider ? Producer flow
2. **Search logs** for the emoji prefixes
3. **Trace a specific UrlHash** end-to-end
4. **Verify outbox messages** are created in database
5. **Check Service Bus** for message delivery

If events are published but not processed, the issue is likely in Provider's handling or Service Bus configuration, not in this processor.
