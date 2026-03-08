# Enhanced DocumentCreatedHandler Logging Guide

## Overview

Comprehensive logging has been added to the `DocumentCreatedHandler` and `DocumentCreatedProcessor` to track the complete flow from event reception to document processing. This will help identify where processing stops.

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

**Handler (`DocumentCreatedHandler`):** Uses plain text prefixes without emojis:
- `HANDLER_ENTRY`, `HANDLER_ENQUEUE_IMMEDIATE`, `HANDLER_ENQUEUED`, `HANDLER_SCHEDULE_DELAYED`, `HANDLER_SCHEDULE_IMMEDIATE`, `HANDLER_SCHEDULED`, `HANDLER_EXIT`, `HANDLER_MAX_RETRIES`, `HANDLER_EXCEPTION`

**Processor (`DocumentCreatedProcessor`):** Uses `DOC_CREATED_` prefix with emojis:
- `DOC_CREATED_PROCESSOR_ENTRY`, `DOC_CREATED_PROCESSOR_DOCUMENT_INLINE`, `DOC_CREATED_PROCESSOR_FETCH_DOCUMENT`, `DOC_CREATED_PROCESSOR_DOCUMENT_FETCHED`, `DOC_CREATED_PROCESSOR_DOCUMENT_NULL`, `DOC_CREATED_PROCESSOR_DOCUMENT_EMPTY`, `DOC_CREATED_PROCESSOR_GET_PROCESSOR`, `DOC_CREATED_PROCESSOR_FOUND`, `DOC_CREATED_PROCESSOR_EXECUTE`, `DOC_CREATED_PROCESSOR_EXECUTE_COMPLETED`, `DOC_CREATED_PROCESSOR_COMPLETED`, `DOC_CREATED_PROCESSOR_FAILED`

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
