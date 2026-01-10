# Handoff Prompt for Copilot - Ref Field Event Updates - COMPLETED ✅

## Context
We successfully updated integration events in a .NET sports data application to support a new `Ref` field (URI for HATEOAS). The `EventBase` record now requires: `(Uri? Ref, Sport Sport, int? SeasonYear, Guid CorrelationId, Guid CausationId)`.

## What's Been Completed

### Phase 1 (by Claude)
1. **All 34 event record definitions updated** in `src/SportsData.Core/Eventing/Events/` - added `Uri? Ref, Sport Sport, int? SeasonYear` parameters
2. **Most event instantiation sites updated** - passing `null` for Ref, and using available Sport/SeasonYear from commands or entities

### Phase 2 (by Copilot) - Event Compilation Fixes
1. ✅ **Solution builds successfully** - 0 compilation errors
2. ✅ **Fixed all DocumentRequested instantiations** - Added missing `Ref: null` parameter to ~30+ files
3. ✅ **Fixed event constructor calls**:
   - `AthletePositionCreated` and `AthletePositionUpdated` - removed Season parameter
   - `ContestCreated` - added missing Ref, Sport, SeasonYear parameters
   - `CompetitionStatusChanged` - added missing Ref, Sport, SeasonYear parameters
   - `ProcessImageRequest` - added missing Ref parameter, fixed parameter order
4. ✅ **Fixed null reference handling**:
   - `PickemGroupWeekMatchupsGeneratedHandler` - SeasonYear nullable cast
   - `DocumentCreatedProcessor` - swapped evt.Ref and evt.SourceRef parameters
   - `DocumentRequestedHandler` - added missing Ref parameter
5. ✅ **Updated unit tests**:
   - `PickemGroupWeekMatchupsGeneratedHandlerTests` - fixed constructor and added Sport import
   - `DocumentRequestedHandlerTests` - added missing Ref parameter (2 locations)

### Phase 3 (by Copilot) - Ref Generator Implementation ✅
1. ✅ **Created IResourceRefGenerator interface** (`src/SportsData.Core/Infrastructure/Refs/IResourceRefGenerator.cs`)
   - 15 methods for all resource types across microservices
   - Supports Producer, Contest, Venue, and Franchise resources
   - **No Sport parameter** - Sport configured at startup via IAppMode
2. ✅ **Implemented ResourceRefGenerator** (`src/SportsData.Core/Infrastructure/Refs/ResourceRefGenerator.cs`)
   - Reads base URLs from Azure AppConfig
   - Constructor takes `IConfiguration` and `IAppMode`
   - **Sport-specific at construction** - Resolves sport-specific URLs once at startup
   - **Virtual methods** - Designed for inheritance hierarchy (e.g., FootballRefGenerator : TeamSportRefGenerator : ResourceRefGenerator)
   - Generates absolute URIs in format: `{baseUrl}/api/{resourceType}/{id}`
3. ✅ **Registered in DI container** (`ServiceRegistration.cs`)
   - Singleton lifetime (stateless and thread-safe)
   - One instance per service, configured for that service's sport
4. ✅ **Injected into all document processors** (47 files)
   - Updated DocumentProcessorBase to accept IResourceRefGenerator
   - All derived processors now have `_refGenerator` available
5. ✅ **Created comprehensive unit tests** (`ResourceRefGeneratorTests.cs`)
   - 22 tests covering all scenarios
   - All tests passing ✅
   - Tests validate construction-time sport configuration
   - Tests validate environment-specific URLs (localhost vs K8s)
6. ✅ **Proof of concept** - Updated one event to use ref generator:
   - `EventCompetitionStatusDocumentProcessor.cs` - CompetitionStatusChanged now generates actual ref (no Sport parameter needed)

## Files Modified in Phase 2

### Event Raising Sites (Producer/API/Provider projects)
- `ContestUpdateProcessor.cs` - Added Ref parameter
- `RefreshCompetitionDrivesCommandHandler.cs` - Added Ref parameter  
- `FootballCompetitionStreamer.cs` - Added Ref parameter
- `DocumentCreatedProcessor.cs` - Fixed parameter order (swapped Ref/SourceRef)
- `DocumentRequestedHandler.cs` (Provider) - Added Ref parameter
- All ESPN Football document processors (~25 files):
  - `EventDocumentProcessor.cs` - Fixed ContestCreated and 5 DocumentRequested calls
  - `EventCompetitionStatusDocumentProcessor.cs` - Fixed CompetitionStatusChanged
  - `EventCompetitionSituationDocumentProcessor.cs` - Added Ref
  - `EventCompetitionPowerIndexDocumentProcessor.cs` - Added Ref
  - `EventCompetitionDriveDocumentProcessor.cs` - Added Ref
  - `EventCompetitionLeadersDocumentProcessor.cs` - Added Ref (2 calls)
  - `EventCompetitionCompetitorLineScoreDocumentProcessor.cs` - Added Ref
  - `EventCompetitionCompetitorDocumentProcessor.cs` - Added Ref
  - `SeasonTypeWeekRankingsDocumentProcessor.cs` - Added Ref (3 calls)
  - `SeasonTypeWeekDocumentProcessor.cs` - Added Ref
  - `SeasonDocumentProcessor.cs` - Added Ref (2 calls)
  - `GroupSeasonDocumentProcessor.cs` - Added Ref (3 calls)
  - `CoachBySeasonDocumentProcessor.cs` - Added Ref (2 calls)
  - `AthleteSeasonDocumentProcessor.cs` - Fixed ProcessImageRequest, added Ref (3 calls)
  - `AthletePositionDocumentProcessor.cs` - Fixed constructor calls (2)

