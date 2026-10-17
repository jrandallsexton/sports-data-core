# Plan: Eliminate CanonicalDataProvider

## Status (2026-10-17)

Phases 1–4 are complete. `CanonicalDataProvider` and `IProvideCanonicalData` were gone before this date, but `CanonicalDataQueryProvider` was still registered and its 23 SQL files were still embedded, with no callers.

The one remaining direct reader was `CanonicalAdminDataProvider`, which this plan never listed. It backed the four admin diagnostics in API's `DiagnosticsController`. Branch `feat/producer-diagnostics-competitors` finishes the job and completes Phase 5.

**The diagnostics moved to Producer.** They are now EF LINQ handlers under `Application/Competitions/Queries`:
- `GET api/competitions/without-competitors`
- `GET api/competitions/without-plays`
- `GET api/competitions/without-drives`
- `GET api/competitions/without-metrics`

API aggregates every sport's answer through `Application/Diagnostics/ProducerFanOut`. That helper calls an explicit sport list in parallel and is all or nothing: if any Producer fails, the request fails and names the sport.

**Behaviour changes, deliberate rather than a verbatim port:**
- The plays, drives and metrics checks no longer hard-code FBS 2025. They run against a season (`?seasonYear=`, defaulting to the latest season with a started contest), on the pod's own sport.
- The FBS filter applies only on the NCAA pod, the same rule as `RefreshAllCompetitionMediaCommandHandler`.
- For FBS 2025 on local NCAA data, the results match the old SQL exactly.
- The competitors check now returns `StartDateUtc`, which the old SQL never selected.

**Deleted from API:**
- The whole `Infrastructure/Data/Canonical` folder: both query providers, the admin provider, and every embedded SQL file.
- The canonical `IDbConnection` registration.
- The `Dapper` package.

**Remaining owner action:** remove `SportsData.Api:CanonicalDataProvider:ConnectionString` from API's App Config. Nothing reads it any more.

## Context

`CanonicalDataProvider` in the API project uses Dapper to query Producer's PostgreSQL database directly via a single `IDbConnection`. This violates the service boundary (API should call Producer via HTTP) and is broken for multi-sport — the single connection string can only point to one Producer database. With NFL sourcing active, this must be fixed before UI work can proceed.

21 methods, 23 embedded SQL files, 24+ callers across the API project.

## Approach

