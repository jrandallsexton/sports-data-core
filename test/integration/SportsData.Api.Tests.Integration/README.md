# SportsData.Api.Tests.Integration

End-to-end tests that boot the real `SportsData.Api` in-process via
`WebApplicationFactory<Program>` and drive it over HTTP against a
Testcontainers-managed Postgres. Tests exercise the real controller →
middleware → handler → DbContext path.

## How the fixture works

- **`Program.cs`** gates prod-only subsystems (Azure AppConfig, Firebase,
  Azure SignalR, MassTransit, Hangfire) on
  `Environment.IsEnvironment(Program.TestingEnvironmentName)` so the app can
  boot without external infrastructure.
- **`ApiIntegrationFixture`** (collection fixture) spins up one Postgres
  container per test run, builds the factory, applies EF migrations, and
  seeds a canonical test user keyed by `TestIdentity.FirebaseUid`.
- **`SportsDataApiFactory`** overrides the real `DbContext` to point at
  Testcontainers, replaces the message bus with `NoOpBus`, swaps the
  Producer-bound `IFranchiseClientFactory` for
  `FakeFranchiseClientFactory`, and injects `TestAuthHandler` so requests
  are authenticated as the seeded test user.
- **`ResetDatabaseAsync`** truncates `PickemGroup` with `CASCADE` between
  scenarios so each test starts from a clean slate (the seeded user
  survives).

## Writing a new test

1. Mark the class with `[Collection(nameof(ApiIntegrationCollection))]`
   and take `ApiIntegrationFixture` in the constructor.
2. In each test: call `await _fixture.ResetDatabaseAsync()` in the arrange
   phase, configure the `FakeFranchiseClientFactory.Client` mock as needed
   (or call `ResolveSlugsAsNewGuids()` for the common case), then use
   `_fixture.Factory.CreateClient()` to hit the API.
3. For DB assertions, grab a scoped `AppDataContext` via
   `_fixture.CreateScope()`.

## Running

```
dotnet test test/integration/SportsData.Api.Tests.Integration
```

Docker must be running. First test pays the Postgres container startup
cost (~5s); subsequent tests reuse the container.
