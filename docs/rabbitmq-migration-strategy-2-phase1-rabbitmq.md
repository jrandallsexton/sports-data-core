# Phase 1: RabbitMQ Migration (Weeks 1-2)

[← Back to Overview](rabbitmq-migration-strategy-0-overview.md)

---

## Objectives
- Eliminate Azure Service Bus dependency
- Zero ongoing messaging costs
- Lower latency (in-cluster pod-to-pod communication)
- Full control over messaging infrastructure

---

## Days 1-2: Deploy & Configure

### 1.0 Local Docker Testing (Day 1)

**Before touching the cluster, validate locally:**

**docker-compose.yml** (see `docker-compose.yml` in repo root):
```yaml
version: '3.8'

services:
  rabbitmq:
    image: rabbitmq:3-management-alpine
    container_name: rabbitmq-local
    ports:
      - "5672:5672"   # AMQP
      - "15672:15672" # Management UI
    environment:
      RABBITMQ_DEFAULT_USER: sportsdata
      RABBITMQ_DEFAULT_PASS: local-dev-password
    volumes:
      - rabbitmq-data:/var/lib/rabbitmq
    restart: unless-stopped

volumes:
  rabbitmq-data:
```

**Test MassTransit dual-transport code:**
1. Run `.\24_StartRabbitMQ.ps1`
2. Update local `appsettings.Development.json`: `"CommonConfig:Messaging:UseRabbitMq": true`
3. Set connection: `"CommonConfig:Messaging:RabbitMq:Host": "localhost"`
4. Run Provider/Producer/API locally
5. Queue test jobs in Hangfire
6. Verify messages flow through RabbitMQ (check management UI at http://localhost:15672)
7. Verify jobs complete successfully
8. Switch back to ASB, verify still works

**Success criteria:**
- Zero code changes needed for cluster deployment
- Message delivery works identically to ASB
- No connection issues or serialization errors

### 1.1 Deploy RabbitMQ Cluster (Day 1-2)

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

### 1.2 Configure MassTransit for Dual Transport

Update application configs to support **both** ASB and RabbitMQ during migration:

```csharp
// SportsData.Core/DependencyInjection/MessagingRegistration.cs
// Transport selection is in the private ConfigureTransport method.
// Consumers are passed as an explicit List<Type>, not discovered via Assembly scanning.

private static void ConfigureTransport(IBusRegistrationConfigurator x, IConfiguration config)
{
    var useRabbitMq = config.GetValue<bool>(CommonConfigKeys.MessagingUseRabbitMq);
    // CommonConfigKeys.MessagingUseRabbitMq = "CommonConfig:Messaging:UseRabbitMq"

    if (useRabbitMq)
    {
        var rabbitHost = config[CommonConfigKeys.RabbitMqHost]; // read from config dynamically
        var rabbitUsername = config[CommonConfigKeys.RabbitMqUsername];
        var rabbitPassword = config[CommonConfigKeys.RabbitMqPassword];

        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(rabbitHost, "/", h =>
            {
                h.Username(rabbitUsername);
                h.Password(rabbitPassword);
            });
            cfg.ConfigureEndpoints(context);
        });
    }
    else
    {
        var sbConnString = config[CommonConfigKeys.AzureServiceBus];
        x.UsingAzureServiceBus((context, cfg) =>
        {
            cfg.Host(sbConnString);
            cfg.ConfigureEndpoints(context);
        });
    }
}

// Callers pass explicit consumer lists:
public static IServiceCollection AddMessaging<TDbContext>(
    this IServiceCollection services,
    IConfiguration config,
    List<Type>? consumers) { ... }
```

**Configuration in Azure App Config:**

```
CommonConfig:Messaging:UseRabbitMq = false  (initially, flip to true during cutover)
CommonConfig:Messaging:RabbitMq:Username = sportsdata
CommonConfig:Messaging:RabbitMq:Password = <SECURE_PASSWORD>
CommonConfig:Messaging:RabbitMq:Host = rabbitmq.rabbitmq-system.svc.cluster.local
```

### 1.3 Deploy Monitoring

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

---

## Days 3-5: Parallel Run & Validation

### 2.1 Enable RabbitMQ for Non-Critical Services First

Start with lower-risk services:
1. Enable RabbitMQ for API (lowest risk, not part of sourcing)
2. Monitor for 1 day
3. Enable for Producer
4. Monitor for 1 day
5. Enable for Provider (highest risk)

### 2.2 Run Weekly Sourcing on RabbitMQ

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

### 2.3 Cutover & Decommission ASB

Once confident:
1. Set `CommonConfig:Messaging:UseRabbitMq = true` for all services
2. Restart all pods
3. Monitor for 1 week
4. Cancel Azure Service Bus resources (save ~$10-50/month)

---

[Next: Phase 2 - Rate Limiting →](rabbitmq-migration-strategy-3-phase2-rate-limiting.md)
