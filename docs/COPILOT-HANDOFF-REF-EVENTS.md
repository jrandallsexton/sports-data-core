# Handoff Prompt for Copilot - Ref Field Event Updates - COMPLETED ✅

## Context
We successfully updated integration events in a .NET sports data application to support a new `Ref` field (URI for HATEOAS). The `EventBase` record now requires: `(Uri? Ref, Sport Sport, int? SeasonYear, Guid CorrelationId, Guid CausationId)`.

## What's Been Completed

### Phase 1 (by Claude)
1. **All 34 event record definitions updated** in `src/SportsData.Core/Eventing/Events/` - added `Uri? Ref, Sport Sport, int? SeasonYear` parameters
2. **Most event instantiation sites updated** - passing `null` for Ref, and using available Sport/SeasonYear from commands or entities

### Phase 2 (by Copilot - COMPLETED)
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
✅ All unit tests updated
✅ No compilation warnings related to the changes

## Notes for Future Work
- The handoff mentioned some events may be unused (ConferenceCreated, ConferenceSeasonCreated, PositionCreated, DocumentSourcingStarted) - these definitions were updated but no instantiation sites were found
- Hard-coded `Sport.FootballNcaa` was used where sport wasn't explicitly available (as noted by Claude) - this may need review if multi-sport support is added
- All `Ref` parameters are currently set to `null` - future work may populate these with actual HATEOAS URIs
