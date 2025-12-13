# Historical Season Sourcing - Logging & Observability Guide

## Overview

The historical season sourcing feature now has comprehensive logging using structured log messages and correlation tracking. This allows you to trace an entire historical sourcing operation from start to finish using just your logs in Seq.

## Key Correlation Fields

### CorrelationId Flow
The `CorrelationId` from your historical sourcing request flows through the entire pipeline:

1. **HistoricalSeasonSourcingService** - Creates the correlationId and stores it in `ResourceIndex.CreatedBy`
2. **ResourceIndexJob** - Retrieves correlationId from `CreatedBy` field
3. **ResourceIndexItemProcessor** - Uses correlationId from command
4. **DocumentCreated** event - Carries correlationId through message bus
5. **DocumentCreatedHandler** - Logs with correlationId
6. **DocumentCreatedProcessor** - Processes with correlationId

### Additional Context Fields
- **CausationId** - Static GUID identifying which component created the event
- **TierName** - The DocumentType being sourced (Season, Venue, TeamSeason, AthleteSeason)
- **SeasonYear** - The season being sourced
- **DocumentType** - Type of document being processed
- **ResourceIndexId** - The ResourceIndex job ID

## Tier Dependency Validation

**NEW:** Each tier now validates that upstream dependencies completed successfully before processing:

### Dependency Chain
```
Season (no dependencies)
  ?
Venue (requires: Season)
  ?
TeamSeason (requires: Season, Venue)
  ?
AthleteSeason (requires: Season, Venue, TeamSeason)
```

### Failure Cascade
If an upstream tier fails (e.g., ESPN returns 500 on Season endpoint):
1. Season job fails and is marked `IsEnabled = false`
2. Venue job checks upstream ? detects Season failure ? cancels itself
3. TeamSeason job checks upstream ? detects Season failure ? cancels itself
4. AthleteSeason job checks upstream ? detects Season failure ? cancels itself

**Log Message:** `TIER_CANCELLED: Upstream tier failed. Cancelling tier.`

## Structured Log Messages

### Tier-Level Events

#### TIER_CANCELLED
Logged when a tier is cancelled due to upstream failure.

**Fields:**
- `Tier` - DocumentType that was cancelled
- `SeasonYear` - Season year

**Example:**
```
TIER_CANCELLED: Upstream tier failed. Cancelling tier. Tier=Venue, SeasonYear=2024
```

#### TIER_STARTED
Logged when a ResourceIndex job begins processing a tier.

**Fields:**
- `Tier` - DocumentType (Season, Venue, TeamSeason, AthleteSeason)
- `SeasonYear` - Season year being sourced
- `Shape` - ResourceShape (Leaf or Index)
- `ResourceIndexId` - Job identifier

**Example:**
```
TIER_STARTED: Tier=Season, SeasonYear=2024, Shape=Leaf, ResourceIndexId=abc123...
```

#### TIER_COMPLETED
Logged when a ResourceIndex job finishes processing a tier.

**Fields:**
- `Tier` - DocumentType
- `SeasonYear` - Season year
- `DurationMin` - Duration in minutes (decimal)
- `ResourceIndexId` - Job identifier

**Example:**
```
TIER_COMPLETED: Tier=Venue, SeasonYear=2024, DurationMin=8.45, ResourceIndexId=def456...
```

#### TIER_SOURCING_COMPLETED
Logged when all documents for a tier have been enqueued (but not yet processed).

**Fields:**
- `Tier` - DocumentType
- `TotalDocumentsEnqueued` - Count of documents sent to processing
- `ResourceIndexId` - Job identifier

**Example:**
```
TIER_SOURCING_COMPLETED: Tier=TeamSeason, TotalDocumentsEnqueued=130, ResourceIndexId=ghi789...
```

### Document-Level Events

#### DOC_PROCESSING_STARTED
Logged when a document processor begins processing a document.

**Fields:**
- `DocumentType` - Type of document
- `SourceUrlHash` - Document identifier
- `AttemptCount` - Retry count

**Example:**
```
DOC_PROCESSING_STARTED: DocumentType=TeamSeason, SourceUrlHash=abc123, AttemptCount=0
```

#### DOC_PROCESSING_COMPLETED
Logged when a document processor finishes processing a document.

**Fields:**
- `DocumentType` - Type of document
- `DurationMs` - Duration in milliseconds

**Example:**
```
DOC_PROCESSING_COMPLETED: DocumentType=TeamSeason, DurationMs=1234
```

