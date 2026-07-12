# PostgreSQL connection exhaustion — design & fix

Status: **proposed** (awaiting authorization to implement)
Date: 2026-07-12
Owner: platform

## Problem

PostgreSQL (`sdprod-data-0`, 192.168.0.250) throws `FATAL: sorry, too many
clients already` (SQLSTATE **53300**) during MLB live-game bursts, even though a
`pg_stat_activity` snapshot shows only ~365 connections (5 active). The snapshot
falls *between* sub-minute spikes — Seq shows **132 failures in a single minute**.

### Evidence (Seq, 2026-07-12 ~14:18–14:30 UTC)

Two distinct failure modes, both on the `BaseballMlb` pipeline:

1. **Server-side 53300 — the actual error (dominant).**
   `SportsData.Producer`, role **`Ingest`**. 177/200 hits are
   `Hangfire.BackgroundJobClientException → Npgsql.PostgresException 53300` — the
   `DocumentCreated` consumers **enqueuing Hangfire jobs** during the document
   fan-out, each opening a PG connection to Hangfire storage. The failures throw
   MassTransit receive-faults; at-least-once **redelivery retries them**,
   amplifying the connection storm.

2. **Client-side pool exhaustion — standing bug (secondary).**
   `Producer`/`Provider`, role **`Worker`**:
   `The connection pool has been exhausted … Max Pool Size (currently 22)`.
   Hangfire runs **30 workers/pod** against a **22-slot pool** → guaranteed to
   starve under load, independent of the 500 cap.

Ruled out: EF migration locks (`__EFMigrationsHistory`, `LOCK TABLE`) — zero
hits; and deploy/startup — no rollout in the window. The trigger is live-game
document volume.

## Root cause

**The aggregate client-side Npgsql pool capacity can exceed `max_connections=500`.**
A connection string can't override the server cap; it sets the *client* pool
(`Max Pool Size`, default **100**). Summed across services × pods × Hangfire
storage pools, the transient total blows past 500.

Pool sizing is **asymmetric** — per-role sizing (`ResolvePoolSize()` from
AppConfig, clamped at `MaxAllowedPoolSize=50` in
`Core/DependencyInjection/ServiceRegistration.cs`) was applied to some services
but not others:

| Service | Data pool | Hangfire pool | Notes |
|---|---|---|---|
| Provider | per-role (All 60→50, Worker 22, Api 5, Ingest 5) | per-role | configured |
| Producer | per-role (All 60→50, Worker 22, Api 5, Ingest 5, Daemon 10) | per-role | configured |
| Api | 20 | 30 | configured |
| **Contest** | **100 (default)** | — | **unguarded** |
| **Franchise** | **100 (default)** | — | **unguarded** |
| **Notification** | **100 (default)** | **100 (default)** | **unguarded — 200/pod** |
| **Player** | **100 (default)** | — | **unguarded** |
| **Season** | **100 (default)** | — | **unguarded** |
| **Venue** | **100 (default)** | — | **unguarded** |
| JobsDashboard | — | 6 × 5 (read-only) | configured |

The six unguarded services alone can demand **~700** connections at one pod each
— more than the entire cap — before Producer's spikes are even counted.

## Capacity reality (this is NOT a hardware limit)

Live settings on the 24 GB / 6-vCPU VM:
`max_connections=500`, `shared_buffers=6GB` (25%), `work_mem=80MB`,
`effective_cache_size=18GB` (75%). Correctly tuned for 24 GB.

- PostgreSQL is process-per-connection, but an **idle** backend is cheap
  (~5–10 MB). 800 idle backends ≈ ~7 GB.
- Budget: 6 GB shared_buffers + ~2–3 GB overhead → ~15 GB for backends +
  work_mem. 800 backends (~7 GB) leaves ~8 GB → headroom for **~100 concurrent
  work_mem-heavy operations**. We run **~5 active**. ~20× margin.
- CPU: 6 vCPUs want only ~12–24 *active* connections for throughput; the rest
  should be idle-pooled. At 3% CPU we're nowhere near it.

**Conclusion:** 24 GB comfortably supports ~800 connections for this workload.
`500` is an arbitrary cap. The fix is the client-pool demand side; raising the
cap is cheap insurance, not a requirement. **32 GB is optional headroom, not a
fix** (the config is already correct for 24 GB).

Caveat: `work_mem=80MB` is the one variable that could bite if `max_connections`
is raised high *and* active concurrency ever spikes with real sorts. Optional
mitigation below.

## Fixes

### 1. Clamp the six unguarded services (biggest win)

Apply the existing `ResolvePoolSize()` pattern (as Provider/Producer/Api do) in
each service's `Program.cs` so the data (and Hangfire) pools are explicit
instead of the Npgsql default 100. These are low-traffic, typically single-pod
services.

