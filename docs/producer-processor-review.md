# Producer Document Processor Code Review

**Date:** 2026-03-13
**Scope:** All ~47 DocumentProcessors in `src/SportsData.Producer/Application/Documents/Processors/`
**Reviewed by:** Claude Code (5 parallel agents covering A-D, E-L, M-R, S-Z, and base class)

---

## Critical Bugs (fix first)

### 1. ~~SeasonTypeWeekRankingsDocumentProcessor — `seasonPollId` never assigned on fallback path~~ FIXED

- **File:** `Providers/Espn/Football/SeasonTypeWeekRankingsDocumentProcessor.cs`, lines 49-64
- **Impact:** Data corruption. When `ParentId` is not a valid GUID, the fallback block finds the poll but never assigns the resolved ID to `seasonPollId`. Every poll week is created with `seasonPollId = Guid.Empty`.
- **Fixed in:** PR #160. Added `seasonPollId = seasonPoll.Id;` on the fallback path. Tests added.

### 2. ~~DocumentProcessorBase — Missing `SaveChangesAsync` on success path~~ FALSE POSITIVE

- **File:** `DocumentProcessorBase.cs`, lines 55-63
- **Impact:** In the retry `catch` block (line 79), `SaveChangesAsync` is called to flush the MassTransit outbox. But on the success path, `SaveChangesAsync` is only called when `NotifyOnCompletion` is true. Entity changes and outbox messages from `PublishChildDocumentRequest` calls may not be persisted.
- **Resolution:** Not a bug. All concrete processors call `SaveChangesAsync` themselves on the happy path. This is by design.

### 3. ~~EventCompetitionPowerIndexDocumentProcessor — Duplicate rows on re-processing~~ FIXED

- **File:** `Providers/Espn/Football/EventCompetitionPowerIndexDocumentProcessor.cs`, lines 105-141
- **Impact:** No duplicate check. Re-processing the same document always ADDs power index records without checking if they already exist. Creates duplicate `CompetitionPowerIndex` rows.
- **Fixed in:** PR #161. Added in-memory duplicate check + unique DB index on `(CompetitionId, FranchiseSeasonId, PowerIndexId)`. Migration with dedup cleanup in PR #163. Tests added.

### 4. ~~DocumentCreatedProcessor — Invalid documents silently swallowed~~ FALSE POSITIVE

- **File:** `DocumentCreatedProcessor.cs`, lines 85-89
- **Impact:** When `IsInvalidDocument` returns true, the method logs an error and returns normally. Hangfire marks the job as succeeded. The document is permanently lost with no retry.
- **Resolution:** Not a bug. Provider uses a transactional outbox — the `DocumentCreated` event is only published after the document is persisted to Mongo. If the document is empty/null at this point, it is genuinely invalid and retrying would not help.

---

## Bugs (should fix)

### 5. ~~AthletePositionDocumentProcessor — Wrong exception type prevents retry~~ FIXED

- **File:** `Providers/Espn/Football/AthletePositionDocumentProcessor.cs`, line 120
- **Impact:** Throws `InvalidOperationException` instead of `ExternalDocumentNotSourcedException` when parent position is not yet sourced. The base class only handles `ExternalDocumentNotSourcedException` for retry logic.
- **Fixed in:** PR #161. Changed to `ExternalDocumentNotSourcedException`. Test updated.

### 6. ~~SeasonTypeDocumentProcessor — Wrong exception type prevents retry~~ FIXED

- **File:** `Providers/Espn/Football/SeasonTypeDocumentProcessor.cs`, line 75
- **Impact:** Throws generic `Exception` instead of `ExternalDocumentNotSourcedException` when parent Season is not found. Document fails permanently rather than retrying.
- **Fixed in:** PR #161. Changed to `ExternalDocumentNotSourcedException`. Test added.

### 7. AthleteSeasonStatisticsDocumentProcessor — Non-atomic delete+insert

- **File:** `Providers/Espn/Football/AthleteSeasonStatisticsDocumentProcessor.cs`, lines 79-86
- **Impact:** Intermediate `SaveChangesAsync` after delete. If the subsequent insert fails, statistics are permanently lost. Should delete and insert within a single `SaveChangesAsync` call.

### 8. ~~EventCompetitionOddsDocumentProcessor — Events published outside outbox~~ FALSE POSITIVE

- **File:** `Providers/Espn/Football/EventCompetitionOddsDocumentProcessor.cs`, lines 134-155
- **Impact:** Events published AFTER `SaveChangesAsync`, outside the outbox transaction. If the process crashes after save but before publish, the event is lost. Other processors correctly publish before save.
- **Resolution:** Not a bug. Current code saves first, then publishes — this is the correct order (save before publish ensures the entity exists before consumers see the event).

### 9. ~~EventCompetitionDriveDocumentProcessor — Copy-paste CausationId~~ FIXED

- **File:** `Providers/Espn/Football/EventCompetitionDriveDocumentProcessor.cs`, line 207
- **Impact:** Uses `CausationId.Producer.GroupSeasonDocumentProcessor` instead of the drive processor's own CausationId. Incorrect tracing/observability.
- **Fixed in:** PR #164. Added `EventCompetitionDriveDocumentProcessor` CausationId entry and corrected reference.