## Seq Queries

### Query 1: Historical Sourcing Overview (with Cancellations)
Get all tier events including cancellations:

```sql
select 
  @Timestamp,
  @Message,
  TierName,
  SeasonYear,
  DurationMin,
  TotalDocumentsEnqueued
from stream
where CorrelationId = '00000000-0000-0000-0000-000000000000' -- Replace with your correlationId
  and (@Message like 'TIER_%' or @Message like 'DOC_PROCESSING_%')
order by @Timestamp
```

### Query 2: Detect Failed Tiers
Find tiers that were cancelled due to upstream failures:

```sql
select 
  @Timestamp,
  TierName,
  SeasonYear,
  @Message
from stream
where @Message like 'TIER_CANCELLED%'
  and CorrelationId = '00000000-0000-0000-0000-000000000000'
order by @Timestamp
```

### Query 3: Tier Duration Analysis
Compare tier processing times:

```sql
select 
  TierName,
  SeasonYear,
  DurationMin,
  TotalDocumentsEnqueued,
  @Timestamp as CompletedAt
from stream
where CorrelationId = '00000000-0000-0000-0000-000000000000' -- Replace with your correlationId
  and @Message like 'TIER_COMPLETED%'
order by @Timestamp
```

### Query 4: Document Processing Rate by Tier
Calculate average processing time per document type:

```sql
select 
  DocumentType,
  count(*) as DocumentCount,
  avg(DurationMs) as AvgDurationMs,
  max(DurationMs) as MaxDurationMs,
  min(DurationMs) as MinDurationMs
from stream
where CorrelationId = '00000000-0000-0000-0000-000000000000' -- Replace with your correlationId
  and @Message like 'DOC_PROCESSING_COMPLETED%'
group by DocumentType
```

### Query 5: Failed/Retried Documents
Find documents that required retries:

```sql
select 
  @Timestamp,
  DocumentType,
  SourceUrlHash,
  AttemptCount
from stream
where CorrelationId = '00000000-0000-0000-0000-000000000000' -- Replace with your correlationId
  and @Message like 'DOC_PROCESSING_STARTED%'
  and AttemptCount > 0
order by @Timestamp
```

### Query 6: Tier Timeline Visualization
Create a timeline view showing tier start/completion/cancellation:

```sql
select 
  @Timestamp,
  case 
    when @Message like 'TIER_STARTED%' then 'Started'
    when @Message like 'TIER_COMPLETED%' then 'Completed'
    when @Message like 'TIER_SOURCING_COMPLETED%' then 'Sourced'
    when @Message like 'TIER_CANCELLED%' then 'Cancelled'
  end as EventType,
  TierName,
  DurationMin,
  TotalDocumentsEnqueued
from stream
where CorrelationId = '00000000-0000-0000-0000-000000000000' -- Replace with your correlationId
  and @Message like 'TIER_%'
order by @Timestamp
```

### Query 7: Processing Lag Detection
Identify gaps between tier sourcing completion and actual document processing:

```sql
select 
  TierName,
  first(@Timestamp) as SourcingCompleted,
  last(@Timestamp) as LastDocProcessed,
  DateDiff(minute, first(@Timestamp), last(@Timestamp)) as ProcessingLagMin
from stream
where CorrelationId = '00000000-0000-0000-0000-000000000000' -- Replace with your correlationId
  and (@Message like 'TIER_SOURCING_COMPLETED%' or @Message like 'DOC_PROCESSING_COMPLETED%')
group by TierName
order by SourcingCompleted
```

## Creating Seq Signals

### Signal 1: Tier Completion Alert
Get notified when each tier completes:

**Name:** Historical Tier Completed  
**Query:**
```sql
@Message like 'TIER_COMPLETED%' 
and CorrelationId is not null
```

**Notification:** Slack/Email with message:
```
Tier {TierName} completed for season {SeasonYear} in {DurationMin} minutes
```

### Signal 2: Tier Cancellation Alert
Get notified when tiers are cancelled due to upstream failures:

**Name:** Historical Tier Cancelled  
**Query:**
```sql
@Message like 'TIER_CANCELLED%'
```

**Notification:** Slack/Email with message:
```
?? Tier {TierName} cancelled for season {SeasonYear} due to upstream failure
```

### Signal 3: Long-Running Document Alert
Alert on documents taking longer than expected:

**Name:** Slow Document Processing  
**Query:**
```sql
@Message like 'DOC_PROCESSING_COMPLETED%' 
and DurationMs > 30000
```

