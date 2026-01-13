# ProducerClient Migration - COMPLETED ✅

## Overview

~~We are migrating~~ **We have successfully migrated** all methods from `ProducerClient` to domain-specific clients (`ContestClient`, `VenueClient`, `FranchiseClient`) as part of establishing clean domain boundaries. ~~The goal is to eventually delete `ProducerClient` entirely.~~ **ProducerClient has been deleted.**

## Final Architecture

```
API -> VenueClient -> Producer (or VenueService in future)
API -> FranchiseClient -> Producer (or FranchiseService in future)
API -> ContestClient -> Producer (or ContestService in future)
```

**Producer runs quietly in the background:**
- Processes documents sourced from ESPN by Provider
- Stores them as canonical data
- Generates events
- Serves canonical/domain data (temporarily - will move to domain services)
- **No longer has a client in API - API only knows about domain clients**

## Completed Work ✅

### All Methods Migrated

| Method | From | To | Consumer Updated |
|--------|------|-----|------------------|
| `GetContestOverviewByContestId` | ProducerClient | ContestClient | ✅ User migrated manually |
| `RefreshContestByContestId` | ProducerClient | ContestClient | ✅ Renamed to `RefreshContest` |
| `GetFranchiseSeasonMetrics(int seasonYear)` | ProducerClient | FranchiseClient | ✅ GetFranchiseSeasonMetricsQueryHandler |
| `GetFranchiseSeasonMetricsByFranchiseSeasonId(Guid)` | ProducerClient | FranchiseClient | ✅ GetTeamMetricsQueryHandler, MatchupPreviewProcessor |
| `RefreshContestMediaByContestId(Guid)` | ProducerClient | ContestClient | ✅ RefreshContestMediaCommandHandler |
| `GetFranchiseSeasonRankings(int seasonYear)` | ProducerClient | FranchiseClient | ✅ GetRankingsBySeasonYearQueryHandler |

### ProducerClient Deletion ✅

**All files deleted:**
- ❌ `ProducerClient.cs` - DELETED
- ❌ `ProducerClientFactory.cs` - DELETED  
- ❌ `ProducerClientConfig.cs` - DELETED
- ❌ `Producer/` directory - DELETED
- ✅ Removed from DI registration in `ServiceRegistration.cs`
- ✅ Removed from `HttpClients` constants
- ✅ Removed from `CanonicalDataProvider` constructor
- ✅ Updated `CommonConfigKeys` to use string literals instead of type references

**Config kept (for now):**
- ✅ `ProducerClientConfigs` in `CommonConfig` - needed for ResourceRefGenerator
- ✅ `GetProducerProviderUri()` in `CommonConfigKeys` - generates HATEOAS links

These will be updated when canonical data moves to domain services.

### Handler Updates
All handlers using client factories now follow this pattern:
```csharp
// Resolve sport/league to mode
Sport mode;
try
{
    mode = ModeMapper.ResolveMode(query.Sport, query.League);
}
catch (NotSupportedException ex)
{
    _logger.LogWarning(ex, "Unsupported sport/league combination...");
    return new Failure<T>(...);
}

var client = _clientFactory.Resolve(mode);
```

## Migration Complete - No Remaining Work

~~### Remaining Methods in ProducerClient~~

**All methods have been migrated. ProducerClient has been deleted.**

---

## Historical Migration Steps (For Reference)

The following steps were used during the migration:

### 1. Add to Domain Client Interface
File: `src/SportsData.Core/Infrastructure/Clients/{Domain}/{Domain}Client.cs`

```csharp
public interface IProvide{Domain}s : IProvideHealthChecks
{
    // Add new method
    Task<ReturnType> MethodName(params, CancellationToken cancellationToken = default);
}
```

### 2. Implement in Domain Client
Same file, in the class:

```csharp
public async Task<ReturnType> MethodName(params, CancellationToken cancellationToken = default)
{
    return await GetOrDefaultAsync(
        $"endpoint/path",
        new ReturnType(),
        cancellationToken);
}
```

### 3. Update Consumer (Usually in API)

**Option A: If consumer is a Query/Command Handler**
- Add `Sport` property to the Query/Command class
- Update handler to use domain client factory
- Update controller to pass Sport (hardcode `Sport.FootballNcaa` with TODO for now if needed)

**Option B: If consumer is CanonicalDataProvider**
- Remove method from `IProvideCanonicalData` interface
- Remove implementation from `CanonicalDataProvider`
- Update the actual consumer (handler) to use domain client factory directly

### 4. Remove from ProducerClient
- Remove from `IProvideProducers` interface
- Remove implementation from `ProducerClient`

### 5. Build and Test
```bash
dotnet build sports-data.sln --no-restore
dotnet test sports-data.sln --no-build --filter "FullyQualifiedName~.Tests.Unit"
```

## Key Files

### Client Infrastructure
- `src/SportsData.Core/Infrastructure/Clients/ClientBase.cs` - Base class with HTTP helpers
- `src/SportsData.Core/Infrastructure/Clients/ClientFactoryBase.cs` - Factory base with mode resolution
- `src/SportsData.Core/Common/Mapping/ModeMapper.cs` - Maps sport/league strings to Sport enum

### Domain Clients
- `src/SportsData.Core/Infrastructure/Clients/Contest/ContestClient.cs`
- `src/SportsData.Core/Infrastructure/Clients/Venue/VenueClient.cs`
- `src/SportsData.Core/Infrastructure/Clients/Franchise/FranchiseClient.cs`
- ~~`src/SportsData.Core/Infrastructure/Clients/Producer/ProducerClient.cs`~~ (deleted)

### CanonicalDataProvider (API intermediary - being phased out for domain clients)
- `src/SportsData.Api/Infrastructure/Data/Canonical/IProvideCanonicalData.cs`
- `src/SportsData.Api/Infrastructure/Data/Canonical/CanonicalDataProvider.cs`

### Configuration
- `src/SportsData.Core/Config/CommonConfigKeys.cs` - Config key patterns

## Configuration Pattern

```
# Mode-specific (used by factories)
CommonConfig:ContestClientConfig:FootballNcaa:ApiUrl
CommonConfig:FranchiseClientConfig:FootballNcaa:ApiUrl
CommonConfig:VenueClientConfig:FootballNcaa:ApiUrl

# ProducerClientConfig kept for HATEOAS link generation (ResourceRefGenerator)
CommonConfig:ProducerClientConfig:ApiUrl

# Fallback (if mode-specific not set)
CommonConfig:ContestClientConfig:ApiUrl
CommonConfig:FranchiseClientConfig:ApiUrl
CommonConfig:VenueClientConfig:ApiUrl
```

## UI Controllers (BFF)

UI controllers at `src/SportsData.Api/Application/UI/` don't have sport/league in their routes. When migrating methods used by these controllers:

1. Add `Sport` property to the Command/Query
2. Hardcode `Sport.FootballNcaa` in the controller with a TODO comment
3. Example: `src/SportsData.Api/Application/UI/Contest/ContestController.cs`

```csharp
// TODO: Support multiple sports
var command = new RefreshContestCommand { ContestId = id, Sport = Sport.FootballNcaa };
```

## Notes

- `CanonicalDataProvider` is being phased out as an intermediary; handlers should use domain client factories directly
- All domain clients currently point to Producer service URLs; this will change when services are split
- Producer and Provider services run in single-mode (one Sport per instance)
- API will run in mixed-mode (handles all sports, routes to appropriate backend)
- ProducerClientConfig is retained only for `ResourceRefGenerator` to generate HATEOAS links pointing to Producer's canonical data endpoints
