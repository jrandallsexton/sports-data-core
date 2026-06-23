# Enhanced DocumentCreatedHandler Logging Guide

## Overview

Comprehensive logging has been added to the `DocumentCreatedHandler` and `DocumentCreatedProcessor` to track the complete flow from event reception to document processing. This will help identify where processing stops.

This guide combines the general document-processing logging guide with the processor-specific additions for `EventCompetitionCompetitorDocumentProcessor` (formerly `event-competition-competitor-processor-logging.md`). The general flow forms the spine; the processor-specific section is folded in as an appendix near the end.

## Log Flow

### Phase 1: Event Reception (DocumentCreatedHandler)

When a `DocumentCreated` event is received from Provider, you'll see these logs in sequence:

```
HANDLER_ENTRY: DocumentCreated event received.
HANDLER_ENQUEUE_IMMEDIATE: First attempt - enqueueing background job immediately.
HANDLER_ENQUEUED: Background job enqueued successfully. {JobId}
HANDLER_EXIT: Handler completed successfully (immediate enqueue).
```

**For retry attempts (AttemptCount > 0):**
```
HANDLER_ENTRY: DocumentCreated event received.
HANDLER_SCHEDULE_DELAYED: Scheduling retry with backoff.
HANDLER_SCHEDULED: Background job scheduled successfully. HangfireJobId={HangfireJobId}
HANDLER_EXIT: Handler completed successfully (scheduled retry).
```

**Note:** The handler uses `IProvideBackgroundJobs` abstraction (not `IBackgroundJobClient` directly) for Hangfire job scheduling.

### Phase 2: Background Job Execution (DocumentCreatedProcessor)

Once Hangfire picks up the job, you'll see (all log messages use `DOC_CREATED_` prefix):

```
DOC_CREATED_PROCESSOR_ENTRY: Hangfire job started.
DOC_CREATED_PROCESSOR_DOCUMENT_INLINE: Document included in event payload.
  (or)
DOC_CREATED_PROCESSOR_FETCH_DOCUMENT: Document not in payload, fetching from Provider.
DOC_CREATED_PROCESSOR_DOCUMENT_FETCHED: Document fetched from Provider successfully.
DOC_CREATED_PROCESSOR_GET_PROCESSOR: Looking up document processor from factory.
DOC_CREATED_PROCESSOR_FOUND: Document processor found.
DOC_CREATED_PROCESSOR_EXECUTE: Executing document-specific processor.
DOC_CREATED_PROCESSOR_EXECUTE_COMPLETED: Document-specific processor completed.
DOC_CREATED_PROCESSOR_COMPLETED: Document processing completed successfully.
```

**Inline document path:** The processor checks the `DocumentJson` property on the event first. If the document JSON is included inline (within size limits), it uses that directly instead of fetching from Provider.

### Phase 3: Document-Specific Processor

Each document type has its own processor (e.g., `EventDocumentProcessor`, `EventCompetitionStatusDocumentProcessor`). These will log their own start/complete messages.

## Log Message Prefix Conventions

**Handler (`DocumentCreatedHandler`):** Uses plain-text prefixes without emojis:
- `HANDLER_ENTRY`, `HANDLER_ENQUEUE_IMMEDIATE`, `HANDLER_ENQUEUED`, `HANDLER_SCHEDULE_DELAYED`, `HANDLER_SCHEDULE_IMMEDIATE`, `HANDLER_SCHEDULED`, `HANDLER_EXIT`, `HANDLER_MAX_RETRIES`, `HANDLER_EXCEPTION`

