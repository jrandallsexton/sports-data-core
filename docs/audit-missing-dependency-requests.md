# Audit: Missing PublishDependencyRequest Calls

## Problem

When a document processor throws `ExternalDocumentNotSourcedException` for a missing dependency, it should first call `PublishDependencyRequest` to request that dependency from Provider. Without the publish, the processor retries blindly until max retries (10), then dead-letters — but the missing dependency is never requested, so replay won't help either.

## Discovered via

`TeamSeasonInjuriesDocumentProcessor` hit max retries for a missing AthleteSeason. The AthleteSeason was never requested because the processor threw without publishing. **Already fixed.**

## Audit Results

### Correctly Paired (15 processors)
These processors properly call `PublishDependencyRequest` before every `ExternalDocumentNotSourcedException` throw:

- AthleteSeasonDocumentProcessor (3 throws, 3 publishes)
- CoachSeasonDocumentProcessor (2 throws, 2 publishes)
- EventCompetitionCompetitorDocumentProcessor (1 throw, 1 publish)
- EventCompetitionCompetitorLineScoreDocumentProcessor (1 throw, 1 publish)
- EventCompetitionCompetitorScoreDocumentProcessor (1 throw, 1 publish)
- EventCompetitionCompetitorStatisticsDocumentProcessor (1 throw, 1 publish)
- EventCompetitionLeadersDocumentProcessor (2 throws, 2 publishes — uses PublishChildDocumentRequest)
- EventCompetitionSituationDocumentProcessor (1 throw, 1 publish — uses PublishChildDocumentRequest)
- EventDocumentProcessor (4 throws, 4 publishes)
- FootballAthleteDocumentProcessor (1 throw, 1 publish)
- GroupSeasonDocumentProcessor (2 throws, 2 publishes)
- SeasonTypeWeekDocumentProcessor (1 throw, 1 publish)
- SeasonTypeWeekRankingsDocumentProcessor (2 throws, 2 publishes)
- TeamSeasonDocumentProcessor (2 throws, 2 publishes)
- TeamSeasonInjuriesDocumentProcessor (1 throw, 1 publish — **just fixed**)

---

### Missing PublishDependencyRequest (6 processors)

#### 1. AthletePositionDocumentProcessor.cs — Line 123
- **Missing**: Parent AthletePosition
- **Has $ref**: YES — `dto.Parent?.Ref`
- **ESPN-sourceable**: Yes (AthletePosition documents have ESPN URLs)
- **Context**: AthletePosition has a self-referencing parent hierarchy. If parent isn't sourced yet, the child can't resolve its ParentId.
- **Fix**: Add `PublishDependencyRequest` for `DocumentType.AthletePosition` using `dto.Parent` before the throw.
- **Risk**: Low — parent positions are sourced in the same batch, so this is a timing issue.

#### 2. EventCompetitionPowerIndexDocumentProcessor.cs — Line 69
- **Missing**: Competition (by parent ID)
- **Has $ref**: NO — lookup is by `competitionIdValue` derived from `command.ParentId`
- **ESPN-sourceable**: No (Competition is created by EventCompetitionCompetitorDocumentProcessor as part of event processing)
- **Context**: PowerIndex processing runs before the parent Competition entity has been persisted. This is a processing order issue, not a missing ESPN document.
- **Fix**: No PublishDependencyRequest possible (no $ref URL). The throw-and-retry is correct behavior — the Competition will exist after its parent processor completes.
- **Risk**: None — this resolves naturally via retry.

#### 3. FranchiseDocumentProcessor.cs — Line 126
- **Missing**: Venue
- **Has $ref**: YES — `dto.Venue.Ref`
- **ESPN-sourceable**: Yes (Venues are Tier 1 documents)
- **Context**: Franchise processing expects Venue to already be sourced (Tier 1 before Tier 2). If a venue is missing, the franchise retries but never requests the venue.
- **Fix**: Add `PublishDependencyRequest` for `DocumentType.Venue` using `dto.Venue` before the throw.
- **Risk**: Medium — during historical sourcing, venues should already exist. But for edge cases (new venue, missed sourcing), this would prevent the franchise from permanently failing.

#### 4. SeasonTypeDocumentProcessor.cs — Line 77
- **Missing**: Season (parent)
- **Has $ref**: NO — lookup is by derived parent Season ID
- **ESPN-sourceable**: No (Season is created by historical sourcing kickoff, not by a document processor)
- **Context**: SeasonType (phase) processing depends on the parent Season existing. If Season isn't created yet, SeasonType retries fail.
- **Fix**: No PublishDependencyRequest possible (Season isn't an ESPN document — it's created by the sourcing saga). The throw-and-retry is correct. However, if this happens, it indicates the sourcing order is wrong (Season should be created before SeasonType).
- **Risk**: Low — indicates a sourcing orchestration issue, not a processor bug.

#### 5. TeamSeasonAwardDocumentProcessor.cs — Line 65
- **Missing**: FranchiseSeason (parent)
- **Has $ref**: NO — lookup is by `franchiseSeasonIdValue` from `command.ParentId`
- **ESPN-sourceable**: No (FranchiseSeason is created by TeamSeasonDocumentProcessor)
- **Context**: Awards are child documents of TeamSeason. If the parent FranchiseSeason hasn't been persisted yet, awards can't reference it.
- **Fix**: No PublishDependencyRequest possible (no $ref URL — the parent is already in the processing pipeline). The throw-and-retry is correct behavior.
- **Risk**: None — resolves naturally when parent completes.

#### 6. TeamSeasonLeadersDocumentProcessor.cs — Line 67
- **Missing**: FranchiseSeason (parent)
- **Has $ref**: NO — lookup is by `franchiseSeasonIdValue` from `command.ParentId`
- **ESPN-sourceable**: No (FranchiseSeason is created by TeamSeasonDocumentProcessor)
- **Context**: Leaders are child documents of TeamSeason. Same pattern as Awards above.
- **Fix**: No PublishDependencyRequest possible. Throw-and-retry is correct.
- **Risk**: None — resolves naturally when parent completes.

Note: TeamSeasonLeadersDocumentProcessor Line 133 (missing AthleteSeason batch) IS correctly paired — it loops through missing refs and publishes before throwing.

---

## Summary

| Processor | Line | Missing Entity | Has $ref | Action Needed |
|-----------|------|---------------|----------|---------------|
| AthletePositionDocumentProcessor | 123 | Parent AthletePosition | YES | **Add PublishDependencyRequest** |
| EventCompetitionPowerIndexDocumentProcessor | 69 | Competition | NO | None — retry is correct |
| FranchiseDocumentProcessor | 126 | Venue | YES | **Add PublishDependencyRequest** |
| SeasonTypeDocumentProcessor | 77 | Season | NO | None — sourcing order issue |
| TeamSeasonAwardDocumentProcessor | 65 | FranchiseSeason | NO | None — parent processing order |
| TeamSeasonLeadersDocumentProcessor | 67 | FranchiseSeason | NO | None — parent processing order |

**Action items: 2 processors need PublishDependencyRequest added:**
1. `AthletePositionDocumentProcessor.cs:123` — publish AthletePosition dependency
2. `FranchiseDocumentProcessor.cs:126` — publish Venue dependency

**Already fixed:**
- `TeamSeasonInjuriesDocumentProcessor.cs` — AthleteSeason dependency (fixed today)
