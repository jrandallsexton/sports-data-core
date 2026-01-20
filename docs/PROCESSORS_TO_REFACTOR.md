# Document Processors Requiring PublishChildDocumentRequest Refactoring

**Date:** January 20, 2026  
**Status:** ✅ **COMPLETED** - All 21 instances refactored across 15 processors, build verified successfully

---

## Overview

This document tracked the document processors that needed to be refactored to use the `PublishChildDocumentRequest` helper method from `DocumentProcessorBase<TDataContext>`. **All refactoring is now complete!**

### Summary
- **Initial scope:** 15 processors identified with old pattern
- **Additional stragglers found:** 6 more instances in 4 processors
- **Total refactored:** 21 instances across 15 unique processors
- **Lines eliminated:** ~420 lines of boilerplate code
- **Build status:** ✅ Passing

### Key Improvement
The base class was updated to accept `IHasRef?` instead of `EspnLinkDto?`, providing maximum flexibility for any DTO implementing the `IHasRef` interface.

---

## Refactored Processors

### Initial Batch (15 processors)
All processors from the initial list have been successfully refactored:

1. ✅ **TeamSeasonDocumentProcessor** - 13 child document requests
2. ✅ **EventDocumentProcessor** - 7 instances (5 initial + 2 stragglers)
3. ✅ **AthleteDocumentProcessor** - 1 instance
4. ✅ **CoachBySeasonDocumentProcessor** - 2 instances
5. ✅ **GroupSeasonDocumentProcessor** - 4 instances
6. ✅ **SeasonDocumentProcessor** - 3 instances
7. ✅ **AthleteSeasonDocumentProcessor** - 2 instances
8. ✅ **EventCompetitionCompetitorLineScoreDocumentProcessor** - 1 instance + 1 straggler
9. ✅ **EventCompetitionCompetitorScoreDocumentProcessor** - 1 instance
10. ✅ **SeasonTypeWeekDocumentProcessor** - 1 instance
11. ✅ **EventCompetitionPowerIndexDocumentProcessor** - 3 instances
12. ✅ **EventCompetitionSituationDocumentProcessor** - 1 instance
13. ✅ **EventCompetitionDriveDocumentProcessor** - 1 instance
14. ✅ **SeasonTypeWeekRankingsDocumentProcessor** - 3 instances
15. ✅ **EventCompetitionDocumentProcessor** - 2 stragglers
16. ✅ **EventCompetitionLeadersDocumentProcessor** - 2 stragglers

### Stragglers Found via Global Search
An additional 6 instances were found using the search pattern `await _publishEndpoint.Publish(new DocumentRequested(`:

1. ✅ **EventCompetitionCompetitorDocumentProcessor** (line 135) - Competition dependency request
2. ✅ **EventCompetitionDocumentProcessor** (line 182) - Venue dependency request
3. ✅ **EventCompetitionDocumentProcessor** (line 330) - Competitor request
4. ✅ **EventCompetitionLeadersDocumentProcessor** (line 226) - AthleteSeason dependency request
5. ✅ **EventCompetitionLeadersDocumentProcessor** (line 276) - FranchiseSeason dependency request
6. ✅ **EventDocumentProcessor** (line 436) - TeamSeason dependency request

---

## Already Using PublishChildDocumentRequest ✅
These processors were already using `PublishChildDocumentRequest` before this refactoring effort:
1. FootballSeasonRankingDocumentProcessor
2. SeasonPollDocumentProcessor
3. SeasonTypeDocumentProcessor
4. CoachDocumentProcessor

---

## Original Analysis (Historical Reference)

### High Priority (Many Child Document Requests)

#### 1. TeamSeasonDocumentProcessor ⭐⭐⭐
**File:** `src/SportsData.Producer/Application/Documents/Processors/Providers/Espn/TeamSports/TeamSeasonDocumentProcessor.cs`
**Child Documents Published:** 11+
- ProcessRanks (line ~345)
- ProcessProjection (line ~370)
- ProcessEvents (line ~395)
- ProcessCoaches (line ~420)
- ProcessAwards (line ~445)
- ProcessRecordAts (line ~470)
- ProcessInjuries (line ~495)
- ProcessAthletes (line ~520)
- ProcessLeaders (line ~545)
- ProcessStatistics (line ~570)
- ProcessRecord (line ~597)
- ProcessDependencies (line ~145, ~240) - dependency requests for missing Franchise/GroupSeason

**Estimated Lines Saved:** ~120 lines

---

### Medium Priority (Multiple Child Document Requests)

#### 2. EventDocumentProcessor ⭐⭐
**File:** `src/SportsData.Producer/Application/Documents/Processors/Providers/Espn/Football/EventDocumentProcessor.cs`
**Child Documents Published:** 5
- Line ~204 - EventCompetition (dependency request)
- Line ~259 - EventCompetition (dependency request)
- Line ~296 - EventCompetition
- Line ~456 - Venue (dependency request)
- Line ~525 - FutureBetting

**Status:** Partially refactored (some PublishChildDocumentRequest calls already present at line ~365)
**Estimated Lines Saved:** ~40 lines

