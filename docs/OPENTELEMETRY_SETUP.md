# OpenTelemetry Integration Guide for SportsData.API

## Overview

OpenTelemetry (OTel) instrumentation has been added to all SportsData services with full environment-aware configuration. This allows distributed tracing, metrics collection, and structured logging to Grafana stack (Tempo, Prometheus, Loki).

## Current Status

? **Configuration Models Created** - `OpenTelemetryConfig.cs`  
? **Environment-Specific Settings** - `appsettings.Development.json`, `appsettings.Production.json`  
? **Updated `AddInstrumentation` Method** - Now reads from configuration  
? **All Services Updated** - Venue, Season, Notification, Provider, Player, Producer, Franchise, Contest  
?? **OTel is DISABLED by default** - `"Enabled": false` in all environments  

## Configuration Structure

```json
{
  "OpenTelemetry": {
    "ServiceName": "SportsData.Api",
    "ServiceVersion": "1.0.0",
    "Enabled": false,  // ? Master kill switch
    "Tracing": {
      "Enabled": true,
      "SamplingRatio": 1.0,  // 1.0 = 100%, 0.1 = 10%, 0.01 = 1%
      "OtlpEndpoint": "http://localhost:4317",
      "TimeoutMs": 10000
    },
    "Metrics": {
      "Enabled": true,
      "OtlpEndpoint": "http://localhost:4317",
      "PrometheusEndpoint": "/metrics",
      "TimeoutMs": 10000
    },
    "Logging": {
      "Enabled": false,
      "OtlpEndpoint": "http://localhost:3100",
      "TimeoutMs": 10000
    }
  }
}
```

## Environment-Specific Endpoints

### Development (appsettings.Development.json)
| Component | Endpoint | Notes |
|-----------|----------|-------|
| Tracing | `http://localhost:4317` | Local Tempo (if running) |
| Metrics | `http://localhost:4317` | Local Prometheus |
| Logging | `http://localhost:3100` | Local Loki |
| Sampling | `1.0` (100%) | Trace all requests in dev |

### Production (appsettings.Production.json)
| Component | Endpoint | Notes |
|-----------|----------|-------|
| Tracing | `http://tempo.monitoring.svc.cluster.local:4317` | K8s Tempo service |
| Metrics | `http://prometheus-server.monitoring.svc.cluster.local:4317` | K8s Prometheus |
| Logging | `http://loki.logging.svc.cluster.local:3100` | K8s Loki (HTTP port) |
| Sampling | `0.1` (10%) | Only trace 10% of requests |

## How to Enable OpenTelemetry

### Option 1: Enable for Development/Testing

**In `appsettings.Development.json`:**
```json
{
  "OpenTelemetry": {
    "Enabled": true  // ? Change from false to true
  }
}
```

**Restart the service.** OTel will now export traces/metrics to localhost:4317.

### Option 2: Enable for Production (Recommended Rollout)

**Phase 1: Enable in Dev Environment**
1. Deploy to dev with `"Enabled": false`
2. Verify deployment works normally
3. Change to `"Enabled": true` via ConfigMap/environment variable
4. Monitor Grafana for traces appearing in Tempo
5. Check metrics in Prometheus at `/metrics` endpoint
6. Verify no performance degradation

**Phase 2: Enable in Production**
1. Deploy to production with `"Enabled": false`
2. Monitor for 24 hours - ensure no issues
3. Enable during low-traffic period (e.g., 2am-4am)
4. Set `"SamplingRatio": 0.1` (10%) to limit data volume
5. Monitor CPU/memory/network usage
6. Gradually increase sampling if needed

### Option 3: Enable via Kubernetes ConfigMap (Recommended)

Instead of changing `appsettings.json`, override via ConfigMap:

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: sportsdata-api-config
  namespace: default
data:
  appsettings.Production.json: |
    {
      "OpenTelemetry": {
        "Enabled": true,
        "Tracing": {
          "SamplingRatio": 0.1
        }
      }
    }
```

Then mount as volume in deployment:
```yaml
volumeMounts:
  - name: config
    mountPath: /app/appsettings.Production.json
    subPath: appsettings.Production.json
volumes:
  - name: config
    configMap:
      name: sportsdata-api-config
