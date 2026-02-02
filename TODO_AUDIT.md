# TODO Audit - February 2, 2026

## Overview
Comprehensive audit of all TODO/FIXME/HACK comments before historical sourcing run.

---

## SportsData.Producer (50+ TODOs)

### Document Processors - Retry Logic
**Count:** ~20 instances
**Pattern:** `command.ToDocumentCreated(command.AttemptCount + 1)`
**Files:** Most document processors (EventCompetition*, TeamSeason*, etc.)
**Priority:** LOW - This is working pattern, comment may be outdated
**Action:** Review if this needs to remain a TODO or just remove comment

### Hard-coded Values
1. **SeasonYear hard-coded**
   - File: `FranchiseSeasonEnrichmentJob.cs:14`
   - Code: `const int SEASON_YEAR = 2025;`
   - Priority: MEDIUM
   - Action: Make configurable or dynamic

2. **Sport.FootballNcaa hard-coded**
   - Files:
     - `EnqueueSeasonWeekContestsUpdateCommandHandler.cs:56`
     - `FranchiseSeasonController.cs:47`
   - Priority: HIGH - Blocks multi-sport
   - Action: Resolve from mode/context

3. **SourceDataProvider.Espn hard-coded**
   - File: `RefreshCompetitionDrivesCommandHandler.cs:117`
   - Priority: MEDIUM
   - Action: Pass provider via command

### Unimplemented Features
1. **TeamSeasonInjuriesDocumentProcessor**
   - File: `TeamSeasonInjuriesDocumentProcessor.cs:57-58`
   - Status: Skeleton only
   - Priority: LOW (nice-to-have)
   - Decision: Defer or delete?

2. **StandingsDocumentProcessor**
   - File: `StandingsDocumentProcessor.cs:28`
   - Status: Skeleton only
   - Priority: LOW (can derive from game results)
   - Decision: Defer or delete?

3. **AwardDocumentProcessor**
   - File: `AwardDocumentProcessor.cs:28`
   - Status: Skeleton only
   - Priority: NONE (redundant with TeamSeasonAwardDocumentProcessor)
   - Decision: DELETE

4. **Athlete rename**
   - File: `AthleteDocumentProcessor.cs:21`
   - Note: `// TODO: Rename to FootballAthleteDocumentProcessor`
   - Priority: LOW
   - Action: Rename for clarity

### Data Quality Issues
1. **Null Description in Drives**
   - File: `CompetitionDriveExtensions.cs:28`
   - Issue: `Description` sometimes empty
   - Priority: LOW
   - Action: Investigate ESPN data or accept default

2. **Null SequenceNumber in Drives**
   - File: `CompetitionDriveExtensions.cs:46`
   - Issue: `SequenceNumber` sometimes null
   - Priority: LOW
   - Action: Log warning when "-1" (line 168)

3. **Null Text in Plays**
   - File: `CompetitionPlayExtensions.cs:53`
   - Issue: `Text` sometimes null
   - Priority: LOW
   - Action: Investigate ESPN data

### Missing Dependencies
1. **Venue not found in Franchise**
   - File: `FranchiseDocumentProcessor.cs:149`
   - Code: `// TODO: What to do if the venue does not exist?`
   - Priority: MEDIUM
   - Action: Request venue document or log warning

2. **Sport middleware filtering**
   - File: `DocumentCreatedHandler.cs:15`
   - Note: Filter DocumentCreated events by Sport mode
   - Priority: LOW
   - Action: May not be needed with proper routing

### Missing Features
1. **Team Season Notes**
   - File: `TeamSeasonDocumentProcessor.cs:321`
   - Note: Data not available when following link
   - Priority: LOW
   - Action: Verify if ESPN provides this

2. **Recruiting Class**
   - File: `TeamSeasonDocumentProcessor.cs:378`
   - Note: `/recruiting/{year}/classes/{teamId}`
   - Priority: LOW
   - Action: Future enhancement

3. **College Entity**
   - File: `TeamSeasonDocumentProcessor.cs:379`
   - Note: `/colleges/{collegeId}` (university metadata)
   - Priority: LOW
   - Action: Future enhancement

### Image Processing
1. **Event publishing**
   - Files: Multiple image processors
   - Note: `// TODO: Do I REALLY need to publish this event?`
   - Priority: LOW
   - Action: Review if downstream needs notification

2. **Image metadata**
   - File: `VenueExtensions.cs:64`
   - Note: Add image metadata to VenueImageDto
   - Priority: LOW

3. **Factory singleton**
   - File: `ImageProcessorFactory.cs:50`
   - Note: Refactor to singleton to avoid rebuilding map
   - Priority: LOW (minor optimization)

### Competition/Contest Features
1. **Team stats retrieval**
   - File: `GetContestOverviewQueryHandler.cs:418`
   - Status: Not implemented
   - Priority: MEDIUM
   - Action: Implement or remove

2. **CompetitionBroadcast domain event**
   - File: `TeamSeasonRankDocumentProcessor.cs:105`
   - Priority: LOW
   - Action: Implement if needed for real-time updates

3. **Competition metrics**
   - Files: `CalculateCompetitionMetricsCommandHandler.cs` (multiple)
   - Notes: Special teams/discipline, season from competition
   - Priority: MEDIUM

---

## SportsData.Provider (22 TODOs)

### Configuration
1. **Cosmos container hard-coded**
   - File: `CosmosDocumentService.cs:28`
   - Code: `"FootballNcaa"` container
   - Priority: HIGH - Blocks multi-sport
   - Action: Get from AzAppConfig based on mode

