# NFL Onboarding Plan

## Overview

Onboard NFL as the second sport in the sportDeets platform. NCAA football (FootballNcaa) is the only active sport today. NFL uses the same `FootballDataContext`, same ESPN data source, and most of the same document processors.

Target: NFL historical data sourced and Pick'em product functional before the September 2026 NFL season.

## Current State — What Already Works

These components already support `Sport.FootballNfl` with zero code changes:

| Component | Status | Notes |
|-----------|--------|-------|
| `Sport.FootballNfl = 3` enum value | Exists | `Sport.cs` |
| `ModeMapper` ("football", "nfl") | Mapped | Returns `Sport.FootballNfl` |
| `SportExtensions` kebab-case | Works | Produces `"football-nfl"` |
| `FootballDataContext` | Shared | Both NCAA and NFL route to it via `DataContextFactory` |
| `FootballSeeder` | Supports NFL | Maps `FootballNfl` → ESPN league `"nfl"` |
| `ImageProcessorFactory<FootballDataContext>` | Works | |
| `DocumentProcessorFactory<FootballDataContext>` | Works | |
| Command-line `-mode FootballNfl` | Parsed | `CommandLineHelpers.ParseFlag<Sport>` |
| PostgreSQL database naming | Automatic | `sdProvider.FootballNfl`, `sdProducer.FootballNfl`, etc. |
| Hangfire database naming | Automatic | `sdProvider.FootballNfl.Hangfire`, etc. |
| Health checks | Automatic | Names derived from `{apiName}-{mode}` |
| MassTransit consumers | Mode-agnostic | Kebab-case endpoint names, no sport prefix |
| EF Outbox | Works | Uses `FootballDataContext` for both |
| Connection pool sizing | Configurable | Via `{appName}:ConnectionPool:{roleName}` keys |

## Work Required

### Phase 1: Document Processor Attributes

All 50 processors are currently attributed for `Sport.FootballNcaa` only. Each shared processor needs a second attribute for `Sport.FootballNfl`.

**Approach:** Add `[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNfl, DocumentType.X)]` to each existing processor. Do NOT create a base `Sport.Football` — keep NCAA and NFL attributes explicit so processors can be forked independently when sport-specific differences emerge.

**Processor categories:**

| Category | Count | Location | Action |
|----------|-------|----------|--------|
| Football-specific | ~20 | `Processors/Providers/Espn/Football/` | Add `FootballNfl` attribute |
| Team sport shared | ~16 | `Processors/Providers/Espn/TeamSports/` | Add `FootballNfl` attribute |
| Common (Venue) | 1 | `Processors/Providers/Espn/Common/` | Add `FootballNfl` attribute |
| Season-related | ~10 | `Processors/Providers/Espn/Football/` | Add `FootballNfl` attribute |
| Test processors | 2 | `Processors/Providers/Espn/Test/` | Add `FootballNfl` attribute |

**Processors with multiple existing attributes** (handle carefully):
- `AthletePositionDocumentProcessor` — has `Position` and `AthletePosition` document types
- `SeasonDocumentProcessor` — has `Season` and `Seasons` document types

### Phase 2: Azure App Config

New labels needed:

```text
Development.FootballNfl.SportsData.Provider
Development.FootballNfl.SportsData.Producer
Development.FootballNfl.SportsData.Api
Production.FootballNfl.SportsData.Provider
Production.FootballNfl.SportsData.Producer
Production.FootballNfl.SportsData.Api
```

**Required config entries per label:**

SQL connection string (inherited from `CommonConfig:SqlBaseConnectionString` — databases are auto-named).

HTTP client URLs (7 entries):
```text
CommonConfig:VenueClientConfig:FootballNfl:ApiUrl
CommonConfig:FranchiseClientConfig:FootballNfl:ApiUrl
CommonConfig:ContestClientConfig:FootballNfl:ApiUrl
CommonConfig:SeasonClientConfig:FootballNfl:ApiUrl
CommonConfig:ProducerClientConfig:FootballNfl:ApiUrl
CommonConfig:PlayerClientConfig:FootballNfl:ApiUrl
CommonConfig:NotificationClientConfig:FootballNfl:ApiUrl
```

Provider document store:
```text
SportsData.Provider:ProviderDocDatabaseConfig:DatabaseName
SportsData.Provider:ProviderDocDatabaseConfig:ConnectionString
SportsData.Provider:ProviderDocDatabaseConfig:Provider (Mongo or Cosmos)
```

Worker config (can start with defaults):
```text
{AppName}:ConnectionPool:Worker (default 22)
{AppName}:ConnectionPool:Api (default 5)
{AppName}:ConnectionPool:Ingest (default 5)
{AppName}:BackgroundProcessor:MinWorkers (default 20)
```

ESPN client config:
```text
SportsData.Provider:EspnApiClientConfig:RequestDelayMs (1000)
```

### Phase 3: RabbitMQ — Dedicated StatefulSet

Deploy a separate RabbitMQ instance for NFL rather than sharing the NCAA broker.

