# NFL Athlete Data Enrichment

## Context

ESPN's NFL athlete API returns significantly richer data than NCAA. Comparing the two payloads reveals several fields and $ref links that are NFL-specific and not currently captured by our data model or document processors.

## New Fields on Athlete

These fields exist on the ESPN NFL athlete response but are absent (or null) for NCAA athletes. They should be added as nullable columns on the existing `Athlete` entity to avoid splitting the entity across sports.

| Field | Type | Example | Status |
|-------|------|---------|--------|
| `age` | int? | 29 | Already on Athlete entity — verify it's mapped in processor |
| `dateOfBirth` | DateTime? | "1996-12-10T08:00Z" | Already on Athlete entity as `DoB` — verify it's mapped |
| `debutYear` | int? | 2020 | **NEW** — add to Athlete entity |

## New: Draft Information

NFL athletes have a `draft` object with structured data. This should be stored on the Athlete entity as nullable columns.

| Field | Type | Example | Status |
|-------|------|---------|--------|
| `draft.displayText` | string? | "Year: 2020 Round: 1 Pick: 1" | **NEW** — `DraftDisplayText` |
| `draft.round` | int? | 1 | **NEW** — `DraftRound` |
| `draft.year` | int? | 2020 | **NEW** — `DraftYear` |
| `draft.selection` | int? | 1 | **NEW** — `DraftSelection` |
| `draft.team.$ref` | string? | "http://...seasons/2020/teams/4" | **NEW** — `DraftTeamRef` (store the ref URL for traceability) |

## New: Cross-Sport Reference

| Field | Type | Example | Status |
|-------|------|---------|--------|
| `collegeAthlete.$ref` | string? | "http://...college-football/athletes/3915511" | **NEW** — `CollegeAthleteRef` on Athlete entity |

## New $ref Links (New Document Types)

ESPN NFL athletes expose additional sourceable documents that NCAA athletes don't have. These need new DocumentTypes, ESPN DTOs, canonical entities, and document processors.

### Contracts
- **ESPN URL**: `http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/athletes/{id}/contracts`
- **DocumentType**: `AthleteContract` (NEW)
- **ESPN DTO**: `EspnAthleteContractDto` (NEW — needs exploration of the contract response shape)
- **Canonical Entity**: `AthleteContract` (NEW)
  - Likely fields: ContractId, AthleteId, Years, TotalValue, AverageAnnualValue, SigningBonus, GuaranteedMoney, StartYear, EndYear, TeamRef
- **Document Processor**: `AthleteContractDocumentProcessor` (NEW)
- **Checklist**:
  - [ ] Explore ESPN contract endpoint to understand response shape
  - [ ] Create ESPN DTO
  - [ ] Create canonical entity + EF configuration
  - [ ] Create EF migration
  - [ ] Create document processor
  - [ ] Register DocumentType enum value
  - [ ] Register processor with `[DocumentProcessor]` attribute for `Sport.FootballNfl`

### Career Statistics (aggregate)
- **ESPN URL**: `http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/athletes/{id}/statistics`
- **DocumentType**: `AthleteStatistics` (NEW — distinct from `AthleteSeasonStatistics` which is per-season)
- **Note**: This is a career-level aggregate. Need to explore the response shape to determine if it's a summary or detailed breakdown.
- **Checklist**:
  - [ ] Explore ESPN statistics endpoint to understand response shape
  - [ ] Create ESPN DTO
  - [ ] Create canonical entity (or extend existing)
  - [ ] Create document processor
  - [ ] Register DocumentType enum value

### Statistics Log
- **ESPN URL**: `http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/athletes/{id}/statisticslog`
- **Note**: Both NCAA and NFL have this. Verify it's already being sourced for NFL.
- **Checklist**:
  - [ ] Verify `statisticslog` is sourced for NFL athletes
  - [ ] If not, ensure the existing processor handles NFL

## Implementation Checklist

### Entity & Migration
- [ ] Add to `Athlete` entity: `DebutYear` (int?), `DraftDisplayText` (string?), `DraftRound` (int?), `DraftYear` (int?), `DraftSelection` (int?), `DraftTeamRef` (string?), `CollegeAthleteRef` (string?)
- [ ] Add EF configuration (max lengths for string fields)
- [ ] Create EF migration (`FootballDataContext`)
- [ ] Verify `Age` and `DoB` are already mapped in `FootballAthleteDocumentProcessor`

### ESPN DTO Updates
- [ ] Add `draft` object to `EspnAthleteDto` (or the football-specific DTO)
  - Nested: `EspnAthleteDraftDto` with `DisplayText`, `Round`, `Year`, `Selection`, `Team.$ref`, `Pick.$ref`
- [ ] Add `debutYear` field
- [ ] Add `collegeAthlete` $ref field
- [ ] Add `contracts` $ref field
- [ ] Add `statistics` $ref field

### Processor Updates
- [ ] `FootballAthleteDocumentProcessor`: map new fields (debutYear, draft, collegeAthlete) to Athlete entity
- [ ] `FootballAthleteDocumentProcessor`: publish dependency requests for new document types (contracts, statistics) when $refs are present

### New Document Types & Processors
- [ ] `DocumentType.AthleteContract` — enum value
- [ ] `DocumentType.AthleteStatistics` — enum value (career aggregate)
- [ ] `AthleteContractDocumentProcessor` — new processor
- [ ] `AthleteStatisticsDocumentProcessor` — new processor (if needed after exploring the endpoint)

### New Entities
- [ ] `AthleteContract` entity + EF configuration
- [ ] `AthleteContractExternalId` entity
- [ ] Consider: `AthleteCareerStatistic` entity or reuse existing stat patterns

### UI
- [ ] Update `TeamRosterDto` to include draft info (round/pick/year) if available
- [ ] Update `TeamRoster` component to display draft info column for NFL
- [ ] Future: Athlete detail page will use this data extensively

## Notes

- All new fields on Athlete are nullable — NCAA athletes will have null values
- Draft data is only applicable to NFL (and eventually NBA, MLB)
- Contract data is NFL-specific for now but MLB/NBA will have it too
- The `collegeAthlete.$ref` cross-reference enables "where did they play in college?" features
- The `statistics` $ref is a career aggregate distinct from per-season stats
- Existing `statisticslog` may already cover some of this — needs verification