Move the SQL queries to Producer as HTTP endpoints, then replace API callers with typed HTTP client calls through the existing per-sport factory pattern. The complex matchup SQL stays as Dapper in Producer (LATERAL subqueries can't be expressed in EF Core). Simple queries use EF Core.

## Phasing

### Phase 0: Preparation
- Delete dead code: `GetFranchiseIdsBySlugsAsync` (0 callers)
- Move shared DTOs from `SportsData.Api.Infrastructure.Data.Canonical.Models` to `SportsData.Core.Dtos.Canonical` so both Producer and API can reference them
- Add `IDbConnection` registration to Producer's DI (for Dapper queries)
- Create `MatchupQueryBuilder` in Producer with shared SQL join fragments

### Phase 1: SeasonWeek endpoints (simplest, most callers)
**Methods:** `GetCurrentSeasonWeek` (5 callers), `GetCurrentAndLastWeekSeasonWeeks` (4 callers), `GetCompletedSeasonWeeks` (1 caller)

- **Producer:** Add EF Core query handlers + endpoints on `SeasonController`
  - `GET api/seasons/current-week`
  - `GET api/seasons/current-and-last-weeks`
  - `GET api/seasons/{year}/completed-weeks`
- **Core:** Extend `IProvideSeasons` / `SeasonClient` with new methods
- **API:** Replace callers, remove methods from `IProvideCanonicalData`

### Phase 2: Contest matchup endpoints (largest, riskiest)
**Methods:** `GetMatchupsForCurrentWeek`, `GetMatchupsForSeasonWeek`, `GetMatchupsByContestIds`, `GetMatchupByContestId`, `GetMatchupForPreview/Batch`, `GetMatchupResult`, `GetContestResultsByContestIds`, `GetFinalizedContestIds`, `GetCompletedFbsContestIds`

- **Producer:** Dapper query handlers using `MatchupQueryBuilder` + endpoints on `ContestController`
  - `GET api/contests/matchups/current-week`
  - `GET api/contests/matchups/by-season-week?year={y}&week={w}`
  - `POST api/contests/matchups/by-ids`
  - `GET api/contests/{id}/matchup`
  - `GET api/contests/{id}/matchup-preview`
  - `POST api/contests/matchups/previews`
  - `GET api/contests/{id}/result`
  - `POST api/contests/results/by-ids`
  - `GET api/contests/finalized?seasonWeekId={id}`
  - `GET api/contests/completed-fbs?seasonWeekId={id}`
- **Core:** Extend `IProvideContests` / `ContestClient`
- **API:** Replace callers, remove methods

### Phase 3: FranchiseSeason endpoints
**Methods:** `GetFranchiseSeasonStatistics`, `GetFranchiseSeasonStatsForPreview`, `GetFranchiseSeasonCompetitionResults`

- **Producer:** Mix of EF Core (stats) and Dapper (competition results with 12+ joins)
  - `GET api/franchise-seasons/{id}/statistics`
  - `GET api/franchise-seasons/{id}/preview-stats`
  - `GET api/franchise-seasons/{id}/competition-results`
- **Core:** Extend `IProvideFranchises` / `FranchiseClient`
- **API:** Replace callers

### Phase 4: Rankings, TeamCard, remaining
**Methods:** `GetRankingsByPollIdByWeek`, `GetTeamCard`, `GetConferenceNamesAndSlugs`, `GetConferenceIdsBySlugs`, `GetMatchupByContestId` (for AddMatchup)

- **Producer:** New endpoints on existing or new controllers
- **Core/API:** Extend clients, replace callers

### Phase 5: Cleanup
- Delete `CanonicalDataProvider.cs`, `IProvideCanonicalData.cs`, `CanonicalDataQueryProvider.cs`
- Delete `Infrastructure/Data/Canonical/Sql/` (all 23+ SQL files)
- Delete `Infrastructure/Data/Canonical/Models/` (moved to Core)
- Remove `IDbConnection` registration and Producer connection string from API
- Remove Dapper/Npgsql packages from API if no longer needed

## Key Decisions

1. **Dapper vs EF Core in Producer:** Complex matchup queries (LATERAL subqueries) stay as Dapper. Simple queries use EF Core. Copy SQL verbatim — don't rewrite during migration.

2. **Client mapping:** Matchup/contest queries -> `ContestClient`. Season-week queries -> `SeasonClient`. Franchise-season queries -> `FranchiseClient`. No new client needed.

3. **Sport context:** Default to `Sport.FootballNcaa` in callers that lack sport context (background jobs, scoring processors). Thread sport from controller route params where available. Mark remaining hardcoded spots with `// TODO: multi-sport`.

4. **Deployment order:** Producer deploys first with new endpoints. API deploys second with HTTP client calls. Use Strangler Fig — keep old `IProvideCanonicalData` methods until their replacement is verified.

## Critical Files

| File | Role |
|------|------|
| `src/SportsData.Api/Infrastructure/Data/Canonical/CanonicalDataProvider.cs` | Being eliminated |
| `src/SportsData.Api/Infrastructure/Data/Canonical/IProvideCanonicalData.cs` | Interface to shrink per phase |
| `src/SportsData.Api/Infrastructure/Data/Canonical/Sql/*.sql` | SQL to move to Producer |
| `src/SportsData.Producer/Application/Contests/ContestController.cs` | New matchup endpoints |
| `src/SportsData.Producer/Application/Seasons/SeasonController.cs` | New season-week endpoints |
| `src/SportsData.Core/Infrastructure/Clients/Contest/ContestClient.cs` | New client methods |
| `src/SportsData.Core/Infrastructure/Clients/Season/SeasonClient.cs` | New client methods |
| `src/SportsData.Core/Infrastructure/Clients/Franchise/FranchiseClient.cs` | New client methods |

## Verification

After each phase:
1. Build all affected projects (API, Producer, Core) — zero errors
2. Run unit tests for API and Producer
3. Run smoke tests against production after deploy
4. Verify the specific UI flow that uses the migrated methods

After Phase 5:
- Confirm `CanonicalDataProvider` and all SQL files are deleted
- Confirm no `IDbConnection` or Dapper references remain in the API project
- Confirm the API's App Config no longer has a Producer connection string
- Full smoke test suite passes
