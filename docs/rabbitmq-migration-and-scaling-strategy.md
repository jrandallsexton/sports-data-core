# RabbitMQ Migration & Historical Sourcing Scaling Strategy

**Date:** January 24, 2026  
**Status:** Planning Phase  
**Goal:** Migrate from Azure Service Bus to in-cluster RabbitMQ and implement auto-scaling for historical sourcing runs

---

## Executive Summary

We need to migrate messaging infrastructure from Azure Service Bus to self-hosted RabbitMQ **before** executing historical sourcing runs. Historical sourcing will process **tens of millions of events** (120+ teams, 50+ years of NCAA Football data, including play-by-play) and would trigger unsustainable Azure Service Bus costs.

**Key Challenges:**
1. Azure Service Bus Standard tier will throttle under historical sourcing load
2. Azure Service Bus Premium tier costs $677/month (unacceptable for a 1-2 week job)
3. Auto-scaling Provider pods risks ESPN rate limiting (undocumented limits)
4. Need to scale Producer aggressively (database work) while Provider stays conservative

**Solution:** 5-week phased approach:
- Weeks 1-2: RabbitMQ migration and validation
- Week 3: Rate limiting implementation
- Week 4: KEDA deployment and testing
- Week 5+: Historical sourcing execution

---

## Current Architecture

### Messaging Infrastructure
- **Azure Service Bus Standard Tier**
- MassTransit as abstraction layer
- Message flow: Provider → ASB → Producer → ASB → (other consumers)
- Current volume: ~500-1,000 messages/week (weekly sourcing)

### Cluster Configuration
- **5× identical NUCs:**
  - **CPU:** AMD Ryzen 5 7640HS (6 cores / 12 threads, Zen 4, 4.3-5.0 GHz)
  - **RAM:** 32GB DDR5-5600 (2×16GB)
  - **Storage:** 1TB NVMe SSD
- **Allocation:**
  - 4 NUCs: Kubernetes cluster nodes
  - 1 NUC: Dedicated PostgreSQL server
- **Deployment:** Flux GitOps
- **Storage:** SMB CSI driver
- **No Redis currently deployed**
- **Assessment:** High-end homelab setup - modern CPUs, fast memory, NVMe storage across the board

### PostgreSQL Server
- **Hardware:** Dedicated NUC (1 of 5 identical units)
  - **CPU:** Ryzen 5 7640HS (6 cores / 12 threads, Zen 4, 4.3-5.0 GHz)
  - **RAM:** 32GB DDR5-5600 (2×16GB)
  - **Storage:** 1TB NVMe SSD
- **Workload:** ALL canonical data writes from Producer
- **Databases:** Producer.FootballNcaa, Provider.FootballNcaa, Hangfire DBs per service
- **Assessment:** Dedicated high-performance node for database, no resource contention from K8s workloads
- **Expected capacity:** Should handle 12-15 Producer pods with proper tuning (PgBouncer + optimized settings)

### Application Configuration
**Provider (SportsData.Provider)**
- Hangfire workers: 50 per pod
- Current replicas: 1 (manual)
- Workload: I/O-bound (ESPN API calls)

**Producer (SportsData.Producer)**
- Hangfire workers: 50 per pod
- Current replicas: 1 (manual)
- Workload: CPU/DB-bound (data processing, PostgreSQL writes)

**API (SportsData.Api)**
- Hangfire workers: 30 per pod
- Current replicas: 1 (manual)
- Workload: HTTP request handling, HATEOAS response generation

---

## Problem Statement: Historical Sourcing Scale

### Data Volume Estimates

**Scope:**
- 120+ NCAA Football teams
- 50+ years of historical data
- ~10 games per team per season
- Play-by-play data for every game
- Athlete rosters, stats, images

**Message Volume:**
- Conservative: **10 million+ messages**
- Each message triggers:
  - Provider Hangfire job (ESPN fetch)
  - MassTransit event publish
  - Producer Hangfire job (data processing)
  - Additional events (canonical data published)

**Azure Service Bus Impact:**
- Each message = 5+ ASB operations (send, receive, complete, etc.)
- 10M messages × 5 operations = **50M operations**
- Standard tier: 12.5M operations/month included, then throttled
- **Result:** Forced to Premium tier ($677/month) or sourcing takes months

### PostgreSQL Bottleneck Concern