2. **Partition key decision**
   - File: `CosmosDocumentService.cs:154`
   - Note: "HORRIBLE decision" - should have used entire hash
   - Priority: MEDIUM
   - Action: Migration needed?

3. **Config split**
   - File: `ProviderDocDatabaseConfig.cs:3`
   - Note: Split into MongoDB and CosmosDB classes
   - Priority: LOW

4. **CommonConfig duplication**
   - Files: `ResourceIndexItemProcessor.cs` (lines 212, 264)
   - Note: Pull from CommonConfig at class root
   - Priority: LOW

### Controller/CQRS
1. **DocumentController refactor**
   - File: `DocumentController.cs:19`
   - Note: Move everything to CQRS
   - Priority: MEDIUM
   - Action: Multiple cleanup TODOs in this file

2. **Provider client selection**
   - File: `DocumentController.cs:133`
   - Note: Get correct providerClient (only ESPN now)
   - Priority: LOW (ESPN only for foreseeable future)

### Event Handling
1. **DocumentUpdated**
   - File: `ResourceIndexItemProcessor.cs:271`
   - Note: Put back to DocumentUpdated (need handler in Producer)
   - Priority: MEDIUM
   - Action: Implement handler first

2. **BypassCache always true**
   - File: `DocumentRequestedHandler.cs:125`
   - Note: Cannot think of reason to cache here
   - Priority: LOW
   - Action: Remove parameter if truly never used

3. **URI vs source URL**
   - File: `PublishDocumentEventsProcessor.cs:125`
   - Note: Should be source URL, not URI
   - Priority: LOW

### Job Orchestration
1. **Scheduled jobs**
   - File: `SourcingJobOrchestrator.cs:59`
   - Note: Wire scheduled jobs if/when defined
   - Priority: LOW (on-demand works for now)

2. **Polling jobs**
   - File: `SourcingJobOrchestrator.cs:70`
   - Note: Wire polling jobs if/when defined
   - Priority: LOW

3. **EndpointMask**
   - File: `DocumentJobDefinition.cs:30`
   - Note: Do I need this?
   - Priority: LOW
   - Action: Remove if unused

### Logging
1. **MongoDB logging**
   - File: `MongoDocumentService.cs:149`
   - Note: Re-enable logging if needed?
   - Priority: LOW

---

## SportsData.Core (5 TODOs)

### Configuration
1. **SQL connection string**
   - File: `ServiceRegistration.cs:65`
   - Note: "Clean up this hacky mess"
   - Priority: LOW (works, just ugly)

2. **Retry policy**
   - File: `RetryPolicy.cs:40`
   - Note: Extract retry count/delay from config
   - Priority: LOW

### Features
1. **DashboardAuthFilter**
   - File: `DashboardAuthFilter.cs:14`
   - Status: Not implemented
   - Priority: MEDIUM (needed for security?)

2. **Season rankings**
   - File: `SeasonClient.cs:26`
   - Note: Expose rankings endpoint
   - Priority: LOW

3. **DTO type change**
   - File: `EspnFootballSeasonTypeWeekRankingsDto.cs:66`
   - Note: Change to EspnLinkDto once verified
   - Priority: LOW

---

## SportsData.Venue (5 TODOs)

### Configuration
1. **Ports in AzAppConfig**
   - File: `Program.cs:30`
   - Priority: MEDIUM
   - Action: Move to commonConfig

### Handler Issues
1. **VenueCreatedHandler**
   - File: `VenueCreatedHandler.cs` (multiple)
   - Notes:
     - Line 47: `Id = -1` - revisit when service online
     - Line 55: SourceUrlHash placeholder
     - Line 61: Raise cache invalidation event?
   - Priority: LOW (service not active)
   - Action: Complete when Venue service activated

---

## SportsData.Api
**Status:** No TODOs found

---

## Recommendations by Priority

### HIGH Priority (Blocks Multi-Sport)
1. ✅ **Multi-sport migrations** - DONE (per-sport folders)
2. ❌ **Cosmos container selection** - Provider.CosmosDocumentService.cs:28
3. ❌ **Sport.FootballNcaa hard-coding** - Multiple files in Producer

### MEDIUM Priority (Quality/Features)
1. Season year dynamic/configurable
2. Venue dependency handling (FranchiseDocumentProcessor)
3. DocumentController CQRS refactor
4. DocumentUpdated event handler
5. DashboardAuthFilter implementation
6. Team stats retrieval

### LOW Priority (Nice-to-Have/Cleanup)
1. Remove AwardDocumentProcessor (redundant)
2. Decide on TeamSeasonInjuries/Standings (defer or delete)
3. Image event publishing review
4. Config extraction/cleanup
5. Various data quality investigations

### DEFER
1. Recruiting class sourcing
2. College entity sourcing
3. Team season notes (if ESPN doesn't provide)
4. Polling/scheduled job infrastructure (on-demand works)

---

## Proposed Work Order

### Session 1: Multi-Sport Blockers
- [ ] Cosmos container mode-based selection
- [ ] Remove Sport.FootballNcaa hard-coding
- [ ] Test multi-sport configuration

### Session 2: Cleanup/Deletions
- [ ] Delete AwardDocumentProcessor
- [ ] Decide on TeamSeasonInjuries (keep skeleton or delete)
- [ ] Decide on Standings (defer implementation)
- [ ] Athlete processor rename

### Session 3: Medium Priority Features
- [ ] Dynamic season year
- [ ] Venue dependency handling
- [ ] DocumentController CQRS refactor

### Session 4: Low Priority Cleanup
- [ ] Config extractions
- [ ] Image event publishing review
- [ ] Remove unnecessary TODOs

---

**Next Steps:** Which session/project should we tackle first?
