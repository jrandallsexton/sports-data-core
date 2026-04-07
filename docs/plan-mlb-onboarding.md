# Plan: MLB Onboarding

## Context

MLB 2026 season is live. Onboarding MLB enables development of real-time features (contest monitoring, score updates, pick locking, cron scheduling, pick'em league creation) that can only be built against a live season. This is also the first non-football sport, so it validates that the platform's multi-sport abstractions actually work.

Scope: 2-3 seasons of data (2024-2026), not full history. Pick'em format: weekly basis for parity with football.

## What Already Exists

- `Sport.BaseballMlb` enum value (= 1)
- `ModeMapper` routes `("baseball", "mlb")` to `Sport.BaseballMlb`
- `SportExtensions` maps to `"baseball-mlb"` kebab-case
- `BaseballDataContext` (extends `BaseDataContext` — needs to extend `TeamSportDataContext`)
- `BaseballAthlete` entity (extends `TeamAthlete`, currently empty)
- `TeamSportDataContext` has all shared team-sport entities (Franchise, Competition, Coach, Draft, AthleteSeason, etc.)
- Provider pipeline, DocumentProcessorBase, client factories, KEDA scaling — all sport-agnostic
- Document types are generic — no baseball-specific types expected initially

## Phase 0: Discovery — COMPLETE

ESPN baseball JSON fetched and analyzed. 28 files saved to `test/unit/SportsData.Producer.Tests.Unit/Data/EspnBaseballMlb/`.

### Key Findings

**1. Franchise / TeamSeason — Identical to football.**
Same fields: id, slug, location, name, abbreviation, color, logos, venue, record, groups, coaches, athletes refs. Existing DTOs and processors work as-is.

**2. Athlete — Nearly identical, two baseball-specific additions:**
- `bats` — handedness object: `{ type: "RIGHT", abbreviation: "R", displayValue: "Right" }`
- `throws` — same structure
- All other fields match football: draft, debutYear, college, jersey, position, experience, statistics, contracts, status
- `BaseballAthlete` just needs `Bats` and `Throws` string fields.

**3. AthleteSeason — Has baseball-specific data:**
- `hotZones` — inline array (not a ref). 9-zone (3x3) strike zone grid with x/y bounds, atBats, hits, battingAvg, battingAvgScore, slugging, sluggingScore per zone. Useful for player cards, AI analysis, and pick'em context.
- `positions` — array (plural). Baseball players can play multiple positions per season, each with separate stats refs.
- `projections` — ref not seen in football. Worth sourcing.
- `eventLog` — ref for game-by-game log.

**4. Competition/Event — Nearly identical structure to football:**
- Same competitors array: home/away, team refs, score, linescores, roster, statistics, leaders, record
- `format.regulation.periods = 9`, `displayName = "Inning"` — linescore array has 9+ entries instead of 4
- `probables` array on competitors — baseball-specific (starting pitchers with stats refs)
- `series` data — current series, preseason series, season series with win/loss tracking. 13-game season series between divisional opponents.
- Has odds, broadcasts, status, leaders, plays, predictor, probabilities, powerIndexes — all same ref patterns as football
- `duration` field with displayValue (e.g., "2:42")
- `wasSuspended` boolean (rain delays, suspended games)
- **Existing competition processors should largely work.**

**5. Baseball HAS weeks.**
SeasonType includes `week` and `weeks` refs. Currently "Week 2" (Apr 1-8, 2026). ESPN defines week boundaries. Perfect for weekly pick'em — no artificial grouping needed.

**6. Groups — Simpler than football.**
2 top-level groups (American League id=7, National League id=8) with divisions as children. Same structure as football conferences/divisions.

**7. Season types — Same pattern.**
Preseason/regular/postseason. Regular season: Mar 25 - Sep 30, 2026.

**8. Draft — Not available on ESPN core API for MLB.**
404 for both 2024, 2025, and 2026. DraftRounds returns empty items array. Low priority — draft info is available inline on athlete objects (`draft.displayText`, `draft.round`, `draft.year`, `draft.selection`).

**9. Venue — Identical structure.**
Includes `grass` (bool) and `indoor` (bool). Venue IDs are different from football (start at 1).

**10. Positions — Same ref structure.**
Hierarchical with parent refs (e.g., Right Field -> Outfielder parent). 19 positions.

**11. Coaches — Same structure as football.**

### ESPN DTO Reuse Assessment

| DTO | Reusable? | Notes |
|-----|-----------|-------|
| `EspnFranchiseDto` | Yes | Identical structure |
| `EspnTeamSeasonDto` | Yes | Identical structure |
| `EspnVenueDto` | Yes | Identical + grass/indoor |
| `EspnCoachSeasonDto` | Yes | Identical |
| `EspnGroupSeasonDto` | Yes | Identical |
| `EspnAthletePositionDto` | Yes | Identical |
| `EspnImageDto` | Yes | Identical |
| `EspnAwardDto` | Yes | Identical |
| `EspnAthleteDto` | Mostly | Need to add `bats`/`throws` to shared DTO or subclass |
| `EspnResourceIndexDto` | Yes | Same pagination pattern |
| `EspnDraftDto` | N/A | MLB draft not available on core API |
| Event/Competition DTOs | Yes | Same structure, just 9 innings vs 4 quarters |

### Baseball-Specific Entities Needed (BaseballDataContext)

- `BaseballAthlete` — add `Bats` and `Throws` fields (handedness type + abbreviation)
- `BaseballAthleteSeason` — if needed to hold hot zones (or use generic stats hierarchy)
- `AthleteHotZone` / `AthleteHotZoneEntry` — 9 zones per configuration, per season
- Possibly `CompetitionProbable` — starting pitcher per competitor (or handle in existing competition competitor structure)

## Phase 1: Infrastructure + Processor Registration — COMPLETE

Given the high compatibility, Phases 1-3 from the original plan collapse into a single phase.

### 1a. Fix BaseballDataContext
- Change `BaseballDataContext` to extend `TeamSportDataContext` instead of `BaseDataContext`
- Add `BaseballAthlete` fields: `BatsType`, `BatsAbbreviation`, `ThrowsType`, `ThrowsAbbreviation`
- Add `AthleteHotZone` + `AthleteHotZoneEntry` entities to `BaseballDataContext`
- Generate `InitialCreate` migration, verify locally

### 1b. Register existing processors for BaseballMlb
Add `[DocumentProcessor(Espn, BaseballMlb, ...)]` attribute to existing shared processors:

**TeamSports processors (constrained to `TeamSportDataContext`):**
- CoachDocumentProcessor
- CoachSeasonDocumentProcessor
- CoachSeasonRecordDocumentProcessor
- FranchiseDocumentProcessor
- TeamSeasonDocumentProcessor
- TeamSeasonStatisticsDocumentProcessor
- TeamSeasonRecordDocumentProcessor
- TeamSeasonAwardsDocumentProcessor
- TeamSeasonLeadersDocumentProcessor
- AthleteSeasonInjuryDocumentProcessor
- AthleteSeasonNoteDocumentProcessor
- GroupSeasonDocumentProcessor

**Common processors (constrained to `BaseDataContext`):**
- VenueDocumentProcessor
- SeasonDocumentProcessor (if exists)

### 1c. Create baseball-specific processors
Under `Espn/Baseball/`:
- `BaseballAthleteDocumentProcessor` — maps `bats`/`throws` fields
- `BaseballAthleteSeasonDocumentProcessor` — maps hot zones, multi-position

### 1d. Add `bats`/`throws` to EspnAthleteDto (or create subclass)
Evaluate whether to add nullable `bats`/`throws` properties to the shared `EspnAthleteDto` (football ignores them) or create `EspnBaseballAthleteDto`. Shared DTO is simpler if the fields are just nullable.

### 1e. DocumentProviderAndTypeDecoder
Ensure all document types used by baseball are handled in the decoder switch statements.

## Phase 2: Infrastructure + Deployment

### 2a. App Config entries
- Add `Prod.BaseballMlb` label entries to Azure App Config manifest
- `CommonConfig:EspnBaseUrl` = `https://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb`
- Producer client configs, Hangfire settings, connection strings
- Add sport-keyed entries to `Prod.All` for API client routing

### 2b. Kubernetes manifests (sports-data-config repo)
- Provider deployment for `BaseballMlb` mode (Ingest + Worker roles)
- Producer deployment for `BaseballMlb` mode
- KEDA ScaledObjects for Hangfire queues
- PostgreSQL database creation (`baseball-mlb`)

### 2c. Historical sourcing configuration
- Add seeder entries for BaseballMlb in Provider's `HistoricalSourcingUriBuilder`
- Configure document types to source: Franchise, TeamSeason, Venue, Athlete, AthleteSeason, Coach, CoachSeason, GroupSeason, Event, EventCompetition, Positions
- Start with 2025-2026 seasons only
- Trigger initial sourcing run

## Phase 3: API and UI Support

- Verify typed clients route correctly for BaseballMlb (client factory resolution)
- Add baseball routes to web UI SportContext (SPORTS config)
- Test team card, roster, schedule pages with baseball data
- Contest monitoring with live game data

## Phase 4: Pick'em for Baseball

- ESPN already defines weekly boundaries — use them directly
- Verify matchup generation works with baseball contest data
- Test pick submission and scoring flows
- Moneyline-based picks (baseball uses moneyline more than spread)

## Key Risks

1. **Volume** — 162 games/team x 30 teams = 2,430 games/season vs ~870 for college football. More athletes on active rosters (26-man + 40-man). More events to process. May need higher Hangfire worker counts.

2. **Statistics complexity** — Baseball has far more statistical categories than football. The existing stats hierarchy (category -> stat) should handle this, but volume per athlete will be higher.

3. **Extra innings** — Games can go beyond 9 innings. Linescore array length is variable. Existing `CompetitionCompetitorLineScore` entity should handle this (it's just more rows).

4. **Suspended/postponed games** — `wasSuspended` flag exists. Rain delays and postponements are common in baseball. Contest monitoring needs to handle these states.

5. **Draft not available** — MLB draft endpoint 404s on ESPN core API. Draft info only available inline on athlete objects. Not critical for initial launch.

## Test Data

All ESPN JSON responses saved to `test/unit/SportsData.Producer.Tests.Unit/Data/EspnBaseballMlb/`:

| File | Size | Description |
|------|------|-------------|
| Athlete.json | 6.6KB | Aaron Judge (id=33192) |
| Athletes.json | 1.6KB | First page of 2026 athletes |
| AthleteSeason.json | 11.9KB | Judge 2026 season (includes hot zones) |
| Coaches.json | 4.7KB | 2026 coaches |
| Event.json | 33.4KB | Single game (Royals @ Guardians) |
| EventCompetition.json | 22.1KB | Competition with series data |
| EventCompetitionCompetitors.json | 5.3KB | Home/away competitors with probables |
| EventCompetitionOdds.json | 14.3KB | Betting odds |
| EventCompetitionStatus.json | 2.1KB | Game status |
| Events.json | 1.1KB | Current events list |
| Franchise.json | 1.4KB | Yankees franchise |
| Franchises.json | 4.3KB | All 30 franchises |
| Groups.json | 422B | AL/NL groups |
| Positions.json | 3.3KB | 19 baseball positions |
| Scoreboard.json | 1MB | Full daily scoreboard |
| Season.json | 8.3KB | 2026 season |
| Seasons.json | 1.5KB | Season list |
| SeasonType.json | 1.5KB | Regular season (includes weeks!) |
| SeasonTypes.json | 700B | Season type list |
| Standings.json | 4.7KB | AL standings |
| Teams.json | 4.6KB | All 30 teams |
| TeamSeason.json | 12.5KB | Yankees 2026 |
| Venue.json | Fixed | Oriole Park at Camden Yards |
| Venues.json | 7KB | All venues |

## Verification

After each phase:
1. Build all affected projects — zero errors
2. Run unit tests — all pass
3. Deploy to prod, verify pods healthy
4. Check Seq for processing errors

End state: Live MLB data flowing through the pipeline, visible in web UI, available for pick'em league creation.
