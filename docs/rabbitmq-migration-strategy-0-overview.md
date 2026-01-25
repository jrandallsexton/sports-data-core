# RabbitMQ Migration & Historical Sourcing Scaling Strategy

**Date:** January 24, 2026  
**Status:** Planning Phase  
**Goal:** Migrate from Azure Service Bus to in-cluster RabbitMQ and implement auto-scaling for historical sourcing runs

---

## Navigation

- **[Overview](rabbitmq-migration-strategy-0-overview.md)** ← You are here
- [Problem Statement](rabbitmq-migration-strategy-1-problem-statement.md)
- [Phase 1: RabbitMQ Migration](rabbitmq-migration-strategy-2-phase1-rabbitmq.md)
- [Phase 2: Rate Limiting](rabbitmq-migration-strategy-3-phase2-rate-limiting.md)
- [Phase 3: KEDA Deployment](rabbitmq-migration-strategy-4-phase3-keda.md)
- [PostgreSQL Performance Tuning](rabbitmq-migration-strategy-5-postgresql-tuning.md)
- [Phase 4: Historical Sourcing Execution](rabbitmq-migration-strategy-6-phase4-execution.md)
- [Rollback Plans & Success Criteria](rabbitmq-migration-strategy-7-rollback-success.md)
- [Appendix: Questions, Costs, References](rabbitmq-migration-strategy-8-appendix.md)

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

## Timeline Summary

| Days | Phase | Activities | Success Criteria |
|------|-------|------------|------------------|
| 1-2 (Jan 25-26) | RabbitMQ Deploy | Install RMQ cluster, configure MassTransit, enable monitoring | RMQ cluster running, metrics visible |
| 3-5 (Jan 27-29) | RMQ Validation | Parallel run, weekly sourcing test, cutover | Zero message loss, stable operation |
| 6-8 (Jan 30-Feb 1) | Rate Limiting | Deploy Redis, implement rate limiter, reduce Provider workers | No ESPN 429s under load |
| 9-11 (Feb 2-4) | KEDA Deploy | Install KEDA, create ScaledObjects, synthetic load test | Scaling works correctly |
| 12+ (Feb 5+) | Historical Sourcing | Execute historical sourcing, monitor, tune | All data sourced successfully |

**Total time to historical sourcing:** ~12 days (working 7 days/week)  
**Alternative (pay for ASB Premium):** Immediate, but $677 cost

---

## Cost Analysis (Summary)

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

See [Appendix](rabbitmq-migration-strategy-8-appendix.md#cost-analysis) for full analysis.

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

**Document Owner:** SportsData Platform Team  
**Last Updated:** January 24, 2026  
**Next Review:** After Phase 1 completion (Week 2)