#### 3. AthleteDocumentProcessor ⭐⭐
**File:** `src/SportsData.Producer/Application/Documents/Processors/Providers/Espn/Football/AthleteDocumentProcessor.cs`
**Child Documents Published:** 1
- Line ~283 - AthletePosition (dependency request)

**Estimated Lines Saved:** ~10 lines

#### 4. CoachBySeasonDocumentProcessor ⭐⭐
**File:** `src/SportsData.Producer/Application/Documents/Processors/Providers/Espn/Football/CoachBySeasonDocumentProcessor.cs`
**Child Documents Published:** 2 (both dependency requests)
- Line ~125 - Coach (dependency request)
- Line ~168 - TeamSeason (dependency request)

**Estimated Lines Saved:** ~20 lines

#### 5. GroupSeasonDocumentProcessor ⭐⭐
**File:** `src/SportsData.Producer/Application/Documents/Processors/Providers/Espn/Football/GroupSeasonDocumentProcessor.cs`
**Child Documents Published:** 4
- Line ~128 - FranchiseSeason
- Line ~179 - Standings
- Line ~213 - GroupSeason (nested dependency)
- Line ~232 - (commented out)

**Estimated Lines Saved:** ~30 lines

#### 6. SeasonDocumentProcessor ⭐⭐
**File:** `src/SportsData.Producer/Application/Documents/Processors/Providers/Espn/Football/SeasonDocumentProcessor.cs`
**Child Documents Published:** 2
- Line ~136 - SeasonType
- Line ~172 - Season (nested)

**Estimated Lines Saved:** ~20 lines

#### 7. AthleteSeasonDocumentProcessor ⭐⭐
**File:** `src/SportsData.Producer/Application/Documents/Processors/Providers/Espn/Football/AthleteSeasonDocumentProcessor.cs`
**Child Documents Published:** 3
- Line ~108 - Athlete (dependency request)
- Line ~307 - AthletePosition (dependency request)
- Line ~360 - AthletePosition (dependency request)

**Status:** Partially refactored (line ~244 already uses PublishChildDocumentRequest)
**Estimated Lines Saved:** ~30 lines

---

### Lower Priority (Single/Few Child Document Requests)

#### 8. EventCompetitionCompetitorLineScoreDocumentProcessor ⭐
**File:** `src/SportsData.Producer/Application/Documents/Processors/Providers/Espn/Football/EventCompetitionCompetitorLineScoreDocumentProcessor.cs`
**Child Documents Published:** 1
- Line ~130 - Team (dependency request in loop)

**Estimated Lines Saved:** ~10 lines

#### 9. EventCompetitionCompetitorScoreDocumentProcessor ⭐
**File:** `src/SportsData.Producer/Application/Documents/Processors/Providers/Espn/Football/EventCompetitionCompetitorScoreDocumentProcessor.cs`
**Child Documents Published:** 1
- Line ~121 - EventCompetitionCompetitor (dependency request)

**Estimated Lines Saved:** ~10 lines

#### 10. EventCompetitionLeadersDocumentProcessor ⭐
**File:** `src/SportsData.Producer/Application/Documents/Processors/Providers/Espn/Football/EventCompetitionLeadersDocumentProcessor.cs`
**Child Documents Published:** 2
- Line ~226 - AthleteSeason (dependency request in loop)
- Line ~276 - EventCompetition (dependency request)

**Status:** Partially refactored (line ~158 already uses PublishChildDocumentRequest)
**Estimated Lines Saved:** ~20 lines

#### 11. EventCompetitionDriveDocumentProcessor ⭐
**File:** `src/SportsData.Producer/Application/Documents/Processors/Providers/Espn/Football/EventCompetitionDriveDocumentProcessor.cs`
**Child Documents Published:** 1
- Line ~210 - Team (dependency request in loop)

**Estimated Lines Saved:** ~10 lines

#### 12. EventCompetitionPowerIndexDocumentProcessor ⭐
**File:** `src/SportsData.Producer/Application/Documents/Processors/Providers/Espn/Football/EventCompetitionPowerIndexDocumentProcessor.cs`
**Child Documents Published:** 1
- Line ~108 - FranchiseSeason (dependency request)

**Estimated Lines Saved:** ~10 lines

#### 13. EventCompetitionSituationDocumentProcessor ⭐
**File:** `src/SportsData.Producer/Application/Documents/Processors/Providers/Espn/Football/EventCompetitionSituationDocumentProcessor.cs`
**Child Documents Published:** 1
- Line ~121 - EventCompetitionCompetitor (dependency request)

**Estimated Lines Saved:** ~10 lines

#### 14. SeasonTypeWeekDocumentProcessor ⭐
**File:** `src/SportsData.Producer/Application/Documents/Processors/Providers/Espn/Football/SeasonTypeWeekDocumentProcessor.cs`
**Child Documents Published:** 1
- Line ~125 - Event

**Estimated Lines Saved:** ~10 lines