**Notification:** Slack/Email with message:
```
Document processing took {DurationMs}ms for {DocumentType}
```

### Signal 4: High Retry Rate
Alert when documents require multiple retries:

**Name:** High Document Retry Rate  
**Query:**
```sql
@Message like 'DOC_PROCESSING_STARTED%' 
and AttemptCount >= 3
```

**Notification:** Slack/Email with message:
```
Document {SourceUrlHash} requires retry attempt {AttemptCount}
```

## Example: Tracking a Complete Historical Sourcing Run

### Step 1: Initiate Sourcing
```http
POST /api/sourcing/historical/seasons
{
  "sport": 2,
  "sourceDataProvider": 0,
  "seasonYear": 2024
}
```

**Response:**
```json
{
  "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

### Step 2: Monitor in Seq
Use this base query with your correlationId:

```sql
select 
  @Timestamp,
  @Level,
  @Message,
  TierName,
  DocumentType,
  DurationMin,
  DurationMs
from stream
where CorrelationId = '3fa85f64-5717-4562-b3fc-2c963f66afa6'
order by @Timestamp
```

### Step 3: Handle Failures
If you see `TIER_CANCELLED` messages:

1. **Identify root cause** - Check which tier failed first
2. **Fix the issue** (e.g., wait for ESPN API to recover)
3. **Reset and retry**:
```sql
-- Reset all jobs for this correlation
UPDATE "ResourceIndexJobs" 
SET "LastCompletedUtc" = NULL, 
    "LastPageIndex" = NULL, 
    "IsEnabled" = true,
    "IsQueued" = false
WHERE "CreatedBy" = '3fa85f64-5717-4562-b3fc-2c963f66afa6';
```

4. **Manually trigger** via `/api/resourceIndex/{id}/process` for each tier

### Step 4: Analyze Results
After completion, run the duration analysis query to get timing data for future optimization.

## Dashboard Recommendations

Create a Seq dashboard with these widgets:

1. **Tier Progress** - Timeline of TIER_STARTED/TIER_COMPLETED/TIER_CANCELLED events
2. **Document Processing Rate** - Bar chart of documents processed per minute
3. **Average Document Duration** - Line chart of avg processing time by DocumentType
4. **Retry Rate** - Gauge showing percentage of documents requiring retries
5. **Cancellation Rate** - Count of TIER_CANCELLED events
6. **Current Status** - Text widget showing last event timestamp and tier

## Troubleshooting

### Issue: CorrelationId Not Flowing
**Check:** Query for `ResourceIndexId` in the ResourceIndex table to see if `CreatedBy` is set correctly.

### Issue: No Document Processing Logs
**Check:** Verify that documents are reaching the message bus (check MassTransit logs).

### Issue: High Retry Rates
**Check:** Query for `AttemptCount > 2` to identify problematic documents and their error messages.

### Issue: Tier Cancelled Unexpectedly
**Check:** 
1. Query for the previous tier's completion status
2. Verify ESPN API is responding (500 errors will cause failures)
3. Check if delays were too short and jobs started before upstream completed

### Issue: ESPN API Returning 500s
**Resolution:**
1. All subsequent tiers will auto-cancel
2. Wait for ESPN API to recover (usually a few minutes)
3. Reset the jobs using SQL above
4. Manually re-trigger each tier in order

## Recovery Procedures

### Scenario 1: Season Tier Failed (ESPN 500)
```
TIER_CANCELLED: Tier=Venue
TIER_CANCELLED: Tier=TeamSeason  
TIER_CANCELLED: Tier=AthleteSeason
```

**Recovery:**
1. Wait for ESPN to recover
2. Reset all 4 tiers (SQL above)
3. Manually trigger Season tier first
4. Wait for completion
5. Manually trigger remaining tiers

### Scenario 2: Mid-Tier Failure (e.g., Venue failed)
```
TIER_COMPLETED: Tier=Season ?
TIER_CANCELLED: Tier=TeamSeason ?
TIER_CANCELLED: Tier=AthleteSeason ?
```

**Recovery:**
1. Fix Venue tier issue
2. Reset Venue, TeamSeason, AthleteSeason
3. Season is already complete - leave it
4. Manually trigger Venue, then others

---

**Document Version:** 1.1  
**Last Updated:** December 2025  
**Changes:** Added tier dependency validation and cancellation logic
**Related:** HistoricalSeasonSourcingAnalysis.md, HISTORICAL_SEASON_SOURCING.md
