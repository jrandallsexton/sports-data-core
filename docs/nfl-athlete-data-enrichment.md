# NFL Athlete Data Enrichment

## Context

ESPN's NFL athlete API returns significantly richer data than NCAA. Comparing the two payloads reveals several fields and $ref links that are NFL-specific and not currently captured by our data model or document processors.

## New Fields on Athlete

These fields exist on the ESPN NFL athlete response but are absent (or null) for NCAA athletes. Added as nullable columns on the existing `Athlete` entity to avoid splitting the entity across sports.

| Field | Type | Example | Status |
|-------|------|---------|--------|
| `age` | int? | 29 | Already on Athlete entity — verified mapped in processor |
| `dateOfBirth` | DateTime? | "1996-12-10T08:00Z" | Already on Athlete entity as `DoB` — verified mapped |
| `debutYear` | int? | 2020 | DONE — added to Athlete entity |
| `jersey` | string? | "9" | DONE — added to Athlete entity |

## New: Draft Information

NFL athletes have a `draft` object with structured data. Stored on the Athlete entity as nullable columns.

| Field | Type | Example | Status |
|-------|------|---------|--------|
| `draft.displayText` | string? | "Year: 2020 Round: 1 Pick: 1" | DONE — `DraftDisplayText` |
| `draft.round` | int? | 1 | DONE — `DraftRound` |
| `draft.year` | int? | 2020 | DONE — `DraftYear` |
| `draft.selection` | int? | 1 | DONE — `DraftSelection` |
| `draft.team.$ref` | string? | "http://...seasons/2020/teams/4" | DONE — `DraftTeamRef` |

## New: Cross-Sport Reference

| Field | Type | Example | Status |
|-------|------|---------|--------|
| `collegeAthlete.$ref` | string? | "http://...college-football/athletes/3915511" | DONE — `CollegeAthleteRef` on Athlete entity |

## New $ref Links (New Document Types)

### Contracts
- **ESPN URL**: `http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/athletes/{id}/contracts`
- **Status**: DEFERRED — ESPN endpoint returns empty (`{"count": 0, "items": []}`) for all tested athletes. Revisit if ESPN begins exposing contract data.

### Career Statistics (aggregate)
- **ESPN URL**: `http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/athletes/{id}/statistics`
- **DocumentType**: `DocumentType.AthleteCareerStatistics` (value: 68)
- **ESPN DTO**: `EspnAthleteCareerStatisticsDto`
- **Canonical Entities**: `AthleteCareerStatistic`, `AthleteCareerStatisticCategory`, `AthleteCareerStatisticStat`
- **Document Processor**: `AthleteCareerStatisticsDocumentProcessor` (NFL-only)
- **Categories**: general, passing, rushing, receiving, defensive, defensiveInterceptions, kicking, scoring
- **Status**: DONE

### Statistics Log
- **ESPN URL**: `http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/athletes/{id}/statisticslog`
- **Note**: Both NCAA and NFL have this. Already sourced via existing processor.
- **Status**: Already handled

## Implementation Checklist

### Entity & Migration
- [x] Add to `Athlete` entity: `DebutYear`, `DraftDisplayText`, `DraftRound`, `DraftYear`, `DraftSelection`, `DraftTeamRef`, `CollegeAthleteRef`, `Jersey`
- [x] Add EF configuration (max lengths for string fields)
- [x] Create EF migration (`FootballDataContext`) — `AthleteNflFields`
- [x] Verify `Age` and `DoB` are already mapped in `FootballAthleteDocumentProcessor`

### ESPN DTO Updates
- [x] Add `draft` object to `EspnFootballAthleteDto` (already existed as `EspnAthleteDraftDto`)
- [x] Add `debutYear` field to base `EspnAthleteDto`
- [x] Add `collegeAthlete` $ref field to base `EspnAthleteDto`
- [x] Add `contracts` $ref field to base `EspnAthleteDto`
- [x] Add `careerStatistics` field mapped to `[JsonPropertyName("statistics")]` — separate from `statisticslog`
- [x] Created `EspnAthleteCareerStatisticsDto`

### Processor Updates
- [x] `FootballAthleteDocumentProcessor`: map new fields (debutYear, draft, collegeAthlete, jersey) for both new and existing athletes
- [x] `FootballAthleteDocumentProcessor`: publish `AthleteCareerStatistics` child document request for NFL athletes

### New Document Types & Processors
- [ ] ~~`DocumentType.AthleteContract`~~ — DEFERRED (ESPN endpoint empty)
- [x] `DocumentType.AthleteCareerStatistics` — enum value 68
- [x] `AthleteCareerStatisticsDocumentProcessor` — NFL-only processor with delete-and-replace strategy

### New Entities
- [ ] ~~`AthleteContract`~~ — DEFERRED
- [x] `AthleteCareerStatistic` — root entity (FK to Athlete)
- [x] `AthleteCareerStatisticCategory` — category entity (e.g., "passing", "rushing")
- [x] `AthleteCareerStatisticStat` — individual stat entity (e.g., "gamesPlayed", "passingYards")
- [x] Extension methods: `AthleteCareerStatisticsExtensions`

### Future: Draft Aggregate
- [ ] Source `https://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/{year}/draft` as aggregate root
- [ ] New entities: Draft, DraftRound, DraftPick
- [ ] Rich pick data: athlete ref, team ref, pick number, traded flag
- [ ] Separate PR — standalone effort, no dependencies on this work

### UI
- [ ] Update `TeamRosterDto` to include draft info (round/pick/year) if available
- [ ] Update `TeamRoster` component to display draft info column for NFL
- [ ] Future: Athlete detail page will use this data extensively

## Notes

- All new fields on Athlete are nullable — NCAA athletes will have null values
- Draft data is only applicable to NFL (and eventually NBA, MLB)
- Contract data deferred — ESPN endpoint non-functional
- The `collegeAthlete.$ref` cross-reference enables "where did they play in college?" features
- The `careerStatistics` $ref is a career aggregate distinct from per-season `statisticslog`
- `EspnAthleteDto.CareerStatistics` maps to JSON `statistics`, `EspnAthleteDto.Statistics` maps to JSON `statisticslog`
