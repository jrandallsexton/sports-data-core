# Broker detangle plan (2026-05-12)

Refactor the per-environment RabbitMQ topology so that every service
boundary maps to a dedicated broker. Eliminates the current ambiguity
where Producer NCAA and API both connect to the same "base" broker
(`rabbitmq.messaging.svc.cluster.local`), and renames the existing
sport-league brokers to a consistent `{sport}-{league}` convention
ahead of any non-football-non-mlb sport-league joining.

Follow-on to `docs/mlb-replay-broker-diagnosis.md`. That doc captured
the symptom (replay events not reaching the prod browser) and the
narrow fix (shovel two exchanges from `rabbitmq-mlb` → base). This
doc captures the broader cleanup the diagnosis surfaced as
tech debt.

**Status:**
- Round 1 — **executed 2026-05-12**. All five phases shipped; MLB + API split is live. See [Round 1 outcome](#round-1-outcome).
- Round 2 — drafted 2026-05-12; not yet executed.

## Execution rounds

Long-term vision is the full four-broker topology below. Execution
splits into two rounds so risk is bounded per round:

- **Round 1 (this iteration) — MLB + API split.** Stand up
  `rabbitmq-api` and `rabbitmq-baseball-mlb`. Move API, Producer
  MLB, and Provider MLB. Re-target the existing two MLB shovels.
  Football paths (NCAA, NFL) to the API break for the duration of
  Round 1 — accepted, since there are no users and football
  testing isn't imminent.
- **Round 2 (deferred) — Football consolidation.** Rename old
  `rabbitmq` cluster to `rabbitmq-football-ncaa`, rename
  `rabbitmq-nfl` to `rabbitmq-football-nfl`, add shovels for all
  cross-service football events. Triggered when football testing
  approaches.

---

## Goal

Each unit of service ownership has its own RabbitMQ cluster. Intra-
boundary traffic stays on that cluster. Cross-boundary traffic flows
via RabbitMQ shovels that terminate on the **API broker**.

### Why now

User-facing traffic is zero. No maintenance window required. The
existing per-sport-league broker isolation is already in place
operationally; this just makes it consistent and explicit, and adds
API isolation. Doing both at once avoids two separate disruptions
later when there are users.

### Non-goals

- Not converting to a non-RabbitMQ broker.
- Not changing MassTransit configuration patterns at the application
  level. Each service still uses its `AddMassTransit<TDbContext>(...)`
  registration; only the connection target changes.
- Not changing the secret-management approach. Shovel credentials
  continue to be sourced from operator-managed `*-default-user`
  secrets via the kubectl recipe.

---

## Target topology (long-term)

| Broker | Connects from (service pods) | Lands in round |
|---|---|---|
| `rabbitmq-api` | API | 1 |
| `rabbitmq-baseball-mlb` | Producer MLB (Api/Ingest/Worker), Provider MLB (Api/Ingest/Worker) | 1 |
| `rabbitmq-football-ncaa` | Producer NCAA (Api/Ingest/Worker), Provider NCAA (Api/Ingest/Worker) | 2 |
| `rabbitmq-football-nfl` | Producer NFL (Api/Ingest/Worker), Provider NFL (Api/Ingest/Worker) | 2 |

All clusters in the `messaging` namespace. All deployed via the
existing rabbitmq-cluster-operator. The messaging-topology-operator
(MTO) deployed in the MLB shovel work continues to manage Shovel
CRDs.

## Round 1 state (post-execution)

Snapshot of what exists in the cluster after Round 1 — to clarify
the in-between state before Round 2 catches up:

| Cluster | Status after Round 1 | Why it's there |
|---|---|---|
| `rabbitmq-api` | NEW. API connects here. | Target topology, in place. |
| `rabbitmq-baseball-mlb` | NEW. Producer/Provider MLB connect here. | Target topology, in place. |
| `rabbitmq` | LEGACY. Producer/Provider NCAA still connect here. | Deferred to Round 2 — rename to `rabbitmq-football-ncaa`. |
| `rabbitmq-nfl` | LEGACY. Producer/Provider NFL still connect here. | Deferred to Round 2 — rename to `rabbitmq-football-nfl`. |
| `rabbitmq-mlb` | DELETED at end of Round 1. | Replaced by `rabbitmq-baseball-mlb`. |

Football → API cross-service events (FootballPlayCompleted,
ContestStatusChanged from NCAA/NFL, etc.) **do not flow** during
Round 1's in-between state. API listens on `rabbitmq-api`; NCAA/NFL
Producers still publish on the legacy brokers. Bridging those
flows is Round 2's job. Accepted gap since no football testing is
imminent.

### App Config label delta — Round 1

| Label | RabbitMq:Host before | RabbitMq:Host after Round 1 | Final (Round 2) |
|---|---|---|---|
| `Prod` (base) | `rabbitmq.messaging.svc.cluster.local` | `rabbitmq-api.messaging.svc.cluster.local` | unchanged |
| `Prod.All` (API) | *(inherits base)* | *(inherits base — now API broker)* | unchanged |
| `Prod.FootballNcaa` | *(inherits base — legacy `rabbitmq`)* | **NEW override** to `rabbitmq.messaging.svc.cluster.local` so NCAA stays on the legacy broker after API leaves it | flips to `rabbitmq-football-ncaa.messaging.svc.cluster.local` |
| `Prod.FootballNfl` | `rabbitmq-nfl.messaging.svc.cluster.local` | unchanged | flips to `rabbitmq-football-nfl.messaging.svc.cluster.local` |
| `Prod.BaseballMlb` | `rabbitmq-mlb.messaging.svc.cluster.local` | `rabbitmq-baseball-mlb.messaging.svc.cluster.local` | unchanged |

The `Prod.FootballNcaa` new-override is critical: today NCAA
inherits the base broker. Once we flip the base to `rabbitmq-api`,
NCAA would follow API onto the new broker unless we explicitly pin
NCAA to the legacy `rabbitmq`. Round 1 must add the explicit pin
so NCAA stays put.

Same edits to `Username` / `Password` keys per label for any
label whose `RabbitMq:Host` changes (each new cluster has its own
operator-generated credentials).

### `_common-variables.ps1` delta — Round 1

In `/d/Dropbox/Code/sports-data-provision/_secrets/_common-variables.ps1`:

| Variable today | Variable after Round 1 |
|---|---|
| `$rmqUsernameNcaaProd` / `$rmqPasswordNcaaProd` (today used by legacy base broker = NCAA + API) | Renamed conceptually but value unchanged — still tracks the legacy `rabbitmq` cluster's credentials, which post-Round-1 serve only NCAA. Rename to `$rmqUsernameLegacyBaseProd` / `$rmqPasswordLegacyBaseProd` (or keep `NcaaProd` since NCAA is the sole remaining tenant). |
| (none) | NEW: `$rmqUsernameApiProd` / `$rmqPasswordApiProd` for the new `rabbitmq-api` cluster |
| (none) | NEW: `$rmqUsernameBaseballMlbProd` / `$rmqPasswordBaseballMlbProd` for the new `rabbitmq-baseball-mlb` cluster |
| `$rmqUsernameMlbProd` / `$rmqPasswordMlbProd` | DELETE at end of Round 1 (old `rabbitmq-mlb` cluster is decommissioned) |
| `$rmqUsernameNflProd` / `$rmqPasswordNflProd` | Unchanged in Round 1 (NFL deferred to Round 2). |

Values pulled from each new `*-default-user` secret post-provisioning:

```bash
kubectl get secret rabbitmq-api-default-user -n messaging \
  -o jsonpath='{.data.username}' | base64 -d
kubectl get secret rabbitmq-api-default-user -n messaging \
  -o jsonpath='{.data.password}' | base64 -d
kubectl get secret rabbitmq-baseball-mlb-default-user -n messaging \
  -o jsonpath='{.data.username}' | base64 -d
kubectl get secret rabbitmq-baseball-mlb-default-user -n messaging \
  -o jsonpath='{.data.password}' | base64 -d
```

---

## Event surface audit

Verified via grep on 2026-05-12. MassTransit derives RabbitMQ
exchange names from the message type's FullName, so the exchange
name is identical regardless of which sport-league broker the
message originates on — only the broker hosting that exchange
differs per source.

### Round 1 shovels (rabbitmq-baseball-mlb → rabbitmq-api)

| Event | Exchange name | Source broker | Destination broker |
|---|---|---|---|
| `BaseballPlayCompleted` | `SportsData.Core.Eventing.Events.Contests.Baseball:BaseballPlayCompleted` | `rabbitmq-baseball-mlb` | `rabbitmq-api` |
| `ContestStatusChanged` | `SportsData.Core.Eventing.Events.Contests:ContestStatusChanged` | `rabbitmq-baseball-mlb` | `rabbitmq-api` |

These are the existing two shovels, re-targeted from the about-to-be-deleted
`rabbitmq-mlb` to the renamed `rabbitmq-baseball-mlb` source, and
from the legacy `rabbitmq` destination to the new `rabbitmq-api`.

### Round 2 shovels (deferred, sketch only)

Same shape, from `rabbitmq-football-ncaa` and `rabbitmq-football-nfl`
to `rabbitmq-api`. Per-sport-league shovels needed for each of:

- `BaseballPlayCompleted` (MLB only) — already in Round 1
- `FootballPlayCompleted` (NCAA + NFL) — Round 2
- `ContestStatusChanged` (all) — Round 1 covers MLB; Round 2 adds NCAA + NFL
- Plus other cross-service contest events Producer emits and API consumes — full audit deferred to Round 2 prep.

### Intra-broker (no shovel needed)

- `DocumentRequested` — Producer publishes → Provider consumes (same sport-league broker)
- `DocumentCreated` — Provider publishes → Producer Ingest consumes (same sport-league broker)
- `CompetitorScoreUpdated` — Provider publishes → Producer Ingest consumes (same sport-league broker)
- `ProcessImageRequest` / `ProcessImageResponse` — Producer ↔ Provider intra-sport-league
- `PickemGroup*`, `PreviewGenerated` — API publishes + API consumes (intra-API)
- `OutboxTestEvent`, `LoadTestProducerEvent`, `LoadTestProviderEvent`, `DocumentSourcingStarted` — diagnostic/test events, intra-service

### Out of scope

- `VenueCreated` — Producer publishes, `SportsData.Venue` service consumes. Venue service is not deployed in prod (stub). Event dead-letters or sits queued. Skipped.
- `FranchiseSeasonEnrichmentCompleted` — Producer publishes; no current consumer. **Intentional per user — future plans for it. Leave the publish call alone.** No shovel needed today.
- `DocumentDeadLetter` — Producer publishes; `DocumentDeadLetterConsumer` is registered but disabled per CLAUDE.md. Stays disabled.

### Heartbeat — DECIDED: kill it

`Heartbeat` is emitted by every service via Core middleware and
consumed only by `HeartbeatConsumer` in API. Leftover from very
early work; liveness/readiness probes serve the same purpose
better. Decision: **remove both publisher and consumer in a
separate small PR** (sports-data repo), not coupled to this broker
migration:

- `src/SportsData.Core/Middleware/Health/HeartbeatPublisher.cs` — delete or no-op
- `src/SportsData.Api/Infrastructure/HeartbeatConsumer.cs` — delete
- Remove `HeartbeatConsumer` registration from API's
  `Program.cs` consumer list (line ~229).
- The `Heartbeat` event type itself can also be deleted from
  `SportsData.Core.Eventing.Events.Heartbeat`.

Tracked here so it doesn't get lost. Out of the broker migration's
critical path.

---

## Round 1 migration sequence

Phased so each phase is independently verifiable. Phases 1–3 are
purely additive and reversible. Phase 4 (App Config flip + restart)
is the cutover. Phase 5 (decommission `rabbitmq-mlb`) is destructive
but only applied after Phase 4 is verified.

### Phase 1 — Provision new clusters (additive)

In `sports-data-config/app/base/rabbitmq/`:

- Add `rabbitmq-cluster-api.yaml` — new `RabbitmqCluster` named `rabbitmq-api`
- Add `rabbitmq-cluster-baseball-mlb.yaml` — new `RabbitmqCluster` named `rabbitmq-baseball-mlb`

Existing `rabbitmq-cluster.yaml`, `rabbitmq-cluster-nfl.yaml`, and
`rabbitmq-cluster-mlb.yaml` stay in place. Update
`kustomization.yaml` to include the two new files alongside.

Commit + push. Flux brings up two new clusters alongside the three
existing. Five broker pods in `messaging` namespace briefly.

**Verification:** `kubectl get rabbitmqcluster -n messaging` shows
all five clusters in `AllReplicasReady=True` state.

### Phase 2 — Update IngressRoutes for new management UIs

Existing ingress exposes each broker's management UI at
`messaging-{suffix}.sportdeets.com`. Add ingress entries for
`rabbitmq-api` and `rabbitmq-baseball-mlb` so their UIs are
reachable for diagnostics (`messaging-api.sportdeets.com` and
`messaging-baseballmlb.sportdeets.com` — the latter replacing
the existing `messaging-mlb.sportdeets.com` once Phase 5 deletes
the old broker, but both can coexist during Round 1).

Use TLS via the existing cert-manager Letsencrypt issuer.

### Phase 3 — Pre-create the re-targeted shovels (additive)

Under `app/base/rabbitmq/shovels/`, replace the existing two
shovel files with new ones pointing at the new source/destination:

- `shovel-baseball-play-completed-mlb-to-api.yaml` — replaces
  `shovel-baseball-play-completed.yaml`. Source:
  `rabbitmq-baseball-mlb`. Destination: `rabbitmq-api`
  (`rabbitmqClusterReference` updated).
- `shovel-contest-status-changed-mlb-to-api.yaml` — same change.

Each references a credentials Secret named
`shovel-baseball-mlb-to-api-credentials`. The kubectl recipe in
the shovels directory README needs updating to match the new
broker names + Secret name + `%2F` vhost gotcha (which is already
captured in the existing recipe but should be propagated to the
renamed flow).

`rabbitmqClusterReference` points at `rabbitmq-api` (destination).

Shovels fail to reconcile (credentials secret missing) until
Phase 4 — expected and harmless.

### Phase 4 — Cutover (App Config flip + service restart wave)

This is the disruptive step. Order matters.

1. **Pull credentials from new clusters.** For each of the two new
   `*-default-user` secrets, decode the `username` and `password`
   fields and add `$rmqUsernameApiProd` / `$rmqPasswordApiProd` and
   `$rmqUsernameBaseballMlbProd` / `$rmqPasswordBaseballMlbProd`
   variables to `_common-variables.ps1`.
2. **Apply App Config delta.** Run the PS1 sync scripts to push:
   - Base `Prod` label: `RabbitMq:Host` → `rabbitmq-api.messaging.svc.cluster.local` + matching Username/Password
   - `Prod.FootballNcaa` label: **NEW** explicit override for `RabbitMq:Host` = `rabbitmq.messaging.svc.cluster.local` (so NCAA stays on the legacy broker once API leaves the base). Match Username/Password to existing legacy `rabbitmq` cluster credentials.
   - `Prod.BaseballMlb` label: `RabbitMq:Host` → `rabbitmq-baseball-mlb.messaging.svc.cluster.local` + matching Username/Password.
3. **Create shovel credentials Secret** via the kubectl recipe (now
   named `shovel-baseball-mlb-to-api-credentials`, with `%2F` vhost
   suffix). Annotate it
   `kustomize.toolkit.fluxcd.io/prune: disabled`.
4. **Restart affected service deployments.** Rolling restart of
   `api-all`, `producer-baseball-mlb-*`, `provider-baseball-mlb-*`.
   Each pod reconnects to its new broker; MassTransit auto-declares
   queues + exchanges on first connect.

   NCAA/NFL pods do NOT need restarting in Round 1 — they continue
   on the legacy `rabbitmq` and `rabbitmq-nfl` clusters respectively.
   But verify App Config refresh doesn't trigger any spurious bus
   restart in those services that would land them on the wrong
   broker mid-restart (it shouldn't — RabbitMq:Host is read at bus
   bootstrap, not per-config-refresh — but worth watching the first
   few minutes of restart).
5. **Verify shovel reconciliation.** `kubectl get shovel -n messaging`
   — both shovels in `Ready=True`. `rabbitmqctl shovel_status` on
   `rabbitmq-api` shows both `running`.
6. **Smoke test.** Trigger a baseball replay; confirm Seq +
   browser show events flowing from `rabbitmq-baseball-mlb` →
   `rabbitmq-api` → SignalR. Fire the admin baseball debug-card
   broadcast; confirm it still works (intra-API on `rabbitmq-api`).
   Confirm NCAA backfill jobs (if any are running) continue
   without breakage on the legacy broker.

### Phase 5 — Decommission `rabbitmq-mlb`

Once Phase 4 is verified and no regressions surface:

1. **Verify zero consumers on `rabbitmq-mlb`.**
   `rabbitmqctl list_consumers` on the old MLB broker should show
   nothing. All MLB pods have moved to `rabbitmq-baseball-mlb`.
2. **Delete `rabbitmq-mlb` `RabbitmqCluster` resource.** Operator
   cleans up StatefulSet, Service, PVC. Remove
   `rabbitmq-cluster-mlb.yaml` from
   `app/base/rabbitmq/kustomization.yaml`. Commit + push; Flux
   prunes.
3. **Delete old ingress entry** for `messaging-mlb.sportdeets.com`.
4. **Delete `$rmqUsernameMlbProd` / `$rmqPasswordMlbProd`** from
   `_common-variables.ps1`. The new `*BaseballMlbProd` variables
   added in Phase 4 supersede.
5. **Keep the legacy `rabbitmq` cluster running.** Still serving
   NCAA. Round 2 will rename/migrate it.

---

## Round 1 rollback story

| Phase reached | Reversible? | How |
|---|---|---|
| 1 (new clusters provisioned) | Yes | Delete `rabbitmq-cluster-api.yaml` + `rabbitmq-cluster-baseball-mlb.yaml`, push. Old clusters still serving. |
| 2 (IngressRoutes added) | Yes | Delete new IngressRoute entries. |
| 3 (shovel files re-targeted) | Yes | Revert the file changes — shovels go back to pointing at `rabbitmq-mlb` source / legacy `rabbitmq` destination. Until Phase 4 those re-targeted shovels are non-functional anyway (credentials missing). |
| 4 (App Config flipped + MLB pods + API restarted) | Partial | Flip App Config back to the old `RabbitMq:Host` values for `Prod` (back to legacy `rabbitmq`) and `Prod.BaseballMlb` (back to `rabbitmq-mlb`). Also remove the `Prod.FootballNcaa` explicit override so NCAA goes back to inheriting. Restart API + MLB pods. Messages that flowed during the new-broker window are lost. |
| 5 (old `rabbitmq-mlb` deleted) | No | Re-provisioning won't restore the queue state. |

---

## Round 1 open questions

(Most pre-execution questions resolved with the user 2026-05-12.
What remains:)

1. **Watch the App Config refresh behavior on NCAA pods during
   cutover.** When the base `Prod` label flips its `RabbitMq:Host`,
   NCAA pods load `Prod` + `Prod.FootballNcaa`. The new
   `Prod.FootballNcaa` override should pin them to the legacy
   broker, but verify in a tight window that they don't briefly
   pick up the new base value during App Config refresh. Memory
   says the .NET App Config provider caches with ~30s default
   refresh interval; behavior under partial-config-update isn't
   well-documented.
2. **What does the existing `rabbitmq-overview.json` /
   `rabbitmq-queues.json` at sports-data-config root capture?**
   Snapshots? Used by anything? May need refresh post-migration.
3. **Federation vs shovel for Round 2.** Shovels are
   per-event-type. Once we add NCAA + NFL in Round 2, the shovel
   count grows. Consider a single federation policy that mirrors
   the whole "cross-service" exchange set instead of per-event
   shovels. Defer the decision; revisit at Round 2 prep.

---

## Round 1 files involved

| Repo | Path | Action |
|---|---|---|
| sports-data-config | `app/base/rabbitmq/rabbitmq-cluster-api.yaml` | NEW (Phase 1) |
| sports-data-config | `app/base/rabbitmq/rabbitmq-cluster-baseball-mlb.yaml` | NEW (Phase 1) |
| sports-data-config | `app/base/rabbitmq/kustomization.yaml` | edit (Phase 1; Phase 5 removes `rabbitmq-cluster-mlb.yaml` entry) |
| sports-data-config | `app/base/rabbitmq/ingressroute.yaml` | edit (Phase 2 add; Phase 5 remove the old `mlb` entry) |
| sports-data-config | `app/base/rabbitmq/shovels/shovel-baseball-play-completed-mlb-to-api.yaml` | NEW (Phase 3; replaces existing `shovel-baseball-play-completed.yaml`) |
| sports-data-config | `app/base/rabbitmq/shovels/shovel-contest-status-changed-mlb-to-api.yaml` | NEW (Phase 3; replaces existing `shovel-contest-status-changed.yaml`) |
| sports-data-config | `app/base/rabbitmq/shovels/shovel-baseball-play-completed.yaml` | delete (Phase 3) |
| sports-data-config | `app/base/rabbitmq/shovels/shovel-contest-status-changed.yaml` | delete (Phase 3) |
| sports-data-config | `app/base/rabbitmq/shovels/kustomization.yaml` | edit (Phase 3) |
| sports-data-config | `app/base/rabbitmq/shovels/README.md` | edit (Phase 3; update kubectl recipe for renamed brokers + Secret name) |
| sports-data-config | `app/base/rabbitmq/rabbitmq-cluster-mlb.yaml` | delete (Phase 5) |
| sports-data-provision | `util/appconfig/manifest.json` | edit (Phase 4; `Prod`, `Prod.BaseballMlb`, `Prod.FootballNcaa` labels) |
| sports-data-provision | `_secrets/_common-variables.ps1` | edit (Phase 4 add new vars; Phase 5 remove `$rmqUsernameMlbProd` / `$rmqPasswordMlbProd`) |

### Sibling PR (not part of broker migration, but tracked)

| Repo | Path | Action |
|---|---|---|
| sports-data | `src/SportsData.Core/Middleware/Health/HeartbeatPublisher.cs` | delete |
| sports-data | `src/SportsData.Api/Infrastructure/HeartbeatConsumer.cs` | delete |
| sports-data | `src/SportsData.Api/Program.cs` (line ~229) | remove `HeartbeatConsumer` from consumer list |
| sports-data | `src/SportsData.Core/Eventing/Events/Heartbeat.cs` (if exists) | delete |

## Related work

- **PR #312** (sports-data) — diagnostic logging on API consumers.
  Still in place; will continue to be useful for the post-migration
  smoke test.
- **PR #313** (sports-data) — direct-publish in
  `BaseballContestReplayService`. Unrelated to migration; stays.
- **sports-data-config `c446aee` + `ac25344`** — MTO install + two
  MLB shovels. Stays. Will be re-targeted at the renamed
  `rabbitmq-baseball-mlb` broker in Phase 3.

---

## Round 1 outcome

Shipped 2026-05-12 across two repos. Snapshot of what changed and what
held vs. plan:

| Thing | Plan | Outcome |
|---|---|---|
| `rabbitmq-api` cluster | provisioned in Phase 1 | ✅ live |
| `rabbitmq-baseball-mlb` cluster | provisioned in Phase 1 | ✅ live |
| MLB shovels re-targeted | Phase 3 | ✅ both `running` after Phase 4 |
| App Config flip | Phase 4 | ✅ Prod base → API, Prod.BaseballMlb → new MLB, Prod.FootballNcaa **NEW** explicit pin to legacy `rabbitmq` |
| KV rotation strategy | not specified in original | settled on: keep secret names, rotate values in place (`RabbitMqPassword-Prod` → API; `RabbitMqPassword-Prod-Mlb` → baseball-mlb); add NEW `RabbitMqPassword-Prod-Ncaa` (preserves legacy password) |
| Rolling restart (api-all + 6 MLB deployments) | Phase 4 step 4 | ✅ all 7 healthy |
| Legacy `rabbitmq-mlb` decommission | Phase 5 | ✅ Flux pruned cluster + 110 stranded messages discarded per user's "nuke it" |
| IngressRoute swap (`messaging-baseballmlb.sportdeets.com`) | Phase 5 | ✅ now serves `rabbitmq-baseball-mlb` |

### Round 1 surprises (lessons for Round 2)

1. **Shovel `queue.bind` failed on fresh broker** because MassTransit
   only auto-declares the source exchange on first publish, and a
   newly-provisioned Producer broker has no publish history.
   Symptom: shovel CRD `Ready=True`, `rabbitmqctl shovel_status` shows
   `terminated` with reason `"needed a restart"`, broker logs report
   `NOT_FOUND - no exchange '<name>' in vhost '/'`. Fix: pre-declare
   the source exchanges via `rabbitmqadmin declare exchange` before
   any shovel reconciles. Documented in
   `memory/reference_shovel_exchange_predeclare.md`. **Round 2 plans
   this declaration step into Phase 1** so the gotcha doesn't recur.
2. **AMQP URI vhost suffix** — the operator-generated
   `connection_string` ends in `:5672/` which AMQP parses as the
   *empty* vhost. Shovels need `:5672/%2F` (default vhost `/`
   URL-encoded). Already baked into the shovels/README.md recipe.
3. **`Prod.FootballNcaa` explicit override is permanent**, not a
   temporary Round-1 shim. Round 2 flips its values, doesn't delete
   the override. (Originally written as "Final (Round 2): flips to
   `rabbitmq-football-ncaa`" — that wording stands; the override row
   itself stays in the manifest forever.)

---

## Round 2 — Football consolidation

Move NCAA and NFL onto dedicated, consistently-named brokers and
decommission the two legacy clusters that survived Round 1
(`rabbitmq` and `rabbitmq-nfl`). Pattern is a direct repeat of Round
1 with two clusters in parallel instead of one.

**Trigger:** user observation 2026-05-12 that two pods still have the
wrong names (`rabbitmq-server-0` from the legacy base cluster,
`rabbitmq-nfl-server-0` from the legacy NFL cluster). No football
testing pressure forced this; finishing the topology cleanup before
the trail goes cold.

### Round 2 target state

| Broker | Tenants after Round 2 | Origin |
|---|---|---|
| `rabbitmq-api` | API | unchanged from Round 1 |
| `rabbitmq-baseball-mlb` | Producer/Provider MLB | unchanged from Round 1 |
| `rabbitmq-football-ncaa` | Producer/Provider NCAA | NEW |
| `rabbitmq-football-nfl` | Producer/Provider NFL | NEW |
| `rabbitmq` | — | **deleted** |
| `rabbitmq-nfl` | — | **deleted** |

Every service boundary maps cleanly to a named broker. No service
shares a broker with another service.

### Round 2 event surface (shovels)

Audit confirmed against `event-surface-overview.md`. Both NCAA and
NFL Producers publish the same two cross-service event types that
API consumes:

| Event | Exchange name | Source brokers | Destination | Shovels created |
|---|---|---|---|---|
| `FootballPlayCompleted` | `SportsData.Core.Eventing.Events.Contests.Football:FootballPlayCompleted` | `rabbitmq-football-ncaa`, `rabbitmq-football-nfl` | `rabbitmq-api` | 2 |
| `ContestStatusChanged` | `SportsData.Core.Eventing.Events.Contests:ContestStatusChanged` | `rabbitmq-football-ncaa`, `rabbitmq-football-nfl` | `rabbitmq-api` | 2 |

Total: **4 new shovels.** Same shape as the existing two MLB shovels.
All terminate on `rabbitmq-api` (pull mode) for consistency with Round
1.

### Federation vs. shovel revisit

Per Round 1 open question #3: shovel count is now at 6 (4 new + 2
MLB). Federation would consolidate the topology to a single policy
on `rabbitmq-api` that mirrors every "Contests:*" and
"Contests.{Sport}:*" exchange across all upstream brokers. Trade-off:

