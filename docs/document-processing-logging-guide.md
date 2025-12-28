# Enhanced DocumentCreatedHandler Logging Guide

## Overview

Comprehensive logging has been added to the `DocumentCreatedHandler` and `DocumentCreatedProcessor` to track the complete flow from event reception to document processing. This will help identify where processing stops.

## Log Flow

### Phase 1: Event Reception (DocumentCreatedHandler)

When a `DocumentCreated` event is received from Provider, you'll see these logs in sequence:

```
?? HANDLER_ENTRY: DocumentCreated event received
? HANDLER_ENQUEUE_IMMEDIATE: First attempt - enqueueing background job immediately
? HANDLER_ENQUEUED: Background job enqueued successfully. HangfireJobId={JobId}
?? HANDLER_EXIT: Handler completed successfully (immediate enqueue)
```

**For retry attempts (AttemptCount > 0):**
```
?? HANDLER_ENTRY: DocumentCreated event received
?? HANDLER_SCHEDULE_DELAYED: Scheduling retry with backoff
? HANDLER_SCHEDULED: Background job scheduled successfully. HangfireJobId={JobId}
?? HANDLER_EXIT: Handler completed successfully (scheduled retry)
```

### Phase 2: Background Job Execution (DocumentCreatedProcessor)

Once Hangfire picks up the job, you'll see:

```
?? PROCESSOR_ENTRY: Hangfire job started
?? PROCESSOR_FETCH_DOCUMENT: Fetching document from Provider
? PROCESSOR_DOCUMENT_OBTAINED: Document fetched successfully
?? PROCESSOR_GET_PROCESSOR: Looking up document processor from factory
? PROCESSOR_FOUND: Document processor found. ProcessorType={ProcessorType}
?? PROCESSOR_EXECUTE: Executing document-specific processor
? PROCESSOR_EXECUTE_COMPLETED: Document-specific processor completed
? PROCESSOR_COMPLETED: Document processing completed successfully
```

### Phase 3: Document-Specific Processor

Each document type has its own processor (e.g., `EventDocumentProcessor`, `EventCompetitionStatusDocumentProcessor`). These will log their own start/complete messages.

## Log Emoji Legend

| Emoji | Meaning | Used For |
|-------|---------|----------|
| ?? | Entry | Handler received event |
| ? | Success | Operation completed successfully |
| ? | Error | Operation failed |
| ?? | Scheduling | Job being scheduled with delay |
| ?? | Exit | Handler exiting |
| ?? | Exception | Unhandled exception |
| ?? | Start | Processor/job starting |
| ?? | Fetch | Fetching data |
| ?? | Lookup | Finding/searching |
| ?? | Execute | Running operation |

## Troubleshooting Guide

### Issue 1: Event Received but No Background Job Enqueued

**Symptoms:**
- You see `?? HANDLER_ENTRY` but no `? HANDLER_ENQUEUED`
- Or you see `?? HANDLER_EXCEPTION`

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
- You see `? HANDLER_ENQUEUED: HangfireJobId={JobId}`
- But you never see `?? PROCESSOR_ENTRY`

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
- You see `?? PROCESSOR_ENTRY`
- You see `?? PROCESSOR_FETCH_DOCUMENT`
- But you see `? PROCESSOR_DOCUMENT_NULL` or `? PROCESSOR_DOCUMENT_EMPTY`

**Possible Causes:**
1. Document not in Provider's blob storage
2. Provider service is down
3. SourceUrlHash mismatch
4. HTTP client timeout

**What to check:**
```
# Search for:
PROCESSOR_DOCUMENT_NULL
PROCESSOR_DOCUMENT_EMPTY
```

**Actions:**
1. Verify Provider service is running
2. Check Provider logs to confirm document was stored
3. Verify SourceUrlHash matches between Provider and Producer
4. Check HTTP client configuration (timeouts, retries)

### Issue 4: Processor Can't Find Document Processor

**Symptoms:**
- You see `? PROCESSOR_DOCUMENT_OBTAINED`
- You see `?? PROCESSOR_GET_PROCESSOR`
- But you see an exception (no `? PROCESSOR_FOUND`)

**Possible Causes:**
1. No processor registered for this DocumentType/Sport/Provider combination
2. DocumentProcessorFactory misconfiguration
3. Missing `[DocumentProcessor]` attribute

**What to check:**
```
# Search for:
PROCESSOR_GET_PROCESSOR
# followed by exception
```

**Actions:**
1. Verify a processor exists for the DocumentType (e.g., `EventDocumentProcessor`)
2. Check the `[DocumentProcessor]` attribute has correct parameters
3. Verify processor is registered in DI container

### Issue 5: Document Processor Executes but Fails

