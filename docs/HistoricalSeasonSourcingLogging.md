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

## Structured Log Messages

### Tier-Level Events

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

### Query 1: Historical Sourcing Overview
Get all tier events for a specific historical sourcing run:

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

### Query 2: Tier Duration Analysis
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

### Query 3: Document Processing Rate by Tier
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

### Query 4: Failed/Retried Documents
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

### Query 5: Tier Timeline Visualization
Create a timeline view showing tier start/completion:

```sql
select 
  @Timestamp,
  case 
    when @Message like 'TIER_STARTED%' then 'Started'
    when @Message like 'TIER_COMPLETED%' then 'Completed'
    when @Message like 'TIER_SOURCING_COMPLETED%' then 'Sourced'
  end as EventType,
  TierName,
  DurationMin,
  TotalDocumentsEnqueued
from stream
where CorrelationId = '00000000-0000-0000-0000-000000000000' -- Replace with your correlationId
  and @Message like 'TIER_%'
order by @Timestamp
```

### Query 6: Processing Lag Detection
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

### Signal 2: Long-Running Document Alert
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

### Signal 3: High Retry Rate
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
  "sport": "FootballNcaa",
  "sourceDataProvider": "Espn",
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

### Step 3: Analyze Results
After completion, run the duration analysis query to get timing data for future optimization.

## Dashboard Recommendations

Create a Seq dashboard with these widgets:

1. **Tier Progress** - Timeline of TIER_STARTED/TIER_COMPLETED events
2. **Document Processing Rate** - Bar chart of documents processed per minute
3. **Average Document Duration** - Line chart of avg processing time by DocumentType
4. **Retry Rate** - Gauge showing percentage of documents requiring retries
5. **Current Status** - Text widget showing last event timestamp and tier

## Troubleshooting

### Issue: CorrelationId Not Flowing
**Check:** Query for `ResourceIndexId` in the ResourceIndex table to see if `CreatedBy` is set correctly.

### Issue: No Document Processing Logs
**Check:** Verify that documents are reaching the message bus (check MassTransit logs).

### Issue: High Retry Rates
**Check:** Query for `AttemptCount > 2` to identify problematic documents and their error messages.

---

**Document Version:** 1.0  
**Last Updated:** December 2025  
**Related:** HistoricalSeasonSourcingAnalysis.md, HISTORICAL_SEASON_SOURCING.md