**Processor (`DocumentCreatedProcessor`):** Uses emoji-prefixed `DOC_CREATED_` tokens (each token starts with an emoji followed by the identifier):
- `🚀 DOC_CREATED_PROCESSOR_ENTRY`, `📦 DOC_CREATED_PROCESSOR_DOCUMENT_INLINE`, `📥 DOC_CREATED_PROCESSOR_FETCH_DOCUMENT`, `✅ DOC_CREATED_PROCESSOR_DOCUMENT_FETCHED`, `❌ DOC_CREATED_PROCESSOR_DOCUMENT_NULL`, `❌ DOC_CREATED_PROCESSOR_DOCUMENT_EMPTY`, `🔍 DOC_CREATED_PROCESSOR_GET_PROCESSOR`, `✅ DOC_CREATED_PROCESSOR_FOUND`, `⚙️ DOC_CREATED_PROCESSOR_EXECUTE`, `✅ DOC_CREATED_PROCESSOR_EXECUTE_COMPLETED`, `✅ DOC_CREATED_PROCESSOR_COMPLETED`, `💥 DOC_CREATED_PROCESSOR_FAILED`

When filtering in Seq, include the emoji prefix in your query (e.g., `@m like '%DOC_CREATED_PROCESSOR_ENTRY%'` matches the emoji-prefixed form).

## Troubleshooting Guide

### Issue 1: Event Received but No Background Job Enqueued

**Symptoms:**
- You see `HANDLER_ENTRY` but no `HANDLER_ENQUEUED`
- Or you see `HANDLER_EXCEPTION`

**Possible Causes:**
1. Exception in `_backgroundJobProvider.Enqueue()` call
2. Hangfire configuration issue
3. Database connection issue (Hangfire uses DB)

**What to check:**
```
# Search logs for:
HANDLER_EXCEPTION
```

**Actions:**
- Check Hangfire dashboard to see if jobs are being created
- Verify Hangfire database connection string
- Check for exceptions in the logs

### Issue 2: Background Job Enqueued but Never Executes

**Symptoms:**
- You see `HANDLER_ENQUEUED` with a job ID
- But you never see `DOC_CREATED_PROCESSOR_ENTRY`

**Possible Causes:**
1. Hangfire workers not running
2. Hangfire job queue is paused
3. Job is stuck in queue
4. Job dependency issue

**What to check:**
```
# Search logs for the HangfireJobId from HANDLER_ENQUEUED:
HangfireJobId={JobId}
```

**Actions:**
1. Open Hangfire Dashboard (`/dashboard`)
2. Check "Jobs" ? "Processing" or "Enqueued"
3. Look for your job ID
4. Check job status and any error messages
5. Verify Hangfire workers are running (check "Servers" tab)

### Issue 3: Processor Starts but Fails to Fetch Document

**Symptoms:**
- You see `DOC_CREATED_PROCESSOR_ENTRY`
- You see `DOC_CREATED_PROCESSOR_FETCH_DOCUMENT`
- But you see `DOC_CREATED_PROCESSOR_DOCUMENT_NULL` or `DOC_CREATED_PROCESSOR_DOCUMENT_EMPTY`

**Possible Causes:**
1. Document not in Provider's blob storage
2. Provider service is down
3. SourceUrlHash mismatch
4. HTTP client timeout

**What to check:**
```
# Search for:
DOC_CREATED_PROCESSOR_DOCUMENT_NULL
DOC_CREATED_PROCESSOR_DOCUMENT_EMPTY
```

**Actions:**
1. Verify Provider service is running
2. Check Provider logs to confirm document was stored
3. Verify SourceUrlHash matches between Provider and Producer
4. Check HTTP client configuration (timeouts, retries)

### Issue 4: Processor Can't Find Document Processor

**Symptoms:**
- You see document obtained successfully
- You see `DOC_CREATED_PROCESSOR_GET_PROCESSOR`
- But you see an exception (no `DOC_CREATED_PROCESSOR_FOUND`)

**Possible Causes:**
1. No processor registered for this DocumentType/Sport/Provider combination
2. DocumentProcessorFactory misconfiguration
3. Missing `[DocumentProcessor]` attribute

**What to check:**
```
# Search for:
DOC_CREATED_PROCESSOR_GET_PROCESSOR
# followed by exception
```

**Actions:**
1. Verify a processor exists for the DocumentType (e.g., `EventDocumentProcessor`)
2. Check the `[DocumentProcessor]` attribute has correct parameters
3. Verify processor is registered in DI container

### Issue 5: Document Processor Executes but Fails

