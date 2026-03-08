# Claude Code Project Instructions

## Repository Overview
sports-data-core is a microservices platform for sports data ingestion, processing, and consumption.
GitHub: github.com/jrandallsexton/sports-data-core

## Architecture
- **Services** (~12): Provider, Producer, API, Contest, Franchise, Notification, Player, Season, Venue, JobsDashboard, ProcessorGen, Core (shared)
- **Database**: PostgreSQL (canonical data via EF Core/Npgsql), MongoDB/Cosmos DB (Provider document store)
- **Messaging**: MassTransit over RabbitMQ (local) / Azure Service Bus (production). Config in `MessagingRegistration.cs` (NOT `ServiceRegistration.cs`). All config keys prefixed with `CommonConfig:`.
- **Auth**: Firebase Authentication
- **Hosting**: Self-hosted Kubernetes on bare metal (NOT Azure Container Apps)
- **Observability**: OpenTelemetry + Seq at `logging.sportdeets.com`. See `docs/seq-mcp.md` and `docs/seq-mcp-usage.md`.
- **CI**: Azure Pipelines for .NET services (self-hosted agent pool `Default`, agent `Bender`), GitHub Actions for mobile app
- **Mobile**: Expo SDK 55 / React Native 0.83.2 at `src/UI/sd-mobile`. Jest 29 required (Jest 30 incompatible with Expo 55).
- **Web**: React at `src/UI/sd-ui`

## Key Patterns
- **DocumentProcessorBase<TDataContext>**: ~47 processors inherit from this. Override `ProcessInternal` (NOT `ProcessAsync`). Constructor takes 5 params: logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refGenerator.
- **CQRS handlers**: API uses `[FromServices]` query/command handlers, not service classes (no `IContestService`, `IAiService`, etc.)
- **At-least-once delivery**: RabbitMQ/MassTransit; all consumers must be idempotent
- **SHA-256 document identity**: Full hash of normalized URL (scheme+host+path, lowercased, no query string), scoped per sport DB collection. See `HashProvider.GenerateHashFromUri`.
- **DLQ is intentional**: Documents land in dead-letter when dependencies aren't sourced yet due to Provider backlog. Manual replay via endpoint. NEVER purge.
- **Hangfire workers**: Default 20 per pod (NOT 50). Configurable via `{AppName}:BackgroundProcessor:MinWorkers`.
- **ESPN rate limiting**: Uses 403 (not 429) for IP-based rate limiting. Mitigated via `RequestDelayMs=1000ms` in `EspnApiClientConfig` and retry policy in `RetryPolicy.cs`.
- **Only ESPN is active**: CBS, Yahoo, SportsDataIO provider stubs exist but only ESPN is implemented.

## Workflow Conventions
- Branch protection on `main` — all changes require PRs
- CodeRabbit reviews PRs automatically
- Use `gh` CLI for GitHub operations (may need full path: `/c/Program Files/GitHub CLI/gh.exe`)
- Prefer concise communication, no emojis
- Commit messages should be descriptive for CodeRabbit

## Documentation
- `docs/` was audited and cleaned on 2026-03-08. See `memory/docs-audit.md` for details.
- When creating docs, verify claims against actual source code before writing.
- Do not create empty placeholder files.
