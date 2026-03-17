# Phase 2: ESPN Rate Limiting — Redis Token Bucket & Circuit Breaker

[← Back to Overview](rabbitmq-migration-strategy-0-overview.md)

---

## Background

ESPN uses IP-based rate limiting via **403 responses** (not 429). When hit, there's no `Retry-After` header — the IP is simply blocked for an unknown cooldown period. With KEDA scaling Provider pods by Hangfire queue depth, the ESPN request rate scales linearly with pod count if each worker independently sleeps before calling.

The solution decouples pod scaling from ESPN request rate using two Redis-backed controls:

1. **Token Bucket Rate Limiter** — centralized request pacing across all pods
2. **Circuit Breaker** — stops all ESPN calls cluster-wide when a 403 is detected

## Architecture

```text
Hangfire Worker starts
  → Mongo cache check
    → [HIT: done — no ESPN call, no token needed]
    → [MISS: need live data]
      → Circuit breaker check (fast, Redis key lookup)
        → [OPEN: return RateLimited immediately]
        → [CLOSED: proceed]
          → Token bucket acquire (may block briefly)
          → ESPN HTTP call
          → [403: trip circuit breaker]
```

Jobs that hit the Mongo document cache never contend for a token. Only actual ESPN HTTP calls require one.

## Token Bucket Rate Limiter

### How It Works

A Lua script runs atomically in Redis to implement a token bucket:

1. Calculate tokens to add based on elapsed time since last refill
2. Cap at max burst size
3. If a token is available, consume it and return success
4. If not, caller polls every 100ms until a token appears or max wait is exceeded

### Redis Key

`espn:ratelimit:bucket` — hash with `tokens` and `last_refill` fields. TTL is set as a safety net to prevent orphaned keys.

### Configuration

| Setting | Default | Purpose |
|---------|---------|---------|
| `RateLimitMaxTokens` | 2 | Burst capacity (small to prevent stampedes) |
| `RateLimitTokensPerSecond` | 1.0 | Refill rate — matches the prior 1000ms per-request delay |
| `RateLimitMaxWaitMs` | 30000 | Max block time before fail-open |

All settings are in `EspnApiClientConfig`, configurable via Azure App Config.

### Fail-Open Behavior

- **Redis unavailable**: Returns `true` (allow request). Logs warning.
- **Max wait exceeded**: Returns `true` (allow request). Logs warning.
- **Cancellation token fired**: Propagates `OperationCanceledException`.

The rate limiter never blocks indefinitely and never causes a job to fail due to Redis issues.

## Circuit Breaker

### How It Works

When any Provider pod receives a 403 from ESPN:

1. The circuit breaker writes a Redis key with a TTL equal to the cooldown period
2. All pods check this key before making ESPN calls
3. While the key exists, all ESPN calls return `RateLimited` immediately — no HTTP request is made
4. Redis automatically expires the key after cooldown, closing the circuit

### Redis Key

`espn:circuit:open` — string value containing the ISO-8601 datetime when the circuit will close.

### Configuration

| Setting | Default | Purpose |
|---------|---------|---------|
| `CircuitBreakerCooldownSeconds` | 300 | How long to pause after a 403 (5 minutes) |

### Fail-Open Behavior

- **Redis unavailable on read**: Circuit treated as closed (allow requests). Logs error.
- **Redis unavailable on write (trip)**: Trip attempt is lost; subsequent 403s will retry the trip. Logs error.

### Logging

- **Critical**: Logged once when circuit transitions from closed to open (not on every 403 while already open)
- **Format**: `ESPN circuit breaker TRIPPED. Reason: {reason}. All ESPN API calls paused until {openUntil} ({cooldownSeconds}s cooldown)`

## Integration in EspnHttpClient

Both controls are checked in `FetchLiveAsync` and `GetCachedImageStreamAsync`:

```csharp
// 1. Check circuit first (prevents unnecessary token consumption)
if (await _circuitBreaker.IsOpenAsync())
    return RateLimited failure

// 2. Acquire rate limiter token (blocks until available or timeout)
await _rateLimiter.AcquireAsync(ct)

// 3. Make ESPN HTTP call
var response = await _httpClient.GetAsync(requestUri, ct)

// 4. Trip circuit on 403
if (response.StatusCode == HttpStatusCode.Forbidden)
    await _circuitBreaker.TripAsync($"ESPN returned 403 for {uri}")
```

## NoOp Defaults

Services other than Provider (API, Producer, etc.) use NoOp implementations registered by `AddClients()`:

- `NoOpEspnRateLimiter` — always returns `true` immediately
- `NoOpEspnCircuitBreaker` — circuit always closed, trip is a no-op

Provider overrides these with Redis-backed implementations only when `CommonConfig:CacheServiceUri` is configured:

```csharp
// Provider Program.cs
if (!string.IsNullOrWhiteSpace(config[CommonConfigKeys.CacheServiceUri]))
{
    services.AddSingleton<IEspnCircuitBreaker, RedisEspnCircuitBreaker>();
    services.AddSingleton<IEspnRateLimiter, RedisEspnRateLimiter>();
}
```

This means Provider runs without Redis locally (falls back to NoOp), and Redis-backed controls activate automatically in the cluster.

## Redis Infrastructure

- **Image**: `redis:7-alpine`, single replica, no persistence
- **K8s service**: `cache-svc:6379` (ClusterIP, default namespace)
- **Config key**: `CommonConfig:CacheServiceUri` = `cache-svc:6379`
- **Manifests**: `sports-data-config/app/base/caching/`

## Observability

- **Grafana**: OTel counters track ESPN cache hits vs API calls (Provider pods emit metrics)
- **Seq**: Structured logs for rate limiter waits, circuit breaker trips, and fail-open events

## Tuning

The defaults (1 token/sec, burst of 2, 5-minute cooldown) are conservative — a drop-in replacement for the old per-worker `Task.Delay(1000ms)`. To increase throughput:

- Increase `RateLimitTokensPerSecond` (e.g., 2.0 for 2 req/sec)
- Increase `RateLimitMaxTokens` (e.g., 5 for larger bursts after idle)
- Decrease `CircuitBreakerCooldownSeconds` if ESPN's actual block window is shorter than 5 minutes

Monitor the Grafana dashboard and Seq logs under load to find the right balance.

---

[Next: Phase 3 — KEDA Deployment →](rabbitmq-migration-strategy-4-phase3-keda.md)