**Symptoms:**
- You see `DOC_CREATED_PROCESSOR_EXECUTE`
- But you never see `DOC_CREATED_PROCESSOR_EXECUTE_COMPLETED`
- Or you see `DOC_CREATED_PROCESSOR_FAILED`

**Possible Causes:**
1. Exception in document-specific processor
2. Database constraint violation
3. Missing dependencies (e.g., Season, Venue not created yet)
4. JSON deserialization error

**What to check:**
```
# Search for:
DOC_CREATED_PROCESSOR_FAILED
DOC_CREATED_PROCESSOR_EXECUTE
# And check document-specific processor logs
```

**Actions:**
1. Check the specific processor's logs (e.g., `EventDocumentProcessor`)
2. Look for exceptions and stack traces
3. Check database constraints
4. Verify all dependencies exist

## Filtering Logs

### View Complete Flow for a Single Document

```
# In Seq or log viewer, filter by DocumentId:
DocumentId="<guid>"

# Or by CorrelationId to see all related events:
CorrelationId="<guid>"
```

### View Only Handler Activity

```
# Filter by:
HANDLER_ENTRY OR HANDLER_ENQUEUED OR HANDLER_EXIT OR HANDLER_EXCEPTION
```

### View Only Processor Activity

```
# Filter by:
DOC_CREATED_PROCESSOR_ENTRY OR DOC_CREATED_PROCESSOR_COMPLETED OR DOC_CREATED_PROCESSOR_FAILED
```

### View Only Errors

```
# Filter by log level:
Level = "Error"
```

## Expected Timeline

For a successful document processing flow:

1. **T+0ms**: `HANDLER_ENTRY` - Event received
2. **T+5ms**: `HANDLER_ENQUEUED` - Job enqueued to Hangfire
3. **T+10ms**: `HANDLER_EXIT` - Handler completes
4. **T+100ms**: `DOC_CREATED_PROCESSOR_ENTRY` - Hangfire picks up job (varies based on queue)
5. **T+150ms**: `DOC_CREATED_PROCESSOR_DOCUMENT_INLINE` or `DOC_CREATED_PROCESSOR_FETCH_DOCUMENT` - Getting document
6. **T+300ms**: `DOC_CREATED_PROCESSOR_DOCUMENT_FETCHED` - Document obtained
7. **T+305ms**: `DOC_CREATED_PROCESSOR_GET_PROCESSOR` - Finding processor
8. **T+310ms**: `DOC_CREATED_PROCESSOR_FOUND` - Processor found
9. **T+315ms**: `DOC_CREATED_PROCESSOR_EXECUTE` - Executing processor
10. **T+500ms**: `DOC_CREATED_PROCESSOR_EXECUTE_COMPLETED` - Processor done
11. **T+505ms**: `DOC_CREATED_PROCESSOR_COMPLETED` - Full completion

**Note:** Timings vary based on document complexity, network latency, and database operations.

## Testing the Enhanced Logging

### Test 1: Trigger Event from Provider

```bash
# Call Provider's comprehensive test endpoint:
curl -X POST http://localhost:<provider-port>/api/test/outbox/comprehensive-test
```

### Test 2: Search Producer Logs

```
# In Seq, search for:
HANDLER_ENTRY

# You should see the complete flow if successful
```

### Test 3: Check Hangfire Dashboard

1. Navigate to `http://localhost:<producer-port>/dashboard`
2. Go to "Jobs" ? "Succeeded" (if successful) or "Failed" (if failed)
3. Find your job by timestamp
4. Click to see details and any error messages

## Common Log Sequences

### Successful Processing