### 10. ~~EventCompetitionProbabilityDocumentProcessor — Unguarded DateTime.Parse~~ FIXED

- **File:** `Providers/Espn/Football/EventCompetitionProbabilityDocumentProcessor.cs`, line 116
- **Impact:** `DateTime.Parse(dto.LastModified)` with no null check or `TryParse`. Throws `FormatException` if ESPN omits or malforms the field.
- **Fixed in:** PR #164. Changed to `DateTime.TryParse` with `DateTime.UtcNow` fallback.

### 11. ~~VenueDocumentProcessor — Event published before save on create path~~ FIXED

- **File:** `Providers/Espn/Common/VenueDocumentProcessor.cs`, lines 93-103
- **Impact:** `VenueCreated` event published at line 101, then `SaveChangesAsync` at line 103. If save fails, downstream consumers process an entity that doesn't exist. The update path (line 188-198) correctly saves first.
- **Fixed in:** PR #165. Swapped order to save before publish, matching the update path.

### 12. ~~TeamSeasonRecordDocumentProcessor — Guid.Empty instead of CorrelationId~~ FIXED

- **File:** `Providers/Espn/TeamSports/TeamSeasonRecordDocumentProcessor.cs`, line 81
- **Impact:** `Guid.Empty` passed as `correlationId` to `AsEntity` instead of `command.CorrelationId`. Breaks audit trail for all team season records.
- **Fixed in:** PR #164. Changed to `command.CorrelationId`. Test added.

### 13. ~~TeamSeasonInjuriesDocumentProcessor — Silent data loss on missing parent~~ FIXED

- **File:** `Providers/Espn/TeamSports/TeamSeasonInjuriesDocumentProcessor.cs`, lines 65-67
- **Impact:** Missing AthleteSeason causes silent return instead of throwing `ExternalDocumentNotSourcedException`. Injury data permanently lost — no retry.
- **Fixed in:** PR #164. Changed to throw `ExternalDocumentNotSourcedException`. Test added.

### 14. ~~CoachSeasonDocumentProcessor — Wrong publish method for dependencies~~ FIXED

- **File:** `Providers/Espn/TeamSports/CoachSeasonDocumentProcessor.cs`, lines 86, 109
- **Impact:** Uses `PublishChildDocumentRequest` for dependencies (Coach, FranchiseSeason) instead of `PublishDependencyRequest`. The base class tracks dependency requests to prevent duplicates on retries. Using the child method means every retry re-publishes the same dependency request.
- **Fixed in:** PR #165. Changed both calls to `PublishDependencyRequest`.

### 15. ~~CoachRecordDocumentProcessor / CoachSeasonRecordDocumentProcessor — Unguarded .First()~~ FIXED

- **Files:** `Providers/Espn/TeamSports/CoachRecordDocumentProcessor.cs` line 78, `CoachSeasonRecordDocumentProcessor.cs` line 77
- **Impact:** `.First()` on `ExternalIds` collection with no guard for empty collection. Throws `InvalidOperationException` if `ExternalIds` is empty.
- **Fixed in:** PR #165. Changed to `FirstOrDefault()` with null-conditional, skipping duplicate check when no ExternalIds exist.

### 16. ~~SeasonPollDocumentProcessor — No null check on dto.Ref~~ FIXED

- **File:** `Providers/Espn/Football/SeasonPollDocumentProcessor.cs`, line 39
- **Impact:** `_externalRefIdentityGenerator.Generate(dto.Ref)` will throw `ArgumentNullException` if `dto.Ref` is null. Other processors guard against this.
- **Fixed in:** PR #165. Added null check with early return and error log.

---

## Recurring Patterns (systemic issues)

### Delete-then-insert with data loss window

Processors that delete existing data and call `SaveChangesAsync` before inserting new data. If the insert fails, the original data is permanently lost.

**Affected:**
- AthleteSeasonStatisticsDocumentProcessor
- EventCompetitionCompetitorStatisticsDocumentProcessor (lines 110-112)
- TeamSeasonLeadersDocumentProcessor (lines 141-144)
- TeamSeasonRecordDocumentProcessor (lines 72-74)
- EventCompetitionLeadersDocumentProcessor

**Fix:** Remove and add within a single `SaveChangesAsync` call, or use a transaction.

### Silent return instead of retry on missing parent

Processors that log a warning/error and return when a parent entity hasn't been sourced yet, instead of throwing `ExternalDocumentNotSourcedException` to trigger a retry. The data is permanently lost.

**Affected:**
- EventCompetitionCompetitorRecordDocumentProcessor (lines 56-59)
- AthleteSeasonNoteDocumentProcessor (lines 61-67)
- AthleteSeasonStatisticsDocumentProcessor (lines 46-53)
- CoachRecordDocumentProcessor (lines 68-71)
- CoachSeasonRecordDocumentProcessor (lines 67-70)
- ~~TeamSeasonInjuriesDocumentProcessor (lines 65-67)~~ Fixed in PR #164

