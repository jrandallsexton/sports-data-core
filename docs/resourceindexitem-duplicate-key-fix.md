# ResourceIndexItem Duplicate Key Fix

## Problem

Provider was throwing duplicate key violations when processing documents:

```
23505: duplicate key value violates unique constraint "IX_ResourceIndexItem_Composite"
DETAIL: Key ("ResourceIndexId", "SourceUrlHash")=(00000000-0000-0000-0000-000000000000, 398baac08a35e344f21395a3b11432c40be393cfb87af9cc4e0a6d0a7c597eec) already exists.
```

### Root Cause

The `DocumentRequestedHandler` was creating `ProcessResourceIndexItemCommand` with `ResourceIndexId: Guid.Empty` for ad-hoc document requests (documents requested by Producer via `DocumentRequested` events).

```csharp
// DocumentRequestedHandler.cs
private void ProcessResourceIndexItem(Uri uri, DocumentRequested evt)
{
    var cmd = new ProcessResourceIndexItemCommand(
        CorrelationId: evt.CorrelationId,
        ResourceIndexId: Guid.Empty,  // ? PROBLEM!
        Id: urlHash,
        Uri: uri,
        Sport: evt.Sport,
        SourceDataProvider: evt.SourceDataProvider,
        DocumentType: evt.DocumentType,
        ParentId: evt.ParentId,
        SeasonYear: evt.SeasonYear,
        BypassCache: true);

    _backgroundJobProvider.Enqueue<IProcessResourceIndexItems>(p => p.Process(cmd));
}
```

The `ResourceIndexItem` table has a unique constraint on `(ResourceIndexId, SourceUrlHash)`. When multiple ad-hoc requests were processed, they all tried to insert with `ResourceIndexId = 00000000-0000-0000-0000-000000000000`, causing violations.

### Why This Happened

**There are two ways documents get processed:**

1. **Scheduled Resource Index Jobs** (`ResourceIndexJob`)
   - Provider scans ESPN API indices periodically
   - Each index has a unique `ResourceIndexId` (not Guid.Empty)
   - Items tracked in `ResourceIndexItem` table for monitoring

2. **Ad-hoc Document Requests** (`DocumentRequested` events)
   - Producer discovers missing dependency
   - Provider fetches document on-demand
   - **These should NOT create ResourceIndexItems**

## Solution

**Only create `ResourceIndexItem` records when `ResourceIndexId != Guid.Empty`**

### Code Changes

**File: `src/SportsData.Provider/Application/Processors/ResourceIndexItemProcessor.cs`**

Added conditional check to skip ResourceIndexItem creation for ad-hoc requests:

```csharp
if (command.ResourceIndexId != Guid.Empty)
{
    // Create/update ResourceIndexItem
}
else
{
    _logger.LogDebug("Skipping ResourceIndexItem creation for ad-hoc request");
}
```

## Impact

? **No more duplicate key violations** for ad-hoc document requests  
? **Scheduled jobs still work** the same  
? **Ad-hoc requests are faster** (no unnecessary DB inserts)  
? **Cleaner separation** of concerns  

## Verification

After deploying, you should see in Provider logs:
```
Skipping ResourceIndexItem creation for ad-hoc request. Uri=https://...
```

And in database:
```sql
SELECT COUNT(*) 
FROM "ResourceIndexItem" 
WHERE "ResourceIndexId" = '00000000-0000-0000-0000-000000000000';
-- Should be 0
```
