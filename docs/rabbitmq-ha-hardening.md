# RabbitMQ HA Hardening — Design Notes

**Status**: Proposal. No changes applied yet.
**Written**: 2026-05-24, after a NUC failure stranded the FootballNcaa messaging bus.

## Incident that motivated this

A K3s node went hard-offline this afternoon (suspected hardware failure on a 7-month-old Intel NUC, still under diagnosis). The Provider/Producer pods rescheduled cleanly to other nodes — they're stateless `Deployment`s, K8s did its job there. But the **RabbitMQ cluster for FootballNcaa stayed down** and stayed `Pending`, taking that sport's entire messaging pipeline with it until the node returns.

MLB self-healed because its RabbitMQ cluster happens to be on a different node, and stateless Producer/API pods reconnected to it after rescheduling. NFL is presumably also unaffected for the same reason. The per-sport split (per `memory/reference_per_sport_rabbitmq_split.md`) is doing its job — isolation worked, only one sport's bus was lost — but each per-sport cluster is its own single point of failure.

## Why K8s didn't reschedule the RabbitMQ pod

Three reasons, in priority:

1. **Single-replica StatefulSet with local-path PV.** Each `RabbitmqCluster` is `replicas: 1` with `storageClassName: local-path`. K8s `local` PVs are bound to a specific node's disk; the scheduler reports `volume node affinity conflict` and refuses to place the pod elsewhere. The pod sits `Pending` until the node returns.

2. **StatefulSet identity guarantee.** Even without local storage, K8s deliberately won't force-reschedule a StatefulSet pod when the kubelet on the original node is unreachable — only the kubelet can confirm the old pod is actually dead, and K8s won't risk two pods with the same identity (`rabbitmq-football-ncaa-server-0`) running simultaneously. For RabbitMQ specifically that would mean split-brain Mnesia state.

3. **Node-eviction grace period.** Even for reschedulable workloads, K8s waits `--node-monitor-grace-period` (40s) + `tolerationSeconds: 300` (5 min) ≈ 5 min 40 s after the node goes silent before evicting pods from it. Intentional, to absorb transient blips.

So even if RabbitMQ used network storage, a single-replica StatefulSet still wouldn't move automatically when the node went unreachable without `kubectl delete pod --force --grace-period=0` — and force-deleting a stateful workload risks state corruption if the dead node ever returns.

The right fix is not "make a single replica move faster"; it's **stop having only one replica**.

## Current state (in `sports-data-config`)

Three identical `RabbitmqCluster` manifests under `app/base/rabbitmq/`:
- `rabbitmq-cluster-baseball-mlb.yaml`
- `rabbitmq-cluster-football-ncaa.yaml`
- `rabbitmq-cluster-football-nfl.yaml`

All share:
- `replicas: 1`
- `storageClassName: local-path`, `storage: 10Gi`
- Resources: requests 500m CPU / 512Mi memory; limits 2 CPU / 2Gi memory
- `cluster_formation.peer_discovery_backend = rabbit_peer_discovery_k8s` already configured (ready for multi-replica clustering)
- `podAntiAffinity` already configured to prefer different nodes (currently no effect since replicas=1, but correct for ≥2 replicas)

The peer-discovery config and antiAffinity being already in place means the leap to multi-replica is small from a manifest perspective.

## Options considered

### A. Bump `replicas` to 3 with quorum queues — **recommended**

True HA. Survives any single-node failure. RabbitMQ cluster formation via k8s peer discovery is already configured; each replica gets its own local-path PV on a separate node (the existing `podAntiAffinity` enforces this).

**Pros**
- Real HA — no manual intervention needed when a node dies.
- Preserves the per-sport split philosophy (each sport still has its own cluster, just resilient).
- No new storage system to operate (Longhorn etc.) — stays on familiar local-path.
- Standard RabbitMQ on K8s pattern.

**Cons**
- ~3× memory + storage per sport cluster. Three sports × 3 replicas = 9 RabbitMQ pods cluster-wide. At 512Mi requests / 2Gi limits each, that's ~4.6 Gi requested / 18 Gi worst-case limits just for RabbitMQ.
- Requires at least 3 healthy nodes to satisfy `podAntiAffinity` for any one cluster (preferred, not required — falls back to colocation if forced).
- Quorum queues must be the default for the HA guarantee to actually apply. MassTransit auto-declares quorum queues when configured with `cfg.UseQuorumQueues(...)` or the per-endpoint equivalent — needs to be confirmed in `MessagingRegistration.cs` and applied per sport.

