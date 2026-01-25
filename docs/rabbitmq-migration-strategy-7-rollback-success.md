# Rollback Plans & Success Criteria

[← Back to Overview](rabbitmq-migration-strategy-0-overview.md)

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
- ✅ Producer scales 1→15 pods based on queue depth
- ✅ Provider scales 1→4 pods (capped)
- ✅ Scaling up/down without job failures
- ✅ Cooldown periods prevent flapping

### Historical Sourcing
- ✅ All jobs complete successfully (>99% success rate)
- ✅ Data quality validation passes
- ✅ No ESPN rate limiting penalties
- ✅ Cluster remains stable throughout execution

---

[Next: Appendix →](rabbitmq-migration-strategy-8-appendix.md)
