# Producer TODO Audit

Audit date: 2026-03-10

## Real Implementation Gaps

### 5. GetContestOverviewQueryHandler.cs:482
```
// TODO: Implement team stats retrieval logic
```
`GetTeamStatsAsync` is stubbed out with commented-out query code. Determine if this code path is reachable or dead.

### 7. CalculateCompetitionMetricsCommandHandler.cs:113,145
```
// Special teams / Discipline (TODO)
NetPunt = 0m,
FgPctShrunk = ...
```
Special teams and discipline metrics are hardcoded to `0m` in both home and away metric calculations. Depends on whether source data for these metrics is now available.

### 8. SeasonTypeWeekRankingsDocumentProcessor.cs:75
```
// TODO: At the end of the season, correct this data and adjust for next season
```
Rankings are offset by one week (ESPN publishes Week 9 poll after Week 9 games, used for Week 10). End-of-season correction logic is missing. Could affect historical data accuracy for final rankings.

## Feature Backlog

### 9. ~~TeamSeasonDocumentProcessor.cs:238~~ — RESOLVED
TODO removed. `TeamSeasonNoteDocumentProcessor` stub created. TeamSeasonDocumentProcessor already publishes child requests for `DocumentType.TeamSeasonNote`. ESPN currently returns empty collections for all TeamSeasonNote refs, so the processor logs and returns. Full implementation will follow the `AthleteSeasonNoteDocumentProcessor` pattern once real data is available.

### 10. TeamSeasonDocumentProcessor.cs:290-291
```
// TODO: LOW: Add recruiting class sourcing - .../recruiting/{year}/classes/{teamId}
// TODO: LOW: Add college entity sourcing - .../colleges/{collegeId}
```
Two new ESPN resource types that could be sourced. Tagged as low priority.

### 11. ContestEnrichmentProcessor.cs:78
```
// TODO: Do we _actually_ need to fetch the status? If the StartTime is in the past,
// and we are running this job, isn't it safe to assume the game is over?
```
Possible simplification of contest enrichment logic. The status is already available on `competition.Status`.

### 12. ContestEnrichmentProcessor.cs:228
```
// TODO: Later we might want to score each odd individually - or even see if they were updated
```
Currently only processes the first matching odds provider (ESPN Bet or DraftKings). Individual odds scoring and update detection deferred.

### 13. VenueExtensions.cs:64 / GetAllVenuesQueryHandler.cs:77
```
Images = new List<VenueImageDto>(), // TODO: Add image metadata
// TODO: Add Images projection once LogoDtoBase issue is resolved
```
Venue image metadata is not included in DTO projections. Blocked by a `LogoDtoBase` issue.

## Duplicate Pattern: Image Event Publishing (4 files)

All four processors ask the same question:
```
// TODO: Do I REALLY need to publish this event? It will just cause more work for downstream
```

Files:
- `VenueImageRequestProcessor.cs:134`
- `GroupSeasonLogoRequestProcessor.cs:73`
- `FranchiseSeasonLogoRequestProcessor.cs:64`
- `FranchiseLogoRequestProcessor.cs:66`

When an image already exists, these processors still publish a `ProcessImageResponse` event. This creates unnecessary downstream work. A single decision applies to all four: either skip the publish when the image exists, or document why it's needed.

## Optimization

### 14. ~~EventCompetitionAthleteStatisticsDocumentProcessor.cs:118~~ — RESOLVED
Removed TODO and cleaned up comments. The const `maxConcurrencyRetries = 3` is appropriate as-is; runtime tuning via AppConfig is not worth the overhead for a retry count on an edge case.

### 15. EventCompetitionDriveDocumentProcessor.cs:181
```
// TODO: we could optimize this by doing a batch query for all play identities instead of one at a time
```
Currently queries play identities one-by-one in a foreach loop. A batch query would reduce DB round-trips during drive processing.

### 15. ImageProcessorFactory.cs:50
```
// TODO: refactor this as a singleton or static to avoid re-building the map on every request
```
`RegisterProcessors()` uses reflection to scan assemblies and build a processor map. This runs on every factory instantiation. Easy singleton/static refactor.
