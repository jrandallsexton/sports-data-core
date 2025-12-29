# Document Processor Migration Plan

## Overview

Systematic migration of all document processors to use `DocumentProcessorBase<TDataContext>`.

**Total Processors:** 51  
**Already Migrated:** 1 (EventCompetitionCompetitorDocumentProcessor)  
**Remaining:** 50

---

## Migration Priority

### ? Completed (1)
1. **EventCompetitionCompetitorDocumentProcessor** - Pilot migration (uses ProcessScores, ProcessLineScores)

### ?? High Priority - Has Child Document Publishing (Will benefit most from base class)

These processors publish child `DocumentRequested` events and will see significant code reduction:

1. **EventCompetitionDocumentProcessor** - Already has helper method, needs to inherit from base
2. **EventDocumentProcessor** - Publishes EventCompetition
3. **TeamSeasonDocumentProcessor** - Publishes multiple child documents
4. **FranchiseDocumentProcessor** - Publishes logos/images
5. **VenueDocumentProcessor** - Publishes venue images
6. **CoachDocumentProcessor** - Publishes coach records
7. **AthleteSeasonDocumentProcessor** - Publishes statistics

### ?? Medium Priority - Simple Processors (Easy migrations)

These are leaf processors or simple data persistence, minimal code changes:

8. EventCompetitionCompetitorLineScoreDocumentProcessor
9. EventCompetitionCompetitorScoreDocumentProcessor
10. EventCompetitionAthleteStatisticsDocumentProcessor
11. EventCompetitionCompetitorStatisticsDocumentProcessor
12. EventCompetitionBroadcastDocumentProcessor
13. EventCompetitionDriveDocumentProcessor
14. EventCompetitionLeadersDocumentProcessor
15. EventCompetitionOddsDocumentProcessor
16. EventCompetitionPlayDocumentProcessor
17. EventCompetitionPowerIndexDocumentProcessor
18. EventCompetitionPredictionDocumentProcessor
19. EventCompetitionProbabilityDocumentProcessor
20. EventCompetitionSituationDocumentProcessor
21. EventCompetitionStatusDocumentProcessor

### ?? Low Priority - Leaf/Simple Processors

22. AthleteDocumentProcessor
23. AthletePositionDocumentProcessor
24. AthleteSeasonStatisticsDocumentProcessor
25. AwardDocumentProcessor
26. CoachBySeasonDocumentProcessor
27. CoachRecordDocumentProcessor
28. FootballSeasonRankingDocumentProcessor
29. GroupSeasonDocumentProcessor
30. SeasonDocumentProcessor
31. SeasonFutureDocumentProcessor
32. SeasonPollDocumentProcessor
33. SeasonTypeDocumentProcessor
34. SeasonTypeWeekDocumentProcessor
35. SeasonTypeWeekRankingsDocumentProcessor
36. StandingsDocumentProcessor
37. TeamSeasonAwardDocumentProcessor
38. TeamSeasonCoachDocumentProcessor
39. TeamSeasonInjuriesDocumentProcessor
40. TeamSeasonLeadersDocumentProcessor
41. TeamSeasonProjectionDocumentProcessor
42. TeamSeasonRankDocumentProcessor
43. TeamSeasonRecordAtsDocumentProcessor
44. TeamSeasonRecordDocumentProcessor
45. TeamSeasonStatisticsDocumentProcessor

### ?? Golf Processors (Future sport)

46. GolfCalendarDocumentProcessor
47. GolfEventDocumentProcessor

### ?? Test Processors (Keep as-is for testing)

48. OutboxTestDocumentProcessor - Keep minimal for testing BaseDataContext
49. OutboxTestTeamSportDocumentProcessor - Keep minimal for testing TeamSportDataContext

---

## Migration Checklist (Per Processor)

For each processor:

- [ ] **Inherit from base class**
  - Change `: IProcessDocuments` to `: DocumentProcessorBase<TDataContext>`
  
- [ ] **Update constructor**
  - Remove field declarations for: `_logger`, `_dataContext`, `_publishEndpoint`, `_externalRefIdentityGenerator`
  - Call `: base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator)`
  - Keep processor-specific fields (like `_config`)
  
- [ ] **Add override keyword**
  - Change `public async Task ProcessAsync(...)` to `public override async Task ProcessAsync(...)`
  
- [ ] **Replace child document publishing** (if applicable)
  - Find methods that publish `DocumentRequested` events
  - Replace with `await PublishChildDocumentRequest(command, linkDto, parentId, documentType, causationId)`
  - Remove the old helper methods
  
- [ ] **Verify and test**
  - Build succeeds
  - Run unit tests
  - Check logs for emoji markers

---

## Standard Migration Pattern

### Before
```csharp
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Example)]
public class ExampleDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : TeamSportDataContext
{
    private readonly ILogger<ExampleDocumentProcessor<TDataContext>> _logger;
    private readonly TDataContext _dataContext;
    private readonly IEventBus _publishEndpoint;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

    public ExampleDocumentProcessor(
        ILogger<ExampleDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator)
    {
        _logger = logger;
        _dataContext = dataContext;
        _publishEndpoint = publishEndpoint;
        _externalRefIdentityGenerator = externalRefIdentityGenerator;
    }

    public async Task ProcessAsync(ProcessDocumentCommand command)
    {
        // ... implementation
    }
}
```

### After
```csharp
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Example)]
public class ExampleDocumentProcessor<TDataContext> 
    : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public ExampleDocumentProcessor(
        ILogger<ExampleDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator)
    {
    }

    public override async Task ProcessAsync(ProcessDocumentCommand command)
    {
        // ... implementation
    }
}
```

---

## Migration Sessions

### Session 1: High Priority Processors (7 processors)
- EventCompetitionDocumentProcessor
- EventDocumentProcessor  
- TeamSeasonDocumentProcessor
- FranchiseDocumentProcessor
- VenueDocumentProcessor
- CoachDocumentProcessor
- AthleteSeasonDocumentProcessor

**Expected time:** 30-45 minutes  
**Expected code reduction:** ~300-400 lines

### Session 2: Medium Priority - Competition Child Docs (14 processors)
- All EventCompetition* leaf processors

**Expected time:** 45-60 minutes  
**Expected code reduction:** ~100-150 lines

### Session 3: Low Priority - Simple Processors (24 processors)
- All remaining simple/leaf processors

**Expected time:** 60-90 minutes  
**Expected code reduction:** ~100-150 lines

### Session 4: Golf Processors (2 processors)
- Golf-specific processors

**Expected time:** 10-15 minutes  
**Expected code reduction:** ~20-30 lines

---

## Total Expected Impact

**Code Reduction:** ~500-730 lines eliminated  
**Consistency:** All processors use same base class and patterns  
**Maintainability:** Single source of truth for child document publishing  
**Future Benefit:** Adding new child document types becomes trivial (1 line)

---

## Ready to Start!

Which processor would you like to migrate first? I recommend we continue with the high-priority processors since they'll show the most benefit from the refactoring.

**Suggested next:** `EventCompetitionDocumentProcessor` (already has a helper method, just needs to inherit from base)