**Problem:** Producer scales to 15 pods (450 concurrent workers), all writing to **one 32GB Ryzen PostgreSQL server**.

**Risk:** Database becomes bottleneck before ESPN does:
- 15 Producer pods × 30 workers = 450 concurrent DB operations
- Each job: Parse JSON → Validate → Transform → INSERT/UPDATE canonical data
- Heavy transaction load during historical sourcing
- Potential for connection pool exhaustion, lock contention, I/O saturation

**Unknown variables:**
- Connection pool limits (max_connections setting - likely default 100)
- Lock contention on high-write tables
- Index overhead during bulk inserts
- Current PostgreSQL version and configuration

**Good news:** 
- **CPU:** Ryzen 5 7640HS (6c/12t, Zen 4) is excellent for database transactions
- **RAM:** 32GB DDR5-5600 is fast and sufficient for shared_buffers + cache
- **Storage:** 1TB NVMe SSD means disk I/O should handle heavy write load easily

**Mitigation strategies needed:**
1. PostgreSQL performance tuning (connection pooling, WAL settings, autovacuum)
2. Monitor database metrics during scaling tests
3. Consider batching writes in Producer (bulk INSERT instead of row-by-row)
4. May need to cap Producer scaling lower than 15 if DB can't handle load
5. Connection pooling at application layer (PgBouncer?)

### ESPN Rate Limiting Concern

**Problem:** ESPN API has **zero documentation** on rate limits.

**Risk:** Scaling Provider from 1 pod to 10+ pods:
- 1 pod × 50 workers = 50 concurrent ESPN calls (current)
- 10 pods × 50 workers = 500 concurrent ESPN calls (scaled)
- **Likely result:** Rate limited, blocked, or banned

**Unknown variables:**
- Requests per second limit
- Requests per minute limit
- IP-based vs API key-based limiting
- Burst allowances
- Penalty severity

---

## Phase 1: RabbitMQ Migration (Weeks 1-2)

### Objectives
- Eliminate Azure Service Bus dependency
- Zero ongoing messaging costs
- Lower latency (in-cluster pod-to-pod communication)
- Full control over messaging infrastructure

### Week 1: Deploy & Configure

**1.1 Deploy RabbitMQ Cluster**

Create 3-node RabbitMQ cluster for high availability:

```yaml
# app/base/rabbitmq/namespace.yaml
apiVersion: v1
kind: Namespace
metadata:
  name: rabbitmq-system
```

**Deployment options:**
- Option A: Bitnami Helm Chart (recommended for ease)
- Option B: RabbitMQ Cluster Operator (production-grade)
- Option C: Custom StatefulSet (maximum control)

**Recommended: Bitnami Helm Chart**

```bash
# Add Bitnami repo
helm repo add bitnami https://charts.bitnami.com/bitnami

# Install RabbitMQ cluster
helm install rabbitmq bitnami/rabbitmq \
  --namespace rabbitmq-system \
  --set replicaCount=3 \
  --set persistence.enabled=true \
  --set persistence.storageClass=smb \
  --set persistence.size=50Gi \
  --set auth.username=sportsdata \
  --set auth.password=<SECURE_PASSWORD> \
  --set metrics.enabled=true \
  --set metrics.serviceMonitor.enabled=true \
  --set clustering.enabled=true
```

**Storage requirements:**
- 3 PVCs via SMB CSI driver
- 50GB per node (150GB total)
- Persistent message storage

**Resource allocation per node:**
- Memory: 2-4GB
- CPU: 1-2 cores
- Adjust based on NUC capacity

**1.2 Configure MassTransit for Dual Transport**

Update application configs to support **both** ASB and RabbitMQ during migration:

```csharp
// SportsData.Core/DependencyInjection/ServiceRegistration.cs
public static IServiceCollection AddMassTransit(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var useRabbitMq = configuration.GetValue<bool>("Messaging:UseRabbitMq");
    
    services.AddMassTransit(x =>
    {
        x.AddConsumers(Assembly.GetExecutingAssembly());
        
        if (useRabbitMq)
        {
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host("rabbitmq.rabbitmq-system.svc.cluster.local", "/", h =>
                {
                    h.Username(configuration["Messaging:RabbitMq:Username"]);
                    h.Password(configuration["Messaging:RabbitMq:Password"]);
                });
                
                cfg.ConfigureEndpoints(context);
            });
        }
        else
        {
            x.UsingAzureServiceBus((context, cfg) =>
            {
                cfg.Host(configuration["Messaging:AzureServiceBus:ConnectionString"]);
                cfg.ConfigureEndpoints(context);
            });
        }
    });
    
    return services;
}
```

