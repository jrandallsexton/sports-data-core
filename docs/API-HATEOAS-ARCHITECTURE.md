# API & HATEOAS Architecture Strategy

**Date:** January 10, 2026  
**Status:** Architectural Decision  
**Context:** Designing external API layer with HATEOAS while keeping internal services clean

## Overview

This document captures the architectural decisions around exposing internal microservices (Producer, Provider, VenueService, etc.) through the external-facing SportsData.Api, with a focus on HATEOAS implementation and separation of concerns.

## Core Principle

**Internal APIs should NOT include HATEOAS. External APIs SHOULD include HATEOAS.**

### Internal Services (Producer, Provider, VenueService, etc.)
- **Purpose:** Data management and canonical operations
- **DTOs:** Clean, focused on data integrity
- **No refs:** Services return canonical DTOs without navigation links
- **Service-to-service:** Optimized for inter-service communication
- **Example:** Producer's VenueController returns `VenueDto` (canonical data only)

### External API (SportsData.Api)
- **Purpose:** Consumer-facing interface with discoverability
- **DTOs:** Enriched with HATEOAS refs and navigation links
- **Adds refs:** Uses `IResourceRefGenerator` to inject links
- **Consumer-focused:** Optimized for external client navigation
- **Example:** API's VenueController returns enriched response with refs

## Architectural Decision: API Controller + Client Pattern

### The Question
When exposing internal services externally (e.g., Venues), should we:
- **Option A:** API Controller + VenueClient (pass-through with enrichment)
- **Option B:** Ocelot API Gateway (direct routing)

### The Decision: Option A (API Controller + Client)

**Rationale:**

1. **HATEOAS Injection Point**
   - API is the ONLY layer where refs should be added
   - Allows transformation from canonical DTO → enriched DTO
   - Ocelot cannot add refs without custom middleware

2. **Separation of Concerns**
   - Internal: "Here's venue data" (canonical)
   - External: "Here's venue data + navigation" (enriched)
   - Clean boundary between internal and external contracts

3. **Consistent Pattern**
   - Already using this for Producer, Contest, Franchise clients
   - Maintains architectural consistency
   - Well-understood pattern across team

4. **Future-Proof**
   - When VenueService replaces Producer, just update client base URL
   - API controller and external contracts unchanged
   - Smooth migration path

5. **Flexibility**
   - Can aggregate data from multiple sources
   - Can apply user-specific filtering/authorization
   - Can maintain stable external contracts even when internal changes
   - Supports API versioning independently of internal services

### Why NOT Ocelot for This Use Case

Ocelot is excellent for:
- ✅ Pure routing/load balancing
- ✅ Exposing admin/internal endpoints directly
- ✅ Rate limiting, circuit breakers
- ✅ When no transformation is needed

But for consumer-facing APIs with HATEOAS:
- ❌ No transformation layer to add refs
- ❌ External consumers would get internal canonical format
- ❌ Tight coupling between external API contract and internal implementation
- ❌ Defeats the purpose of having a public API layer

## Implementation Pattern

### Internal Service (Producer/VenueService)

**Canonical DTO (no refs):**
```csharp
// VenueDto.cs (in Core)
public class VenueDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    // No Ref property!
    // No Links property!
}
```

**Controller:**
```csharp
// Producer/VenuesController.cs
[ApiController]
[Route("api/venues")]
public class VenuesController : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<ActionResult<VenueDto>> GetVenue(Guid id)
    {
        var venue = await _venueService.GetVenueAsync(id);
        return Ok(venue); // Returns canonical DTO only
    }
}
```

### External API (SportsData.Api)

**Enriched Response DTO:**
```csharp
// VenueResponseDto.cs (in API project)
public class VenueResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    
    // HATEOAS additions
    public Uri Ref { get; set; }
    public Dictionary<string, Uri> Links { get; set; }
}
```