Target sizes:

| Service | Data pool | Hangfire pool |
|---|---|---|
| Contest, Franchise, Player, Season, Venue | 10 | — |
| Notification | 15 | 15 |

Files: `src/SportsData.{Contest,Franchise,Notification,Player,Season,Venue}/Program.cs`,
using `ResolvePoolSize()` from `Core/DependencyInjection/ServiceRegistration.cs`.
Sizes overridable via Azure AppConfig (same keys the configured services use).

### 2. Reconcile Worker pool with Hangfire worker count (kills mode 2)

The `Worker` role pool is **22** but Hangfire runs **30 workers/pod**. Either:
- raise the Worker data pool to **≥ 32** (worker count + small headroom), or
- lower `{AppName}:BackgroundProcessor:MinWorkers` to **≤ 20** for Producer/Provider Worker.

Prefer **lowering workers** — 30 concurrent DB-bound workers per pod against a
shared 500-cap is the deeper problem; fewer workers × more pods (KEDA) scales
better and keeps per-pod connection cost bounded. Pick a worker count and set
the pool = workers + ~2.

### 3. Tame the Producer `Ingest` Hangfire-enqueue burst (the 53300 trigger)

The `DocumentCreated` fan-out enqueues Hangfire jobs faster than the Ingest
pool (5) can supply connections, and redelivery amplifies it. Options (pick one,
smallest first):
- Batch/`BatchEnqueue` the Hangfire job creation so N documents share fewer
  enqueue connections, or
- cap the MassTransit consumer concurrency for `document-created-handler` on the
  MLB broker so fan-out can't open connections unboundedly.

This is the only fix that needs live-pipeline validation; sequence it last.

### 4. Raise `max_connections` 500 → 700 (cheap insurance) — ✅ DONE 2026-07-12

Memory-safe on the current 24 GB (see budget). Gives spike margin while 1–3 roll
out. **`max_connections` is a `postmaster`-context setting — it cannot be changed
at runtime; a full restart is required** (a reload / `pg_reload_conf()` is NOT
enough). Also: `ALTER SYSTEM` does not itself reload the config, so
`pending_restart` stays `false` until you run `SELECT pg_reload_conf();` — but the
restart is what actually applies the value regardless of that flag.

Run on the VM as a superuser (e.g. `postgres`):

```sql
-- 1. Stage the new value (writes to postgresql.auto.conf; not yet live).
ALTER SYSTEM SET max_connections = 700;

-- 2. Confirm it's staged and pending a restart.
SELECT name, setting, pending_restart
FROM pg_settings
WHERE name = 'max_connections';
-- Expect: setting=500, pending_restart=true
```

Then restart PostgreSQL **on the VM** (not via SQL). A restart briefly drops all
connections; Npgsql pools reconnect automatically. Do it in a quiet window.

```sh
# systemd (adjust unit name to your install; Debian/Ubuntu often:
#   postgresql@<major>-main):
sudo systemctl restart postgresql
```

Verify after restart:

```sql
SHOW max_connections;   -- expect 700
```

To roll back: `ALTER SYSTEM SET max_connections = 500;` (or
`ALTER SYSTEM RESET max_connections;`) then restart again.

Note: raising `max_connections` slightly increases shared memory for lock/proc
tables — negligible at 700; no `shared_buffers` change needed.

### 5. (Optional) trim `work_mem` 80 → 64 MB for margin

Only if we later push `max_connections` toward 800. Unlike `max_connections`,
`work_mem` is reload-able (**no restart**):

```sql
ALTER SYSTEM SET work_mem = '64MB';
SELECT pg_reload_conf();   -- takes effect for new statements
```

### 6. (Observability) set `Application Name` in connection strings

`Application Name` is currently unset, so `pg_stat_activity.application_name` is
blank and incident attribution falls back to `client_addr` (pod IP). Add
`Application Name=sd{Service}.{Role}` where the connection string is built
(`Core/DependencyInjection/ServiceRegistration.cs`). Small, high-value for the
next incident. Can ship independently.

## Connection budget (real numbers)

Confirmed from code + AppConfig manifest + Flux config:

**Per-pod cost = 2 × role pool size** — every pod opens a **data** pool
(`AddDataPersistence(...maxPoolSize)`) *and* a **Hangfire** pool
(`AddHangfire(...maxPoolSize)`), each capped at the role's size (`Include Error
Detail` DBs: `sd{svc}.{sport}` + `sd{svc}.{sport}.Hangfire`). No `ConnectionPool`
keys exist in AppConfig, so the **code defaults apply**:

| Role (Producer/Provider) | Pool size | Per-pod (data+HF) |
|---|---|---|
| Worker | 22 | **44** |
| Daemon (Producer) | 10 | 20 |
| Ingest | 5 | 10 |
| Api | 5 | 10 |

Hangfire worker counts (AppConfig PROD `BackgroundProcessor:MinWorkers`):
**Api=30, Producer=25, Provider=25.**

Topology (Flux `sports-data-config`): per **sport × role** deployments.
`*-worker` deployments are KEDA-scaled on `hangfire.jobqueue` depth; MLB Producer
worker **max 6** (already reduced from 15 for this exact issue), NCAA/NFL
worker max 4; Provider worker max 4 per sport. `*-ingest`/`*-api` are static 1–2.

Leaf/other services (default pool 100, `Min Pool Size 0` so this is a **ceiling**,
not steady-state):

| Service | Pools × ceiling | Per pod |
|---|---|---|
| Api | data 20 + HF 30 | 50 |
| Notification | data 100 + HF 100 | **200** |
| Contest/Franchise/Player/Season/Venue | data 100 | **100** each |
| JobsDashboard | 6 × 5 | 30 |

### Two findings the numbers expose

**1. `Worker` pool 22 < 25 workers → guaranteed mode-2 starvation** (matches the
Seq "job parameter … pool exhausted (22)"). Fix: set `MinWorkers ≤ 20` **and/or**
Worker pool ≥ worker count.

**2. Worker pods are the structural multiplier.** At full KEDA scale the worker
*ceiling* is Producer (6+4+4) + Provider (4+4+4) = **26 worker pods × 44 = 1,144**
— and even two sports scaling concurrently on a fall weekend is ~800, over the
700 cap. The leaf services add a **620-connection unguarded ceiling** on top
(5×100 + Notification 200). Idle they hold little, but a traffic burst lets each
race toward its ceiling — which is how the transient 53300 spikes happen.

### Target after fixes

| Change | Ceiling reclaimed |
|---|---|
| Clamp Contest/Franchise/Player/Season/Venue 100 → 12 | −440 |
| Clamp Notification 100/100 → 15/15 | −170 |
| Worker: `MinWorkers` 25 → 18, pool 22 → 20 (per-pod 44 → 40) | −4/pod × worker pods |

Immediate (MLB-only now): clamps + worker fix keep the realistic ceiling well
under 500. **Fall multi-sport is the real constraint** — 2–3 sports of workers
scaling at once still approaches/exceeds 700 even at 40/pod. That is the case
for **PgBouncer** (deferred below): it collapses ~1,000+ client connections to
50–100 server-side and removes the pod-count multiplier entirely. Recommend:
ship the immediate fixes now; stand up PgBouncer before NCAA/NFL kickoff (Sept).

> Budget rule: Σ (peak_pods × 2 × pool_size) over all (service, role) < 450 for
> ~50 headroom under 500, or < 650 under 700.

## Rollout order

1. **#4 `max_connections` → 700** — immediate margin, one restart, reversible.
2. **#1 clamp the six leaf services** — cheap, removes the largest unguarded
   surface. One PR, no infra change.
3. **#2 Worker pool vs worker count** — reconcile per Producer/Provider Worker.
4. **#6 Application Name** — observability, ship anytime.
5. **#3 Ingest enqueue burst** — the real 53300 trigger; validate on the live
   MLB pipeline. Sequence last so the safer changes are already in.
6. Re-run the budget against confirmed pod counts; remove the 700 margin only if
   the demand side proves it's unnecessary.

## Validation

- After each change: watch Seq for `53300` and `pool has been exhausted` during
  the next MLB window; both should trend to zero.
- `pg_stat_activity` grouped by `client_addr` + `datname` (the `sd*.Hangfire`
  DBs are the tell) during a spike — peak total should stay well under 500.
- Add an alert: active connections > 80% of `max_connections`.

## Deferred / not now

- **PgBouncer** (`5-postgresql-tuning.md` §Connection Pooling) — biggest lever
  (240 → 50–100 real connections) but needs Hangfire distributed-lock
  compatibility testing. Revisit if the above doesn't hold under NFL/NCAA load.
- **32 GB RAM bump** — optional headroom / match the NUC; not required for this
  problem.

## References

- Seq investigation (this incident): server-53300 burst + Worker pool-exhaustion,
  2026-07-12 14:18–14:30 UTC.
- `docs/infrastructure/messaging/rabbitmq-migration/5-postgresql-tuning.md` — the
  32 GB tuning profile + PgBouncer notes + Jan-27 load-test connection-exhaustion.
- `Core/DependencyInjection/ServiceRegistration.cs` — `ResolvePoolSize()`,
  `MaxAllowedPoolSize`, connection-string construction.
- `[[project_connection_pool_per_role]]` (memory) — this is the deferred per-role
  pool-sizing work, now scoped.