- **Stay with shovels** (chosen): explicit, per-event, easy to reason
  about, already working, observable via `rabbitmqctl shovel_status`.
  Cost: every new cross-service event needs a manifest add. With only
  2 active events that's not yet painful.
- **Switch to federation**: zero per-event manifest churn, but
  configuration is policy-based + opaque, and we'd need to convert
  the existing MLB shovels too. More disruption than payoff at this
  scale.

Decision: continue with shovels for Round 2. Reconsider if the
cross-service event count crosses ~10.

### App Config delta — Round 2

| Label | RabbitMq:Host before Round 2 | RabbitMq:Host after Round 2 |
|---|---|---|
| `Prod` (base) | `rabbitmq-api.messaging.svc.cluster.local` | unchanged |
| `Prod.All` (API) | *(inherits base)* | unchanged |
| `Prod.FootballNcaa` | `rabbitmq.messaging.svc.cluster.local` (Round 1 pin) | **flips** to `rabbitmq-football-ncaa.messaging.svc.cluster.local` |
| `Prod.FootballNfl` | `rabbitmq-nfl.messaging.svc.cluster.local` | **flips** to `rabbitmq-football-nfl.messaging.svc.cluster.local` |
| `Prod.BaseballMlb` | `rabbitmq-baseball-mlb.messaging.svc.cluster.local` | unchanged |