**Rationale:** Message contracts (`DocumentCreated`, `DocumentRequested`) carry `Sport` in the payload, and the `DocumentProcessorRegistry` routes by `(SourceDataProvider, Sport, DocumentType)` — so shared queues *work* technically. However, separate brokers provide full isolation: no contention during parallel historical sourcing runs, independent scaling, and no impact if one broker has issues.

**Implementation:** No code changes needed. The RabbitMQ connection string is already resolved from Azure App Config per label. NFL pods get a different `CommonConfig:Messaging:RabbitMq:Host` pointing to the NFL broker.

```text
rabbitmq-football-nfl    (new StatefulSet)
rabbitmq-football-ncaa   (existing, rename for clarity)
```

Config entries under `{Environment}.FootballNfl.*` labels:
```text
CommonConfig:Messaging:RabbitMq:Host = rabbitmq-football-nfl
CommonConfig:Messaging:RabbitMq:Username = ...
CommonConfig:Messaging:RabbitMq:Password = ...
```

Resource footprint is minimal (~256-512MB RAM). Additional sports in the future get their own broker — cluster capacity scales by adding NUCs.

### Phase 4: Database Provisioning

Create PostgreSQL databases (EF migrations run automatically on first startup):
- `sdProvider.FootballNfl`
- `sdProducer.FootballNfl`
- `sdApi.FootballNfl`
- `sdProvider.FootballNfl.Hangfire`
- `sdProducer.FootballNfl.Hangfire`

Create MongoDB database/collection for NFL document store:
- Database name configured via `ProviderDocDatabaseConfig:DatabaseName`
- If using Cosmos: ensure container `"FootballNfl"` exists

### Phase 5: Kubernetes Deployments

Six new deployments (3 Provider roles + 3 Producer roles), all with `-mode FootballNfl`:

```text
provider-football-nfl-api
provider-football-nfl-worker
provider-football-nfl-ingest
producer-football-nfl-api
producer-football-nfl-worker
producer-football-nfl-ingest
```

KEDA ScaledObjects for worker pods (Hangfire queue depth scaling).

Consider starting with fewer worker replicas than NCAA — 32 NFL teams vs 130+ means significantly less processing volume.

### Phase 6: Seed and Source

1. Verify seeder: `FootballSeeder.Generate(Sport.FootballNfl, [2025])` seeds the correct ESPN `"nfl"` league endpoints
2. Trigger initial sourcing run for current/recent season to validate end-to-end
3. Once validated, begin historical sourcing runs backward

### Phase 7: Validate and Fix

Expected issues to watch for:
- **Processor-level differences:** NCAA and NFL ESPN data shapes may diverge in edge cases (e.g., playoff structure, draft picks, salary cap entities). Fork processors as needed.
- **MongoDB naming collisions:** Ensure NCAA and NFL document stores are fully isolated
- **Rate limiting:** Two sports sourcing simultaneously doubles ESPN request volume. May need to coordinate sourcing windows or increase `RequestDelayMs`.
- **PostgreSQL max_connections:** NFL adds 6+ more pods. Verify total connection count stays under 500. Current estimate with NCAA: ~480 at peak. May need to reduce pool sizes or increase `max_connections`.

## ESPN API Comparison: NCAA vs NFL Data Shapes

Compared on 2026-03-25 against the live ESPN Core API (`sports.core.api.espn.com/v2`).

### Team Object

The core team data shape is virtually identical. Shared fields: `id`, `guid`, `uid`, `alternateIds`, `slug`, `location`, `name`, `nickname`, `abbreviation`, `displayName`, `shortDisplayName`, `color`, `alternateColor`, `isActive`, `isAllStar`, `logos`, `venue`, `record`, `groups`, `ranks`, `statistics`, `leaders`, `links`, `injuries`, `notes`, `againstTheSpreadRecords`, `awards`, `franchise`, `events`, `coaches`.

| NFL-only `$ref` endpoints | Notes |
|--------------------------|-------|
| `oddsRecords` | Betting records (separate from ATS) |
| `athletes` | Direct team→athletes ref (NCAA lacks this at team level) |
| `depthCharts` | Roster depth chart |
| `projection` | Season projection data |
| `transactions` | Trades, signings, cuts |

| NCAA-only `$ref` endpoints | Notes |
|---------------------------|-------|
| `college` | College-specific metadata |

### Season Object

Core structure identical: `year`, `startDate`, `endDate`, `displayName`, `type` (with `id`, `name`, `abbreviation`, `year`, `startDate`, `endDate`, `hasGroups`, `hasStandings`, `weeks`, `leaders`, `slug`), `types`, `awards`, `leaders`.

| NCAA-only fields | NFL-only fields |
|-----------------|-----------------|
| `rankings` | — |
| `athletes` | — |
| `futures` | — |

All NCAA-only fields are additive `$ref` endpoints. DTOs need these as nullable — no structural change.

### Event/Competition Object

This is the most complex resource and the shapes are nearly identical. All of these fields are shared:

`id`, `uid`, `date`, `name`, `shortName`, `season`, `seasonType`, `week`, `timeValid`, `competitions[].id`, `competitions[].guid`, `competitions[].uid`, `competitions[].date`, `competitions[].attendance`, `competitions[].type`, `competitions[].timeValid`, `competitions[].dateValid`, `competitions[].neutralSite`, `competitions[].divisionCompetition`, `competitions[].conferenceCompetition`, all availability flags (`previewAvailable`, `recapAvailable`, `boxscoreAvailable`, `playByPlayAvailable`, etc.), `venue`, `competitors[].id/uid/type/order/homeAway/winner/team/score/linescores/roster/statistics/leaders/record`, `notes`, `situation`, `status`, `odds`, `broadcasts`, `officials`, `details`, `leaders`, `predictor`, `probabilities`, `powerIndexes`, `format`, `drives`, `links`, `venues`, `league`.

| Field | Difference |
|-------|-----------|
| `competitors[].curatedRank` | NCAA-only (AP/CFP rankings) |
| `competitions[].groups` | NCAA-only (conference group ref) |
| `format.overtime` | NCAA: untimed, no periods. NFL: sudden-death, periods=1, clock=600 |
| `statsSource.id` | NCAA=4 ("official"), NFL=3 ("scrubbed") — value difference, same shape |

The overtime format is a value difference, not a structural one. DTOs handle it fine with nullable `periods` and `clock` fields.

### Athlete Object

Core fields shared: `id`, `firstName`, `lastName`, `fullName`, `displayName`, `shortName`, `weight`, `height`, `age`, `dateOfBirth`, `position` (with `id`, `name`, `displayName`, `abbreviation`), `jersey`, `birthPlace` (with `city`, `state`, `country`).

| NFL-only fields | Notes |
|----------------|-------|
| `uid`, `guid`, `type`, `alternateIds` | Additional identifiers |
| `draft` (round, year, selection, pick, displayText) | Professional draft history |
| `experience` (years) | Professional career length |
| `debutYear` | First professional season |
| `active` | Active roster boolean |
| `status` (id, name, type) | Roster status tracking |
| `displayHeight`, `displayWeight` | Formatted strings alongside numeric values |

All NFL-only fields are additive. DTOs just need them as nullable properties.

### Franchise Object

Nearly identical: `id`, `uid`, `slug`, `location`, `name`, `nickname`, `abbreviation`, `displayName`, `shortDisplayName`, `color`, `isActive`, `venue` (full inline object with address, grass, indoor, images), `team`.

| NCAA-only fields | NFL-only fields |
|-----------------|-----------------|
| `logos` (array at franchise level) | — |
| `awards` | — |

### DTO Impact Summary

**No breaking changes required.** All differences are additive nullable fields.

| Resource | Fields to make nullable for cross-league support |
|----------|------------------------------------------------|
| Season | `rankings`, `athletes`, `futures` |
| Event | `competitors[].curatedRank`, `competitions[].groups` |
| Athlete | `uid`, `guid`, `type`, `alternateIds`, `draft`, `experience`, `debutYear`, `active`, `status`, `displayHeight`, `displayWeight` |
| Franchise | `logos` (at franchise level), `awards` |

Existing processors deserialize these resources today for NCAA. The NFL payloads will deserialize through the same DTOs — missing NCAA-only fields become null, NFL-only fields are ignored until processors are updated to use them.

### Design Validation

This comparison confirms the original architecture decision: `FootballDataContext` and sport-agnostic document processors can serve both NCAA and NFL with explicit `[DocumentProcessor]` attributes per sport. No base `Sport.Football` abstraction needed. When NFL-specific processing is required (draft history, depth charts, transactions), fork individual processors rather than restructuring the shared ones.

## Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| ESPN NFL data shape differs from NCAA | **Low** | API comparison (2026-03-25) confirms shapes are nearly identical. All differences are additive nullable fields. Fork processors only if processing logic diverges. |
| PostgreSQL connection exhaustion | Medium | Pool sizes are now configurable via App Config (PR #193). NFL adds ~6 pods × 2 pools × 22 = 264 connections. May need to increase `max_connections` beyond 500 or stagger NCAA/NFL sourcing. |
| ESPN rate limiting from doubled volume | Medium | Two sports sourcing simultaneously doubles request volume. Stagger historical sourcing runs (finish NCAA before starting NFL). Circuit breaker and rate limiter already in place for live season. |
| MongoDB on shared box during NFL+NCAA sourcing | Low | NUC migration in progress; dedicated Mongo box eliminates contention. |
| Azure App Config and K8s manifest complexity | Medium | Six new deployments, new config labels, KEDA ScaledObjects. Most likely source of onboarding friction. Template from existing NCAA manifests. |

## Not In Scope

- New processor types unique to NFL (salary cap, draft, etc.) — add as needed post-onboarding
- NFL Pick'em UI changes — existing Pick'em supports multiple sports via routing
- NFL-specific StatBot/MetricBot models — train after sufficient historical data is sourced