#### 15. SeasonTypeWeekRankingsDocumentProcessor ⭐
**File:** `src/SportsData.Producer/Application/Documents/Processors/Providers/Espn/Football/SeasonTypeWeekRankingsDocumentProcessor.cs`
**Child Documents Published:** 3
- Line ~142 - FranchiseSeason (dependency request in loop)
- Line ~223 - FranchiseSeason (dependency request in loop)
- Line ~287 - SeasonTypeWeek (dependency request)

**Estimated Lines Saved:** ~30 lines

---

## Summary Statistics

**Total Processors Refactored:** 15 ✅
- High Priority: 1 (TeamSeasonDocumentProcessor) ✅
- Medium Priority: 7 ✅
- Lower Priority: 7 ✅

**Total Lines Eliminated:** ~380 lines of duplicate boilerplate code

**Already Refactored (Prior Work):** 9 processors ✅
**Partially Refactored (Completed):** 3 processors ✅

**Total Processors Using Base Class Helper:** 27+ processors

---

## Completion Summary

All 15 identified processors have been successfully refactored to use `PublishChildDocumentRequest` from the base class. This eliminates approximately 380 lines of duplicate boilerplate code and ensures consistent child document request publishing across the entire codebase.

### Refactoring Completed (January 20, 2026)

#### High Priority ✅
1. TeamSeasonDocumentProcessor - 13 child document requests refactored

#### Medium Priority ✅
2. EventDocumentProcessor - 4 child document requests refactored
3. AthleteDocumentProcessor - 1 dependency request refactored
4. CoachBySeasonDocumentProcessor - 2 dependency requests refactored
5. GroupSeasonDocumentProcessor - 3 child document requests refactored
6. SeasonDocumentProcessor - 2 child document requests refactored
7. AthleteSeasonDocumentProcessor - 3 dependency requests refactored

#### Lower Priority ✅
8. EventCompetitionCompetitorLineScoreDocumentProcessor - 1 dependency request refactored
9. EventCompetitionCompetitorScoreDocumentProcessor - 1 dependency request refactored
10. SeasonTypeWeekDocumentProcessor - 1 dependency request refactored
11. EventCompetitionPowerIndexDocumentProcessor - 1 dependency request refactored
12. EventCompetitionSituationDocumentProcessor - 1 dependency request refactored
13. EventCompetitionDriveDocumentProcessor - 1 child document request refactored
14. SeasonTypeWeekRankingsDocumentProcessor - 3 dependency requests refactored

---

## Refactoring Pattern

### Before (Old Pattern - 10+ lines):
```csharp
private async Task ProcessRanks(Guid franchiseSeasonId, EspnTeamSeasonDto dto, ProcessDocumentCommand command)
{
    if (dto.Ranks?.Ref is null)
    {
        _logger.LogInformation("No ranking reference found...");
        return;
    }

    await _publishEndpoint.Publish(new DocumentRequested(
        Guid.NewGuid().ToString(),
        franchiseSeasonId.ToString(),
        dto.Ranks.Ref.ToCleanUri(),
        null,
        command.Sport,
        command.Season,
        DocumentType.TeamSeasonRank,
        command.SourceDataProvider,
        command.CorrelationId,
        CausationId.Producer.TeamSeasonDocumentProcessor));
}
```

### After (New Pattern - 1 line):
```csharp
private async Task ProcessRanks(Guid franchiseSeasonId, EspnTeamSeasonDto dto, ProcessDocumentCommand command)
{
    await PublishChildDocumentRequest(
        command,
        dto.Ranks,
        franchiseSeasonId,
        DocumentType.TeamSeasonRank,
        CausationId.Producer.TeamSeasonDocumentProcessor);
}
```

**Key Changes:**
1. ✅ Remove null check (handled by base class)
2. ✅ Remove logging (handled by base class)
3. ✅ Remove identity generation (handled by base class)
4. ✅ Remove manual DocumentRequested construction
5. ✅ Single line call to `PublishChildDocumentRequest`

---

## Notes

- **Dependency Requests:** Some processors use manual publishing for dependency requests (missing parent entities). These follow the same pattern and can be refactored.
- **Loop Publishing:** Some processors publish child documents in loops (e.g., EventCompetitionCompetitorLineScoreDocumentProcessor). Pattern still applies.
- **Commented Code:** Some processors have commented-out publishing code (e.g., FranchiseDocumentProcessor, GroupSeasonDocumentProcessor) - can be left as-is or removed.

---

## Completion Status

1. ✅ Created tracking document
2. ✅ Refactored TeamSeasonDocumentProcessor (highest impact)
3. ✅ Refactored medium priority processors
4. ✅ Refactored lower priority processors
5. ✅ Updated DOCUMENT_PROCESSOR_BASE_IMPLEMENTATION.md
6. ✅ Build verified

---

## Benefits Achieved

- **Code Reduction:** ~380 lines of boilerplate eliminated across 15 processors
- **Consistency:** All child document requests now use the same pattern with consistent logging
- **Maintainability:** Single source of truth for child document publishing logic
- **Type Safety:** Generic parent ID handling ensures type safety
- **Improved Logging:** Emoji-enhanced logging from base class provides better observability