`Username` (operator-generated default user) and the
`ManagementApiBaseUrl` value flip alongside each `Host` change.
`Password` keys reference KV secrets that stay named the same — only
the secret *values* rotate.

### KV password strategy — Round 2

Same in-place rotation pattern as Round 1. No new secret names.

| KV secret | Value before Round 2 | Value after Round 2 |
|---|---|---|
| `RabbitMqPassword-Prod` | rabbitmq-api password | unchanged |
| `RabbitMqPassword-Prod-Mlb` | rabbitmq-baseball-mlb password | unchanged |
| `RabbitMqPassword-Prod-Ncaa` | legacy `rabbitmq` cluster password (Round 1 preserved) | **rotate** to `rabbitmq-football-ncaa-default-user` password |
| `RabbitMqPassword-Prod-Nfl` | legacy `rabbitmq-nfl` cluster password | **rotate** to `rabbitmq-football-nfl-default-user` password |

### `_common-variables.ps1` delta — Round 2

| Variable today | Variable after Round 2 |
|---|---|
| `$rmqUsernameNcaaProd` / `$rmqPasswordNcaaProd` (legacy `rabbitmq` cluster creds; used by sports-data-provision scripts) | **rotate value** to `rabbitmq-football-ncaa-default-user`. Optionally rename to `$rmqUsernameFootballNcaaProd` for clarity, but the existing name is still defensible (NCAA's broker, whatever it's called). |
| `$rmqUsernameNflProd` / `$rmqPasswordNflProd` (legacy `rabbitmq-nfl` creds) | **rotate value** to `rabbitmq-football-nfl-default-user`. Same rename note as above. |
| (none today) | NEW conceptually: `$rmqUsernameApiProd` / `$rmqPasswordApiProd` was added in Round 1 — already there, unchanged. |

