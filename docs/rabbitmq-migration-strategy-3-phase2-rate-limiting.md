# Phase 2: Rate Limiting Implementation ~~(Week 3)~~ ⚠️ PHASE SKIPPED

[← Back to Overview](rabbitmq-migration-strategy-0-overview.md)

---

> **Update (2026):** ESPN DOES use IP-based rate limiting via 403 responses (not 429). The actual mitigation is `RequestDelayMs = 1000ms` in `EspnApiClientConfig` and ESPN-specific 403 retry handling in `RetryPolicy.cs` (`SportsData.Core/Http/Policies/RetryPolicy.cs`). The Polly Bulkhead/CircuitBreaker policies described in sections 3.1-3.5 below were **proposed but never implemented**.

---

## ⚠️ PHASE 2 NOT REQUIRED - EMPIRICAL TESTING RESULTS

**Date:** January 25, 2026  
**Finding:** ESPN API has no meaningful rate limits

### Testing Methodology

Conducted empirical load testing using concurrent PowerShell workers to simulate production Provider pod behavior:

**Test 1: Burst Test (Single-threaded)**
- 500 requests over 127.96s
- Throughput: ~3.91 req/sec
- Result: 0 rate limit errors (429s)

**Test 2: Concurrent Test (Realistic)**
- 10 concurrent workers, 100 requests each = 1,000 total
- Duration: 27.4s
- Throughput: ~36.5 req/sec
- Result: 0 rate limit errors

**Test 3: Aggressive Peak Load**
- 50 concurrent workers, 100 requests each = 5,000 total
- Duration: 35.87s
- **Throughput: ~139.4 req/sec** 🎉
- Result: **0 rate limit errors, no response degradation**

### Conclusion

ESPN's public API appears to be an **abandoned/legacy service with no active rate limiting**:
- No 429 (Too Many Requests) responses detected
- No 503 (Service Unavailable) responses
- No Retry-After headers observed
- No X-RateLimit-* headers present
- Response times remained stable (250-300ms avg)

**Distributed rate limiting via Redis is unnecessary.**

---

## Updated Strategy: Simple Resilience with Polly

Instead of complex distributed rate limiting, use **Polly policies** for good HTTP client hygiene:

### Objectives (Revised)
- ✅ Prevent overwhelming ESPN with uncontrolled concurrency
- ✅ Handle transient failures gracefully
- ✅ Protect Provider from ESPN downtime
- ❌ ~~Distributed rate limiting~~ (not needed)

---

## Proposed Implementation: Polly Policies Only (Not Implemented)

> **Note:** The Polly policies below were **proposed but never implemented**. The actual rate-limit mitigation uses `RequestDelayMs = 1000ms` and ESPN-specific 403 retry handling in `RetryPolicy.cs`.

**No Redis, no distributed coordination needed.** Just good HTTP client practices.

### 3.1 HttpClient Configuration with Polly (Proposed)