**Configuration in Azure App Config:**

```
Messaging:UseRabbitMq = false  (initially, flip to true during cutover)
Messaging:RabbitMq:Username = sportsdata
Messaging:RabbitMq:Password = <SECURE_PASSWORD>
Messaging:RabbitMq:Host = rabbitmq.rabbitmq-system.svc.cluster.local
```

**1.3 Deploy Monitoring**

RabbitMQ Prometheus exporter + Grafana dashboard:

```yaml
# Enable metrics in Helm values
metrics:
  enabled: true
  serviceMonitor:
    enabled: true
    namespace: monitoring
```

Import RabbitMQ Grafana dashboard: https://grafana.com/grafana/dashboards/10991

**Metrics to monitor:**
- Queue depth
- Message rate (publish/deliver)
- Consumer count
- Memory usage
- Disk usage
- Connection count

### Week 2: Parallel Run & Validation

**2.1 Enable RabbitMQ for Non-Critical Services First**

Start with lower-risk services:
1. Enable RabbitMQ for API (lowest risk, not part of sourcing)
2. Monitor for 2-3 days
3. Enable for Producer
4. Monitor for 2-3 days
5. Enable for Provider (highest risk)

**2.2 Run Weekly Sourcing on RabbitMQ**

Execute 2-3 weekly sourcing runs entirely on RabbitMQ:
- Validate message delivery
- Validate retry behavior
- Validate error handling
- Compare performance to ASB baseline

**Success criteria:**
- Zero message loss
- Equal or better latency
- No RabbitMQ cluster issues
- Hangfire jobs complete successfully

**2.3 Cutover & Decommission ASB**

Once confident:
1. Set `Messaging:UseRabbitMq = true` for all services
2. Restart all pods
3. Monitor for 1 week
4. Cancel Azure Service Bus resources (save ~$10-50/month)

---

## Phase 2: Rate Limiting Implementation (Week 3)

### Objectives
- Protect against ESPN rate limiting
- Enable safe scaling of Provider pods
- Distributed rate limiting across cluster

### 3.1 Deploy Redis to Cluster

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

### 3.2 Implement Distributed Rate Limiter

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

### 3.3 Reduce Provider Worker Count

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

### 3.4 Add Polly Resilience Policies

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

### 3.5 Testing Strategy

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

## Phase 3: KEDA Deployment (Week 4)

### Objectives
- Auto-scale Producer based on Hangfire queue depth
- Conservatively scale Provider (respect ESPN limits)
- **Monitor PostgreSQL performance under load**
- Validate scaling behavior before historical sourcing

### 4.1 Install KEDA

```bash
# Install KEDA using Helm
helm repo add kedacore https://kedacore.github.io/charts
helm install keda kedacore/keda \
  --namespace keda \
  --create-namespace \
  --set prometheus.metricServer.enabled=true \
  --set prometheus.operator.enabled=true
```

**Verify installation:**

```bash
kubectl get pods -n keda
# Should see: keda-operator, keda-metrics-apiserver
```

### 4.2 Create ScaledObject for Producer


⚠️ **WARNING:** Max replica count may need to be tuned based on PostgreSQL server capacity. Start conservative, increase if DB can handle load.
**Producer scales aggressively** (database work, no external API limits):

```yaml
# clusters/home/sportsdata-producer-scaledobject.yaml
apiVersion: keda.sh/v1alpha1
kind: ScaledObject
metadata:
  name: sportsdata-producer-scaler
  namespace: sportsdata
spec:
  scaleTargetRef:
    name: sportsdata-producer
  pollingInterval: 15  # Check every 15 seconds
  cooldownPeriod: 300  # AGGRESSIVE - May need to reduce if PostgreSQL saturates
  minReplicaCount: 1   # Baseline capacity (current production state)
  maxReplicaCount: 15  # Historical sourcing may need high scale
  triggers:
    - type: postgresql
      metadata:
        # Query Hangfire database for pending jobs
        query: >-
          SELECT COUNT(*)::int
          FROM hangfire.jobqueue
          WHERE fetchedat IS NULL
            AND queue = 'default'
        targetQueryValue: "10"  # Scale up if >10 jobs per pod
        activationTargetQueryValue: "1"  # Scale from 0 if any jobs
        # Connection string from pod environment variable
        connectionFromEnv: HANGFIRE_CONNECTION_STRING
```