### Round 2 migration sequence

Five phases mirroring Round 1. Phases 1–3 additive. Phase 4 cutover.
Phase 5 destructive.

#### Phase 1 — Provision new clusters + pre-declare source exchanges

In `sports-data-config/app/base/rabbitmq/`:

- Add `rabbitmq-cluster-football-ncaa.yaml` (mirror of
  `rabbitmq-cluster-baseball-mlb.yaml`).
- Add `rabbitmq-cluster-football-nfl.yaml` (same).
- Update `kustomization.yaml` to include both new files.

Commit + push. Flux brings up two more brokers; six broker pods in
`messaging` briefly (api, baseball-mlb, ncaa, nfl, football-ncaa,
football-nfl).

**Verification:** `kubectl get rabbitmqcluster -n messaging` shows
both new clusters `AllReplicasReady=True` and `ReconcileSuccess=True`.

**Pre-declare the four source exchanges** before any shovel CRD lands
(applies the Round 1 lesson up-front):

```bash
for cluster in rabbitmq-football-ncaa rabbitmq-football-nfl; do
  U=$(kubectl get secret ${cluster}-default-user -n messaging -o jsonpath='{.data.username}' | base64 -d)
  P=$(kubectl get secret ${cluster}-default-user -n messaging -o jsonpath='{.data.password}' | base64 -d)
  for exch in \
    "SportsData.Core.Eventing.Events.Contests.Football:FootballPlayCompleted" \
    "SportsData.Core.Eventing.Events.Contests:ContestStatusChanged"; do
    kubectl exec -n messaging ${cluster}-server-0 -- rabbitmqadmin \
      -u "$U" -p "$P" declare exchange name="$exch" type=fanout durable=true
  done
done
```

