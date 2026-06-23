# Historical Season Sourcing Design

This document defines the architecture and implementation plan for sourcing historical season data from ESPN for NCAA Football. The goal is to systematically load complete seasons of data (teams, athletes, events, stats, etc.) in a tier-based approach that respects dependency ordering and prevents retry storms in the Producer service. Combines `HISTORICAL_SEASON_SOURCING.md` (the design) and `HistoricalSeasonSourcingAnalysis.md` (a design-review analysis of that design), previously separate root-level docs; review findings appear inline as "Analysis note" call-outs alongside the relevant design sections.

> **Overall review assessment (from design analysis):** A- (Excellent for One-Time Execution). The tier-based approach with time delays is pragmatic and appropriate given the architectural constraints (decoupled Producer/Consumer with no completion signaling). The observe-then-adjust methodology is sound engineering practice.

## Overview

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

> **Analysis note — strengths of the problem framing:** The design correctly distinguishes historical (one-time) vs. active season (recurring) sourcing requirements, identifies the retry-storm constraint as load-bearing, acknowledges Producer/Consumer decoupling (no downstream completion visibility), and recognizes ESPN's `$ref` auto-spawning behavior and cascade effects.

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

> **Analysis note — dependency hierarchy:** This ordering respects data dependencies and prevents circular references. Tier 2's auto-spawn cascade (TeamSeasons → Events → EventCompetition → stats/rosters) is the load-bearing reason Tier 3's delay is so much longer than the others.

### Important Notes

1. **Franchises already loaded** - Current season (2025) sourcing already loaded all Franchises, no need to re-source
2. **Events auto-spawn from TeamSeasons** - For completed historical seasons, the TeamSeason `Events.$ref` includes full schedule with postseason games
3. **Bowl venues may be missing** - Some bowl venues aren't in the Venues collection and will be reactively sourced via `DocumentRequested`
4. **Current season (2025) is different** - Bowl matchups aren't determined until December, so they had to be manually sourced

> **Analysis note — bowl venue strategy (recommended documentation):** Make the reactive-sourcing strategy explicit:
> - **Strategy**: Reactive sourcing via `DocumentRequested` events
> - **Rationale**: Bowl venues change yearly (rotating sites); pre-seeding would require manual curation; reactive sourcing adds minimal latency (~2-5 sec per unique venue); expected impact ~5-10 venues per season.
> - **Alternative considered**: Pre-seed known bowl venues (rejected due to maintenance overhead).

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
| `force` | `bool` | No | If true, re-schedules jobs even if the season has already been sourced. Defaults to false. |

### Tier Delay Keys

- `season` - Delay before processing Season document (typically 0)
- `venue` - Delay before processing Venues collection (after Season completes)
- `teamSeason` - Delay before processing TeamSeasons (after Venues processing window)
- `athleteSeason` - Delay before processing AthleteSeasons (after TeamSeasons + Events processing window)

**Note**: No delay needed after last tier (AthleteSeasons) since it's the final tier.

### Response

```json
{
  "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "message": null
}
```

| Field | Type | Description |
|-------|------|-------------|
| `correlationId` | `Guid` | Correlation ID for tracking the sourcing job in logs and monitoring. |
| `message` | `string?` | Optional message with additional context (e.g., force reschedule status, warnings). |

The `correlationId` can be used to track the sourcing job in logs and monitoring dashboards.

> **Analysis note — API design strengths:** Clean RESTful endpoint, tunable delays support operational adjustment, `correlationId` response enables tracking.

### Saga Endpoint

```http
POST /api/sourcing/historical/seasons/saga
```

Creates ResourceIndex records for saga-based orchestration (event-driven tier progression instead of time-based delays). The saga controls when each tier starts based on `DocumentProcessingCompleted` events from Producer. See [historical-sourcing-saga-design.md](./historical-sourcing-saga-design.md) for details.

## Implementation Details

### URI Generation

URI construction is handled by the `HistoricalSourcingUriBuilder` class (injected via `IHistoricalSourcingUriBuilder`), which reads `EspnBaseUrl` from `HistoricalSourcingConfig`:

```csharp
public class HistoricalSourcingUriBuilder : IHistoricalSourcingUriBuilder
{
    private readonly HistoricalSourcingConfig _config;

    public Uri BuildUri(DocumentType documentType, int seasonYear, Sport sport, SourceDataProvider provider)
    {
        if (sport == Sport.FootballNcaa && provider == SourceDataProvider.Espn)
        {
            return BuildEspnFootballNcaaUri(documentType, seasonYear);
        }
        throw new NotSupportedException(...);
    }

    private Uri BuildEspnFootballNcaaUri(DocumentType documentType, int seasonYear)
    {
        var baseUrl = _config.EspnBaseUrl.TrimEnd('/');
        var path = documentType switch
        {
            DocumentType.Season => $"{baseUrl}/seasons/{seasonYear}",
            DocumentType.Venue => $"{baseUrl}/venues",
            DocumentType.TeamSeason => $"{baseUrl}/seasons/{seasonYear}/teams",
            DocumentType.AthleteSeason => $"{baseUrl}/seasons/{seasonYear}/athletes",
            _ => throw new ArgumentException(...)
        };
        return new Uri(path);
    }
}
```

### ResourceIndex Record Creation

For each tier, create a **non-recurring** ResourceIndex record:

```csharp
var resourceIndex = new ResourceIndex
{
    Id = Guid.NewGuid(),
    Ordinal = baseOrdinal * 100L + i, // Timestamp-based ordinal (YYYYMMDDHHmmssfff * 100 + tierIndex)
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

> **Analysis note — ordinal collision prevention:** The timestamp-based ordinal (`baseOrdinal = long.Parse(DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"))`, `ordinal = baseOrdinal * 100L + i`) is collision-resistant under single-threaded execution. Example: `2026030814230012300` (base * 100 + tierIndex). If bulk sourcing is implemented later, revisit to confirm concurrent requests can't collide on the millisecond boundary.

### Job Scheduling

Use Hangfire's `.Schedule()` to enqueue jobs with `TimeSpan` delays:

```csharp
foreach (var (resourceIndex, delay) in resourceIndexes)
{
    var jobDefinition = new DocumentJobDefinition(resourceIndex);

    backgroundJobProvider.Schedule<ResourceIndexJob>(
        job => job.ExecuteAsync(jobDefinition),
        delay); // TimeSpan.FromMinutes(tier.DelayMinutes)
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

> **Analysis note — defaults are conservatively correct:** For one-time execution, erring on the side of longer delays is the right call. Better to wait too long than trigger retry storms.

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

> **Analysis note — minimal database footprint:** 4 ResourceIndex records per season with `IsRecurring = false` provides audit trail, re-run capability, and progress monitoring at negligible storage cost (20 records for 5 historical seasons).

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

> **Analysis note — enhanced observability (implemented):** `TIER_STARTED` and `TIER_COMPLETED` logging has been implemented in `ResourceIndexJob.cs`. The originally-recommended shape:
>
> ```csharp
> // At tier scheduling
> _logger.LogInformation(
>     "Historical sourcing tier scheduled. Tier={TierName}, Season={SeasonYear}, " +
>     "ScheduledTime={ScheduledTime}, DelayMinutes={DelayMinutes}, CorrelationId={CorrelationId}",
>     tierName, seasonYear, scheduledTime, delayMinutes, correlationId);
>
> // At tier start
> _logger.LogInformation(
>     "Historical sourcing tier started. Tier={TierName}, Season={SeasonYear}, " +
>     "ResourceIndexId={ResourceIndexId}, PageCount={PageCount}, CorrelationId={CorrelationId}",
>     tierName, seasonYear, resourceIndexId, totalPageCount, correlationId);
>
> // At tier completion
> _logger.LogInformation(
>     "Historical sourcing tier completed. Tier={TierName}, Season={SeasonYear}, " +
>     "Duration={DurationMinutes}m, PagesProcessed={PageCount}, CorrelationId={CorrelationId}",
>     tierName, seasonYear, duration.TotalMinutes, pageCount, correlationId);
> ```

> **Analysis note — status polling endpoint (recommended):** To close the "must manually verify Producer finished processing" gap, add a lightweight status tracking endpoint:
>
> ```http
> GET /api/sourcing/historical/seasons/{correlationId}/status
> ```
>
> ```json
> {
>   "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
>   "seasonYear": 2024,
>   "sport": "FootballNcaa",
>   "startedAt": "2025-12-11T10:00:00Z",
>   "currentTier": "TeamSeason",
>   "tiers": [
>     { "name": "Season",       "status": "Completed",  "scheduledAt": "2025-12-11T10:00:00Z", "completedAt": "2025-12-11T10:02:00Z", "durationMinutes": 2 },
>     { "name": "Venue",        "status": "Completed",  "scheduledAt": "2025-12-11T10:30:00Z", "completedAt": "2025-12-11T10:38:00Z", "durationMinutes": 8, "pagesProcessed": 2 },
>     { "name": "TeamSeason",   "status": "InProgress", "scheduledAt": "2025-12-11T11:00:00Z", "progress": "1/1 pages" },
>     { "name": "AthleteSeason","status": "Scheduled",  "scheduledAt": "2025-12-11T15:00:00Z" }
>   ]
> }
> ```

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

> **Analysis note — error recovery endpoint (post-2024 enhancement):** Replace the manual SQL update + re-trigger flow with a proper retry endpoint once the manual procedure has been exercised in production:
>
> ```http
> POST /api/sourcing/historical/seasons/{correlationId}/retry
> ```
>
> ```json
> {
>   "restartFromTier": "TeamSeason",
>   "resetDownstreamJobs": false
> }
> ```
>
> Response:
>
> ```json
> {
>   "correlationId": "new-guid",
>   "retriedTiers": ["TeamSeason", "AthleteSeason"],
>   "message": "Retry scheduled. Original jobs preserved for audit."
> }
> ```

## Future Enhancements

### Potential Improvements

1. **Completion detection**: Add optional webhook/event when Producer finishes processing a tier
2. **Progress API**: `GET /api/sourcing/historical/seasons/{jobId}/status` with detailed progress
3. **Automatic cleanup**: Background job to delete completed historical ResourceIndex records after 30 days
4. **Multi-season bulk**: `POST /api/sourcing/historical/seasons/bulk` to source multiple seasons (2020-2024)
5. **Per-dependency overrides**: Allow `EnableDependencyRequests` override per tier/DocumentType

> **Analysis note — bulk season sourcing sketch:**
>
> ```http
> POST /api/sourcing/historical/seasons/bulk
> ```
>
> ```json
> {
>   "sport": "FootballNcaa",
>   "sourceDataProvider": "Espn",
>   "seasonYears": [2020, 2021, 2022, 2023],
>   "delayBetweenSeasons": 60
> }
> ```

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

> **Analysis note — actual timing capture template:** Once 2024 has run, capture real-world data in a structured format to drive future delay tuning:
>
> ```markdown
> ## 2024 Season Actual Timings
>
> **Execution Date**: [Date]
> **Total Duration**: [Duration]
> **Infrastructure**: [Environment details]
>
> | Tier | Configured Delay | Actual Start | Actual Completion | Processing Duration | Queue Clear Time | Recommended Delay |
> |------|------------------|--------------|-------------------|---------------------|------------------|-------------------|
> | Season       | 0 min   | 10:00 | 10:02 | 2 min  | 10:05 | 0 min (keep)         |
> | Venue        | 30 min  | 10:30 | 10:38 | 8 min  | 10:45 | 20 min (reduce)      |
> | TeamSeason   | 60 min  | 11:00 | 11:12 | 12 min | 14:30 | 240 min (increase)   |
> | AthleteSeason| 240 min | 15:00 | 15:28 | 28 min | 15:30 | 300 min (buffer)     |
>
> **Observations**:
> - TeamSeason spawned 847 Events (cascade took 3.5 hours to process)
> - No retry storms observed
> - 3 bowl venues reactively sourced (Rose Bowl, Sugar Bowl, Peach Bowl)
>
> **Recommendations for 2020-2023**:
> - Increase TeamSeason delay to 240 min (4 hours for cascade completion)
> - Reduce Venue delay to 20 min (processing faster than expected)
> ```

## Open Questions

1. **Should we support CoachSeason tier?** - Currently excluded, but might be valuable
2. **What about GroupSeasons (conferences)?** - Current season loads these, but historical?
3. **Rankings/Polls?** - Should historical seasons include weekly rankings?
4. **Optimal delay tuning?** - Need real-world data to optimize delays
5. **Notification on completion?** - Email/Slack notification when season sourcing complete?

> **Analysis note — proposed resolutions for the open questions:**
>
> | Question | Recommendation | Priority |
> |----------|----------------|----------|
> | Support CoachSeason tier? | **Defer** — not critical for analytics MVP. Add if user stories require it. | Low |
> | GroupSeasons (conferences)? | **Include** — conference standings are important for historical context. Add as Tier 2.5 (after TeamSeasons, before AthleteSeasons). | Medium |
> | Rankings/Polls? | **Defer** — nice-to-have for historical context, not MVP. Consider as a separate feature. | Low |
> | Optimal delay tuning? | **Measure** — 2024 run will provide real-world data. | High |
> | Notification on completion? | **Manual for 2024** — consider email/Slack notification for bulk runs (2020-2023). | Low |

## Execution Plan for 2024 Season

> **Analysis note — execution plan (from design review):** A structured checklist for the pre-, during-, and post-execution phases of the inaugural 2024 run.

### Pre-Execution

- [ ] Deploy status endpoint (`GET /api/sourcing/historical/seasons/{correlationId}/status`)
- [ ] Add comprehensive logging (tier scheduling, start, completion with timings)
- [ ] Document bowl venue reactive sourcing strategy
- [ ] Prepare timing capture template
- [ ] Verify Hangfire dashboard access (Provider and Consumer services)

### During Execution (Active Monitoring)

- [ ] Call status endpoint every 30 minutes
- [ ] Monitor Hangfire dashboards for job progression
- [ ] Watch for retry activity in consumer logs
- [ ] Note actual completion times for each tier
- [ ] Check downstream queue depths before each tier starts

### Post-Execution (Analysis)

- [ ] Document actual timings vs. configured delays
- [ ] Calculate recommended delays for future seasons
- [ ] Verify data completeness:
  - 1 Season document
  - ~130 FranchiseSeasons
  - ~800-900 Contests (Events)
  - ~5000 AthleteSeasons
  - Event stats and rosters populated
- [ ] Document any issues (missing venues, retry storms, etc.)
- [ ] Update default delays in `appsettings.json`

### Future Seasons (2020-2023)

- [ ] Use updated delays from 2024 analysis
- [ ] Consider bulk sourcing API if running multiple seasons
- [ ] Monitor first season closely, then allow unattended execution

## Risk Assessment

> **Analysis note — risk register:**
>
> | Risk | Likelihood | Impact | Mitigation | Status |
> |------|-----------|--------|------------|--------|
> | Delays too short → retry storms | Medium | Critical | Conservative defaults, active monitoring | Mitigated |
> | Missing bowl venues | Medium | Low | Reactive sourcing via DocumentRequested | Acceptable |
> | Manual error recovery | Low | Medium | Document recovery procedures, add retry endpoint post-2024 | Acceptable |
> | Ordinal collisions | Low | Low | Single-threaded execution for 2024, fix if bulk sourcing implemented | Deferred |
> | No completion tracking | Medium | Low | Status endpoint resolves | Resolved (with status endpoint) |
> | TeamSeason cascade underestimated | Medium | Medium | 240-min delay accounts for cascade, monitor closely | Watch |

## Approval & Sign-off

- [x] Design reviewed and approved
- [x] Configuration values agreed upon
- [x] Error handling strategy confirmed
- [x] Monitoring approach acceptable
- [x] Ready for implementation

**Approved by**: User  
**Approval Date**: December 11, 2025  
**Implementation Target**: Dev environment - 2024 season test run

> **Analysis note — final review recommendation:** **Ship it.** The design is production-ready for the 2024 season run with the following conditions: (1) implement status endpoint (1-2 hours dev), (2) add comprehensive logging (30 minutes dev), (3) document bowl venue strategy, (4) active monitoring during execution (dedicated 4-hour window), (5) post-execution analysis (capture timings for future optimization).
>
> The observe-then-adjust approach is textbook engineering: make it work (2024 with conservative delays) → make it right (analyze actuals) → make it fast (optimize for 2020-2023). Time-based delays are appropriate for one-time runs with observation; the "fragility" concern is moot when you're actively monitoring and adjusting; manual verification is acceptable for a single season before automating.
>
> **Summary scorecard:**
>
> | Aspect | Assessment | Notes |
> |--------|-----------|-------|
> | Architecture | Excellent | Tier-based dependency ordering is correct |
> | API Design | Excellent | Clean, tunable, returns tracking ID |
> | Default Delays | Good | Conservative for first run, tunable for future |
> | Observability | Needs Work | Add logging and status endpoint before execution |
> | Error Handling | Acceptable | Manual recovery OK for 2024, automate later |
> | Documentation | Excellent | Comprehensive, clear rationale for decisions |
> | Testing Strategy | Good | Pragmatic observe-then-adjust approach |

---

**Document Version**: 1.0 (merged from design doc + design-review analysis)  
**Last Updated**: December 11, 2025  
**Status**: Approved - Ready for Implementation
