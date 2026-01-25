# Problem Statement: Historical Sourcing Scale

[← Back to Overview](rabbitmq-migration-strategy-0-overview.md)

---

## Data Volume Estimates

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

---

## PostgreSQL Bottleneck Concern

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

See [PostgreSQL Performance Tuning](rabbitmq-migration-strategy-5-postgresql-tuning.md) for detailed strategies.

---

## ESPN Rate Limiting Concern

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

**Mitigation:** See [Phase 2: Rate Limiting](rabbitmq-migration-strategy-3-phase2-rate-limiting.md) for implementation strategy.

---

[Next: Phase 1 - RabbitMQ Migration →](rabbitmq-migration-strategy-2-phase1-rabbitmq.md)
