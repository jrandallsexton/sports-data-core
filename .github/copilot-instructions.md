# GitHub Copilot Instructions for SportsData Platform

## Core Architectural Principles

### Service Architecture
- **Modular Monolith with Clean Extraction Seams**: Three primary services with clear boundaries
  - **API**: External-facing, slug-based routes, HATEOAS-enriched responses
  - **Producer**: Internal canonical data source, GUID-based operations, VSA/CQRS pattern
  - **Provider**: Integration layer for external data sources (ESPN, etc.)
- **No Premature Microservices**: Keep services coarse-grained unless clear scaling/team boundaries exist
- **Service Communication**: HTTP-based with typed clients, not gRPC (HTTP/2 gains not worth ceremony)

### Domain-Driven Design
- **Aggregate Roots Define Service Boundaries**: Services align with aggregate roots (Franchise, Venue, Competition, etc.), NOT child entities
- **Never Create Services for Child Entities**: No FranchiseSeasonService, VenueLogoService, TeamRosterService - these belong to their parent aggregate
- **RESTful Nesting Reflects Aggregates**: `/franchises/{id}/seasons`, `/teams/{id}/roster` - access child entities through parent

### API Design Patterns

#### External API (SportsData.Api)
- **Slug-Based Routes**: Use human-readable slugs for all primary identifiers
  - Example: `/api/football/ncaa/franchises/lsu-tigers` instead of GUIDs
  - Route pattern: `/api/{sport}/{league}/{resource}/{slug}`
- **HATEOAS Required**: All responses must include `ref` and `links` properties
  - Use `ApiResourceRefGenerator` to generate external URLs
  - Always include `self` link, add navigation links to related resources
  - Example: Franchise DTO includes `links.seasons` pointing to seasons endpoint
- **Two-Step Resolution**: API resolves slug → GUID, then calls Producer with GUID
- **No Canonical DTOs**: API has its own response DTOs enriched with HATEOAS

#### Internal Producer (SportsData.Producer)
- **GUID-Based Operations**: All internal operations use GUIDs for performance (indexed PK lookups)
- **Canonical DTOs**: Producer returns canonical DTOs from `SportsData.Core.Dtos.Canonical`
- **VSA/CQRS Pattern**: Replacing MediatR for better F12 navigation
  - Explicit query handler interfaces: `IGetFranchiseByIdQueryHandler`
  - Handlers in `Queries/{QueryName}/{QueryName}QueryHandler.cs`
  - Commands in `Commands/{CommandName}/{CommandName}CommandHandler.cs`
- **Result<T> Pattern**: All handlers return `Result<T>`, use `.ToActionResult()` extension
- **Route Pattern**: `/api/{resource}` (no sport/league prefix - multi-tenancy via mode)

### Configuration Management
- **Azure AppConfig Exclusively**: NO appsettings.json for configuration values
- **Label-Based Multi-Tenancy**: Configuration differentiated via labels
  - Pattern: `{Environment}.{Mode}.{Application}` (e.g., `Local.FootballNcaa.SportsData.Api`)
  - Mode examples: `FootballNcaa`, `FootballNfl`, `BasketballNba`, `GolfPga`, etc.
- **Config Keys Without Mode**: Keys like `CommonConfig:FranchiseClientConfig:ApiUrl` - labels provide mode differentiation
- **Mode Resolution**: `ModeMapper.ResolveMode(sport, league)` converts sport/league → Sport enum

### Client Factory Pattern
- **Factory Per Aggregate Root**: VenueClientFactory, FranchiseClientFactory, etc.
- **Runtime Sport/League Resolution**: Factories resolve clients by sport/league mode
- **Client Caching**: Cache clients per Sport enum to avoid recreation
- **HttpClient Registration**: Register HttpClient with BaseAddress per client in DI
- **No Attribute-Based Factories for Clients**: Explicit factories only (low cardinality ~5-10 clients)

### Document Processor Pattern
- **Attribute-Based Registration**: Use `[DocumentProcessor(source, sport, documentType)]` for auto-discovery
- **High Cardinality Pattern**: 50+ processors growing multiplicatively with sports
- **Generic DbContext Handling**: `DocumentProcessorFactory<TDbContext>` ensures MassTransit outbox works
- **Registry-Based Resolution**: `DocumentProcessorRegistry` scans assemblies at startup

## Technology Stack

### Infrastructure (Kubernetes on Bare Metal)
- **Cluster**: 4-node NUC cluster + separate PostgreSQL NUC
- **GitOps**: Flux for declarative deployments
- **Ingress**: Traefik
- **Observability**:
  - Metrics: Prometheus + Grafana
  - Logs: Serilog → Seq, Loki
  - Traces: OpenTelemetry → Tempo
  - Alerting: AlertManager
- **Storage**: SMB CSI driver
- **Management**: Headlamp, Reloader

### Data Persistence
- **Relational**: PostgreSQL (canonical data in Producer)
- **Document**: MongoDB/CosmosDB (Provider raw JSON documents)
- **Caching**: Redis (distributed cache)
- **Polyglot Persistence**: Choose storage based on access pattern

### .NET Patterns
- **Target Framework**: .NET 10
- **Validation**: FluentValidation
- **Messaging**: MassTransit with PostgreSQL outbox
- **Background Jobs**: Hangfire
- **ORM**: Entity Framework Core with Npgsql

## Code Organization

### Project Structure
- `src/SportsData.Api` - External API
- `src/SportsData.Producer` - Canonical data source
- `src/SportsData.Provider` - External integrations
- `src/SportsData.Core` - Shared libraries (DTOs, clients, common)
- `test/unit/` - Unit tests
- `test/integration/` - Integration tests

