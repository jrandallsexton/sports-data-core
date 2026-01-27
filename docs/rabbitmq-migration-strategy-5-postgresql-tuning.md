# PostgreSQL Performance Tuning for Historical Sourcing

[← Back to Overview](rabbitmq-migration-strategy-0-overview.md)

---

## Current State Assessment Needed

**Before historical sourcing, benchmark your PostgreSQL server:**

1. **Hardware verification:**
   - CPU: Ryzen 5 7640HS (6c/12t, Zen 4, 4.3-5.0 GHz) ✅
   - RAM: 32GB DDR5-5600 ✅
   - Disk: 1TB NVMe SSD ✅ (excellent for PostgreSQL)

2. **Current PostgreSQL config:**
   ```bash
   # SSH to PostgreSQL NUC, check settings
   psql -U postgres -c "SHOW max_connections;"
   psql -U postgres -c "SHOW shared_buffers;"
   psql -U postgres -c "SHOW work_mem;"
   psql -U postgres -c "SHOW maintenance_work_mem;"
   psql -U postgres -c "SHOW checkpoint_completion_target;"
   ```

---

## Recommended Tuning for Write-Heavy Workload

**For 32GB RAM server handling bulk writes:**

```ini
# /etc/postgresql/XX/main/postgresql.conf

# Memory settings
shared_buffers = 8GB              # 25% of RAM
work_mem = 64MB                   # Per connection sort/hash memory
maintenance_work_mem = 2GB        # For VACUUM, CREATE INDEX
effective_cache_size = 24GB       # 75% of RAM (hint for query planner)

# Connection settings
max_connections = 800             # Support scaled Producer + Provider pods
                                  # Producer: 12 pods × 50 workers = 600 connections
                                  # Provider: 12 pods × 50 workers = 600 connections
                                  # Total potential: 1200 (cap at 800 per database)
                                  # Note: Each service has separate Hangfire DB

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

---

## Connection Pooling: PgBouncer

**Problem:** 600+ direct PostgreSQL connections per service (12 pods × 50 workers) is expensive.

**⚠️ WARNING:** PgBouncer transaction pooling may interfere with Hangfire's distributed locking and long-running transactions. Test thoroughly before production use.

**Alternative:** Increase `max_connections` to 800-1000 and use Npgsql connection pooling (built-in to .NET client).

**If using PgBouncer** (advanced - requires Hangfire compatibility testing):

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

**Npgsql Connection Pooling (Recommended for Hangfire):**
```csharp
// In connection strings, limit pool size per pod
"Host=<POSTGRES_IP>;Port=5432;Database=sdProducer.FootballNcaa.Hangfire;Username=postgres;Password=***;Maximum Pool Size=50;Minimum Pool Size=10"

// With 12 pods × 50 max pool size = 600 connections (within 800 limit)
// Pools automatically reuse connections, reducing actual active connections
```

**PgBouncer Connection String (if tested and compatible):**
```
# Instead of: Host=<POSTGRES_IP>;Port=5432
# Use:        Host=pgbouncer.sportsdata.svc.cluster.local;Port=6432;Pooling=false  # Disable client pooling, PgBouncer handles it
```

**Benefit (PgBouncer):** 600 application connections → 50-100 actual PostgreSQL connections.
**Benefit (Npgsql Pooling):** Automatic connection reuse without additional infrastructure.

---

## Database Schema Optimizations

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

---

## Monitoring During Historical Sourcing

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

---

## Scaling Test Strategy

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

**Likely outcome:** With proper tuning (increased max_connections, Npgsql pooling, batched writes, indexed tables), PostgreSQL should handle **12 Producer + 12 Provider pods** (1200 potential connections across 2 Hangfire databases) without issue. The Ryzen 5 7640HS is a modern, capable CPU for this workload.

**KEDA Load Test Results (Jan 27, 2026):**
- ✅ KEDA scaling: 2 → 12 pods worked perfectly based on Hangfire queue depth
- ⚠️ PostgreSQL bottleneck: Hit `max_connections` limit with default settings (100-200)
- ✅ Hangfire retries: Gracefully handled connection exhaustion, jobs completed successfully
- 📊 Result: 50k jobs processed across Producer + Provider with autoscaling, identified PostgreSQL as next tuning target

**Confidence level:** HIGH - Your PostgreSQL server is well-specced. Focus on connection pooling and avoiding lock contention.

---

## Future Considerations

**If PostgreSQL becomes permanent bottleneck:**

1. **Read replicas:** Offload reporting/analytics queries
2. **Partitioning:** Partition large tables by season/year
3. **Upgrade hardware:** More RAM, NVMe SSD, faster CPU
4. **Managed service:** Azure Database for PostgreSQL (scales vertically/horizontally)
5. **Caching layer:** Redis cache for frequently read data

---

[Next: Phase 4 - Historical Sourcing Execution →](rabbitmq-migration-strategy-6-phase4-execution.md)
