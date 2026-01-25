# Phase 2: Rate Limiting Implementation (Week 3)

[← Back to Overview](rabbitmq-migration-strategy-0-overview.md)

---

## Objectives
- Protect against ESPN rate limiting
- Enable safe scaling of Provider pods
- Distributed rate limiting across cluster

---

## 3.1 Deploy Redis to Cluster

**Redis is required** for distributed rate limiting.

```bash
# Deploy Redis using Bitnami Helm chart
helm install redis bitnami/redis \
  --namespace sportsdata \
  --set architecture=standalone \
  --set auth.enabled=true \
  --set auth.password=<SECURE_PASSWORD> \
  --set master.persistence.enabled=true \
  --set master.persistence.storageClass=smb \
  --set master.persistence.size=10Gi
```

**Resource requirements:**
- Memory: 1-2GB
- Storage: 10GB (persistent)
- CPU: 0.5-1 core

---

## 3.2 Implement Distributed Rate Limiter

**Goal:** All Provider pods coordinate to respect ESPN rate limits.

**Strategy:** Token bucket algorithm with Redis as shared state.

```csharp
// SportsData.Core/Infrastructure/RateLimiting/DistributedRateLimiter.cs
public class DistributedRateLimiter
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<DistributedRateLimiter> _logger;
    private readonly string _keyPrefix;
    private readonly int _tokensPerWindow;
    private readonly TimeSpan _windowDuration;
    
    public DistributedRateLimiter(
        IConnectionMultiplexer redis,
        ILogger<DistributedRateLimiter> logger,
        string keyPrefix = "ratelimit",
        int tokensPerWindow = 100,
        TimeSpan? windowDuration = null)
    {
        _redis = redis;
        _logger = logger;
        _keyPrefix = keyPrefix;
        _tokensPerWindow = tokensPerWindow;
        _windowDuration = windowDuration ?? TimeSpan.FromMinutes(1);
    }
    
    public async Task<bool> TryAcquireAsync(string resource, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var key = $"{_keyPrefix}:{resource}";
        
        // Decrement token count
        var remaining = await db.StringDecrementAsync(key);
        
        if (remaining < 0)
        {
            _logger.LogWarning("Rate limit exceeded for {Resource}", resource);
            return false;
        }
        
        // Set expiry on first use
        if (remaining == _tokensPerWindow - 1)
        {
            await db.KeyExpireAsync(key, _windowDuration);
        }
        
        return true;
    }
    
    public async Task WaitForTokenAsync(string resource, CancellationToken ct = default)
    {
        while (!await TryAcquireAsync(resource, ct))
        {
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
    }
}

// SportsData.Provider/Infrastructure/EspnHttpClient.cs
public class EspnHttpClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DistributedRateLimiter _rateLimiter;
    
    public async Task<HttpResponseMessage> GetAsync(string url, CancellationToken ct = default)
    {
        // Wait for rate limit token
        await _rateLimiter.WaitForTokenAsync("espn-api", ct);
        
        // Make request
        var client = _httpClientFactory.CreateClient("ESPN");
        return await client.GetAsync(url, ct);
    }
}
```

**Configuration:**

```json
// Azure App Config
"RateLimiting:ESPN:TokensPerMinute": "100",
"RateLimiting:ESPN:WindowDuration": "00:01:00"
```

**Start conservative, tune based on observed ESPN behavior:**
- Initial: 100 tokens per minute (cluster-wide)
- Monitor for 429 responses
- Increase if no throttling observed
- Decrease if throttling occurs

---

## 3.3 Reduce Provider Worker Count

**Current:** 50 Hangfire workers per Provider pod (too many for external API calls)

**Recommended:** 10-15 workers per Provider pod

```json
// Azure App Config
"SportsData.Provider:BackgroundProcessor:MinWorkers": "10"
```

**Math:**
- 4 Provider pods × 10 workers = 40 potential concurrent ESPN calls
- Rate limiter caps at 100/minute cluster-wide
- Allows burst capacity while respecting limits

---

## 3.4 Add Polly Resilience Policies

**Polly policies for ESPN HttpClient:**

```csharp
// SportsData.Provider/DependencyInjection/ServiceRegistration.cs
services.AddHttpClient("ESPN")
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

## 3.5 Testing Strategy

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
