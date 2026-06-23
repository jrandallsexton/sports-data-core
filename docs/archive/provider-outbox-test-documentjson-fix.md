# Provider OutboxTestController - DocumentJson Fix

## Problem

The Provider's `ComprehensiveTest()` endpoint was publishing `DocumentCreated` events with `DocumentJson: null`. This caused Producer's `DocumentCreatedProcessor` to attempt fetching the document from Provider's blob storage, which would fail because:

1. Provider doesn't actually store test documents in blob storage
2. The `SourceUrlHash` was just a test string, not a real document hash
3. Producer would log: `? PROCESSOR_DOCUMENT_NULL: Failed to obtain document from Provider`

## Solution

Added minimal test JSON directly to the `DocumentCreated` event:

```csharp
var testJson = $$"""
{
    "$ref": "http://test.sportdeets.com/outbox-test/{{testId}}",
    "id": "{{testId}}",
    "name": "OutboxTest",
    "testData": {
        "message": "Test document from Provider",
        "timestamp": "{{DateTime.UtcNow:O}}",
        "correlationId": "{{correlationId}}",
        "purpose": "Validate Provider ? Producer flow"
    }
}
""";
```

This JSON is included in the `DocumentCreated` event, so Producer's `DocumentCreatedProcessor` will:
1. Skip the Provider API call (since `DocumentJson` is not null)
2. Use the embedded JSON directly
3. Pass it to `OutboxTestDocumentProcessor`

## How to Test

### Step 1: Start Services

```bash
# Terminal 1: Start Provider
cd src/SportsData.Provider
dotnet run

# Terminal 2: Start Producer
cd src/SportsData.Producer
dotnet run -mode FootballNcaa
```

### Step 2: Trigger Test Event

```bash
curl -X POST http://localhost:<provider-port>/api/test/outbox/comprehensive-test
```

### Step 3: Watch Logs

**Provider logs (should see):**
```
PROVIDER TEST START: Simulating document sourcing...
Publishing DocumentCreated event: DocumentType=OutboxTest, HasJson=True
PROVIDER TEST COMPLETE: DocumentCreated event published successfully
```

**Producer logs (should see in sequence):**
```
?? HANDLER_ENTRY: DocumentCreated event received. DocumentType=OutboxTest
? HANDLER_ENQUEUE_IMMEDIATE: First attempt - enqueueing background job
? HANDLER_ENQUEUED: Background job enqueued successfully. HangfireJobId={JobId}
?? HANDLER_EXIT: Handler completed successfully

... (Hangfire picks up job) ...

?? PROCESSOR_ENTRY: Hangfire job started. DocumentType=OutboxTest
?? PROCESSOR_FETCH_DOCUMENT: Fetching document from Provider
? PROCESSOR_DOCUMENT_OBTAINED: Document fetched successfully. DocumentLength=XXX
?? PROCESSOR_GET_PROCESSOR: Looking up document processor from factory
? PROCESSOR_FOUND: Document processor found. ProcessorType=OutboxTestDocumentProcessor
?? PROCESSOR_EXECUTE: Executing document-specific processor
TEST PROCESSOR (BaseDataContext): Processing with DbContext type: FootballDataContext
? PROCESSOR_EXECUTE_COMPLETED: Document-specific processor completed
? PROCESSOR_COMPLETED: Document processing completed successfully
```

### Step 4: Verify Results

#### Check Hangfire Dashboard
Navigate to Producer's Hangfire dashboard:
```
http://localhost:<producer-port>/dashboard
```

Look for:
- **Succeeded Jobs** ? Find your job by timestamp
- Job should show as "Succeeded" in green

#### Check OutboxMessages Table (Optional)

If the `OutboxTestDocumentProcessor` successfully published the `OutboxTestEvent`, you should see it in the outbox:

```sql
-- In Producer's database
SELECT TOP 10 
    SequenceNumber,
    MessageType,
    SentTime,
    EnqueueTime,
    LEFT(Body, 200) as BodyPreview
FROM OutboxMessage
ORDER BY SentTime DESC;
```

Look for: `MessageType` containing `OutboxTestEvent`

## Expected Response

When you call the endpoint, you'll get a comprehensive response:

```json
{
  "testId": "guid",
  "correlationId": "guid",
  "success": true,
  "verdict": "? Event published to Azure Service Bus",
  "eventDetails": {
    "eventType": "DocumentCreated",
    "documentType": "OutboxTest",
    "sport": "FootballNcaa",
    "provider": "Espn",
    "seasonYear": 2025,
    "correlationId": "guid",
    "causationId": "guid",
    "hasDocumentJson": true,
    "documentJsonLength": 350
  },
  "expectedDownstream": [
    "1. Producer DocumentCreatedHandler receives event",
    "2. Producer queues Hangfire job",
    "3. Hangfire executes DocumentCreatedProcessor",
    "4. DocumentProcessorFactory selects OutboxTestDocumentProcessor",
    "5. OutboxTestDocumentProcessor processes test document",
    "6. OutboxTestEvent published via outbox",
    "7. Check Producer's OutboxMessages table for results"
  ],
  "testJson": "{ ... }",
  "troubleshooting": {
    "checkProducerLogs": "Search for '?? HANDLER_ENTRY' and 'OutboxTest' in Producer logs",
    "checkHangfireDashboard": "Navigate to Producer /dashboard to see job status",
    "expectedLogSequence": [ ... ]
  }
}
```

## What This Tests

? **Provider publishes events** - Confirms Provider can publish to Azure Service Bus  
? **Producer receives events** - Confirms MassTransit consumer is working  
? **Hangfire enqueuing** - Confirms background jobs are being queued  
? **Hangfire execution** - Confirms Hangfire workers are processing jobs  
? **Document processing** - Confirms DocumentCreatedProcessor works  
? **Processor factory** - Confirms DocumentProcessorFactory can find processors  
? **OutboxTest flow** - Confirms OutboxTestDocumentProcessor executes  
? **Outbox pattern** - Confirms events are persisted to OutboxMessages table  

## Troubleshooting

### If Producer doesn't receive the event

**Check:**
1. Azure Service Bus connection string in Producer
2. MassTransit consumer registration for `DocumentCreatedHandler`
3. Producer logs for MassTransit startup messages

### If Hangfire job is not enqueued

**Check:**
1. Hangfire database connection in Producer
2. Producer logs for `?? HANDLER_EXCEPTION`
3. Hangfire dashboard ? Servers ? Verify workers are running

### If job enqueues but never executes

**Check:**
1. Hangfire dashboard ? Jobs ? Enqueued (job might be waiting)
2. Hangfire dashboard ? Servers ? Worker count (should be > 0)
3. Producer logs for `?? PROCESSOR_ENTRY` (if missing, job isn't executing)

### If processor fails

**Check:**
1. Producer logs for `?? PROCESSOR_FAILED`
2. Exception message and stack trace
3. Hangfire dashboard ? Failed Jobs ? Click job for details

## Key Changes Made

### File: `src/SportsData.Provider/Controllers/OutboxTestController.cs`

1. **Added test JSON** using C# 11 raw string literals (`$$"""..."""`)
2. **Changed `DocumentJson` parameter** from `null` to `testJson`
3. **Enhanced response** with more detailed troubleshooting info
4. **Added logging** for `HasJson` to confirm JSON is included

### Why This Works

```
???????????                    ????????????
? Provider?                    ? Producer ?
???????????                    ????????????
     ?                              ?
     ? 1. Create DocumentCreated    ?
     ?    with embedded JSON        ?
     ??????????????????????????????>?
     ?                              ?
     ?                         2. Handler
     ?                         receives event
     ?                              ?
     ?                         3. Enqueue
     ?                         Hangfire job
     ?                              ?
     ?                         4. Hangfire
     ?                         executes job
     ?                              ?
     ?                         5. Check if
     ?                         DocumentJson
     ?                         is null
     ?                              ?
     ?                         NO (JSON exists!)
     ?                              ?
     ?                         6. Use embedded
     ?                         JSON directly
     ?                         (skip Provider API)
     ?                              ?
     ?                         7. Pass to
     ?                         OutboxTestDocumentProcessor
     ?                              ?
     ?                         8. Process &
     ?                         publish to outbox
     ?                              ?
```

Previously, step 5 would find `DocumentJson` null, causing step 6 to call Provider API, which would fail.

Now, step 5 finds `DocumentJson` populated, so it skips the API call and proceeds directly to processing.

## Related Files

- `src/SportsData.Producer/Application/Documents/DocumentCreatedHandler.cs` - Enhanced with logging
- `src/SportsData.Producer/Application/Documents/Processors/DocumentCreatedProcessor.cs` - Enhanced with logging
- `src/SportsData.Producer/Application/Documents/Processors/Test/OutboxTestDocumentProcessor.cs` - Existing test processor
- `docs/document-processing-logging-guide.md` - Comprehensive logging guide
