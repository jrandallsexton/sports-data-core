# Migration Runner Separation

**Status**: proposal, not started
**Author**: drafted 2026-06-20 after the MLB Worker `LOCK TABLE __EFMigrationsHistory` 30s-timeout incident
**Driver memory**: `project_migration_runner` (this doc is the codebase-side artifact for review)

## Problem

EF Core migrations run inside every pod's startup, with EF taking an
`ACCESS EXCLUSIVE` lock on `__EFMigrationsHistory` before checking what's
pending. With many pods coming up in parallel — KEDA-scaled Workers,
plus the Api / Ingest / Daemon roles, plus the per-sport multiplication
(NCAA + NFL + MLB Producers all hitting the same per-sport DB) — pods
race for that lock. On 2026-06-20 the symptom surfaced clearly: two
newly-scaled MLB Worker pods sat for 30s each on the LOCK statement and
emitted command timeouts to Seq, even though zero migrations were
pending. The losing pod gets stuck behind whichever pod EF Core happens
to schedule first, and if a stale `idle in transaction` session is
sitting on the table the loser waits up to PG's lock timeout.

Beyond the failure mode, the pattern taxes every startup:

- Each pod opens a DB connection just to verify "no migrations pending."
- Each pod takes + releases an exclusive lock against a hot table.
- Worker pods are on the critical path for queue draining, so their
  startup time is user-visible (queue depth grows while they boot).

The right boundary is: **migrations are a deploy-time concern, not a
runtime concern**. Pods should trust that the schema is current by the
time they boot.

## Current behavior

`src/SportsData.Producer/Program.cs:236-260`:

```csharp
using (var scope = app.Services.CreateScope())
{
    var appServices = scope.ServiceProvider;
    switch (mode)
    {
        case Sport.GolfPga:
            await appServices.GetRequiredService<GolfDataContext>().Database.MigrateAsync();
            break;
        case Sport.FootballNcaa:
        case Sport.FootballNfl:
            await appServices.GetRequiredService<FootballDataContext>().Database.MigrateAsync();
            break;
        case Sport.BaseballMlb:
            await appServices.GetRequiredService<BaseballDataContext>().Database.MigrateAsync();
            break;
    }
}
```

Producer has multiple roles per sport: `ProducerRole.Api`,
`ProducerRole.Ingest`, `ProducerRole.Worker` (replicated, KEDA-scaled),
`ProducerRole.Daemon`. Every one of them runs `MigrateAsync` against
the same per-sport DB. Per-sport pod count varies but a typical state is:

| Sport | Api | Ingest | Worker (KEDA min/max) | Daemon | Total racers |
|---|---|---|---|---|---|
| NCAA | 1 | 1 | 2-5 | 1 | 5-8 |
| NFL  | 1 | 1 | 2-5 | 1 | 5-8 |
| MLB  | 1 | 1 | 2-5 | 1 | 5-8 |

API and Provider services have the same pattern against their own DBs.

## Proposed design

**Separate console application per service that performs migrations,
nothing else.** Pods stop calling `MigrateAsync` entirely and instead
verify (cheaply) that the schema is at the expected revision.

### `SportsData.Producer.Migrator` (new project)

- Type: `Microsoft.NET.Sdk` console app (not `Microsoft.NET.Sdk.Web`)
- References: `SportsData.Producer` (to reach the `DbContext` types) and
  `SportsData.Core` (for config + DateTimeProvider, which migrations
  occasionally touch via seed data).
- CLI: `dotnet SportsData.Producer.Migrator.dll -context BaseballDataContext`
  (or `-context FootballDataContext`, etc.).
- Reads its connection string the same way `SportsData.Producer` does
  (Azure App Config → `CommonConfig:SqlBaseConnectionString`).
- Runs `MigrateAsync` exactly once, logs the applied migrations, exits
  0 on success, non-zero on failure.
- No Hangfire, no MassTransit, no HTTP, no SignalR — just enough DI to
  spin up the DbContext.

Same shape for `SportsData.Api.Migrator` and `SportsData.Provider.Migrator`.
Each is small (probably <100 lines of `Main`).

### Pod startup change

Replace `MigrateAsync` with a one-row read that asserts the schema is
at the expected revision:

```csharp
var latestApplied = await context.Database
    .GetAppliedMigrationsAsync();
var expected = context.Database
    .GetMigrations()
    .Last();

if (!latestApplied.Contains(expected))
{
    throw new InvalidOperationException(
        $"Schema is behind expected revision {expected}. Migrations Job must run before pods.");
}
```

This is a single `SELECT MigrationId FROM __EFMigrationsHistory ORDER BY
MigrationId DESC LIMIT 1` (EF's `GetAppliedMigrationsAsync` is a bit
chattier, but still cheap and uses no locks). No `ACCESS EXCLUSIVE`,
no race window.

