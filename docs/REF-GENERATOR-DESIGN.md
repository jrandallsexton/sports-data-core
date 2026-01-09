# Resource Ref (URI) Generator Design for HATEOAS

**Status:** Design Discussion - Not Yet Implemented  
**Date:** January 8, 2026  
**Context:** Building a URI generator to support HATEOAS principles across integration events and API DTOs

## Overview

This document outlines the design for a centralized resource reference (URI) generator that will:
1. Generate absolute URIs for resources in integration events
2. Support HATEOAS links on API-returned DTOs
3. Work across all microservices (Producer, API, Venue, etc.)
4. Leverage existing Azure AppConfig for service discovery

## Problem Statement

We want to include absolute URIs (refs) on:
- **Integration Events**: When Producer publishes `CompetitionStatusChanged`, consumers should be able to fetch full competition data via the ref
- **API DTOs**: API responses should include HATEOAS links to related resources

### Example Event with Ref
```csharp
// Current state (line 121 in EventCompetitionStatusDocumentProcessor)
await _publishEndpoint.Publish(new CompetitionStatusChanged(
    competitionId,
    null, // ← Ref is currently null
    command.Sport,
    command.Season,
    entity.StatusTypeName,
    command.CorrelationId,
    CausationId.Producer.EventCompetitionStatusDocumentProcessor
));

// Desired state
await _publishEndpoint.Publish(new CompetitionStatusChanged(
    competitionId,
    _refGenerator.ForCompetition(competitionId), // ← Generate ref
    command.Sport,
    command.Season,
    entity.StatusTypeName,
    command.CorrelationId,
    CausationId.Producer.EventCompetitionStatusDocumentProcessor
));
```

### Example DTO with Ref
```csharp
public class ContestDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public Uri Ref { get; set; } // Self link
    public Uri CompetitionRef { get; set; } // Cross-service ref to Producer
}
```

## Requirements

1. **Absolute URIs**: All refs must be absolute (include full base URL)
2. **Cross-Service Support**: Must work across all microservices in the solution
3. **Environment-Agnostic**: URLs automatically correct for local/dev/qa/prod
4. **Configuration-Driven**: Leverage existing Azure AppConfig infrastructure
5. **Kubernetes-Aware**: Use internal cluster DNS for service-to-service communication
6. **Sport-Aware**: Handle sport-specific service configurations

## Existing Configuration Infrastructure

Our services already use Azure AppConfig with service discovery:

### Configuration Keys
```csharp
// From CommonConfigKeys.cs
public static string GetProducerProviderUri() =>
    $"{nameof(CommonConfig)}:{nameof(ProducerClientConfig)}:{nameof(ProducerClientConfig.ApiUrl)}";

public static string GetContestProviderUri(Sport mode) =>
    $"{nameof(CommonConfig)}:{nameof(ContestClientConfig)}:{mode}:{nameof(ContestClientConfig.ApiUrl)}";

public static string GetVenueProviderUri() =>
    $"{nameof(CommonConfig)}:{nameof(VenueClientConfig)}:{nameof(VenueClientConfig.ApiUrl)}";
```

### Example Configuration (Azure AppConfig)
```json
{
  "CommonConfig": {
    "ProducerClientConfig": {
      "ApiUrl": "http://producer-svc-football-ncaa"
    },
    "ContestClientConfig": {
      "FootballNcaa": {
        "ApiUrl": "http://api-svc-football-ncaa"
      }
    },
    "VenueClientConfig": {
      "ApiUrl": "http://venue-svc"
    },
    "FranchiseClientConfig": {
      "FootballNcaa": {
        "ApiUrl": "http://franchise-svc-football-ncaa"
      }
    }
  }
}
```

### Kubernetes Service Names
From `sports-data-config/app/base/apps/producer/producer-football-ncaa-service.yaml`:
```yaml
apiVersion: v1
kind: Service
metadata:
  name: producer-svc-football-ncaa
  namespace: default
  labels:
    app: producer-football-ncaa
spec:
  selector:
    app: producer-football-ncaa
  ports:
  - name: http
    protocol: TCP
    port: 80
    targetPort: 8080
  type: ClusterIP
```

## Proposed Solution

### Interface Design

