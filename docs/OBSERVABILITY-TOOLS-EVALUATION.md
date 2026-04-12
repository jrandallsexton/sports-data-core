# Observability Tools Evaluation

**Created**: 2026-03-28  
**Status**: Backlog — no active work yet  
**Context**: Evaluating DataDog-like observability alternatives for the self-hosted bare-metal Kubernetes cluster (4-node NUC + separate PostgreSQL NUC).

---

## Current Stack

| Signal | Tool |
|--------|------|
| Logs | Seq + Loki |
| Metrics | Prometheus + Grafana |
| Traces | OpenTelemetry → Tempo |
| Alerts | AlertManager |

The existing LGTM stack is solid. The gap vs. DataDog is: **unified UI**, **APM/tracing UI**, **correlated logs+traces**, and **dashboards that don't require manual setup**. None of the current tools provide a single pane of glass.

---

## Candidates

### Kibana (ELK/EFK Stack)

- **What it is**: Visualization layer over Elasticsearch. Logs, metrics, APM, dashboards.
- **Self-hosted**: Yes, but Elasticsearch is extremely resource-hungry.
- **Licensing**: `elastic` license is free for self-hosted but has restrictions. OpenSearch (AWS fork) is fully open.
- **Fit**: Poor for this cluster. Would require replacing Seq/Loki/Prometheus with a heavier stack needing 3+ Elasticsearch nodes for stability. Loses the excellent Serilog → Seq developer experience.
- **Verdict**: Skip. Too heavy, too complex, replaces things that already work well.

---

### OneUptime

- **What it is**: Open-source **status page + uptime monitoring + on-call + incident management**. Think PagerDuty + StatusPage.io combined.
- **Self-hosted**: Yes, Docker/Kubernetes. Reasonable resource footprint.
- **Strengths**: Synthetic monitoring (HTTP checks, TCP, DNS), public/private status pages, on-call scheduling, incident timelines.
- **Weaknesses**: Not an APM or full observability platform. No distributed tracing. Metrics are basic. Focused on **availability**, not performance.
- **Fit**: Complementary, not a replacement. Fills the gap of public status page and uptime alerting — neither of which currently exist in the stack.
- **Verdict**: Worth adding as an additive tool. Low resource cost, unique value.
- **Links**: https://oneuptime.com / https://github.com/OneUptime/oneuptime

---

### SigNoz

- **What it is**: Open-source **full observability platform** — logs, metrics, and distributed traces in a single UI. Backed by ClickHouse (columnar DB).
- **Self-hosted**: Yes, Helm chart available. More resource-efficient than ELK but ClickHouse is non-trivial.
- **Strengths**:
  - Closest open-source DataDog analog
  - Native OpenTelemetry (existing OTel pipeline can point directly at it)
  - APM views: flamegraphs, service maps, RED metrics (Rate/Error/Duration) out of the box
  - Excellent .NET/ASP.NET Core support
  - Correlated logs + traces (click a trace span, see the logs)
  - Large community, more battle-tested
- **Weaknesses**: ClickHouse has meaningful memory/storage requirements (4–8GB RAM under load). Would need to evaluate whether NUC nodes can absorb it alongside the existing stack.
- **Fit**: High. OTel traces + metrics could route to SigNoz. Seq can be kept for structured Serilog developer logs — they complement each other.
- **Verdict**: Strong candidate. Best DataDog-like option in open-source with the largest community.
- **Links**: https://signoz.io / https://github.com/SigNoz/signoz

---

### HyperDX

- **What it is**: Open-source **developer-focused observability** platform. Logs, traces, metrics, session replay (RUM), and alerts — all in one UI. Built on ClickHouse + OpenTelemetry.
- **Self-hosted**: Yes, Docker Compose and Kubernetes.
- **Strengths**:
  - Most polished UI — the most "DataDog-feeling" of the group
  - Full-text log search with trace correlation
  - **Session replay (RUM)** — unique among these options; directly applicable to `sd-ui` (React) and `sd-mobile` (Expo/React Native)
  - Dashboard builder
  - Alerting with Slack/PagerDuty webhooks
  - Potentially lighter self-host footprint than SigNoz in some configurations
- **Weaknesses**: Younger project, smaller community than SigNoz. Less battle-tested at scale.
- **Fit**: High. RUM/session replay for the React and React Native apps is a capability nothing in the current stack provides.
- **Verdict**: Strong candidate, especially given the frontend apps. Evaluate before SigNoz.
- **Links**: https://www.hyperdx.io / https://github.com/hyperdxio/hyperdx

---

## Comparison Matrix

| Tool | Replaces | Resource Cost | DataDog Likeness | .NET Fit | Verdict |
|------|----------|--------------|-----------------|----------|---------|
| Kibana | Seq + Loki + Grafana | Very High | Medium | Good | Skip |
| OneUptime | Nothing (additive) | Low | Low | N/A | Add it |
| SigNoz | Loki + Tempo + Grafana | Medium-High | High | Excellent | Strong candidate |
| HyperDX | Loki + Tempo + Grafana | Medium | Very High | Good | Strong candidate |

---

## Evaluation Strategy

### Resource Warning

Both SigNoz and HyperDX use ClickHouse, which wants 4–8GB RAM under load. Do **not** run them simultaneously — evaluate one at a time to avoid OOMKills on other pods.

### Cluster Safety

- Deploy in a dedicated namespace (e.g., `observability-eval`) — not into `monitoring`
- Manage via Flux `HelmRelease` in a dedicated kustomization (not raw `kubectl apply`)
- On teardown: `kubectl delete namespace observability-eval` removes pods/services, but **PVCs must be manually deleted** to reclaim SMB CSI storage:
  ```bash
  kubectl delete pvc -n observability-eval --all
  ```
  Then reclaim the backing storage on the NAS share manually.

### Recommended Order

1. **OneUptime** — low risk, doesn't compete for resources, genuinely additive (status page + uptime)
2. **HyperDX** — deploy in isolated namespace, evaluate for 1–2 weeks, delete cleanly; RUM is the differentiator
3. **SigNoz** — only if HyperDX doesn't satisfy; don't run both simultaneously

### Keep Seq Regardless

The Serilog → Seq developer experience for structured .NET logs is best-in-class for local dev and debugging. None of these tools match it for that use case. The right end state is likely **Seq for structured dev logs + SigNoz or HyperDX for APM/traces/metrics unified view**.

---

## Links

- [SigNoz Helm Chart](https://github.com/SigNoz/charts)
- [HyperDX Kubernetes Docs](https://www.hyperdx.io/docs/install/kubernetes)
- [OneUptime Helm Chart](https://github.com/OneUptime/oneuptime/tree/master/helm-chart)