```csharp
// SportsData.Core/DependencyInjection/ServiceRegistration.cs
services.AddHttpClient("ESPN", client =>
    {
        client.BaseAddress = new Uri("https://site.api.espn.com/");
    })
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        MaxConnectionsPerServer = 20  // Prevent connection exhaustion
    })
    // Policy 1: Bulkhead - Limit concurrent requests per pod
    .AddPolicyHandler(Policy
        .BulkheadAsync<HttpResponseMessage>(
            maxParallelization: 10,   // Max 10 concurrent ESPN calls per pod
            maxQueuingActions: 50     // Queue up to 50 waiting requests
        ))
    // Policy 2: Retry with exponential backoff
    .AddPolicyHandler((sp, request) =>
    {
        var logger = sp.GetRequiredService<ILogger<EspnHttpClient>>();
        
        return Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .OrResult<HttpResponseMessage>(r => 
                r.StatusCode == (HttpStatusCode)429 ||  // Rate limit (shouldn't happen)
                r.StatusCode == (HttpStatusCode)503 ||  // Service unavailable
                r.StatusCode >= (HttpStatusCode)500)    // Server errors
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: (retryAttempt, result, context) =>
                {
                    // Honor Retry-After header if present
                    if (result?.Result?.Headers?.RetryAfter?.Delta != null)
                        return result.Result.Headers.RetryAfter.Delta.Value;
                    
                    // Otherwise exponential backoff
                    return TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                },
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    logger.LogWarning(
                        "ESPN request retry {RetryCount} after {Delay}ms. Status: {StatusCode}",
                        retryCount, 
                        timespan.TotalMilliseconds,
                        outcome.Result?.StatusCode);
                });
    })
    // Policy 3: Circuit breaker - stop calling ESPN if it's down
    .AddPolicyHandler((sp, request) =>
    {
        var logger = sp.GetRequiredService<ILogger<EspnHttpClient>>();
        
        return Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => r.StatusCode >= (HttpStatusCode)500)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,  // Open after 5 failures
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (result, duration) =>
                {
                    logger.LogError(
                        "ESPN circuit breaker OPENED for {Duration}s after repeated failures",
                        duration.TotalSeconds);
                },
                onReset: () => logger.LogInformation("ESPN circuit breaker RESET"),
                onHalfOpen: () => logger.LogInformation("ESPN circuit breaker HALF-OPEN (testing)"));
    })
    // Policy 4: Timeout - don't wait forever
    .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(30)));
```

### 3.2 Monitoring for Rate Limits (Just in Case) (Proposed)