Idempotent — re-running is safe.

#### Phase 2 — IngressRoute swap (additive then destructive)

The football IngressRoutes (`rabbitmq-ncaa-http/https`,
`rabbitmq-nfl-http/https`) currently route
`messaging-footballncaa.sportdeets.com` and
`messaging-footballnfl.sportdeets.com` to the legacy `rabbitmq` and
`rabbitmq-nfl` services. Rename + repoint:

- Edit `app/base/rabbitmq/ingressroute.yaml`: rename
  `rabbitmq-ncaa-{http,https}` → `rabbitmq-football-ncaa-{http,https}`
  and change `services[0].name` to `rabbitmq-football-ncaa`.
  Same for NFL.

Certificate names (`messaging-footballncaa-sportdeets-tls`,
`messaging-footballnfl-sportdeets-tls`) stay the same — hostnames
unchanged, cert reused.

Commit + push. Flux prunes the old IngressRoutes and applies the new
ones. URL keeps working through the cutover (cert-manager doesn't
re-issue; service backend swap is instant).

#### Phase 3 — Pre-create the new shovels (additive)

Under `app/base/rabbitmq/shovels/`, add four new shovel files:

- `shovel-football-play-completed-ncaa-to-api.yaml`
- `shovel-contest-status-changed-ncaa-to-api.yaml`
- `shovel-football-play-completed-nfl-to-api.yaml`
- `shovel-contest-status-changed-nfl-to-api.yaml`