```csharp
/// <summary>
/// Generates absolute URIs for resources across all microservices.
/// Supports HATEOAS links in DTOs and integration events.
/// </summary>
public interface IResourceRefGenerator
{
    // Producer resources
    Uri ForCompetition(Guid competitionId);
    Uri ForFranchiseSeason(Guid franchiseSeasonId);
    Uri ForAthlete(Guid athleteId);
    
    // API/Contest resources
    Uri ForContest(Guid contestId);
    Uri ForPick(Guid pickId);
    Uri ForRanking(int seasonYear);
    
    // Venue resources
    Uri ForVenue(Guid venueId);
    
    // Add more as services/resources are added
}
```

### Implementation

```csharp
/// <summary>
/// Generates resource URIs using Azure AppConfig service URLs.
/// Thread-safe singleton service.
/// </summary>
public class ResourceRefGenerator : IResourceRefGenerator
{
    private readonly string _producerBaseUrl;
    private readonly string _contestBaseUrl;
    private readonly string _venueBaseUrl;
    private readonly string _franchiseBaseUrl;

    public ResourceRefGenerator(IConfiguration configuration, IAppMode appMode)
    {
        // Producer (not sport-specific)
        _producerBaseUrl = configuration[CommonConfigKeys.GetProducerProviderUri()]
            ?? throw new InvalidOperationException("ProducerClientConfig:ApiUrl not configured");
        
        // Contest/API (sport-specific)
        _contestBaseUrl = configuration[CommonConfigKeys.GetContestProviderUri(appMode.Sport)]
            ?? throw new InvalidOperationException($"ContestClientConfig:{appMode.Sport}:ApiUrl not configured");
        
        // Venue (not sport-specific)
        _venueBaseUrl = configuration[CommonConfigKeys.GetVenueProviderUri()]
            ?? throw new InvalidOperationException("VenueClientConfig:ApiUrl not configured");
        
        // Franchise (sport-specific)
        _franchiseBaseUrl = configuration[CommonConfigKeys.GetFranchiseProviderUri(appMode.Sport)]
            ?? throw new InvalidOperationException($"FranchiseClientConfig:{appMode.Sport}:ApiUrl not configured");
    }

    // Producer resources
    public Uri ForCompetition(Guid competitionId) =>
        new Uri($"{_producerBaseUrl}/api/competition/{competitionId}");

    public Uri ForFranchiseSeason(Guid franchiseSeasonId) =>
        new Uri($"{_producerBaseUrl}/api/franchiseseason/{franchiseSeasonId}");

    public Uri ForAthlete(Guid athleteId) =>
        new Uri($"{_producerBaseUrl}/api/athlete/{athleteId}");

    // API/Contest resources
    public Uri ForContest(Guid contestId) =>
        new Uri($"{_contestBaseUrl}/api/contest/{contestId}");

    public Uri ForPick(Guid pickId) =>
        new Uri($"{_contestBaseUrl}/api/pick/{pickId}");

    public Uri ForRanking(int seasonYear) =>
        new Uri($"{_contestBaseUrl}/api/rankings/{seasonYear}");

    // Venue resources
    public Uri ForVenue(Guid venueId) =>
        new Uri($"{_venueBaseUrl}/api/venue/{venueId}");
}
```

### Dependency Injection Registration

Add to `ServiceRegistration.cs` in `SportsData.Core`:

```csharp
public static IServiceCollection AddCoreServices(
    this IServiceCollection services,
    IConfiguration configuration,
    Sport mode = Sport.All)
{
    services.AddScoped<IDecodeDocumentProvidersAndTypes, DocumentProviderAndTypeDecoder>();
    services.Configure<CommonConfig>(configuration.GetSection("CommonConfig"));
    services.AddScoped<IDateTimeProvider, DateTimeProvider>();
    services.AddSingleton<IAppMode>(new AppMode(mode));
    services.AddScoped<IGenerateRoutingKeys, RoutingKeyGenerator>();
    services.AddScoped<IJsonHashCalculator, JsonHashCalculator>();
    services.AddSingleton<IGenerateExternalRefIdentities, ExternalRefIdentityGenerator>();
    services.AddSingleton<IResourceRefGenerator, ResourceRefGenerator>(); // ← ADD THIS
    return services;
}
```

## Usage Examples

### In Integration Events (Producer)

Update `DocumentProcessorBase` to inject the ref generator:

```csharp
public abstract class DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    protected readonly ILogger _logger;
    protected readonly TDataContext _dataContext;
    protected readonly IEventBus _publishEndpoint;
    protected readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
    protected readonly IResourceRefGenerator _refGenerator; // ← ADD THIS

    protected DocumentProcessorBase(
        ILogger logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IResourceRefGenerator refGenerator) // ← ADD PARAMETER
    {
        _logger = logger;
        _dataContext = dataContext;
        _publishEndpoint = publishEndpoint;
        _externalRefIdentityGenerator = externalRefIdentityGenerator;
        _refGenerator = refGenerator; // ← ASSIGN
    }
}
```

Use in event publishing:

```csharp
// In EventCompetitionStatusDocumentProcessor.cs line 121
await _publishEndpoint.Publish(new CompetitionStatusChanged(
    competitionId,
    _refGenerator.ForCompetition(competitionId), // ← Use ref generator
    command.Sport,
    command.Season,
    entity.StatusTypeName,
    command.CorrelationId,
    CausationId.Producer.EventCompetitionStatusDocumentProcessor
));
```

### In API DTOs

```csharp
public class ContestDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public DateTime Date { get; set; }
    
    // HATEOAS links
    public Uri Ref { get; set; } // Self
    public Uri CompetitionRef { get; set; } // Related resource from Producer
    public Uri VenueRef { get; set; } // Related resource from Venue service
}

// In your controller or mapper:
public ContestDto MapToDto(Contest contest)
{
    return new ContestDto
    {
        Id = contest.Id,
        Name = contest.Name,
        Date = contest.Date,
        
        // Same-service ref
        Ref = _refGenerator.ForContest(contest.Id),
        
        // Cross-service refs
        CompetitionRef = _refGenerator.ForCompetition(contest.CompetitionId),
        VenueRef = _refGenerator.ForVenue(contest.VenueId)
    };
}
```

### In Event Consumers

```csharp
public class CompetitionStatusChangedConsumer : IConsumer<CompetitionStatusChanged>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CompetitionStatusChangedConsumer> _logger;

    public async Task Consume(ConsumeContext<CompetitionStatusChanged> context)
    {
        var evt = context.Message;
        
        _logger.LogInformation(
            "Competition {CompetitionId} status changed to {Status}. Ref: {Ref}",
            evt.CompetitionId,
            evt.Status,
            evt.Ref);
        
        // Option 1: Use just the event data
        await ProcessStatusChange(evt);
        
        // Option 2: Fetch full resource if needed
        if (NeedsFullCompetitionData(evt))
        {
            var httpClient = _httpClientFactory.CreateClient();
            var competition = await httpClient.GetFromJsonAsync<Competition>(evt.Ref);
            await ProcessWithFullData(competition);
        }
    }
}
```

## Benefits

1. **Single Source of Truth**: All service URLs managed in Azure AppConfig
2. **Environment-Agnostic**: Automatically correct URLs for local/dev/qa/prod
3. **Cross-Service HATEOAS**: Any service can generate refs to any other service's resources
4. **No New Configuration**: Leverages existing `ProducerClientConfig`, `ContestClientConfig`, etc.
5. **Type-Safe**: Strongly-typed methods prevent typos in resource paths
6. **Testable**: Easy to mock for unit tests
7. **Consistent**: All refs generated through same mechanism
8. **Discoverable**: Consumers can navigate resources without hardcoding URLs

## Environment Behavior

### Local Development
```
Base URL: http://localhost:5001
Generated: http://localhost:5001/api/competition/{guid}
```

### Kubernetes (Dev/QA/Prod)
```
Base URL: http://producer-svc-football-ncaa
Generated: http://producer-svc-football-ncaa/api/competition/{guid}
```

The DNS resolution happens automatically within the cluster.

## Considerations & Future Work

### 1. GET Endpoints Required
Currently, many controllers only have POST endpoints. To make refs useful, we need to add GET endpoints:

```csharp
[HttpGet("{competitionId}")]
public async Task<ActionResult<Competition>> GetCompetition(
    [FromRoute] Guid competitionId,
    CancellationToken cancellationToken)
{
    // Implementation
}
```

### 2. API Versioning
Future consideration for versioning:

```csharp
// Option A: Version in path
public Uri ForCompetition(Guid competitionId) =>
    new Uri($"{_producerBaseUrl}/api/v1/competition/{competitionId}");

// Option B: Version in header (ref stays versionless)
public Uri ForCompetition(Guid competitionId) =>
    new Uri($"{_producerBaseUrl}/api/competition/{competitionId}");
```

### 3. External vs Internal Refs
Currently assumes internal Kubernetes DNS. For external consumers, may need:

```csharp
public interface IResourceRefGenerator
{
    Uri ForCompetition(Guid competitionId, bool external = false);
}

// Internal: http://producer-svc-football-ncaa/api/competition/{id}
// External: https://api.sportdeets.com/producer/api/competition/{id}
```

### 4. Route Consistency
Need to ensure URL patterns in `ForXxx()` methods match actual controller routes. Consider:

- Using route names and reflection to stay in sync
- Automated tests to validate ref patterns
- Code generation from controller attributes

### 5. EventBase Ref Property
The `EventBase` already has `Uri? Ref` property:

```csharp
// From SportsData.Core/Eventing/EventBase.cs
public abstract record EventBase(Uri? Ref, Sport Sport, int? SeasonYear, Guid CorrelationId, Guid CausationId)
{
    // TODO: Make Ref non-nullable in the future
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
}
```

Once implemented, can make `Ref` non-nullable.

### 6. Additional Events to Update
Beyond `CompetitionStatusChanged`, consider adding refs to:
- `ContestCreated`
- `ContestStartTimeUpdated`
- `ContestOddsUpdated`
- `CompetitionPlayCompleted`
- `CompetitionWinProbabilityChanged`

### 7. HATEOAS Link Collections
For richer HATEOAS support, consider link collections:

```csharp
public class ContestDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    
    public Dictionary<string, Uri> Links { get; set; } = new()
    {
        ["self"] = _refGenerator.ForContest(id),
        ["competition"] = _refGenerator.ForCompetition(competitionId),
        ["venue"] = _refGenerator.ForVenue(venueId),
        ["teams"] = new Uri($"{baseUrl}/api/contest/{id}/teams"),
        ["plays"] = new Uri($"{baseUrl}/api/contest/{id}/plays")
    };
}
```

## File Locations

### New Files to Create
- `src/SportsData.Core/Infrastructure/Refs/IResourceRefGenerator.cs`
- `src/SportsData.Core/Infrastructure/Refs/ResourceRefGenerator.cs`

### Files to Modify
- `src/SportsData.Core/DependencyInjection/ServiceRegistration.cs` - Add DI registration
- `src/SportsData.Producer/Application/Documents/Processors/DocumentProcessorBase.cs` - Inject ref generator
- All document processors that publish events - Use ref generator when publishing

### Tests to Create
- `test/unit/SportsData.Core.Tests.Unit/Infrastructure/Refs/ResourceRefGeneratorTests.cs`

## Migration Path

1. **Phase 1: Core Implementation**
   - Create `IResourceRefGenerator` interface
   - Implement `ResourceRefGenerator`
   - Add DI registration
   - Write unit tests

2. **Phase 2: Producer Integration**
   - Update `DocumentProcessorBase`
   - Update event publishing in document processors
   - Add GET endpoints to Producer controllers

3. **Phase 3: API Integration**
   - Add ref generation to DTO mappers
   - Include refs in API responses
   - Add GET endpoints where missing

4. **Phase 4: Cross-Service Refs**
   - Enable cross-service ref generation
   - Update DTOs to include related resource refs
   - Document ref patterns

5. **Phase 5: EventBase Cleanup**
   - Make `Ref` property non-nullable once all events generate refs

## Related Documentation

- [INTEGRATION_EVENTS.md](INTEGRATION_EVENTS.md) - Integration event catalog
- [HATEOAS Principles](https://en.wikipedia.org/wiki/HATEOAS) - Industry standard
- Azure AppConfig documentation
- Kubernetes service discovery documentation

## Decision Log

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-01-08 | Use existing Azure AppConfig for service URLs | Already configured and used by HttpClients |
| 2026-01-08 | Create single IResourceRefGenerator for all services | Centralized, reusable, cross-service support |
| 2026-01-08 | Generate absolute URIs | Required for service-to-service communication |
| 2026-01-08 | Use template-based approach (not route-based) | Simpler, works in background jobs without HttpContext |

## Questions for Future Resolution

1. Should refs point to internal Kubernetes services or external API gateway?
2. How to handle API versioning in refs?
3. Should we support both internal and external ref generation?
4. Do we need a ref validator to ensure endpoints exist?
5. Should we auto-generate ref methods from controller routes?

---

**Next Steps When Resuming:**
1. Review and refine this design
2. Create interfaces and implementation
3. Add comprehensive unit tests
4. Update EventCompetitionStatusDocumentProcessor as proof of concept
5. Expand to other events and DTOs iteratively
