# Phase 3: KEDA Deployment (Week 4)

[← Back to Overview](rabbitmq-migration-strategy-0-overview.md)

---

## Objectives
- Auto-scale Producer based on Hangfire queue depth
- Conservatively scale Provider (respect ESPN limits)
- **Monitor PostgreSQL performance under load**
- Validate scaling behavior before historical sourcing

---

## 4.1 Install KEDA

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

**What is KEDA?**
- **Not native Kubernetes** - Created by Microsoft/Red Hat in 2019, CNCF graduated 2023
- **Extends native HPA** - Adds event-driven scaling based on external metrics (queue depth, DB rows, etc.)
- **How it works:** KEDA Operator watches ScaledObjects → KEDA Metrics Server queries external sources → Native HPA consumes metrics and scales pods
- **Why powerful:** Native HPA only scales on CPU/memory; KEDA scales on RabbitMQ queues, PostgreSQL rows, Kafka lag, HTTP requests, etc.

---

## 4.2 Create ScaledObject for Producer

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
  cooldownPeriod: 300  # Wait 5 min before scaling down
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

**Key KEDA Parameters Explained:**

- **`pollingInterval`**: How often KEDA queries PostgreSQL for pending job count (15 seconds = responsive)
- **`cooldownPeriod`**: Wait time before scaling down after queue is empty (300s = 5 min prevents flapping)
- **`minReplicaCount`**: Minimum pods running at all times (1 = always have baseline capacity)
- **`maxReplicaCount`**: Maximum pods KEDA can scale to (15 = cap to protect PostgreSQL)
- **`targetQueryValue`**: Desired jobs per pod (10 = if 100 jobs queued, scale to 10 pods)
- **`activationTargetQueryValue`**: Threshold to scale from 0 to min (1 = any pending job triggers scaling)

**Scaling behavior:**
- 10 pending jobs → 1 pod (baseline)
- 20 pending jobs → 2 pods (10 jobs/pod)
- 100 pending jobs → 10 pods
- 150+ pending jobs → 15 pods (maxed out)
- 0 pending jobs → wait 5 min → scale to 1 pod

⚠️ **PostgreSQL will be the bottleneck, not Kubernetes resources.**  
If PostgreSQL saturates at 5-8 pods, reduce `maxReplicaCount` accordingly.

---

## 4.3 Create ScaledObject for Provider

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

---

## 4.4 Configure Hangfire Connection String

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

---

## 4.5 Testing KEDA Scaling

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

**Critical:** Monitor PostgreSQL during test - if CPU/I/O saturates, reduce Producer `maxReplicaCount`

**Tuning knobs:**
- Adjust `pollingInterval` for responsiveness
- Adjust `targetQueryValue` for faster/slower scaling
- Adjust `maxReplicaCount` based on observed performance

---

## 4.6 PostgreSQL Performance Monitoring

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
2. Tune PostgreSQL settings (see [PostgreSQL Performance Tuning](rabbitmq-migration-strategy-5-postgresql-tuning.md))
3. Consider PgBouncer for connection pooling
4. Batch writes in Producer code (bulk INSERT)

---

[Next: PostgreSQL Performance Tuning →](rabbitmq-migration-strategy-5-postgresql-tuning.md)