```
HANDLER_ENTRY: DocumentCreated event received.
HANDLER_ENQUEUE_IMMEDIATE: First attempt - enqueueing background job immediately.
HANDLER_ENQUEUED: Background job enqueued successfully. {JobId}
HANDLER_EXIT: Handler completed successfully (immediate enqueue).
... (Hangfire picks up job) ...
DOC_CREATED_PROCESSOR_ENTRY: Hangfire job started.
DOC_CREATED_PROCESSOR_DOCUMENT_INLINE: Document included in event payload.
DOC_CREATED_PROCESSOR_GET_PROCESSOR: Looking up document processor from factory.
DOC_CREATED_PROCESSOR_FOUND: Document processor found.
DOC_CREATED_PROCESSOR_EXECUTE: Executing document-specific processor.
DOC_CREATED_PROCESSOR_EXECUTE_COMPLETED: Document-specific processor completed.
DOC_CREATED_PROCESSOR_COMPLETED: Document processing completed successfully.
```

### Retry Due to Missing Dependency

```
HANDLER_ENTRY: DocumentCreated event received.
HANDLER_ENQUEUE_IMMEDIATE: First attempt - enqueueing background job immediately.
HANDLER_ENQUEUED: Background job enqueued successfully. {JobId}
HANDLER_EXIT: Handler completed successfully (immediate enqueue).
DOC_CREATED_PROCESSOR_ENTRY: Hangfire job started.
DOC_CREATED_PROCESSOR_DOCUMENT_INLINE: Document included in event payload.
DOC_CREATED_PROCESSOR_GET_PROCESSOR: Looking up document processor from factory.
DOC_CREATED_PROCESSOR_FOUND: Document processor found.
DOC_CREATED_PROCESSOR_EXECUTE: Executing document-specific processor.
  (base class ProcessAsync catches ExternalDocumentNotSourcedException)
  "Dependency not ready (attempt 1). Will retry later."
  (event republished with AttemptCount=1)
HANDLER_ENTRY: DocumentCreated event received. (AttemptCount=1)
HANDLER_SCHEDULE_IMMEDIATE: Scheduling retry immediately (no backoff).
HANDLER_SCHEDULED: Background job scheduled successfully. HangfireJobId={HangfireJobId}
HANDLER_EXIT: Handler completed successfully (scheduled retry).
```

### Maximum Retries Reached

When max retries are reached, a `DocumentDeadLetter` event is published (the message is NOT silently dropped):
```
HANDLER_ENTRY: DocumentCreated event received. (AttemptCount=10)
HANDLER_MAX_RETRIES: Maximum retry attempts (10) reached for document. Publishing dead-letter event.
```

**Retry mechanism:** The `DocumentProcessorBase` catches `ExternalDocumentNotSourcedException` thrown by processors and republishes the `DocumentCreated` event with an incremented `AttemptCount`. The handler then schedules the retry with exponential backoff via `IProvideBackgroundJobs`. This is NOT Hangfire's built-in retry mechanism.

### Handler Exception (MassTransit will retry)

```
HANDLER_ENTRY: DocumentCreated event received.
HANDLER_ENQUEUE_IMMEDIATE: First attempt - enqueueing background job immediately.
HANDLER_EXCEPTION: Unhandled exception in DocumentCreatedHandler.
... (MassTransit retries) ...
HANDLER_ENTRY: DocumentCreated event received.
HANDLER_ENQUEUE_IMMEDIATE: First attempt - enqueueing background job immediately.
HANDLER_ENQUEUED: Background job enqueued successfully. {JobId}
HANDLER_EXIT: Handler completed successfully (immediate enqueue).
```

## Additional Diagnostics

### Check MassTransit Consumer Status

Look for MassTransit startup logs:
```
Configured endpoint: DocumentCreatedHandler
Consumer: DocumentCreatedHandler
```

### Check Hangfire Status

Look for Hangfire startup logs:
```
Hangfire Server started
Worker count: {count}
```

### Check Provider Connectivity

```bash
# Test Provider endpoint:
curl http://localhost:<provider-port>/api/documents/by-hash/{SourceUrlHash}
```

## Summary

With this enhanced logging, you can now:

1. **Confirm event reception** - Look for `HANDLER_ENTRY`
2. **Verify Hangfire enqueueing** - Look for `HANDLER_ENQUEUED`
3. **Check job execution** - Look for `DOC_CREATED_PROCESSOR_ENTRY`
4. **Track document fetching** - Look for `DOC_CREATED_PROCESSOR_FETCH_DOCUMENT` or `DOC_CREATED_PROCESSOR_DOCUMENT_INLINE`
5. **Monitor processor selection** - Look for `DOC_CREATED_PROCESSOR_FOUND`
6. **Watch processing** - Look for `DOC_CREATED_PROCESSOR_EXECUTE`
7. **Identify failures** - Look for `DOC_CREATED_PROCESSOR_FAILED` or `HANDLER_EXCEPTION`, or filter by `Level = "Error"`

The logs will tell you **exactly** where processing stops, making it much easier to diagnose the issue.

## Appendix: EventCompetitionCompetitorDocumentProcessor logging

This appendix covers the processor-specific logging added to
`EventCompetitionCompetitorDocumentProcessor` to track the complete flow
of processing competitor documents and their child documents (Score,
LineScore, Roster, Statistics, Record). The general flow above describes
the entry/exit pattern that all `DocumentCreated`-driven processors
inherit; the content below adds the competitor-processor-specific
tokens, expected sequences, and diagnostic queries.

### Purpose

The logging was added to diagnose why downstream `DocumentRequested`
events for `EventCompetitionCompetitorScore` and
`EventCompetitionCompetitorLineScore` might not be published or
processed.

**Key question being investigated:** Are the `DocumentRequested` events
being published but not reaching the child processors?

Possible causes:
1. Events not being published at all
2. Events published but not persisted to outbox
3. Events in outbox but not sent to Service Bus
4. Events sent but not consumed by handlers
5. Handlers consuming but child processors failing

### Added logging

#### 1. Entity creation/update flow

**New entity created:**
```
?? CREATE_COMPETITOR: Creating new CompetitionCompetitor.
   CompetitionId={guid}, FranchiseSeasonId={guid}, HomeAway=home/away
? COMPETITOR_CREATED: CompetitionCompetitor entity created. CompetitorId={guid}
```

**Existing entity updated:**
```
?? UPDATE_COMPETITOR: Updating existing CompetitionCompetitor.
   CompetitorId={guid}, HomeAway=home/away
```

#### 2. Child document processing flow

**Initiation:**
```
?? PROCESS_CHILD_DOCUMENTS: Processing child documents for competitor.
   CompetitorId={guid}, IsNew={true/false}
```

**Completion:**
```
? CHILD_DOCUMENTS_COMPLETED: Child document processing completed. CompetitorId={guid}
```

#### 3. Child document publishing (Score, LineScore, Roster, Statistics, Record)

All 5 child document types use the base class `PublishChildDocumentRequest` method, which provides standardized logging:

**Skipping (no ref):**
```
SKIP_CHILD_DOCUMENT: No reference found.
```
(Logged at Debug level with `ChildDocumentType` and `ParentId` in scope)

**Publishing request:**
```
? CHILD_REQUEST_PUBLISHED: DocumentRequested published. UrlHash={hash}
```

The processor spawns up to 5 child document types:
1. `EventCompetitionCompetitorScore`
2. `EventCompetitionCompetitorLineScore`
3. `EventCompetitionCompetitorRoster`
4. `EventCompetitionCompetitorStatistics`
5. `EventCompetitionCompetitorRecord`

#### 4. Database save & outbox flush

**Before save:**
```
?? SAVING_CHANGES: About to call SaveChangesAsync to persist CompetitionCompetitor and flush outbox.
   CompetitionId={guid}, HasPendingChanges=true/false
```

**After save:**
```
? SAVE_COMPLETED: SaveChangesAsync completed. All outbox messages should now be flushed to service bus.
   CompetitionId={guid}
```

### Expected log flow (happy path)

#### For new competitor

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

#### For updated competitor

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

### Diagnostic queries in Seq

#### 1. Check if child document events are being published

```
@m like '%CHILD_REQUEST_PUBLISHED%'
| select ChildDocumentType, ParentId, @t
```

**What this tells you:**
- If you see these logs, events ARE being published
- If you don't see these logs, check for SKIP_CHILD_DOCUMENT or PROCESS_CHILD_DOCUMENTS logs