**Controller with Enrichment:**
```csharp
// API/VenueController.cs
[ApiController]
[Route("api/venues")]
public class VenueController : ControllerBase
{
    private readonly IVenueClient _venueClient;
    private readonly IResourceRefGenerator _refGenerator;

    [HttpGet("{id}")]
    public async Task<ActionResult<VenueResponseDto>> GetVenue(Guid id)
    {
        // Get canonical data from internal service
        var canonicalVenue = await _venueClient.GetVenue(id);
        
        // Enrich with HATEOAS
        var response = new VenueResponseDto
        {
            Id = canonicalVenue.Id,
            Name = canonicalVenue.Name,
            City = canonicalVenue.City,
            State = canonicalVenue.State,
            
            // Add refs using IResourceRefGenerator
            Ref = _refGenerator.ForVenue(canonicalVenue.Id),
            Links = new Dictionary<string, Uri>
            {
                ["self"] = _refGenerator.ForVenue(canonicalVenue.Id),
                ["events"] = new Uri($"{_baseUrl}/api/venues/{id}/events"),
                ["teams"] = new Uri($"{_baseUrl}/api/venues/{id}/teams")
            }
        };
        
        return Ok(response);
    }
}
```

### VenueClient Configuration

```csharp
// ServiceRegistration.cs
services.AddHttpClient<IVenueClient, VenueClient>(client =>
{
    // Currently points to Producer
    client.BaseAddress = new Uri(configuration[CommonConfigKeys.GetProducerProviderUri()]);
    
    // Later: Update to VenueService
    // client.BaseAddress = new Uri(configuration[CommonConfigKeys.GetVenueProviderUri()]);
});
```

## Integration Events - Special Case

Integration events bridge internal and external worlds. They should include refs pointing to the **external API**, not internal services.

### Event with Ref

```csharp
// VenueCreated.cs
public record VenueCreated(
    VenueDto Canonical,
    Uri? Ref,
    Sport Sport,
    int? SeasonYear,
    Guid CorrelationId,
    Guid CausationId
) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
```

### Publishing from Internal Service

```csharp
// In Producer/VenueProcessor
await _publishEndpoint.Publish(new VenueCreated(
    canonical: venueDto,
    @ref: _refGenerator.ForVenue(venueDto.Id), // Points to API, not Producer!
    sport: Sport.FootballNcaa,
    seasonYear: 2025,
    correlationId: correlationId,
    causationId: CausationId.Producer.VenueProcessor
));
```

**Why ref points to external API:**
- Event consumers (other services, external systems) should use the public API
- Refs provide discoverability: "If you need more data, GET this URL"
- Decouples consumers from internal service topology
- Consumers don't need to know about Producer vs VenueService

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                    External Consumer                         │
│                 (Web UI, Mobile App, etc.)                   │
└────────────────────────┬────────────────────────────────────┘
                         │ HTTPS
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                   SportsData.Api                             │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  VenueController                                     │   │
│  │  - Gets canonical DTO from VenueClient               │   │
│  │  - Enriches with refs via IResourceRefGenerator      │   │
│  │  - Returns VenueResponseDto with HATEOAS             │   │
│  └──────────────────┬───────────────────────────────────┘   │
│                     │                                        │
│  ┌──────────────────▼───────────────────────────────────┐   │
│  │  VenueClient (HttpClient)                            │   │
│  └──────────────────┬───────────────────────────────────┘   │
└─────────────────────┼────────────────────────────────────────┘
                      │ HTTP (internal)
                      ▼