If the assert fails the pod crashes loud — which is what we want, since
it means the deploy ran out of order and pods would otherwise serve
traffic against a stale schema.

### Deploy choreography

Kubernetes Job per service, gated *before* the Deployment image rollout:

```yaml
# producer-mlb-migrator-job.yaml — generated per-deploy
apiVersion: batch/v1
kind: Job
metadata:
  name: producer-baseball-mlb-migrator-{{ revision }}
spec:
  backoffLimit: 2
  ttlSecondsAfterFinished: 3600
  template:
    spec:
      restartPolicy: Never
      containers:
      - name: migrator
        image: sportsdata/producer-migrator:{{ tag }}
        args: ["-context", "BaseballDataContext"]
```

CI sequence:

1. Build + push migrator image alongside the service image.
2. `kubectl apply` the Job manifest.
3. `kubectl wait --for=condition=complete --timeout=300s job/<name>`.
4. On success: `kubectl apply` the Deployment manifest (image swap).
5. On failure: abort deploy, alert, surface logs.

For the existing self-hosted Azure Pipelines path (`Bender`), this is
two extra steps in the Producer / Api / Provider templates. For the
GitHub Actions mobile path, n/a (no DB).

The Job runs as a single pod, so there is exactly one migrator at a
time per service+context. The lock contention disappears at the source.

## Failure modes

| Failure | Behavior | Recovery |
|---|---|---|
| Migration throws (FK violation, etc.) | Job exits non-zero, `backoffLimit: 2` retries twice, then deploy stage fails | Fix the migration, redeploy. Pods never roll, no half-migrated state. |
| Migration takes longer than the Job timeout (300s) | Job is killed, deploy fails | Bump timeout for known-large migrations; default fine for column renames + indexes. |
| Pod schema-assert fails post-deploy | Pod crashes at startup, K8s restarts it; eventually CrashLoopBackOff | Indicates Job didn't actually run / deploy sequence broken. Page on this — it's an out-of-order deploy bug. |
| Multiple deploys queued simultaneously | Second deploy's Job waits for first to complete (Kubernetes serializes by name) | Standard CI queueing. |
| Need to roll back code without rolling back schema | Deploy old image; old code's schema-assert sees a *newer* migration than it expects | Decide: relax the assert to "at least my expected revision" so old code can run against new schema, OR require explicit down-migration. Default: allow newer (forward-compatible schema is the project standard). |

## Rollout plan

Three phases, each safe to ship independently:

### Phase 1 — Build the migrator, keep startup migrations

- New project `SportsData.Producer.Migrator` (+ Api + Provider variants).
- New Dockerfile / image build in CI.
- Add the Job manifest to `sports-data-config` under
  `app/base/jobs/<service>/migrator-job.yaml.template` (rendered per-deploy).
- **Do not** remove `MigrateAsync` from pod startup yet — run both.
- The Job becomes a no-op if it runs first (no pending migrations by the
  time pods start), or it correctly applies migrations and pods then
  see "no work to do."

This is risk-free: the worst case is the Job runs successfully and pods
*also* run `MigrateAsync` finding nothing to do.

### Phase 2 — Cut over via flag

- Add `-skip-startup-migrations` arg to each service.
- CI deploys flip it to true once the Job step is wired in and proven.
- Pods now boot with the schema-assert path instead of `MigrateAsync`.
- The lock-contention symptom disappears here.

Can be done per-service (Api first, then Provider, then Producer) so
the riskiest one (Producer, the most pods) is last.

### Phase 3 — Remove the dead code

- Once all services run on the migrator + assert path in prod, delete
  the `MigrateAsync` call sites and the `-skip-startup-migrations` flag.
- Single follow-up PR; pure deletion.

## Out of scope

- **Expand/contract migration patterns** (the textbook zero-downtime
  column rename CodeRabbit asked about on PR #434). Orthogonal — this
  proposal addresses *when* migrations run, not *how* they're structured.
- **Cross-service migration ordering** (Api migrating before Producer
  on a coordinated change). Each service's migrator is independent; if
  cross-service ordering becomes a concern, it's solved at the
  deploy-pipeline level, not in the migrator.
- **Schema drift detection** (catching cases where someone changes a
  model without adding a migration). Out of scope here, but the
  schema-assert in Phase 2 actually catches a subset of it for free —
  the pod won't start if its expected revision is missing.

## Why this is worth doing soon

- Eliminates the 30s `LOCK TABLE` timeout class of incident outright.
- Removes the racing-pods debugging story from oncall.
- Trims a measurable chunk off Worker pod startup latency (no migration
  check on the critical path for queue draining).
- Makes the next migration-touching feature PR safer — no deploy-day
  anxiety about lock windows.

Best slotted in *before* the next migration-touching feature lands, so
that feature ships on the cleaner path.
