# Appendix: Questions, Costs, References

[← Back to Overview](rabbitmq-migration-strategy-0-overview.md)

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
