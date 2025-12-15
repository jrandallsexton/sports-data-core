# Historical Season Sourcing Design

## Overview

This document defines the architecture and implementation plan for sourcing historical season data from ESPN for NCAA Football. The goal is to systematically load complete seasons of data (teams, athletes, events, stats, etc.) in a tier-based approach that respects dependency ordering and prevents retry storms in the Producer service.

## Problem Statement

Currently, ResourceIndex is designed for **active season monitoring** with recurring jobs. For historical seasons (e.g., 2020-2023), we need:

1. **One-time sourcing** - Load complete historical data once
2. **Dependency-aware ordering** - Process foundation data before dependent data
3. **Downstream processing time** - Allow time for consumers to process each tier before starting the next
4. **Simple, predictable execution** - No complex inter-service communication

### Key Constraints

- **Provider is decoupled from consumers** - Provider just sources and enqueues data; it has no knowledge of downstream services
- **No completion signaling** - Provider's `LastCompletedUtc` means "fetched from ESPN and enqueued", not "processed by consumers"
- **Avoid retry storms** - If we source Events before TeamSeasons, downstream consumers will retry constantly due to missing dependencies
- **Historical data is immutable** - Once sourced correctly, should never need to run again

## Tier-Based Sourcing Strategy

### Dependency Hierarchy

Based on document processor analysis:

**Tier 1: Foundation** (No dependencies)
- Season (single document)
- Venues (collection)

**Tier 2: Organizations** (Depends on: Season)
- TeamSeasons (collection)
  - **Auto-spawns**: Events (entire schedule including postseason via `$ref`)
    - **Auto-spawns**: EventCompetition, stats, rosters (cascade)

**Tier 3: Personnel** (Depends on: Season, TeamSeasons)
- AthleteSeasons (collection - LARGE: ~5000+ athletes)
- Coaches (collection)

### Important Notes

1. **Franchises already loaded** - Current season (2025) sourcing already loaded all Franchises, no need to re-source
2. **Events auto-spawn from TeamSeasons** - For completed historical seasons, the TeamSeason `Events.$ref` includes full schedule with postseason games
3. **Bowl venues may be missing** - Some bowl venues aren't in the Venues collection and will be reactively sourced via `DocumentRequested`
4. **Current season (2025) is different** - Bowl matchups aren't determined until December, so they had to be manually sourced

## API Design

### Endpoint

```http
POST /api/sourcing/historical/seasons
```

### Request Body

```json
{
  "sport": "FootballNcaa",
  "sourceDataProvider": "Espn",
  "seasonYear": 2023,
  "tierDelays": {
    "season": 0,
    "venue": 30,
    "teamSeason": 60,
    "athleteSeason": 240
  }
}
```

### Request Schema

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `sport` | `Sport` enum | Yes | Sport type (e.g., FootballNcaa) |
| `sourceDataProvider` | `SourceDataProvider` enum | Yes | Data provider (e.g., Espn) |
| `seasonYear` | `int` | Yes | Year of the season to source (e.g., 2023) |
| `tierDelays` | `Dictionary<string, int>` | No | Minutes to wait after starting before processing each tier. If omitted, uses configured defaults. |

### Tier Delay Keys

- `season` - Delay before processing Season document (typically 0)
- `venue` - Delay before processing Venues collection (after Season completes)
- `teamSeason` - Delay before processing TeamSeasons (after Venues processing window)
- `athleteSeason` - Delay before processing AthleteSeasons (after TeamSeasons + Events processing window)

**Note**: No delay needed after last tier (AthleteSeasons) since it's the final tier.

### Response

