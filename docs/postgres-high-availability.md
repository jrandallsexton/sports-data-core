# PostgreSQL High Availability

## Motivation

Production PostgreSQL runs on a single Intel NUC (sdprod-data-0, 192.168.0.250). NUCs are prone to thermal throttling and unexpected reboots. Before the 2026 NCAAFB season, we need automatic failover so a single-node failure doesn't take down the platform.

## Options

### Option 1: Patroni (bare metal)

Keep PostgreSQL on bare metal, add a second NUC as a standby.

- **Patroni** manages streaming replication and automatic failover
- Requires a consensus store (etcd) — can run on the NUCs themselves or inside the K8s cluster
- Apps connect via a virtual IP (keepalived/vip-manager) or HAProxy that tracks the current primary
- Pros: minimal change to current architecture, PostgreSQL stays outside K8s
- Cons: another NUC to maintain, networking (VIP/proxy) adds complexity, not GitOps-managed

### Option 2: CloudNativePG (move PostgreSQL into K8s)

Run PostgreSQL inside the existing Kubernetes cluster using the CloudNativePG operator.

- Operator manages primary + replicas, automated failover, backups — all declarative YAML
- Apps connect via a K8s Service that always points to the current primary
- No dedicated NUC required (though a second NUC adds capacity/fault tolerance for the cluster itself)
- Pros: GitOps-managed via Flux, failover is automatic, consistent with how everything else is managed
- Cons: migration from bare metal is the hard part, resource contention with application pods

## Recommendation

CloudNativePG. The cluster is already Kubernetes-native, and managing PostgreSQL the same way (declarative YAML, Flux, GitOps) reduces operational burden. A second NUC still helps for node-level redundancy but is no longer a single point of failure.

## Migration Path

1. Deploy CloudNativePG operator into the cluster
2. Create a PostgreSQL cluster resource (primary + 1 replica)
3. Import data from current bare-metal instance (pg_dump/restore)
4. Update connection strings (point to the new K8s Service)
5. Validate all services connect and function correctly
6. Decommission bare-metal PostgreSQL on sdprod-data-0

## Timeline

Target completion before 2026 NCAAFB season kickoff (late August 2026).
