# EventCompetitionCompetitorDocumentProcessor Enhanced Logging

## Overview

Added comprehensive logging to `EventCompetitionCompetitorDocumentProcessor` to track the complete flow of processing competitor documents and their downstream documents (Scores and LineScores).

## Purpose

The logging was added to diagnose why downstream `DocumentRequested` events for `EventCompetitionCompetitorScore` and `EventCompetitionCompetitorLineScore` might not be published or processed.

## Key Question Being Investigated

**Are the `DocumentRequested` events being published but not reaching the downstream processors?**

Possible causes:
1. Events not being published at all
2. Events published but not persisted to outbox
3. Events in outbox but not sent to Service Bus
4. Events sent but not consumed by handlers
5. Handlers consuming but downstream processors failing

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

### 2. Downstream Processing Flow

**Initiation:**
```
?? PROCESS_DOWNSTREAM: Processing downstream documents (Scores and LineScores). 
   CompetitorId={guid}
```

**Completion:**
```
? DOWNSTREAM_COMPLETED: Downstream processing completed. CompetitorId={guid}
```

### 3. Score Processing

**Checking for Scores:**
```
?? PROCESS_SCORES: Checking for competitor score. 
   CompetitorId={guid}, HasScoreRef=true/false
```

**Skipping (No Ref):**
```
?? SKIP_SCORES: No score reference found in DTO. CompetitorId={guid}
```

**Publishing Request:**
```
?? PUBLISH_SCORE_REQUEST: Publishing DocumentRequested for competitor score. 
   CompetitorId={guid}, ScoreUrl={url}, UrlHash={hash}
? SCORE_REQUEST_PUBLISHED: DocumentRequested published for competitor score. 
   CompetitorId={guid}, DocumentType=EventCompetitionCompetitorScore
```

### 4. LineScore Processing

**Checking for LineScores:**
```
?? PROCESS_LINESCORES: Checking for competitor line scores. 
   CompetitorId={guid}, HasLineScoresRef=true/false
```

**Skipping (No Ref):**
```
?? SKIP_LINESCORES: No line scores reference found in DTO. CompetitorId={guid}
```