**Fix:** Throw `ExternalDocumentNotSourcedException` so the base class retry logic kicks in.

### No-op ProcessUpdate (updates silently ignored)

Processors where the update path does not update entity properties. If ESPN data changes, those changes are silently discarded.

**Affected:**
- FootballAthleteDocumentProcessor (lines 247-274) — no property updates, only image re-processing
- GroupSeasonDocumentProcessor (lines 170-174) — explicit no-op
- SeasonDocumentProcessor (lines 153-157) — explicit no-op
- EventCompetitionPlayDocumentProcessor (lines 184-203) — only updates FK refs, not play data
- TeamSeasonDocumentProcessor (lines 330-336) — only processes children, not scalar properties

### Direct `DateTime.UtcNow` usage

Processors and entities use `DateTime.UtcNow` directly instead of an injected time abstraction. This couples code to the system clock, making unit tests non-deterministic and preventing controlled time in tests.

**Status:** Deferred. Low value for current workload — timestamps are audit fields and tests verify behavior, not exact times. Revisit when in-game live processing resumes, where clock control may matter for time-dependent ordering/sequencing logic.

### SaveChangesAsync inside loops

- EventCompetitionDriveDocumentProcessor (line 216) — called per play, creating N database round-trips

### ~~Wrong exception type preventing retry~~ FIXED

- ~~AthletePositionDocumentProcessor — `InvalidOperationException`~~ Fixed in PR #161
- ~~SeasonTypeDocumentProcessor — generic `Exception`~~ Fixed in PR #161
- EventCompetitionPowerIndexDocumentProcessor — two `InvalidOperationException` throws — Fixed in PR #161

**Fix:** Use `ExternalDocumentNotSourcedException` in all cases.

---

## Warnings (lower priority)

| Processor | Line | Issue |
|-----------|------|-------|
| AthleteSeasonDocumentProcessor | 161 | Duplicate `HeightDisplay` assignment — likely missing a different property |
| AthletePositionDocumentProcessor | 66 | `dto.Name.ToCanonicalForm()` without null check |
| AthletePositionDocumentProcessor | 159 | Update path uses raw `dto.Name` but create path uses `ToCanonicalForm()` |
| CoachSeasonRecordDocumentProcessor | 69 | Log parameter mismatch: logs `command.ParentId` as `{CoachSeasonId}` |
| EventCompetitionBroadcastDocumentProcessor | 67-71 | No null check on `externalDto.Items` |
| EventCompetitionCompetitorStatisticsDocumentProcessor | 91 | Checks `Value` instead of `SourceUrlHash` for FranchiseSeason lookup |
| EventCompetitionLeadersDocumentProcessor | 103-109 | Race condition on manual ID generation (`MaxAsync + 1`) |
| EventDocumentProcessor | 309 | `.First()` without null safety on competitor filter |
| FranchiseDocumentProcessor | 194 | Inconsistent venue hash method between new vs update paths |
| SeasonFutureDocumentProcessor | 53 | Season lookup by year only, no sport scoping |
| SeasonTypeDocumentProcessor | 38, 43 | Log messages reference wrong DTO type name |
| SeasonTypeWeekDocumentProcessor | 157 | `LogError` for normal update path |
| SeasonTypeWeekRankingsDocumentProcessor | 70 | No null check on `dto.Season` / `dto.Season.Type` |
| SeasonTypeWeekRankingsDocumentProcessor | 165 | No null check on `dto.Ranks` |
| SeasonTypeWeekRankingsDocumentProcessor | 81 | Off-by-one week number at end of season could cause infinite retries |
| TeamSeasonDocumentProcessor | 53 | No null check on `externalProviderDto.Franchise` |
| TeamSeasonRecordAtsDocumentProcessor | 64, 67 | No null check on `dto.Items` and `item.Type` |
| TeamSeasonStatisticsDocumentProcessor | 184 | Unsafe `(int)` cast on `gamesPlayedStat.Value` |
| VenueDocumentProcessor | 192 | `VenueUpdated` passes `null` for ref parameter; `VenueCreated` passes actual ref |
| DocumentProcessorBase | 181-188 | `TryGetOrDeriveParentId` catches all exceptions at Debug level |
| DocumentProcessorBase | 226-232 | `PublishDependencyRequest` returns silently on identity generation failure |
| ProcessDocumentCommandExtensions | 22 | `CausationId` set to `CorrelationId` instead of `MessageId` on retry |
| EventBusAdapter | 89, 104 | Hardcoded 1-second delay in direct publish mode |

---

## Processors with no issues found
- CoachDocumentProcessor
- EventCompetitionCompetitorDocumentProcessor
- EventCompetitionAthleteStatisticsDocumentProcessor
- FootballSeasonRankingDocumentProcessor
- TeamSeasonRankDocumentProcessor
- TeamSeasonProjectionDocumentProcessor
- TeamSeasonAwardDocumentProcessor
- DocumentProcessorFactory
- DocumentProcessorRegistry

## Not implemented (stubs)
- GolfCalendarDocumentProcessor
- GolfEventDocumentProcessor
- TeamSeasonNoteDocumentProcessor
