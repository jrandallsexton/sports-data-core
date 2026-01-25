# Phase 4: Historical Sourcing Execution (Week 5+)

[← Back to Overview](rabbitmq-migration-strategy-0-overview.md)

---

## Data Volume & Timeline

**Estimated scope:**
- **120 teams** × **50 seasons** × **10 games/season** = **60,000 games**
- Plus: Rosters, stats, play-by-play, athlete images
- **Conservative estimate: 10-20 million Hangfire jobs**

**Estimated timeline:**
- Provider throughput: 100 ESPN calls/minute (rate limited)
- Producer throughput: 1,000 DB operations/minute (scaled to 15 pods)
- **Bottleneck: ESPN API calls**
- **Timeline: 2-4 weeks for full historical sourcing** (accuracy over speed)

---

## Execution Strategy

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

---

## Monitoring During Execution

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

---

## Graceful Handling of Issues

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

[Next: Rollback Plans & Success Criteria →](rabbitmq-migration-strategy-7-rollback-success.md)