**Publishing Request:**
```
?? PUBLISH_LINESCORES_REQUEST: Publishing DocumentRequested for competitor line scores. 
   CompetitorId={guid}, LineScoresUrl={url}, UrlHash={hash}
? LINESCORES_REQUEST_PUBLISHED: DocumentRequested published for competitor line scores. 
   CompetitorId={guid}, DocumentType=EventCompetitionCompetitorLineScore
```

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
[Time] EventCompetitionCompetitorDocumentProcessor started. Ref={url}
[Time] Processing new CompetitionCompetitor entity. Ref={url}
[Time] ?? CREATE_COMPETITOR: Creating new CompetitionCompetitor. CompetitionId={guid}, FranchiseSeasonId={guid}, HomeAway=home
[Time] ? COMPETITOR_CREATED: CompetitionCompetitor entity created. CompetitorId={guid}
[Time] ?? PROCESS_DOWNSTREAM: Processing downstream documents (Scores and LineScores). CompetitorId={guid}
[Time] ?? PROCESS_SCORES: Checking for competitor score. CompetitorId={guid}, HasScoreRef=true
[Time] ?? PUBLISH_SCORE_REQUEST: Publishing DocumentRequested for competitor score. CompetitorId={guid}, ScoreUrl={url}, UrlHash={hash}
[Time] ? SCORE_REQUEST_PUBLISHED: DocumentRequested published for competitor score. CompetitorId={guid}
[Time] ?? PROCESS_LINESCORES: Checking for competitor line scores. CompetitorId={guid}, HasLineScoresRef=true
[Time] ?? PUBLISH_LINESCORES_REQUEST: Publishing DocumentRequested for competitor line scores. CompetitorId={guid}, LineScoresUrl={url}, UrlHash={hash}
[Time] ? LINESCORES_REQUEST_PUBLISHED: DocumentRequested published for competitor line scores. CompetitorId={guid}
[Time] ? DOWNSTREAM_COMPLETED: Downstream processing completed. CompetitorId={guid}
[Time] ?? SAVING_CHANGES: About to call SaveChangesAsync... HasPendingChanges=true
[Time] ? SAVE_COMPLETED: SaveChangesAsync completed. All outbox messages should now be flushed...
[Time] EventCompetitionCompetitorDocumentProcessor completed.
```

### For Updated Competitor

```
[Time] EventCompetitionCompetitorDocumentProcessor started. Ref={url}
[Time] Processing CompetitionCompetitor update. CompetitorId={guid}, Ref={url}
[Time] ?? UPDATE_COMPETITOR: Updating existing CompetitionCompetitor. CompetitorId={guid}, HomeAway=home
[Time] ?? PROCESS_DOWNSTREAM: Processing downstream documents (Scores and LineScores). CompetitorId={guid}
[Time] ?? PROCESS_SCORES: Checking for competitor score. CompetitorId={guid}, HasScoreRef=true
[Time] ?? PUBLISH_SCORE_REQUEST: Publishing DocumentRequested for competitor score...
[Time] ? SCORE_REQUEST_PUBLISHED: DocumentRequested published for competitor score...
[Time] ?? PROCESS_LINESCORES: Checking for competitor line scores. CompetitorId={guid}, HasLineScoresRef=true
[Time] ?? PUBLISH_LINESCORES_REQUEST: Publishing DocumentRequested for competitor line scores...
[Time] ? LINESCORES_REQUEST_PUBLISHED: DocumentRequested published for competitor line scores...
[Time] ? DOWNSTREAM_COMPLETED: Downstream processing completed. CompetitorId={guid}
[Time] ?? SAVING_CHANGES: About to call SaveChangesAsync... HasPendingChanges=true
[Time] ? SAVE_COMPLETED: SaveChangesAsync completed...
[Time] EventCompetitionCompetitorDocumentProcessor completed.
```

## Diagnostic Queries in Seq

### 1. Check if Downstream Events Are Being Published

```
@m like '%SCORE_REQUEST_PUBLISHED%' OR @m like '%LINESCORES_REQUEST_PUBLISHED%'
| select CompetitorId, DocumentType, @t
```

**What This Tells You:**
- ? If you see these logs, events ARE being published
- ? If you don't see these logs, check for SKIP or PROCESS logs

### 2. Check if Refs Are Missing in DTO

```
@m like '%SKIP_SCORES%' OR @m like '%SKIP_LINESCORES%'
| select CompetitorId, @m
```

**What This Tells You:**
- If you see many SKIP logs, the ESPN DTOs don't have Score/LineScore refs
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

### 5. Find Competitors That Published Downstream Events

```
@m like '%DOWNSTREAM_COMPLETED%'
AND @m like '%SCORE_REQUEST_PUBLISHED%'
| select CompetitorId
```

## Troubleshooting Guide

### Issue: No Downstream Events Published

**Symptoms:**
- See `?? PROCESS_DOWNSTREAM` 
- See `?? SKIP_SCORES` and `?? SKIP_LINESCORES`
- Never see `?? PUBLISH_*_REQUEST`

**Diagnosis:**
The ESPN DTO doesn't have Score or LineScore refs. This might be normal for:
- Pre-game competitors
- Incomplete data from ESPN
- Certain game statuses

**Action:**
Check the source ESPN JSON to confirm refs are present:
```csharp
var dto = command.Document.FromJson<EspnEventCompetitionCompetitorDto>();
// dto.Score?.Ref should not be null
// dto.Linescores?.Ref should not be null
```

### Issue: Events Published But Never Processed

**Symptoms:**
- See `? SCORE_REQUEST_PUBLISHED`
- See `? SAVE_COMPLETED`
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
- 1x `PROCESS_DOWNSTREAM`
- 1x `PROCESS_SCORES` (may be SKIP if no ref)
- 0-1x `SCORE_REQUEST_PUBLISHED`
- 1x `PROCESS_LINESCORES` (may be SKIP if no ref)
- 0-1x `LINESCORES_REQUEST_PUBLISHED`
- 1x `DOWNSTREAM_COMPLETED`

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