NCAA shovels reference `shovel-football-ncaa-to-api-credentials`.
NFL shovels reference `shovel-football-nfl-to-api-credentials`.
Both Secrets created out-of-band in Phase 4.

Update `app/base/rabbitmq/shovels/kustomization.yaml` and
`shovels/README.md` (extend the kubectl recipe to cover both new
Secret names).

Until Phase 4 creates the credentials Secrets, the four new shovels
fail to reconcile (expected; same as Round 1 Phase 3).

#### Phase 4 — Cutover (App Config flip + service restart wave)

1. **Capture new cluster credentials.** Decode `username` and
   `password` from `rabbitmq-football-ncaa-default-user` and
   `rabbitmq-football-nfl-default-user`. Update
   `_common-variables.ps1` (rotate `$rmqUsername/PasswordNcaaProd`
   and `$rmqUsername/PasswordNflProd` values).

2. **Create the two shovel credentials Secrets** via the kubectl
   recipe (with `%2F` vhost suffix and `prune=disabled` annotation):
   - `shovel-football-ncaa-to-api-credentials`
   - `shovel-football-nfl-to-api-credentials`

3. **Apply App Config delta.** Push manifest changes for:
   - `Prod.FootballNcaa` label: `RabbitMq:Host`,
     `ManagementApiBaseUrl`, `Username` → flip to football-ncaa values.
     `Password` KV ref unchanged.
   - `Prod.FootballNfl` label: same flip to football-nfl values.