┌─────────────────────────────────────────────────────────────┐
│             Producer (or VenueService)                       │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  VenuesController                                    │   │
│  │  - Returns canonical VenueDto (no refs)              │   │
│  │  - Pure data, no navigation concerns                 │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  VenueProcessor                                      │   │
│  │  - Publishes VenueCreated event                      │   │
│  │  - Event includes ref to external API                │   │
│  └──────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────┘
```

## Migration Path: Producer → VenueService

When VenueService is created to replace Producer's venue functionality:

### Step 1: Create VenueService
```csharp
// VenueService/VenuesController.cs
[ApiController]
[Route("api/venues")]
public class VenuesController : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<ActionResult<VenueDto>> GetVenue(Guid id)
    {
        // Same canonical DTO, different service
        var venue = await _repository.GetVenueAsync(id);
        return Ok(venue);
    }
}
```

### Step 2: Update VenueClient Configuration
```csharp
// ServiceRegistration.cs - ONE LINE CHANGE
services.AddHttpClient<IVenueClient, VenueClient>(client =>
{
    // OLD: Producer
    // client.BaseAddress = new Uri(configuration[CommonConfigKeys.GetProducerProviderUri()]);
    
    // NEW: VenueService
    client.BaseAddress = new Uri(configuration[CommonConfigKeys.GetVenueProviderUri()]);
});
```

### Step 3: Update Azure AppConfig
```json
{
  "CommonConfig": {
    "VenueClientConfig": {
      "ApiUrl": "http://venue-svc"  // New service
    }
  }
}
```

**External API (SportsData.Api) remains unchanged!**
- Controller code unchanged
- External contracts unchanged
- Consumers unaffected
- HATEOAS refs still point to same external API endpoints

## Benefits of This Approach

### 1. Clean Separation of Concerns
- **Internal services:** Focus on data correctness, canonical operations
- **External API:** Focus on consumer experience, discoverability, navigation

### 2. Flexibility
- Can aggregate data from multiple internal services
- Can apply user-specific filtering/permissions
- Can version external API independently of internal changes
- Can transform DTOs as needed for different consumers

### 3. HATEOAS Implementation
- Single point to inject refs (API layer)
- Refs always point to correct external endpoints
- Consumers navigate via refs, not hardcoded paths
- Enables true REST Level 3 maturity

### 4. Future-Proof
- Internal service refactoring doesn't break external consumers
- Can introduce new internal services transparently
- Can change internal communication patterns (gRPC, message queues) without affecting external API

### 5. Testability
- Can mock VenueClient in API tests
- Can test ref generation independently
- Can verify HATEOAS structure without internal services running

## Anti-Patterns to Avoid

### ❌ Don't: Add Refs to Canonical DTOs
```csharp
// BAD - in Core/Dtos
public class VenueDto
{
    public Guid Id { get; set; }
    public Uri Ref { get; set; } // ❌ Don't do this!
}
```
**Why:** Canonical DTOs should be pure data, reusable across internal services.

### ❌ Don't: Have Internal Services Call IResourceRefGenerator
```csharp
// BAD - in Producer
public async Task<VenueDto> GetVenue(Guid id)
{
    var venue = await _repository.GetAsync(id);
    venue.Ref = _refGenerator.ForVenue(id); // ❌ Don't do this!
    return venue;
}
```
**Why:** Internal services shouldn't know about external API structure.

### ❌ Don't: Return Different DTO Types Based on Caller
```csharp
// BAD - trying to be "smart"
public async Task<object> GetVenue(Guid id, bool includeRefs = false)
{
    var venue = await _repository.GetAsync(id);
    if (includeRefs) 
        return new VenueResponseDto { ...venue, Ref = ... }; // ❌ Don't do this!
    return venue;
}
```
**Why:** Internal services should have single, predictable return types.

### ❌ Don't: Use Ocelot When Transformation is Needed
```yaml
# BAD - in ocelot.json
{
  "UpstreamPathTemplate": "/api/venues/{id}",
  "DownstreamPathTemplate": "/api/venues/{id}",
  "DownstreamScheme": "http",
  "DownstreamHostAndPorts": [
    { "Host": "producer-svc", "Port": 80 }
  ]
}
```
**Why:** Direct routing bypasses the enrichment layer where refs are added.

## Related Documentation

- [REF-GENERATOR-DESIGN.md](REF-GENERATOR-DESIGN.md) - Resource Ref/URI Generator Design
- [INTEGRATION_EVENTS.md](INTEGRATION_EVENTS.md) - Integration Events Catalog
- [TOKEN-EXPIRATION-ANALYSIS.md](TOKEN-EXPIRATION-ANALYSIS.md) - Authentication Flow

## Decision Log

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-01-10 | HATEOAS only in external API | Keeps internal services focused on data, external API focused on consumer experience |
| 2026-01-10 | API Controller + Client over Ocelot | Provides transformation layer for refs, maintains architectural consistency |
| 2026-01-10 | Event refs point to external API | Decouples consumers from internal service topology |

## Future Considerations

### API Versioning
When introducing API v2:
- Create separate response DTOs (VenueResponseDtoV2)
- Maintain v1 controllers alongside v2
- Internal canonical DTOs unchanged
- Both versions call same VenueClient

### GraphQL/BFF Pattern
If implementing GraphQL or Backend-for-Frontend:
- GraphQL resolvers call VenueClient (same as REST controllers)
- Add refs in resolver layer, not data fetchers
- Canonical DTOs remain unchanged

### Performance Optimization
If pass-through latency becomes an issue:
- Consider response caching at API layer
- Implement circuit breakers on VenueClient
- Use read replicas for internal services
- Keep the pattern - don't bypass enrichment for performance

---

**Status:** Active - This is the current architectural pattern for external API design.

## Implementation Status

### ✅ Completed: Venues Vertical Slice (January 10, 2026)

The Venues resource has been fully implemented following this architecture:

**Files Modified/Created:**

1. **SportsData.Core (Canonical DTOs - No HATEOAS)**
   - ✅ `src/SportsData.Core/Dtos/Canonical/DtoBase.cs` - Removed `Ref` property
   - ✅ `src/SportsData.Core/Dtos/Canonical/VenueDto.cs` - Already clean (inherits from DtoBase)

2. **SportsData.Producer (Internal Service - No HATEOAS)**
   - ✅ `src/SportsData.Producer/Application/Venues/VenuesController.cs` - Already clean, returns canonical DTOs

3. **SportsData.Api (External API - With HATEOAS)**
   - ✅ `src/SportsData.Api/Application/UI/Venues/VenueResponseDto.cs` - Created enriched DTO
   - ✅ `src/SportsData.Api/Application/UI/Venues/GetVenuesResponseDto.cs` - Created enriched response
   - ✅ `src/SportsData.Api/Application/UI/Venues/VenuesController.cs` - Updated with enrichment logic

4. **Infrastructure (Already in place)**
   - ✅ `src/SportsData.Core/Infrastructure/Refs/IGenerateResourceRefs.cs` - Interface exists
   - ✅ `src/SportsData.Core/Infrastructure/Refs/ResourceRefGenerator.cs` - Implementation exists
   - ✅ `src/SportsData.Core/Infrastructure/Clients/Venue/VenueClient.cs` - Client exists
   - ✅ `src/SportsData.Core/DependencyInjection/ServiceRegistration.cs` - DI registration exists

**Key Changes:**

1. **Removed HATEOAS from Canonical DTOs**
   - Removed `Uri? Ref` property from `DtoBase`
   - All canonical DTOs (VenueDto, etc.) now pure data, no navigation

2. **Producer Stays Clean**
   - VenuesController returns canonical `VenueDto` only
   - No awareness of external API structure
   - No refs, no links, no HATEOAS

3. **API Layer Adds HATEOAS**
   - New `VenueResponseDto` with `Ref` and `Links` properties
   - `VenuesController.EnrichVenue()` transforms canonical → enriched
   - Uses `IGenerateResourceRefs` to create URIs
   - Returns enriched DTOs to external consumers

**Example Request/Response:**

```http
GET /api/sports/football/leagues/ncaa/venues/123e4567-e89b-12d3-a456-426614174000
```

```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "name": "Michigan Stadium",
  "shortName": "The Big House",
  "slug": "michigan-stadium",
  "capacity": 107601,
  "isGrass": true,
  "isIndoor": false,
  "address": {
    "city": "Ann Arbor",
    "state": "MI"
  },
  "latitude": 42.2658,
  "longitude": -83.7487,
  "ref": "http://api.sportsdata.com/venues/123e4567-e89b-12d3-a456-426614174000",
  "links": {
    "self": "http://api.sportsdata.com/venues/123e4567-e89b-12d3-a456-426614174000"
  }
}
```

**Internal Producer Response (No HATEOAS):**

```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "name": "Michigan Stadium",
  "shortName": "The Big House",
  "slug": "michigan-stadium",
  "capacity": 107601,
  "isGrass": true,
  "isIndoor": false,
  "address": {
    "city": "Ann Arbor",
    "state": "MI"
  },
  "latitude": 42.2658,
  "longitude": -83.7487
}
```

Notice: No `ref`, no `links` - pure canonical data only.