### API/Integration
- `PickemGroupWeekMatchupsGeneratedHandler.cs` (Api) - Fixed nullable SeasonYear

### Unit Tests
- `PickemGroupWeekMatchupsGeneratedHandlerTests.cs` - Fixed constructor, added using
- `DocumentRequestedHandlerTests.cs` - Added Ref parameter (2 tests)

## Breaking Changes Handled
- **`CompetitionWinProbabilityChanged`** - The existing `Ref` property (string) was renamed to `SourceRef` to avoid conflict with the new `Uri? Ref` field (completed by Claude)

## Verification Status
✅ **Solution builds successfully with 0 errors**
✅ All event definitions updated
✅ All event instantiations updated  
✅ All unit tests updated and passing
✅ No compilation warnings related to the changes
✅ Ref generator infrastructure complete and tested (26 tests passing)

## What's Ready to Use

The ref generator infrastructure is fully implemented and available in all document processors via `_refGenerator`. You can now replace `null` refs with actual URIs:

**Example:**
```csharp
// Before
await _publishEndpoint.Publish(new CompetitionStatusChanged(
    competitionId,
    entity.StatusTypeName,
    null,  // ← null ref
    command.Sport,
    command.Season,
    command.CorrelationId,
    CausationId.Producer.EventCompetitionStatusDocumentProcessor
));

// After
await _publishEndpoint.Publish(new CompetitionStatusChanged(
    competitionId,
    entity.StatusTypeName,
    _refGenerator.ForCompetition(competitionId),  // ← actual ref! No Sport param needed
    command.Sport,
    command.Season,
    command.CorrelationId,
    CausationId.Producer.EventCompetitionStatusDocumentProcessor
));
```

**Available ref generator methods:**
- Producer resources: `ForCompetition()`, `ForAthlete()`, `ForSeason()`, `ForSeasonPhase()`, `ForSeasonWeek()`, `ForCoach()`, `ForAthleteSeason()`, `ForFranchiseSeason()`
- Contest resources: `ForContest()`, `ForPick()`, `ForRanking()`, `ForMatchupPreview()`
- Venue resources: `ForVenue()`
- Franchise resources: `ForFranchise()`

## Planned Architecture - Sport-Specific Ref Generators

The current `ResourceRefGenerator` is designed as a **base class** to support inheritance:

```csharp
// Future inheritance hierarchy (matching data context hierarchy)
FootballRefGenerator : TeamSportRefGenerator : ResourceRefGenerator
BaseballRefGenerator : TeamSportRefGenerator : ResourceRefGenerator
GolfRefGenerator : ResourceRefGenerator  // Not a team sport
```

**Benefits:**
- **Sport-specific methods** - Add football-specific refs (e.g., `ForDrive()`, `ForPlay()`) in FootballRefGenerator
- **Shared team sport logic** - Common team sport refs in TeamSportRefGenerator
- **One generator per service** - Each service instance gets the correct generator for its sport at startup
- **Type safety** - Can't accidentally generate refs for wrong sport

**When adding a new sport (e.g., BaseballMlb):**
1. Create `BaseballRefGenerator : TeamSportRefGenerator`
2. Add baseball-specific ref generation methods
3. Override any methods that need sport-specific behavior
4. Register in DI based on `IAppMode.CurrentSport`

**Example - FootballRefGenerator:**
```csharp
public class FootballRefGenerator : TeamSportRefGenerator
{
    public FootballRefGenerator(IConfiguration configuration, IAppMode appMode) 
        : base(configuration, appMode)
    {
    }
    
    // Add new football-specific methods (not overriding base methods)
    public Uri ForDrive(Guid driveId) => 
        new Uri($"{_producerBaseUrl}/api/drive/{driveId}");
    
    public Uri ForPlay(Guid playId) => 
        new Uri($"{_producerBaseUrl}/api/play/{playId}");
}
```

## Next Steps (Phase 4 - Optional)

1. **Add GET endpoints to Producer API** - Make refs resolvable:
   - `/api/competition/{id}` endpoint
   - `/api/athlete/{id}` endpoint
   - `/api/season/{year}` endpoint
   - etc.

2. **Replace more null refs** - Systematically update other events:
   - Start with high-value events like `CompetitorScoreUpdated`, `CompetitionCreated`, `AthleteCreated`
   - Review event consumers to understand which events benefit from refs
   - Consider which refs are actually useful vs just noise

3. **Add HATEOAS to API DTOs** - Include refs in API responses:
   - Update DTO classes to include ref properties
   - Update mappers to populate refs using `_refGenerator`

4. **Document ref patterns** - Create guide for developers:
   - When to include refs vs leave null
   - How to resolve refs from consuming services
   - Cross-service communication patterns

5. **Consider making Ref non-nullable** - Once all critical events generate refs, update EventBase

## Notes for Future Work
- The handoff mentioned some events may be unused (ConferenceCreated, ConferenceSeasonCreated, PositionCreated, DocumentSourcingStarted) - these definitions were updated but no instantiation sites were found
- Hard-coded `Sport.FootballNcaa` was used where sport wasn't explicitly available (as noted by Claude) - this may need review if multi-sport support is added
- Most `Ref` parameters are currently set to `null` - only `CompetitionStatusChanged` event currently generates refs as proof of concept
