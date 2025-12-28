# ContestUpdateJob Enhanced Logging

## Overview

Added comprehensive logging to `ContestUpdateJob` to track the complete lifecycle of the recurring job that updates contests in the current season week.

## What Was Added

### 1. Job Lifecycle Tracking

**Job Start:**
```
?? JOB_STARTED: ContestUpdateJob started. CorrelationId={CorrelationId}
```

**Job Success:**
```
? JOB_COMPLETED: ContestUpdateJob completed successfully. Duration={DurationSeconds}s
```

**Job Failure:**
```
?? JOB_FAILED: ContestUpdateJob failed. Duration={DurationSeconds}s, Error={ErrorMessage}
```

### 2. Season Week Discovery

**Query Initiated:**
```
?? QUERY_SEASON_WEEK: Querying for current season week. CurrentUtc={CurrentUtc}
```

**Season Week Found:**
```
? SEASON_WEEK_FOUND: Current season week identified. 
   SeasonWeekId={SeasonWeekId}, Season={SeasonYear}, Week={WeekNumber}, 
   StartDate={StartDate}, EndDate={EndDate}
```

**Season Week Not Found:**
```
? SEASON_WEEK_NOT_FOUND: Could not determine current season week. CurrentUtc={CurrentUtc}
```

### 3. Contest Query & Summary

**Query Initiated:**
```
?? QUERY_CONTESTS: Querying for non-finalized contests in current week. SeasonWeekId={SeasonWeekId}
```

**Contests Found:**
```
? CONTESTS_FOUND: Found contests to update. Count={Count}, SeasonWeekId={SeasonWeekId}
```

**Contest Details (Structured Logging):**
```
?? CONTEST_DETAILS: Contest summary. 
   Contests=[{
     Id: guid,
     ShortName: "Team A @ Team B",
     StartDate: datetime,
     IsStarted: true/false,
     HoursUntilStart: 3.5
   }, ...]
```

**No Contests:**
```
?? NO_CONTESTS: No non-finalized contests found in current week. SeasonWeekId={SeasonWeekId}
```

### 4. Job Enqueue Tracking

**Individual Contest Enqueued (Debug Level):**
```
? CONTEST_ENQUEUED: Contest update job enqueued. 
   ContestId={ContestId}, ShortName={ShortName}, HangfireJobId={JobId}, StartDate={StartDate}
```

**Contest Enqueue Failed:**
```
? CONTEST_ENQUEUE_FAILED: Failed to enqueue contest update. 
   ContestId={ContestId}, ShortName={ShortName}, Error={ErrorMessage}
```

**Final Summary:**
```
?? ENQUEUE_SUMMARY: Contest update jobs enqueued. 
   Total={Total}, Succeeded={Succeeded}, Failed={Failed}, 
   SeasonWeekId={SeasonWeekId}, CorrelationId={CorrelationId}
```

## Key Improvements

### 1. Correlation ID
All logs within a single job execution share the same `CorrelationId`, making it easy to trace the complete flow:

```csharp
var correlationId = Guid.NewGuid();
using (_logger.BeginScope(new Dictionary<string, object>
{
    ["CorrelationId"] = correlationId,
    ["JobName"] = nameof(ContestUpdateJob)
}))
{
    // All logs in this scope include CorrelationId
}
```

### 2. Structured Logging
Contest details are logged as structured data for easy querying:

```csharp
var contestSummaries = contests.Select(c => new
{
    c.Id,
    c.ShortName,
    StartDate = c.StartDateUtc,
    IsStarted = c.StartDateUtc < DateTime.UtcNow,
    HoursUntilStart = (c.StartDateUtc - DateTime.UtcNow).TotalHours
}).ToList();

_logger.LogInformation("?? CONTEST_DETAILS: Contest summary. Contests={@Contests}", contestSummaries);
```

### 3. Emoji Prefixes for Easy Filtering

| Emoji | Meaning | Filter In Seq |
|-------|---------|---------------|
| ?? | Job Started | `@m like '%??%'` |
| ? | Success | `@m like '%?%'` |
| ? | Error | `@m like '%?%'` |
| ?? | Exception | `@m like '%??%'` |
| ?? | Query | `@m like '%??%'` |
| ?? | Search | `@m like '%??%'` |
| ?? | Details | `@m like '%??%'` |
| ?? | Summary | `@m like '%??%'` |
| ?? | Info | `@m like '%??%'` |

### 4. Performance Metrics
Job duration is tracked and logged:

```csharp
var startTime = DateTime.UtcNow;
// ... execution ...
var duration = DateTime.UtcNow - startTime;
_logger.LogInformation("Duration={DurationSeconds}s", duration.TotalSeconds);
```