**Scaling behavior:**
- 10 pending jobs → 1 pod (baseline)
- 20 pending jobs → 2 pods (10 jobs/pod)
- 100 pending jobs → 10 pods
- 150+ pending jobs → 15 pods (maxed out)
- 0 pending jobs → cooldown → scale to 1 pod

⚠️ **PostgreSQL will be the bottleneck, not Kubernetes resources.**  
If PostgreSQL saturates at 5-8 pods, reduce `maxReplicaCount` accordingly.

### 4.3 Create ScaledObject for Provider

**Provider scales conservatively** (ESPN rate limits):

```yaml
# clusters/home/sportsdata-provider-scaledobject.yaml
apiVersion: keda.sh/v1alpha1
kind: ScaledObject
metadata:
  name: sportsdata-provider-scaler
  namespace: sportsdata
spec:
  scaleTargetRef:
    name: sportsdata-provider
  pollingInterval: 15
  cooldownPeriod: 300
  minReplicaCount: 1  # Current production state
  maxReplicaCount: 4  # CAPPED to respect ESPN rate limits
  triggers:
    - type: postgresql
      metadata:
        query: >-
          SELECT COUNT(*)::int
          FROM hangfire.jobqueue
          WHERE fetchedat IS NULL
            AND queue = 'default'
        targetQueryValue: "50"  # Conservative threshold
        activationTargetQueryValue: "1"
        connectionFromEnv: HANGFIRE_CONNECTION_STRING
```

**Scaling behavior:**
- 50 pending jobs → 1 pod (baseline)
- 100 pending jobs → 2 pods (50 jobs/pod)
- 200 pending jobs → 4 pods (maxed out, will queue)
- Rate limiter + reduced workers prevent ESPN throttling

**Key difference from Producer:**
- `maxReplicaCount: 4` (vs 15 for Producer)
- `targetQueryValue: 50` (vs 10 for Producer)
- Result: Provider scales slower, stays smaller

### 4.4 Configure Hangfire Connection String

KEDA needs access to Hangfire PostgreSQL database:

```yaml
# deployment.yaml for Producer/Provider
env:
  - name: HANGFIRE_CONNECTION_STRING
    valueFrom:
      secretKeyRef:
        name: hangfire-connection
        key: connection-string
```

Create secret:

```bash
kubectl create secret generic hangfire-connection \
  -n sportsdata \
  --from-literal=connection-string="Host=<POSTGRES_HOST>;Database=sdProducer.FootballNcaa.Hangfire;Username=<USER>;Password=<PASS>"
```

### 4.5 Testing KEDA Scaling

**Synthetic load test:**

1. Queue 500 jobs in Hangfire
2. Watch KEDA scale up:
   ```bash
   kubectl get scaledobject -n sportsdata -w
   kubectl get pods -n sportsdata -w
   ```
3. Validate:
   - Producer scales to ~50 pods (500 jobs ÷ 10)
   - Provider scales to 4 pods max (capped)
   - Jobs process successfully
   - Rate limiter prevents ESPN throttling
4. Wait for queue to drain
5. Watch KEDA scale down after cooldown

****Critical:** Monitor PostgreSQL during test - if CPU/I/O saturates, reduce Producer `maxReplicaCount`
- Adjust `pollingInterval` for responsiveness

### 4.6 PostgreSQL Performance Monitoring

**Critical metrics to watch during scaling test:**

```bash
# Monitor PostgreSQL connections
SELECT count(*) FROM pg_stat_activity;

# Monitor table locks
SELECT relation::regclass, mode, granted 
FROM pg_locks 
WHERE NOT granted;

# Monitor transaction rate
SELECT xact_commit + xact_rollback AS tps 
FROM pg_stat_database 
WHERE datname = 'sdProducer.FootballNcaa';
```

**Add to Grafana:**
- PostgreSQL connections (should not hit max_connections)
- Transaction rate (writes/second)
- Disk I/O (IOPS, throughput)
- CPU usage
- Lock contention
- Query execution time (slow queries)

**Warning thresholds:**
- Connections > 80% of max_connections
- CPU > 80% sustained
- Disk I/O > 80% of capacity
- Lock wait time > 100ms
- Slow query log showing bottlenecks