#### 2. Check if refs are missing in DTO

```
@m like '%SKIP_CHILD_DOCUMENT%'
| select ChildDocumentType, ParentId, @m
```

**What this tells you:**
- If you see many SKIP logs, the ESPN DTOs don't have refs for those child types
- This is NORMAL for some competitors (e.g., pre-game)

#### 3. Verify outbox flush

```
@m like '%SAVING_CHANGES%' OR @m like '%SAVE_COMPLETED%'
| select CompetitionId, HasPendingChanges, @t
```

**What this tells you:**
- If `HasPendingChanges=false` before SAVING_CHANGES, no outbox messages were added
- If `HasPendingChanges=true`, outbox should have messages

#### 4. Track complete flow for a competitor

```
CompetitorId="{specific-guid}"
| select @t, @m
| order by @t asc
```

This shows the complete timeline for a single competitor.

#### 5. Find competitors that published child document events

```
@m like '%CHILD_REQUEST_PUBLISHED%'
| select CompetitorId, ChildDocumentType, ParentId, @t
```

This query matches the actual `CHILD_REQUEST_PUBLISHED` log token emitted by `PublishChildDocumentRequest` in `DocumentProcessorBase` when a `DocumentRequested` event is published. Use this instead of `CHILD_DOCUMENTS_COMPLETED`, which only confirms the child-processing loop finished but does not prove any events were published.

### Competitor-specific troubleshooting

#### Issue: No child document events published

**Symptoms:**
- See `PROCESS_CHILD_DOCUMENTS`
- See `SKIP_CHILD_DOCUMENT` for all child types (logged at Debug level)
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

#### Issue: Events published but never processed

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

#### Issue: SaveChangesAsync has no changes

**Symptoms:**
- See `?? SAVING_CHANGES: HasPendingChanges=false`

**Diagnosis:**
Either:
1. Competitor entity already existed (update scenario)
2. No downstream events were published (Score/LineScore refs were null)

**Action:**
- Check if this is an update: Look for `?? UPDATE_COMPETITOR`
- Check if refs were skipped: Look for `?? SKIP_SCORES` and `?? SKIP_LINESCORES`

### Key insights

#### 1. Outbox pattern

The `DocumentRequested` events are published using the outbox pattern:
```csharp
await _publishEndpoint.Publish(new DocumentRequested(...));
// Event added to outbox in-memory

await _dataContext.SaveChangesAsync();
// Outbox interceptor persists messages to OutboxMessage table
// Background worker sends to Service Bus
```

#### 2. Event flow

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

#### 3. Why logging was critical

Without this logging, you couldn't tell:
- Whether events were published
- Whether DTOs had the required refs
- Whether SaveChangesAsync was called
- Whether outbox had pending changes
- Which stage of the pipeline failed

### Expected counts

For a typical game with 2 competitors:

**Per competitor:**
- 1x `CREATE_COMPETITOR` or `UPDATE_COMPETITOR`
- 1x `PROCESS_CHILD_DOCUMENTS`
- 0-5x `CHILD_REQUEST_PUBLISHED` (one per child type with a valid ref: Score, LineScore, Roster, Statistics, Record)
- 0-5x `SKIP_CHILD_DOCUMENT` (for child types with no ref)
- 1x `CHILD_DOCUMENTS_COMPLETED`

**Per processor run:**
- 1x `SAVING_CHANGES`
- 1x `SAVE_COMPLETED`

### Related processors

This same logging pattern should be applied to:
- `EventCompetitionDocumentProcessor` (publishes many downstream types)
- `EventDocumentProcessor` (publishes EventCompetition)
- Any processor that publishes `DocumentRequested` events

### Emoji legend for filtering

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

### Next steps

1. **Run a test** with Provider ? Producer flow
2. **Search logs** for the emoji prefixes
3. **Trace a specific UrlHash** end-to-end
4. **Verify outbox messages** are created in database
5. **Check Service Bus** for message delivery

If events are published but not processed, the issue is likely in Provider's handling or Service Bus configuration, not in this processor.
