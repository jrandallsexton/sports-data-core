# About-Site Overhaul — about.sportdeets.com

**Status: PLANNED (2026-09-12).** Overhaul `src/about` (the technical
showcase SPA, last accurate ~2025 when sportDeets ran on Azure), and
publish it as a container in the cluster at `about.sportdeets.com`.

## Why

The page showcases an architecture that no longer exists. Nearly every
infrastructure claim on it is stale, and the strongest material — the
bare-metal migration, the mobile launch, the multi-model AI lab — isn't
on it at all.

## Content audit: stale vs. reality

| On the page today | Reality (2026-09) |
|---|---|
| Azure SWA hosts the UI; App Services host workers | Everything runs on self-hosted bare-metal Kubernetes |
| Azure Front Door + APIM + Arc + Bastion + VNet + ADF | Gone entirely; Cloudflare (DNS + ddns) fronts the cluster |
| Azure Cosmos DB document store | MongoDB 8 on a dedicated bare-metal box (NUC7) |
| PostgreSQL on an Azure VM | PostgreSQL 17 bare-metal (revived NUC, Samsung 990 PRO) |
| Azure Service Bus | RabbitMQ, split per-sport across brokers with shovels |
| Azure SignalR Service | Self-hosted SignalR; API is the sole broadcaster |
| .NET 9 | .NET 10 |
| k3s on Vagrant + Hyper-V (4 nodes) | Real bare-metal cluster (see cluster hardware notes) |
| Ollama local LLM | OpenRouter gateway + Model Consensus Lab (many hosted models); verify whether the ollama namespace in the config repo is still live |
| MediatR / AutoMapper listed | CQRS is `[FromServices]` handlers, no MediatR — VERIFY every library claim against source (docs-audit rule) |
| Missing entirely | Mobile app (App Store + Google Play, Expo/RN, EAS), KEDA autoscaling, Redis + circuit breaker, OpenTelemetry, Umami, notification pipeline (per-kickoff reminder waves, poll-release pushes), Model/Preview Labs, player pick'em, MLB/NFL multi-sport |

Azure that REMAINS (the page should say so honestly — hybrid is part of
the story): App Configuration (label-based multi-tenancy), Pipelines
(self-hosted agent), Container Registry (`sportdeets.azurecr.io`), Blob
(prompt storage). Verify Key Vault's status before claiming either way.

## Proposed narrative & sections

**Owner call 2026-09-12: NO migration narrative.** The page describes
the platform as it IS — not what it was, no before/after framing. The
old Azure diagrams are simply replaced by current-state ones.

1. **Overview** — product, stack summary (rewritten), key features incl.
   mobile + multi-sport
2. **The Platform** — ~12 services, event-driven document pipeline
   (~47 processors, SHA-256 doc identity, idempotent consumers,
   DLQ-as-buffer), CQRS API
3. **Infrastructure** — bare-metal k8s, dedicated PG/Mongo data boxes,
   per-sport RabbitMQ, KEDA, Traefik + cert-manager, Cloudflare ddns
4. **Mobile** — Expo SDK 55 / RN, EAS builds, GitHub Actions, both-store
   launch (September 2026)
6. **AI & Models** — Model Consensus Lab (OpenRouter, prompt-scoped
   matrix), matchup preview pipeline, spread-contextualized history,
   "model predicts, LLM explains" direction. NO prompt text on the page
   (public repo rule applies to the site too).
7. **Observability** — OTel + Seq, Prometheus/Grafana (+ Pushgateway),
   Umami
8. **Data Engineering** — ESPN sourcing at scale, backfill corpus,
   rate-limit handling, enrichment/self-heal jobs
9. **DevOps & GitOps** — Azure Pipelines on a self-hosted agent, Flux CD
   + Kustomize overlays, Reloader
10. **Gallery** — refreshed screenshots (web + mobile + Grafana + jobs
    dashboard + Model Lab). KEEP the 2025-era images until replaced —
    the owner uses them as the replacement checklist (owner call
    2026-09-12); prune only as each gets its modern counterpart
11. **Roadmap** — rewrite from the current deferred/active docket

## Technical approach

### Rebuild on Vite (recommendation)
Current app is CRA (`react-scripts` 5 — unmaintained; the package.json
`overrides` block is already hand-fighting CVEs). Since every section is
being rewritten anyway, port the shell (nav/scroll-spy, CollapsibleSection,
MermaidDiagram) to a fresh Vite + React scaffold. Alternative: stay CRA
to match sd-ui — cheaper today, but this is a standalone app and the
showcase page should not open with a deprecated toolchain.

### Containerization (clone the sd-ui pattern)
- Multi-stage Dockerfile: `node:*-alpine` build → `nginx:*-alpine`
  serve, non-root user, port 8080, security-headers include, wget
  healthcheck — copy `src/UI/sd-ui/Dockerfile` + `nginx.conf` +
  `security-headers.conf` and simplify (no runtime env args needed; a
  build timestamp version arg for the footer).
- Image: `sportdeets.azurecr.io/sportsdataabout`.

### Deployment (sports-data-config)
- New `app/base/apps/about-ui/` (deployment 1 replica, service,
  IngressRoute `Host(`about.sportdeets.com`)` websecure + web) modeled
  on `web-ui/`; wire into overlays. NOT KEDA-scaled.
- TLS: add `about.sportdeets.com` to cert-manager config (check whether
  the existing cert is apex+www only — likely needs a new Certificate or
  SAN addition).
- DNS: Cloudflare record for `about` (confirm whether cloudflare-ddns
  manages per-record or the record just points at the same IP it
  updates).
- CI: extend the existing UI pipeline/scripts (BuildImagesAndUploadToACR
  pattern) with the about image.

## Phases

1. **Scaffold** — Vite app, port shell components, boot locally.
2. **Content** — rewrite sections per the narrative above, with a
   grep-verified claims pass (every named technology checked against
   source/config repo before it renders).
3. **Diagrams** — new current-state mermaid set; old Azure set preserved
   in The Migration as "before".
4. **Ship** — Dockerfile, ACR push, config-repo manifests, cert + DNS,
   smoke test at about.sportdeets.com.
5. **Gallery refresh** — new screenshots, ideally after a game-day so
   dashboards show real load.

## Open decisions (owner)

- Vite rebuild vs. stay-CRA (recommended: Vite).
- How loud to be about the Azure exit (the "before/after" framing sells
  it; a neutral "current architecture" page is the conservative option).
- Gallery: include mobile store listing screenshots?
- Domain: `about.` subdomain confirmed vs. a `/about` route on www.