**If PostgreSQL saturates:**
1. Reduce Producer `maxReplicaCount` (e.g., 15 → 8 → 5)
2. Tune PostgreSQL settings (see section below)
3. Consider PgBouncer for connection pooling
4. Batch writes in Producer code (bulk INSERT)
- Adjust `targetQueryValue` for faster/slower scaling
- Adjust `maxReplicaCount` based on observed performance
- Adjust `pollingInterval` for responsiveness

---

## PostgreSQL Performance Tuning for Historical Sourcing

### Current State Assessment Needed

**Before historical sourcing, benchmark your PostgreSQL server:**
 (critical for transaction processing)
   - RAM: 32GB confirmed ✅
   - Disk: 1TB NVMe SSD confirmed ✅ (excellent for PostgreSQL
   - RAM: 32GB confirmed
   - Disk: SSD type? (NVMe vs SATA makes huge difference)
   - Network: 1Gbps? 10Gbps?

2. **Current PostgreSQL config:**
   ```bash
   # SSH to PostgreSQL NUC, check settings
   psql -U postgres -c "SHOW max_connections;"
   psql -U postgres -c "SHOW shared_buffers;"
   psql -U postgres -c "SHOW work_mem;"
   psql -U postgres -c "SHOW maintenance_work_mem;"
   psql -U postgres -c "SHOW checkpoint_completion_target;"
   ```

### Recommended Tuning for Write-Heavy Workload

**For 32GB RAM server handling bulk writes:**

```ini
# /etc/postgresql/XX/main/postgresql.conf

# Memory settings
shared_buffers = 8GB              # 25% of RAM
work_mem = 64MB                   # Per connection sort/hash memory
maintenance_work_mem = 2GB        # For VACUUM, CREATE INDEX
effective_cache_size = 24GB       # 75% of RAM (hint for query planner)

# Connection settings
max_connections = 500             # Support scaled Producer pods
                                  # 15 pods × 30 workers = 450 connections max

# WAL (Write-Ahead Log) settings - Critical for write performance
wal_buffers = 16MB
checkpoint_completion_target = 0.9  # Spread checkpoint I/O over time
max_wal_size = 4GB                  # Allow larger WAL for bulk writes
min_wal_size = 1GB

# Autovacuum tuning - Prevent table bloat during bulk inserts
autovacuum_max_workers = 4
autovacuum_naptime = 10s            # Check more frequently
autovacuum_vacuum_scale_factor = 0.05  # Vacuum sooner

# Query planning
random_page_cost = 1.1              # For NVMe SSD (optimized for random reads)
effective_io_concurrency = 300      # For NVMe SSD (higher than SATA)

# Logging - Monitor slow queries
log_min_duration_statement = 1000   # Log queries > 1 second
log_line_prefix = '%t [%p]: [%l-1] user=%u,db=%d,app=%a,client=%h '
```

**Apply settings:**
```bash
sudo systemctl restart postgresql
```

### Connection Pooling: PgBouncer

**Problem:** 450 direct PostgreSQL connections (15 pods × 30 workers) is expensive.

**Solution:** Deploy PgBouncer in the cluster to multiplex connections:

```yaml
# Deploy PgBouncer as sidecar or standalone service
# Reduces actual PostgreSQL connections to ~50-100
# Applications connect to PgBouncer, which pools to PostgreSQL

apiVersion: v1
kind: ConfigMap
metadata:
  name: pgbouncer-config
  namespace: sportsdata
data:
  pgbouncer.ini: |
    [databases]
    producer_db = host=<POSTGRES_IP> port=5432 dbname=sdProducer.FootballNcaa
    hangfire_db = host=<POSTGRES_IP> port=5432 dbname=sdProducer.FootballNcaa.Hangfire
    
    [pgbouncer]
    listen_port = 6432
    listen_addr = *
    auth_type = md5
    pool_mode = transaction        # Transaction-level pooling
    max_client_conn = 1000          # From all Producer pods
    default_pool_size = 50          # Actual PostgreSQL connections
    reserve_pool_size = 25
    reserve_pool_timeout = 3
```

**Connection string update:**
```
# Instead of: Host=<POSTGRES_IP>;Port=5432
# Use:        Host=pgbouncer.sportsdata.svc.cluster.local;Port=6432
```

**Benefit:** 450 application connections → 50-75 actual PostgreSQL connections.

### Database Schema Optimizations

**Check indexes on hot tables:**

```sql
-- Find tables with most writes during sourcing
SELECT schemaname, tablename, n_tup_ins, n_tup_upd 
FROM pg_stat_user_tables 
ORDER BY (n_tup_ins + n_tup_upd) DESC 
LIMIT 20;

-- For each hot table, ensure you have:
-- 1. Primary key index (automatic)
-- 2. Foreign key indexes (NOT automatic in PostgreSQL!)
-- 3. Indexes on frequent WHERE clause columns

-- Example: If you query by FranchiseId frequently
CREATE INDEX CONCURRENTLY idx_teams_franchise_id 
ON teams(franchise_id);
```

**Bulk insert optimization:**

```csharp
// In Producer code, batch inserts instead of row-by-row
// BAD (slow):
foreach (var team in teams)
{
    await dbContext.Teams.AddAsync(team);
    await dbContext.SaveChangesAsync();  // 1000 round-trips
}

// GOOD (fast):
await dbContext.Teams.AddRangeAsync(teams);  // Batch insert
await dbContext.SaveChangesAsync();          // 1 round-trip
```

### Monitoring During Historical Sourcing

**Set up alerts for:**

1. **Connection exhaustion:**
   ```sql
   SELECT count(*) FROM pg_stat_activity;
   -- Alert if > 80% of max_connections
   ```

2. **Slow queries:**
   ```sql
   SELECT pid, now() - query_start AS duration, query 
   FROM pg_stat_activity 
   WHERE state = 'active' 
   AND now() - query_start > interval '10 seconds';
   ```

3. **Lock contention:**
   ```sql
   SELECT relation::regclass, mode, granted, pid 
   FROM pg_locks 
   WHERE NOT granted;
   ```

4. **Disk I/O:**
   ```bash
   # On PostgreSQL NUC
   iostat -x 5  # Check %util and await
   ```

### Scaling Test Strategy

**Don't scale Producer to 15 immediately. Gradual ramp:**

1. **Baseline:** 1 Producer pod, measure PostgreSQL metrics
2. **Scale to 3:** Monitor CPU, I/O, connections, slow queries
3. **Scale to 5:** Check for degradation
4. **Scale to 8:** Likely upper limit before PostgreSQL saturates
5. **Scale to 10+:** Only if PostgreSQL shows capacity

**Expected bottleneck order:**
1. **Lock contention** (high-write tables with concurrent access) - Most likely limiter
2. **Connection management** (if not using PgBouncer) - 450 connections is a lot
3. **CPU** (transaction processing) - 6 cores @ 4.3-5.0 GHz should handle it well
4. ~~Disk I/O~~ - NVMe easily handles this
5. ~~RAM~~ - 32GB DDR5-5600 is more than sufficient

**Likely outcome:** With proper tuning (PgBouncer, batched writes, indexed tables), PostgreSQL should handle **12-15 Producer pods** without issue. The Ryzen 5 7640HS is a modern, capable CPU for this workload.

**Confidence level:** HIGH - Your PostgreSQL server is well-specced. Focus on connection pooling and avoiding lock contention.

### Future Considerations

**If PostgreSQL becomes permanent bottleneck:**

1. **Read replicas:** Offload reporting/analytics queries
2. **Partitioning:** Partition large tables by season/year
3. **Upgrade hardware:** More RAM, NVMe SSD, faster CPU
4. **Managed service:** Azure Database for PostgreSQL (scales vertically/horizontally)
5. **Caching layer:** Redis cache for frequently read data

---

## Phase 4: Historical Sourcing Execution (Week 5+)

### Data Volume & Timeline

**Estimated scope:**
- **120 teams** × **50 seasons** × **10 games/season** = **60,000 games**
- Plus: Rosters, stats, play-by-play, athlete images
- **Conservative estimate: 10-20 million Hangfire jobs**

**Estimated timeline:**
- Provider throughput: 100 ESPN calls/minute (rate limited)
- Producer throughput: 1,000 DB operations/minute (scaled to 15 pods)
- **Bottleneck: ESPN API calls**
- **Timeline: 2-4 weeks for full historical sourcing** (accuracy over speed)

### Execution Strategy

**Phased approach by season:**

1. Start with oldest season (1970s) - lowest data density
2. Monitor for 24 hours:
   - KEDA scaling behavior
   - ESPN rate limiting
   - RabbitMQ queue depth
   - Hangfire job success rate
3. Tune as needed
4. Continue decade by decade
5. More recent seasons have more data (play-by-play) - will take longer

**Monitoring during execution:**

**Critical metrics:**
1. **Hangfire queue depth** (should drain steadily)
2. **ESPN 429 responses** (should be zero or near-zero)
3. **KEDA replica counts** (should scale up/down appropriately)
4. **RabbitMQ queue depth** (should not back up)
5. **PostgreSQL write throughput** (disk I/O on separate NUC)
6. **Error rates** (data quality issues, ESPN failures)

**Alerting thresholds:**
- Hangfire queue depth > 100,000 for > 1 hour
- ESPN 429 rate > 1% of requests
- RabbitMQ memory > 80%
- PostgreSQL disk > 80%
- Error rate > 5%

### Graceful Handling of Issues

**If ESPN rate limits occur:**
1. KEDA will not scale down (jobs still queued)
2. Polly retry policies back off
3. Rate limiter queues requests
4. Jobs complete slower but eventually succeed

**If RabbitMQ has issues:**
1. MassTransit will retry message delivery
2. Hangfire jobs will fail and retry
3. KEDA continues to scale based on queue depth
4. Can manually restart RabbitMQ pods if needed

**If cluster resources maxed out:**
1. KEDA respects `maxReplicaCount` limits
2. Jobs queue in Hangfire until capacity available
3. Can temporarily stop other workloads to free resources
4. Can pause sourcing, scale manually, resume

---

## Rollback Plan

**If migration fails, can roll back at each phase:**

### During RabbitMQ Migration (Weeks 1-2)
**Rollback:** Set `Messaging:UseRabbitMq = false`, restart pods, back on ASB

### During Rate Limiting Implementation (Week 3)
**Rollback:** Remove rate limiter code, redeploy previous version

### During KEDA Deployment (Week 4)
**Rollback:** Delete ScaledObjects, manually scale deployments

### During Historical Sourcing (Week 5+)
**Rollback:** Pause Hangfire jobs, can resume later, no data loss

---

## Success Criteria

### RabbitMQ Migration
- ✅ Zero message loss during cutover
- ✅ Equal or better latency vs ASB
- ✅ RabbitMQ cluster stable for 1 week
- ✅ Azure Service Bus resources decommissioned

### Rate Limiting
- ✅ ESPN 429 responses < 0.1% of requests
- ✅ Distributed rate limiter working across all Provider pods
- ✅ No manual intervention needed during high load

### KEDA Scaling
- ✅ Producer scales 2→15 pods based on queue depth
- ✅ Provider scales 2→4 pods (capped)
- ✅ Scaling up/down without job failures
- ✅ Cooldown periods prevent flapping

### Historical Sourcing
- ✅ All jobs complete successfully (>99% success rate)
- ✅ Data quality validation passes
- ✅ No ESPN rate limiting penalties
- ✅ Cluster remains stable throughout execution

---

## Open Questions & Decisions Needed

### RabbitMQ Configuration
- [ ] **Decision:** Bitnami Helm chart vs RabbitMQ Operator vs custom StatefulSet?
  - **Recommendation:** Bitnami Helm chart (simplest, battle-tested)
- [ ] **Decision:** RabbitMQ version (3.13 latest stable)?
- [ ] **Decision:** Quorum queues vs classic queues?
  - **Recommendation:** Quorum queues (better HA guarantees)

### Redis Configuration
- [ ] **Decision:** Redis standalone vs Redis Cluster vs Redis Sentinel?
  - **Recommendation:** Standalone for now (simplest, rate limiting is not critical path)
- [ ] **Decision:** Redis persistence strategy (RDB snapshots vs AOF)?
  - **Recommendation:** RDB snapshots (rate limiter state can be recreated)

### ESPN Rate Limiting
- [ ] **Question:** Can we instrument to measure actual ESPN rate limits?
  - Log all ESPN requests with timestamps
  - Analyze to find safe throughput ceiling
- [ ] **Decision:** Conservative starting point for rate limiter?
  - **Recommendation:** 100 requests/minute, tune up based on observation

### Historical Sourcing Scope
- [ ] **Question:** How many seasons of data does ESPN actually have?
  - Need to query ESPN API to determine availability
- [ ] **Question:** Should we prioritize recent seasons (more complete data)?
- [ ] **Question:** Should we run all sports at once or sequentially?
  - **Recommendation:** NCAA Football first (validate approach), then expand

### Resource Allocation
- [ ] **Decision:** How to balance cluster resources during historical sourcing?
  - Temporarily reduce API replicas (not critical)?
  - Run during off-peak hours (if applicable)?
- [ ] **Decision:** Do we need to upgrade NUC RAM/storage for historical run?

---

## Cost Analysis

### Azure Service Bus (Current)
- **Standard tier:** ~$10-20/month (current weekly sourcing)
- **Premium tier:** $677/month (required for historical sourcing)
- **Estimated cost for historical sourcing:** $677 (1 month minimum)

### RabbitMQ + Redis (Proposed)
- **Infrastructure cost:** $0 (existing NUC hardware)
- **Operational cost:** Time investment in setup/monitoring
- **Ongoing cost:** $0/month

### Savings
- **Immediate:** $10-20/month (eliminate ASB Standard)
- **Historical sourcing:** $677 (avoid Premium tier)
- **Long-term:** $120-240/year (ongoing Standard tier)
- **Break-even:** Immediate (no additional hardware needed)

---

## Timeline Summary

| Week | Phase | Activities | Success Criteria |
|------|-------|------------|------------------|
| 1 (Jan 27-31) | RabbitMQ Deploy | Install RMQ cluster, configure MassTransit, enable monitoring | RMQ cluster running, metrics visible |
| 2 (Feb 3-7) | RMQ Validation | Parallel run, weekly sourcing on RMQ, cutover | Zero message loss, stable for 7 days |
| 3 (Feb 10-14) | Rate Limiting | Deploy Redis, implement rate limiter, reduce Provider workers | No ESPN 429s under load |
| 4 (Feb 17-21) | KEDA Deploy | Install KEDA, create ScaledObjects, synthetic load test | Scaling works correctly |
| 5+ (Feb 24+) | Historical Sourcing | Execute historical sourcing, monitor, tune | All data sourced successfully |

**Total time to historical sourcing:** ~5 weeks  
**Alternative (pay for ASB Premium):** Immediate, but $677 cost

---

## Next Steps

### Immediate (This Week)
1. ✅ Document strategy (this file)
2. ⬜ Review and approve approach
3. ⬜ Decide on RabbitMQ deployment method (Helm chart recommended)
4. ⬜ Size Redis resource requirements
5. ⬜ Prepare monitoring dashboards (Grafana)

### Week 1 Prep
1. ⬜ Create RabbitMQ Helm values file
2. ⬜ Create Kubernetes secrets for RMQ/Redis credentials
3. ⬜ Update MassTransit code for dual-transport support
4. ⬜ Add Prometheus ServiceMonitor for RabbitMQ
5. ⬜ Import RabbitMQ Grafana dashboard

### Ongoing
1. ⬜ Monitor Azure Service Bus costs (establish baseline)
2. ⬜ Research ESPN API rate limits (instrument requests)
3. ⬜ Validate historical data availability (query ESPN)
4. ⬜ Plan data quality validation strategy

---

## References & Resources

### Documentation
- [KEDA Documentation](https://keda.sh/docs/)
- [KEDA PostgreSQL Scaler](https://keda.sh/docs/latest/scalers/postgresql/)
- [RabbitMQ Documentation](https://www.rabbitmq.com/docs)
- [MassTransit RabbitMQ Transport](https://masstransit.io/documentation/transports/rabbitmq)

### Helm Charts
- [Bitnami RabbitMQ](https://github.com/bitnami/charts/tree/main/bitnami/rabbitmq)
- [Bitnami Redis](https://github.com/bitnami/charts/tree/main/bitnami/redis)
- [KEDA Helm Chart](https://github.com/kedacore/charts)

### Monitoring
- [RabbitMQ Grafana Dashboard 10991](https://grafana.com/grafana/dashboards/10991)
- [KEDA Grafana Dashboard 19802](https://grafana.com/grafana/dashboards/19802)

### Code Examples
- Token bucket rate limiting: https://github.com/aspnet/AspNetCore/tree/main/src/Middleware/RateLimiting
- Polly resilience: https://github.com/App-vNext/Polly

---

**Document Owner:** SportsData Platform Team  
**Last Updated:** January 24, 2026  
**Next Review:** After Phase 1 completion (Week 2)