**Symptoms:**
- You see `?? PROCESSOR_EXECUTE`
- But you never see `? PROCESSOR_EXECUTE_COMPLETED`
- Or you see `?? PROCESSOR_FAILED`

**Possible Causes:**
1. Exception in document-specific processor
2. Database constraint violation
3. Missing dependencies (e.g., Season, Venue not created yet)
4. JSON deserialization error

**What to check:**
```
# Search for:
PROCESSOR_FAILED
PROCESSOR_EXECUTE
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
PROCESSOR_ENTRY OR PROCESSOR_COMPLETED OR PROCESSOR_FAILED
```

### View Only Errors

```
# Filter by emoji:
? OR ??

# Or by log level:
Level = "Error"
```

## Expected Timeline

For a successful document processing flow:

1. **T+0ms**: `?? HANDLER_ENTRY` - Event received
2. **T+5ms**: `? HANDLER_ENQUEUED` - Job enqueued to Hangfire
3. **T+10ms**: `?? HANDLER_EXIT` - Handler completes
4. **T+100ms**: `?? PROCESSOR_ENTRY` - Hangfire picks up job (varies based on queue)
5. **T+150ms**: `?? PROCESSOR_FETCH_DOCUMENT` - Fetching from Provider
6. **T+300ms**: `? PROCESSOR_DOCUMENT_OBTAINED` - Document fetched
7. **T+305ms**: `?? PROCESSOR_GET_PROCESSOR` - Finding processor
8. **T+310ms**: `? PROCESSOR_FOUND` - Processor found
9. **T+315ms**: `?? PROCESSOR_EXECUTE` - Executing processor
10. **T+500ms**: `? PROCESSOR_EXECUTE_COMPLETED` - Processor done
11. **T+505ms**: `? PROCESSOR_COMPLETED` - Full completion

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
?? HANDLER_ENTRY
? HANDLER_ENQUEUE_IMMEDIATE
? HANDLER_ENQUEUED: HangfireJobId=123
?? HANDLER_EXIT
... (Hangfire picks up job) ...
?? PROCESSOR_ENTRY
?? PROCESSOR_FETCH_DOCUMENT
? PROCESSOR_DOCUMENT_OBTAINED
?? PROCESSOR_GET_PROCESSOR
? PROCESSOR_FOUND
?? PROCESSOR_EXECUTE
? PROCESSOR_EXECUTE_COMPLETED
? PROCESSOR_COMPLETED
```

### Retry Due to Missing Dependency

```
?? HANDLER_ENTRY
? HANDLER_ENQUEUE_IMMEDIATE
? HANDLER_ENQUEUED: HangfireJobId=123
?? HANDLER_EXIT
?? PROCESSOR_ENTRY
?? PROCESSOR_FETCH_DOCUMENT
? PROCESSOR_DOCUMENT_OBTAINED
?? PROCESSOR_GET_PROCESSOR
? PROCESSOR_FOUND
?? PROCESSOR_EXECUTE
?? PROCESSOR_FAILED: ExternalDocumentNotSourcedException
... (event republished with AttemptCount=1) ...
?? HANDLER_ENTRY: AttemptCount=1
?? HANDLER_SCHEDULE_IMMEDIATE: AttemptCount=1
? HANDLER_SCHEDULED: HangfireJobId=456
?? HANDLER_EXIT
```

### Maximum Retries Reached

```
?? HANDLER_ENTRY: AttemptCount=10
? HANDLER_MAX_RETRIES: Maximum retry attempts (10) reached for document. Dropping message.
```

### Handler Exception (MassTransit will retry)

```
?? HANDLER_ENTRY
? HANDLER_ENQUEUE_IMMEDIATE
?? HANDLER_EXCEPTION: Unhandled exception in DocumentCreatedHandler
... (MassTransit retries) ...
?? HANDLER_ENTRY
? HANDLER_ENQUEUE_IMMEDIATE
? HANDLER_ENQUEUED
?? HANDLER_EXIT
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

1. ? **Confirm event reception** - Look for `?? HANDLER_ENTRY`
2. ? **Verify Hangfire enqueueing** - Look for `? HANDLER_ENQUEUED`
3. ? **Check job execution** - Look for `?? PROCESSOR_ENTRY`
4. ? **Track document fetching** - Look for `?? PROCESSOR_FETCH_DOCUMENT`
5. ? **Monitor processor selection** - Look for `? PROCESSOR_FOUND`
6. ? **Watch processing** - Look for `?? PROCESSOR_EXECUTE`
7. ? **Identify failures** - Look for `?` or `??` emojis

The logs will tell you **exactly** where processing stops, making it much easier to diagnose the issue.