Add alerting if ESPN **ever** returns 429 (shouldn't happen based on testing):

```csharp
// Add this as the outermost policy
.AddPolicyHandler((sp, request) =>
{
    var logger = sp.GetRequiredService<ILogger<EspnHttpClient>>();
    
    return Policy
        .HandleResult<HttpResponseMessage>(r => r.StatusCode == (HttpStatusCode)429)
        .FallbackAsync(
            fallbackAction: async (response, context, ct) =>
            {
                logger.LogCritical(
                    "🚨 ESPN RATE LIMIT DETECTED! Status: 429, Retry-After: {RetryAfter}",
                    response.Result?.Headers?.RetryAfter?.Delta);
                
                // TODO: Send alert (email, Slack, etc.)
                // This should NEVER happen based on 139 req/sec testing
                
                return response.Result;
            },
            onFallbackAsync: (response, context) =>
            {
                // Log full request details for investigation
                return Task.CompletedTask;
            });
});
```

### 3.3 Configuration (Proposed)

```json
// Azure App Config (optional overrides)
"SportsData.Provider:EspnClient:BulkheadLimit": "10",        // Max concurrent per pod
"SportsData.Provider:EspnClient:TimeoutSeconds": "30",
"SportsData.Provider:EspnClient:CircuitBreakerThreshold": "5"
```

---

## ~~3.1 Deploy Redis to Cluster~~ (NOT NEEDED)

~~**Redis is required** for distributed rate limiting.~~

**UPDATE:** Redis deployment is **not required** for ESPN rate limiting. ESPN API testing demonstrated no meaningful rate limits exist.

If Redis is needed for **other use cases** (caching, session state, etc.), deploy separately. But it's not needed for this migration.

---

## ~~3.2 Implement Distributed Rate Limiter~~ (NOT NEEDED)

~~**Goal:** All Provider pods coordinate to respect ESPN rate limits.~~

~~**Strategy:** Token bucket algorithm with Redis as shared state.~~

**UPDATE:** Distributed rate limiting is unnecessary. ESPN API handled:
- 139.4 req/sec sustained load
- 5,000 requests in 35.87s
- 50 concurrent workers
- **Zero rate limit errors**

Simple per-pod Polly bulkhead (10 concurrent) is sufficient.

---

## ~~3.3 Reduce Provider Worker Count~~ (OPTIONAL)

~~**Current:** 50 Hangfire workers per Provider pod (too many for external API calls)~~

~~**Recommended:** 10-15 workers per Provider pod~~

**UPDATE:** Worker count defaults to 20 per pod. With `RequestDelayMs = 1000ms` throttling each request, workers are naturally paced. No configuration change needed.

**Math:**
- 5 Provider pods × 20 workers = 100 workers, each paced at ~1 req/sec by `RequestDelayMs`
- ESPN tested successfully at 139 req/sec
- Current architecture is well within safe limits

---

## Testing Scripts

Two PowerShell scripts were created to empirically test ESPN rate limits:

**`util/13_TestEspnRateLimit.ps1`** - Single-threaded burst test
```powershell
.\13_TestEspnRateLimit.ps1 -TotalRequests 500
```

**`util/14_TestEspnRateLimitConcurrent.ps1`** - Multi-worker concurrent test
```powershell
# Simulate production load
.\14_TestEspnRateLimitConcurrent.ps1 -ConcurrentWorkers 10 -RequestsPerWorker 100

# Aggressive peak test
.\14_TestEspnRateLimitConcurrent.ps1 -ConcurrentWorkers 50 -RequestsPerWorker 100
```

Results exported to CSV with full metrics for analysis.

---

## Success Criteria (Revised)

- ✅ ~~Redis deployed and accessible~~
- ✅ ~~Rate limiter implementation complete~~
- ⬜ Polly policies proposed for ESPN HttpClient (not implemented)
- ⬜ Bulkhead limits concurrent requests per pod (not implemented)
- ⬜ Circuit breaker protects from ESPN downtime (not implemented)
- ⬜ Retry policies handle transient failures (not implemented)
- ⬜ Timeout prevents hanging requests (not implemented)
- ⬜ Monitoring alerts if 429 ever occurs (not implemented)
- ✅ Provider pods can scale freely without rate limit coordination

**Timeline:** ~~Week 3~~ → **Completed same day as Phase 1** (January 25, 2026)

---

## Next Steps

**Phase 2 is effectively complete** with simplified Polly-based approach.

→ **[Proceed to Phase 3: KEDA Deployment](rabbitmq-migration-strategy-4-phase3-keda.md)**

---

## 3.4 Add Polly Resilience Policies (Proposed, Not Implemented)

**Polly policies for ESPN HttpClient:**

```csharp
// SportsData.Core/DependencyInjection/ServiceRegistration.cs
services.AddHttpClient("ESPN", client =>
    {
        client.BaseAddress = new Uri("https://site.api.espn.com/");
    })
    // Bulkhead: Limit concurrent requests
    .AddPolicyHandler(Policy.BulkheadAsync<HttpResponseMessage>(
        maxParallelization: 20,  // Max 20 concurrent across cluster
        maxQueuingActions: 200   // Queue up to 200 waiting requests
    ))
    // Retry on rate limit (429)
    .AddPolicyHandler(Policy
        .HandleResult<HttpResponseMessage>(r => 
            r.StatusCode == HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(
            retryCount: 5,
            sleepDurationProvider: retryAttempt => 
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                _logger.LogWarning(
                    "ESPN rate limited, retry {RetryCount} after {Delay}s",
                    retryCount, timespan.TotalSeconds);
            }))
    // Retry on transient errors
    .AddPolicyHandler(Policy
        .Handle<HttpRequestException>()
        .OrResult<HttpResponseMessage>(r => 
            (int)r.StatusCode >= 500)
        .WaitAndRetryAsync(3, retryAttempt => 
            TimeSpan.FromSeconds(retryAttempt)));
```

---

## 3.5 Testing Strategy (Proposed, Not Implemented)

**Test distributed rate limiter:**

1. Deploy Redis + rate limiter code
2. Scale Provider to 4 pods
3. Queue 1,000 jobs
4. Monitor:
   - Redis token consumption
   - ESPN 429 responses (should be zero or minimal)
   - Job completion time
   - Rate limiter backoff events

**Tune based on results:**
- If no 429s, increase tokens per minute
- If 429s occur, decrease tokens per minute
- Sweet spot: Maximum throughput without throttling

---

[Next: Phase 3 - KEDA Deployment →](rabbitmq-migration-strategy-4-phase3-keda.md)