```json
{
  "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

The `correlationId` can be used to track the sourcing job in logs and monitoring dashboards.

## Implementation Details

### URI Generation

Provider builds ESPN URIs internally based on DocumentType and seasonYear:

```csharp
private Uri GetUriForDocumentType(DocumentType docType, int seasonYear, Sport sport, SourceDataProvider provider)
{
    // ESPN Football NCAA patterns
    if (sport == Sport.FootballNcaa && provider == SourceDataProvider.Espn)
    {
        var baseUrl = "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football";
        
        return docType switch
        {
            DocumentType.Season => new Uri($"{baseUrl}/seasons/{seasonYear}"),
            DocumentType.Venue => new Uri($"{baseUrl}/venues"),
            DocumentType.TeamSeason => new Uri($"{baseUrl}/seasons/{seasonYear}/teams"),
            DocumentType.AthleteSeason => new Uri($"{baseUrl}/seasons/{seasonYear}/athletes"),
            _ => throw new ArgumentException($"Unsupported document type: {docType}")
        };
    }
    
    throw new NotSupportedException($"Historical sourcing not yet supported for {sport}/{provider}");
}
```

### ResourceIndex Record Creation

For each tier, create a **non-recurring** ResourceIndex record:

```csharp
var resourceIndex = new ResourceIndex
{
    Id = Guid.NewGuid(),
    Ordinal = existingCount + index, // Unique ordinal
    Name = routingKeyGenerator.Generate(provider, uri),
    IsRecurring = false,
    IsQueued = false,
    CronExpression = null,
    IsEnabled = true,
    Provider = provider,
    DocumentType = documentType,
    Shape = shape,
    SportId = sport,
    Uri = uri,
    SourceUrlHash = HashProvider.GenerateHashFromUri(uri),
    SeasonYear = seasonYear,
    IsSeasonSpecific = true,
    LastAccessedUtc = null,
    LastCompletedUtc = null,
    LastPageIndex = null,
    TotalPageCount = null
};
```

### Job Scheduling

Use Hangfire's `.Schedule()` to enqueue jobs with delays:

```csharp
var startTime = DateTime.UtcNow;