### B. Switch to network-attached storage (Longhorn, NFS, etc.)

Lets a single replica reschedule onto a different node when the original dies. Cheaper than option A.

**Pros**
- 1× storage and memory cost.
- Single PV moves to a healthy node automatically.

**Cons**
- Doesn't fix the SPOF — there's still one replica. If the network storage backend has its own outage, all sport clusters die simultaneously (which removes the isolation benefit of the per-sport split entirely).
- Adds a new operational concern (Longhorn cluster, replication settings, disk space, monitoring).
- Treats the symptom, not the cause.

Verdict: skip if doing option A.

### C. Pin each cluster to a "stable" node via `nodeSelector`

Doesn't solve the problem — still SPOF. Just makes the SPOF more predictable. Pass.

### D. Hybrid: bump only the live-sport cluster, leave off-season sports at 1

Same model as A, but staged based on which sport is in season:
- During MLB season (now): MLB at replicas=3, NCAAFB + NFL at replicas=1
- Before NCAAFB kicks off: bump NCAAFB to replicas=3
- Before NFL kicks off: bump NFL to replicas=3

**Pros**
- Spreads the resource cost across the calendar.
- Highest-impact sport (currently live) gets the resilience first.
- Off-season clusters can fail without user-visible impact — only ingestion lag, which doesn't matter when no games are live.

**Cons**
- Operational discipline required to bump before each season starts. Easy to forget and get caught.

Verdict: **this is the actual recommendation** — A applied in stages. The decision rule "current-season sport at 3, off-season at 1" is simple enough to encode in a checklist or a small Flux annotation.

## Recommendation summary

1. **Confirm NUC RAM headroom first.** Three new RabbitMQ pods at 512Mi requests each = ~1.5 Gi additional reserved per cluster being upscaled. With ≥3 nodes hosting messaging, plenty of headroom needed.
2. **Bump `rabbitmq-cluster-baseball-mlb.yaml` to `replicas: 3` first.** Verify cluster formation + quorum queue declaration before touching the other sports.
3. **Confirm `MessagingRegistration.cs` declares quorum queues.** If not, queues will form as classic mirrored (deprecated) or non-mirrored (no HA). This is the load-bearing detail — replicas=3 with classic non-mirrored queues gives you nothing.
4. **Add a one-line checklist item to the seasonal-launch runbook**: "bump <sport> RabbitMQ to replicas=3 before season starts."
5. **Defer NFL/NCAAFB to closer to season start.**

## Open items

- Verify MassTransit quorum queue declaration in `src/SportsData.Core/Eventing/` registration code.
- Run `kubectl describe node` across the bare-metal NUCs to inventory available memory before bumping replicas.
- Decide whether to bump NCAAFB/NFL right away (resource cost) vs. closer to season (operational lift). The staged approach assumes the latter.
- The dead NUC: separately diagnose hardware vs. recover RabbitMQ data from its local-path volume if possible (queue contents from pre-outage). Likely not worth the effort given pre-outage NCAAFB queue state isn't load-bearing during MLB season.

## Why the per-sport split remains correct

Per `memory/reference_per_sport_rabbitmq_split.md`: the per-sport split is for fault isolation. Today's incident proved it works — only one sport went dark. The fix here is per-cluster resilience, NOT consolidation. Cross-sport events still bridge via shovel/federation (as configured under `app/base/rabbitmq/shovels/`).

## Decision log

| Date       | Decision                                              | Rationale                                                |
|------------|-------------------------------------------------------|----------------------------------------------------------|
| 2026-05-24 | Per-sport RabbitMQ split stays as-is                  | Today's outage proved isolation works                    |
| 2026-05-24 | Local-path storage stays as-is                        | Network storage doesn't solve SPOF, adds complexity      |
| 2026-05-24 | Plan: bump to replicas=3 with quorum queues, staged   | Live-sport-first matches actual impact, spreads RAM cost |
| 2026-05-24 | Open: verify quorum-queue declaration in MassTransit  | Load-bearing prerequisite; replicas=3 useless without it |