4. **Rotate the two existing KV secret values.** After step 3 lands,
   rotate `RabbitMqPassword-Prod-Ncaa` value to the new
   football-ncaa password, and `RabbitMqPassword-Prod-Nfl` value to
   football-nfl. Running pods cache existing creds in their bus; the
   rotated value only matters at pod restart.

5. **Rolling restart NCAA + NFL deployments.** 12 deployments:
   - `producer-football-ncaa-{api,ingest,worker}` (3)
   - `provider-football-ncaa-{api,ingest,worker}` (3)
   - `producer-football-nfl-{api,ingest,worker}` (3)
   - `provider-football-nfl-{api,ingest,worker}` (3)

   `api-all` does **not** need a restart in Round 2 — API stays on
   `rabbitmq-api` (Round 1's terminal state). MLB pods also stay put.

6. **Verify shovel reconciliation.** All 4 new shovels show
   `Ready=True` (CRD) and `running` (`rabbitmqctl shovel_status` on
   `rabbitmq-api`). Pre-declare from Phase 1 means no
   `NOT_FOUND` exchange failures.

7. **Smoke test.** Walk through an NFL or NCAA Provider sourcing
   trigger; confirm DocumentRequested/DocumentCreated round-trip on
   the new broker. If any live football test data is available,
   confirm a FootballPlayCompleted event lands on `rabbitmq-api` and
   reaches the API consumer.

#### Phase 5 — Decommission `rabbitmq` and `rabbitmq-nfl`

1. **Verify zero consumers + zero open connections** on both legacy
   brokers. Expected: empty (all NCAA + NFL pods moved in Phase 4).
   Leftover queue messages (DLQ, error queues) — apply the same
   "nuke it" stance as Round 1 Phase 5 unless user objects.

2. **Delete both legacy `RabbitmqCluster` resources.** Remove
   `rabbitmq-cluster.yaml` and `rabbitmq-cluster-nfl.yaml` from
   `kustomization.yaml`; delete the files. Flux prunes
   StatefulSets, Services, PVCs, the two `*-default-user` Secrets,
   the legacy IngressRoutes (already pruned in Phase 2).

3. **Delete now-orphaned legacy Certificates** —
   `messaging-footballncaa-sportdeets` and
   `messaging-footballnfl-sportdeets` stay (hostname unchanged, new
   IngressRoutes still reference them).

4. **No KV secret deletions in Round 2.** All four
   `RabbitMqPassword-Prod*` secrets remain in use post-Round-2.

5. **Optional `_common-variables.ps1` renames.** If we want clean
   naming, rename `$rmqUsernameNcaaProd` →
   `$rmqUsernameFootballNcaaProd` and same for NFL. Cosmetic; defer
   if the rename touches many callers.

### Round 2 rollback story

| Phase reached | Reversible? | How |
|---|---|---|
| 1 (new clusters + pre-declare) | Yes | Delete the two new cluster files. Pre-declared exchanges are harmless idle. |
| 2 (IngressRoutes renamed) | Yes | Revert the file change — old IngressRoute names + service backends restored. |
| 3 (shovel files added) | Yes | Delete the four new shovel files. Credentials Secrets don't exist yet → non-functional shovels disappear cleanly. |
| 4 (App Config flipped + KV rotated + pods restarted) | Partial | Flip App Config labels back to legacy hosts/users. Rotate KV values back (KV version history has the prior values — verify before overwriting). Restart NCAA + NFL pods. Messages that flowed during the new-broker window are lost. |
| 5 (legacy clusters deleted) | No | Same as Round 1 Phase 5 — queue state non-recoverable. |

### Round 2 open questions

All three resolved with user 2026-05-12:

1. **NCAA / NFL workload activity right now.** ✅ Resolved — no
   active workloads. Restart wave can happen any time.
2. **Legacy `rabbitmq` cluster's `ReconcileSuccess=False` state.**
   ✅ Resolved — root cause is `"shrinking persistent volumes is not
   supported"`. The `persistence.storage` field in
   `rabbitmq-cluster.yaml` was reduced below the provisioned PVC
   size at some point (failing reconcile since 2026-02-27). Kubernetes
   doesn't allow PVC shrinks, so the operator retries every ~5 min
   and gives up with the same error. Pods are healthy; broker
   serves traffic. Phase 5 cluster deletion goes through
   owner-reference teardown (not the PVC scaler) so this error
   doesn't block prune. Same condition likely applies to
   `rabbitmq-nfl` if anyone shrunk its storage too — worth a check
   but not blocking.
   - Separate, also-benign condition: `MemoryRequestAndLimitDifferent`
     since 2026-01-25 (memory request ≠ limit). Recommendation, not
     error.
3. **Cross-service event audit.** ✅ Confirmed correct per user —
   FootballPlayCompleted + ContestStatusChanged are the only ones
   needing shovels for Round 2.

### Round 2 files involved

| Repo | Path | Action |
|---|---|---|
| sports-data-config | `app/base/rabbitmq/rabbitmq-cluster-football-ncaa.yaml` | NEW (Phase 1) |
| sports-data-config | `app/base/rabbitmq/rabbitmq-cluster-football-nfl.yaml` | NEW (Phase 1) |
| sports-data-config | `app/base/rabbitmq/kustomization.yaml` | edit (Phase 1 add; Phase 5 remove `rabbitmq-cluster.yaml` + `rabbitmq-cluster-nfl.yaml`) |
| sports-data-config | `app/base/rabbitmq/ingressroute.yaml` | edit (Phase 2 rename + repoint) |
| sports-data-config | `app/base/rabbitmq/shovels/shovel-football-play-completed-ncaa-to-api.yaml` | NEW (Phase 3) |
| sports-data-config | `app/base/rabbitmq/shovels/shovel-contest-status-changed-ncaa-to-api.yaml` | NEW (Phase 3) |
| sports-data-config | `app/base/rabbitmq/shovels/shovel-football-play-completed-nfl-to-api.yaml` | NEW (Phase 3) |
| sports-data-config | `app/base/rabbitmq/shovels/shovel-contest-status-changed-nfl-to-api.yaml` | NEW (Phase 3) |
| sports-data-config | `app/base/rabbitmq/shovels/kustomization.yaml` | edit (Phase 3) |
| sports-data-config | `app/base/rabbitmq/shovels/README.md` | edit (Phase 3; add the two new credentials recipes) |
| sports-data-config | `app/base/rabbitmq/rabbitmq-cluster.yaml` | delete (Phase 5) |
| sports-data-config | `app/base/rabbitmq/rabbitmq-cluster-nfl.yaml` | delete (Phase 5) |
| sports-data-provision | `util/appconfig/manifest.json` | edit (Phase 4; `Prod.FootballNcaa`, `Prod.FootballNfl` labels) |
| sports-data-provision | `_secrets/_common-variables.ps1` | edit (Phase 4 rotate Ncaa + Nfl values; optional Phase 5 rename) |