foreach (var tier in tiers)
{
    var jobDefinition = new DocumentJobDefinition(tier.ResourceIndex);
    var scheduledTime = startTime.AddMinutes(tier.DelayMinutes);
    
    backgroundJobProvider.Schedule<ResourceIndexJob>(
        job => job.ExecuteAsync(jobDefinition),
        scheduledTime
    );
}
```

## Configuration

### Default Tier Delays (appsettings.json)

```json
{
  "HistoricalSourcing": {
    "DefaultTierDelays": {
      "FootballNcaa": {
        "Espn": {
          "Season": 0,
          "Venue": 30,
          "TeamSeason": 60,
          "AthleteSeason": 240
        }
      }
    }
  }
}
```

### Rationale for Delays

- **Season (0 min)**: Start immediately, single Leaf document processes quickly
- **Venue (30 min)**: Allow Season to be processed downstream and season structure to be created
- **TeamSeason (60 min)**: Allow Venues to complete (~300 venues × 4 pages = quick processing)
- **AthleteSeason (240 min / 4 hours)**: 
  - TeamSeasons collection: ~130 teams
  - Each spawns Events $ref (schedule)
  - Events spawn EventCompetition
  - EventCompetition spawns stats/rosters
  - **This is the big cascade** - need significant time for downstream processing

**Delays are tunable** - can be adjusted based on observed downstream processing times.

## Database Impact

### ResourceIndex Records per Season

- Season: 1 record
- Venue: 1 record
- TeamSeason: 1 record
- AthleteSeason: 1 record

**Total: 4 records per historical season**

### Example Footprint

- 5 historical seasons (2020-2024): 20 records
- Current season (2025): ~18 records (existing)
- **Total: 38 ResourceIndex records**

This is negligible and provides audit trail, re-run capability, and progress monitoring.

## Processing Volumes

### Expected Data Sizes (per season)

| Tier | Collection Size | Pages (250/page) | Processing Time Estimate |
|------|----------------|------------------|--------------------------|
| Season | 1 document | N/A (Leaf) | < 1 minute |
| Venues | ~300 venues | ~2 pages | ~5 minutes |
| TeamSeasons | ~130 teams | ~1 page | ~10 minutes + cascade |
| Events (spawned) | ~800 games | Auto-spawned | ~60 minutes |
| EventCompetition (spawned) | ~800 competitions | Auto-spawned | ~60 minutes |
| AthleteSeasons | ~5000 athletes | ~20 pages | ~30 minutes |

**Total estimated time per season: 3-4 hours**

## Monitoring & Observability

### Progress Tracking

1. **ResourceIndex records**: Check `LastCompletedUtc`, `LastPageIndex`, `TotalPageCount`
2. **Hangfire dashboard (Provider)**: View scheduled jobs, running jobs, completed jobs
3. **Downstream consumer monitoring**: Monitor job queues in consuming services to verify processing
4. **Logs**: Search for "Processing ResourceIndex" and tier completion events

### Success Criteria

A historical season sourcing is complete when:

1. All 4 ResourceIndex jobs have `LastCompletedUtc` set (Provider side)
2. Downstream consumer job queues are empty (no pending work)
3. Database contains expected entities:
   - 1 Season
   - ~130 FranchiseSeasons
   - ~800 Contests (Events)
   - ~5000 AthleteSeasons
   - Event stats, rosters, etc.

## Error Handling

### Retry Strategy

- **ResourceIndexJob failures**: Hangfire will automatically retry with exponential backoff
- **Missing dependencies**: Downstream consumers will retry until dependencies are available
- **API failures**: ESPN API failures will cause job retry

### Recovery Procedures

**If a tier fails:**
1. Check error logs in Provider
2. Reset the ResourceIndex record: `UPDATE ResourceIndex SET LastCompletedUtc = NULL, LastPageIndex = NULL WHERE Id = '{guid}'`
3. Manually re-trigger: `POST /api/resourceIndex/{id}/process`

**If processing seems stuck:**
1. Check Hangfire dashboards in Provider and downstream consumers
2. Verify no retry storms (exponentially growing job queues in consumers)
3. If needed, disable subsequent tiers until current tier completes

## Future Enhancements

### Potential Improvements

1. **Completion detection**: Add optional webhook/event when Producer finishes processing a tier
2. **Progress API**: `GET /api/sourcing/historical/seasons/{jobId}/status` with detailed progress
3. **Automatic cleanup**: Background job to delete completed historical ResourceIndex records after 30 days
4. **Multi-season bulk**: `POST /api/sourcing/historical/seasons/bulk` to source multiple seasons (2020-2024)
5. **Per-dependency overrides**: Allow `EnableDependencyRequests` override per tier/DocumentType

### Known Limitations

1. **No automatic completion verification**: Must manually verify Producer finished processing
2. **Fixed delays**: Can't dynamically adjust based on actual processing speed
3. **No rollback**: If partially complete, must manually identify and re-run failed tiers
4. **ESPN-specific**: Only supports ESPN Football NCAA currently

## Testing Strategy

### Manual Testing Process

1. **Test with recent season first** (e.g., 2024) - data is fresh, easier to verify
2. **Start with small delays** (5, 10, 30, 60 minutes) to iterate faster
3. **Monitor Hangfire dashboards** (Provider and consumers) throughout process
4. **Verify data quality** after each tier:
   - Check entity counts in database
   - Spot-check specific entities (famous players, big games)
5. **Adjust delays** based on observed processing times
6. **Document actual timings** for future reference

### Test Checklist

- [ ] Season document sourced and parsed correctly
- [ ] Venues collection complete (~300 venues)
- [ ] TeamSeasons collection complete (~130 teams)
- [ ] Events auto-spawned from TeamSeasons (verify schedule completeness)
- [ ] Bowl games included in Events (verify postseason)
- [ ] EventCompetition documents created for all events
- [ ] AthleteSeasons collection complete (~5000 athletes)
- [ ] Stats and rosters populated for events
- [ ] No retry storms in downstream consumers
- [ ] No missing dependency errors after final tier completes

## Open Questions

1. **Should we support CoachSeason tier?** - Currently excluded, but might be valuable
2. **What about GroupSeasons (conferences)?** - Current season loads these, but historical?
3. **Rankings/Polls?** - Should historical seasons include weekly rankings?
4. **Optimal delay tuning?** - Need real-world data to optimize delays
5. **Notification on completion?** - Email/Slack notification when season sourcing complete?

## Approval & Sign-off

- [x] Design reviewed and approved
- [x] Configuration values agreed upon
- [x] Error handling strategy confirmed
- [x] Monitoring approach acceptable
- [x] Ready for implementation

**Approved by**: User  
**Approval Date**: December 11, 2025  
**Implementation Target**: Dev environment - 2024 season test run

---

**Document Version**: 1.0  
**Last Updated**: December 11, 2025  
**Status**: Approved - Ready for Implementation