### 5. Error Tracking
Individual enqueue failures are captured without stopping the entire job:

```csharp
var enqueuedCount = 0;
var failedCount = 0;

foreach (var contest in contests)
{
    try
    {
        // Enqueue job
        enqueuedCount++;
    }
    catch (Exception ex)
    {
        failedCount++;
        _logger.LogError(ex, "? CONTEST_ENQUEUE_FAILED: ...");
    }
}
```

## Usage Examples

### Example 1: Trace a Complete Job Run

In Seq, filter by CorrelationId:
```
CorrelationId="abc123-..."
```

You'll see the complete sequence:
```
?? JOB_STARTED
?? QUERY_SEASON_WEEK
? SEASON_WEEK_FOUND
?? QUERY_CONTESTS
? CONTESTS_FOUND
?? CONTEST_DETAILS
? CONTEST_ENQUEUED (x5)
?? ENQUEUE_SUMMARY
? JOB_COMPLETED
```

### Example 2: Find Failed Enqueue Operations

```
@m like '%CONTEST_ENQUEUE_FAILED%'
```

### Example 3: Track Job Performance

```
@m like '%JOB_COMPLETED%'
| select DurationSeconds, CorrelationId
| order by DurationSeconds desc
```

### Example 4: Monitor Season Week Detection Issues

```
@m like '%SEASON_WEEK_NOT_FOUND%'
```

### Example 5: See Contest Distribution

```
@m like '%CONTEST_DETAILS%'
| select Contests
```

This gives you the structured contest data showing start times, which contests have started, etc.

## Expected Log Flow (Happy Path)

```
[2025-12-28 14:00:00] ?? JOB_STARTED: ContestUpdateJob started. CorrelationId=abc123
[2025-12-28 14:00:00] ?? QUERY_SEASON_WEEK: Querying for current season week. CurrentUtc=2025-12-28T14:00:00Z
[2025-12-28 14:00:00] ? SEASON_WEEK_FOUND: Current season week identified. SeasonWeekId=def456, Season=2025, Week=14, StartDate=2025-12-25, EndDate=2025-12-31
[2025-12-28 14:00:00] ?? QUERY_CONTESTS: Querying for non-finalized contests in current week. SeasonWeekId=def456
[2025-12-28 14:00:00] ? CONTESTS_FOUND: Found contests to update. Count=5, SeasonWeekId=def456
[2025-12-28 14:00:00] ?? CONTEST_DETAILS: Contest summary. Contests=[
  { Id=111, ShortName="LSU @ Alabama", StartDate=2025-12-28T18:00:00Z, IsStarted=false, HoursUntilStart=4.0 },
  { Id=222, ShortName="Georgia @ Texas", StartDate=2025-12-28T19:30:00Z, IsStarted=false, HoursUntilStart=5.5 },
  ...
]
[2025-12-28 14:00:00] ?? ENQUEUE_SUMMARY: Contest update jobs enqueued. Total=5, Succeeded=5, Failed=0, SeasonWeekId=def456, CorrelationId=abc123
[2025-12-28 14:00:01] ? JOB_COMPLETED: ContestUpdateJob completed successfully. Duration=1.2s, CorrelationId=abc123
```

## Troubleshooting Guide

### Issue: Job runs but no contests updated

**Check:**
1. Season week detection:
   ```
   @m like '%SEASON_WEEK_FOUND%'
   ```
2. Contest query results:
   ```
   @m like '%CONTESTS_FOUND%'
   ```
3. If `Count=0`, check:
   - Are contests already finalized? (`FinalizedUtc != null`)
   - Is the season week correct?
   - Are there contests in the database for this week?

### Issue: Some contests not being updated

**Check:**
1. Enqueue summary for failures:
   ```
   @m like '%ENQUEUE_SUMMARY%'
   | where Failed > 0
   ```
2. Find specific failures:
   ```
   @m like '%CONTEST_ENQUEUE_FAILED%'
   ```

### Issue: Job taking too long

**Check:**
1. Job duration:
   ```
   @m like '%JOB_COMPLETED%'
   | select DurationSeconds
   ```
2. Contest count:
   ```
   @m like '%CONTESTS_FOUND%'
   | select Count
   ```

If duration is high with many contests, this is expected (each contest gets its own Hangfire job).

## Related Files

- `src/SportsData.Producer/Application/Contests/ContestUpdateJob.cs` - **ENHANCED**
- `src/SportsData.Producer/Application/Contests/ContestUpdateProcessor.cs` - Processor that handles each contest
- `src/SportsData.Producer/Application/Contests/ContestEnrichmentJob.cs` - Similar pattern
- `src/SportsData.Api/Application/Jobs/ContestScoringJob.cs` - Similar pattern