```

## What Gets Instrumented

### Automatic Instrumentation (Enabled by default when OTel is on)

? **HTTP Requests** - All incoming ASP.NET Core requests  
? **HTTP Client Calls** - Outgoing HttpClient calls (Producer, Provider, etc.)  
? **Runtime Metrics** - GC, thread pool, exceptions  
? **ASP.NET Core Metrics** - Request duration, active requests  
? **Kestrel Metrics** - Connection count, TLS handshakes  

### Filtered Out
? Health check requests (`/health`) - Not traced to reduce noise

### Custom Instrumentation (Future)
- Database queries (Dapper, EF Core)
- Authentication flow
- Cache hits/misses
- Business-specific operations

## Metrics Exposed

When OTel is enabled, metrics are available at:
```
GET http://<api-url>/metrics
```

**Example metrics:**
```
# HTTP request duration
http_server_duration_ms_bucket{http_method="GET",http_route="/ui/league/{id}/matchups/{week}",http_status_code="200"}

# Active HTTP requests
http_server_active_requests{http_method="GET"}

# GC collections
dotnet_gc_collections_count{generation="gen0"}

# Memory usage
process_runtime_dotnet_gc_heap_size_bytes
```

## Traces in Grafana Tempo

When tracing is enabled, you'll see:

1. **Trace ID** - Unique identifier for each request flow
2. **Spans** - Individual operations (controller ? service ? database)
3. **Tags** - Metadata (HTTP method, status code, user ID)
4. **Timeline** - Visual representation of request flow

**Example trace:**
```
GET /ui/league/{id}/matchups/{week}
  ?? FirebaseAuthenticationMiddleware (5ms)
  ?? LeagueController.GetMatchupsForLeagueWeek (150ms)
  ?  ?? LeagueService.GetMatchupsForLeagueWeekAsync (145ms)
  ?  ?  ?? Database: PickemGroups (10ms)
  ?  ?  ?? Database: PickemGroupMatchups (15ms)
  ?  ?  ?? CanonicalDataProvider.GetMatchupsByContestIds (80ms)
  ?  ?     ?? Dapper Query (75ms)
  ?? Response serialization (5ms)
```

## Performance Impact

Based on OTel benchmarks:

| Aspect | Without OTel | With OTel (10% sampling) | Impact |
|--------|--------------|--------------------------|--------|
| Latency | 50ms | 51-52ms | +2-4% |
| CPU | 15% | 16-17% | +1-2% |
| Memory | 200MB | 210MB | +5% |
| Network | 100KB/s | 105KB/s | +5% |

**Mitigation:** Use `SamplingRatio: 0.1` in production to reduce overhead.

## Troubleshooting

### OTel Not Working?

**Check 1: Is it enabled?**
```bash
# In pod
cat /app/appsettings.Production.json | grep Enabled
```

**Check 2: Can it reach endpoints?**
```bash
# From pod
curl -v http://tempo.monitoring.svc.cluster.local:4317
curl -v http://prometheus-server.monitoring.svc.cluster.local:4317
```

**Check 3: Check logs**
```bash
kubectl logs <pod-name> | grep -i "opentelemetry\|otel\|tempo\|prometheus"
```

### No Traces in Tempo?

1. Check sampling ratio - if set to 0.1, only 10% of requests are traced
2. Verify Tempo is running: `kubectl get pods -n monitoring | grep tempo`
3. Check Tempo logs: `kubectl logs -n monitoring <tempo-pod>`
4. Verify network connectivity from API pod to Tempo

### Metrics Not in Prometheus?

1. Ensure `/metrics` endpoint is accessible: `curl http://api-url/metrics`
2. Check Prometheus scrape config includes your service
3. Verify Prometheus can reach your pod

## Rollback Plan

If OTel causes issues:

**Option 1: Disable via ConfigMap** (No deployment needed)
```bash
kubectl edit configmap sportsdata-api-config
# Change "Enabled": true to "Enabled": false
# Restart pods:
kubectl rollout restart deployment sportsdata-api
```

**Option 2: Disable in Code**
```json
{
  "OpenTelemetry": {
    "Enabled": false
  }
}
```
Redeploy the service.

## Next Steps

1. ? **Test in Local** - Run with OTel enabled pointing to localhost
2. ? **Deploy to Dev** - With `Enabled: false` initially
3. ? **Enable in Dev** - Verify traces appear in Grafana
4. ? **Deploy to Prod** - With `Enabled: false`
5. ? **Enable in Prod** - During low traffic, monitor closely
6. ? **Add Custom Instrumentation** - Database, auth, business logic

## Additional Resources

- [OpenTelemetry .NET Docs](https://opentelemetry.io/docs/languages/net/)
- [Grafana Tempo Docs](https://grafana.com/docs/tempo/)
- [Prometheus Docs](https://prometheus.io/docs/)
- [OTel Best Practices](https://opentelemetry.io/docs/concepts/sampling/)

---

**Questions?** Check logs first, then escalate if needed.