### Namespace Conventions
- Controllers: `{Project}.Application.{AggregateRoot}`
- Queries: `{Project}.Application.{AggregateRoot}.Queries.{QueryName}`
- Commands: `{Project}.Application.{AggregateRoot}.Commands.{CommandName}`
- Entities: `{Project}.Infrastructure.Data.Entities`
- DTOs: `SportsData.Core.Dtos.Canonical` (Producer), `{Project}.Application.{AggregateRoot}` (API)

### File Naming
- Queries: `{QueryName}Query.cs`, `{QueryName}QueryHandler.cs` in folder `{QueryName}/`
- Commands: `{CommandName}Command.cs`, `{CommandName}CommandHandler.cs` in folder `{CommandName}/`
- Controllers: `{AggregateRoot}Controller.cs` or `{AggregateRoot}sController.cs`

## Coding Standards

### RESTful Controllers
- Use `.ToActionResult()` extension instead of manual Result<T> pattern matching
- Always use `[FromServices]` for handler injection
- Route parameters: `{sport}/{league}/{resourceSlug}` for API, `{resourceId}` for Producer
- Child resources via nested routes: `{parent}/{parentId}/{child}`

### HATEOAS Implementation
- **Two Ref Generators**:
  - `ApiResourceRefGenerator` (API layer) - generates external URLs with sport/league context
  - `ResourceRefGenerator` (Producer/Core) - generates internal service-to-service URLs
- **Always Include**:
  - `ref` property on all resource DTOs
  - `links.self` at minimum
  - Navigation links to related resources (e.g., `links.seasons`, `links.franchise`)
- **Slug Usage**: Use slugs in HATEOAS refs, not GUIDs (cleaner, more discoverable)

### Result Pattern
- All query/command handlers return `Result<T>`
- Success: `new Success<T>(value)`
- Failure: `new Failure<T>(default!, status, errors)` with FluentValidation errors
- Controller conversion: `result.ToActionResult()`
- Status mapping:
  - `ResultStatus.Created` → 201
  - `ResultStatus.Accepted` → 202
  - `ResultStatus.BadRequest` → 400
  - `ResultStatus.NotFound` → 404
  - `ResultStatus.Unauthorized` → 401

### Entity Framework
- Use `.AsNoTracking()` for all read queries
- Explicit projection to DTOs in query (don't return entities)
- Leverage EF Core navigation properties but project to DTOs
- PostgreSQL-specific: Use `xmin` for optimistic concurrency (`uint RowVersion`)

## Testing Strategy
- Prefer integration tests for query/command handlers
- Unit test business logic, not EF queries
- Use TestContainers for database-dependent tests
- Mock external HTTP calls, not internal dependencies

## Performance Guidelines
- **Slug Resolution**: Resolve slug → GUID once at API layer, use GUID for all downstream calls
- **Client Caching**: Factory pattern caches clients per mode (Sport enum)
- **Pagination**: Always paginate collection endpoints (default 50, max configurable)
- **Projection**: Project to DTOs in EF query, don't retrieve full entities

## Future Architectural Considerations
- **GraphQL**: Planned via Hot Chocolate (learning goal)
- **Event Streaming**: Kafka for live contest/competition updates (future need)
- **gRPC**: Decided against for service-to-service (HTTP/2 gains not worth ceremony)

## Critical Reminders
1. **No appsettings.json**: Everything in Azure AppConfig
2. **Slugs for API, GUIDs for Producer**: Two-tier identifier strategy
3. **HATEOAS is non-negotiable**: All API responses must be hypermedia-driven
4. **Aggregate boundaries define services**: Never create services for child entities
5. **Factory pattern for clients**: VenueClientFactory, FranchiseClientFactory, etc.
6. **Mode differentiation via labels**: Config keys don't include mode, labels provide it
7. **VSA over MediatR**: Explicit handler interfaces for F12 navigation
8. **Season ends Jan 19, 2026**: This PR must merge after NCAA football season completes

## Multi-Sport Scaling
- Current sports: FootballNcaa, FootballNfl, BasketballNba, BasketballNcaa, GolfPga, GolfLiv, BaseballMlb, BaseballNcaa
- Factory pattern + Azure AppConfig labels enable seamless sport addition
- No code changes needed for new sports - just add Mode enum, config label, and ModeMapper entry
- Document processors scale multiplicatively: 10 sports × 20 document types = 200+ processors

## Common Patterns to Follow

### Adding a New Aggregate Root Endpoint
1. **Producer**: Create query handler returning canonical DTO, add endpoint using GUID
2. **Core**: Create client + factory with sport/league resolution
3. **API**: Create query handler with slug resolution, controller, response DTOs with HATEOAS
4. **Ref Generator**: Add methods to ApiResourceRefGenerator for new resource
5. **DI Registration**: Register handlers, factories in respective ServiceRegistration.cs files

### Adding Child Resource Endpoint
1. Add to **parent's controller** as nested route (e.g., `/franchises/{id}/seasons`)
2. Use parent's factory (e.g., FranchiseClientFactory for seasons)
3. Add HATEOAS link on parent DTO (e.g., `links.seasons` on FranchiseResponseDto)
4. Never create separate service/controller for child entity

### Configuration Values
- Retrieve via `IConfiguration[key]` using Azure AppConfig keys
- Example: `configuration["SportsData.Api:ApiConfig:BaseUrl"]`
- Label automatically applied based on mode selection at startup
- See `CommonConfigKeys.cs` for standard key patterns
