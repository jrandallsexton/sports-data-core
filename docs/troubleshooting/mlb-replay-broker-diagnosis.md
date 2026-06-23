# MLB replay — broker diagnosis (2026-05-11)

Investigation notes for why `BaseballContestReplayService` events
publish from the Producer Worker pod but never reach the API
`BaseballPlayCompletedHandler` consumer in production.

**Status as of 2026-05-11 end-of-thread:** root cause confirmed.
Decision pending on which of four remediation options to take.

---

## Symptom

- Trigger: `POST /admin/baseball/contests/{contestId}/replay` against
  a known-good MLB contest in prod.
- Producer Seq shows the replay running (`BaseballReplay: starting`,
  per-play `BaseballReplay: published BaseballPlayCompleted`).
- Browser console shows nothing arriving on `BaseballPlayCompleted`.
- Admin SignalR debug card on the same page works fine in the same
  browser session — `BaseballPlayCompleted` debug pushes do arrive.

## What's been ruled out

### Browser ↔ SignalR backplane mismatch
- Prod browser opens `wss://sportdeets-prod.service.signalr.net/...`
- Local browser opens `wss://sportdeets-dev.service.signalr.net/...`
- Debug-card broadcasts (which go API → broker → API consumer → SignalR)
  do reach the prod browser, proving the prod API pod and the prod
  browser are on the same Azure SignalR (`sportdeets-prod`).
- `CommonConfig:AzureSignalR:ConnectionString` has a single value in
  App Config — there isn't a per-service split here, so no chance of
  a SignalR backplane mismatch.

### Outbox dispatch (PR #313)
- Pre-fix theory: Producer Worker's EF outbox rows weren't being
  dispatched to the broker in role-split prod.
- Fix landed in PR #313 — `BaseballContestReplayService` now wraps
  publishes in `IMessageDeliveryScope.Use(DeliveryMode.Direct)`,
  same pattern as the Admin SignalR-debug endpoints that work in
  prod.
- After deploy: **zero change** in observed behavior. Still no
  `BaseballPlayCompleted consume: received` entries on the API side
  for replay-origin events.

### Code-level bugs in the consumers
- PR #312 added Info-level diagnostic logging to all three API
  consumers (entry + post-`SendAsync` log lines).
- API consumer is healthy: every `BaseballPlayCompleted` it receives
  also logs `SendAsync completed`. 9 of 9 in the audited window.
  All 9 carry the debug-broadcaster `CausationId` — not one came
  from a replay.

### Producer-side exception or shutdown
- Searched for Errors / Fatals in `SportsData.Producer` for the
  replay's CorrelationId and for the entire surrounding time window.
  Zero results. No MassTransit / publish / transport errors. No
  shutdown or restart markers.

### Azure Service Bus connection-string mismatch
- The earlier hypothesis was that Producer and API resolve
  different `CommonConfig:AzureServiceBusConnectionString` values
  from different App Config labels. That hypothesis assumed prod
  was on Azure Service Bus. **It isn't** — see root cause below.
  The Service Bus connection strings in App Config are dead weight
  in prod.

---

## Seq evidence (audited 2026-05-11 UTC)

### Producer side

- Most recent replay run:
  - ContestId: `b25f7b05-e583-698b-e98a-d050ff3d3565`
  - CorrelationId: `8b45abd4-466a-fd7a-587f-c41ac20750d3`
  - Started: `2026-05-11T19:58:35.301Z`
  - Total plays expected: 639
  - Plays actually published (Producer log): 73, sequential, no gaps
  - Last published entry: `EmittedCount=73/639` at
    `2026-05-11T20:01:01.925Z` (~2m26s after start)
  - Role: `Worker` (confirmed on every entry)
- Outcome: **partial — the loop silently halted at play 73**. No
  `"BaseballReplay: completed"` marker. No exception logged. No
  pod restart markers in the surrounding window.
- For the same CorrelationId across all levels: zero Warning, Error,
  Fatal entries.

### API side

- `BaseballPlayCompleted consume: received` entries in the last
  ~2 hours: 9 total. All 9 share a single CausationId
  (`10000000-4000-0000-0000-000000000001`, the debug-broadcaster
  causation). **Zero entries from a replay CausationId.**
- `ContestStatusChanged consume: received` entries in the same
  window: **0**. The replay emits one of these before the play loop;
  none reached the API consumer either.
- `SendAsync completed` entries: 9 BaseballPlayCompleted, 0
  ContestStatusChanged. Every received message also broadcast cleanly
  — no SignalR / hub failures.
- API errors in the window: none related to MassTransit, SignalR,
  hub, consumer, or dead-letter.

---

## Root cause (confirmed via App Config manifest)

Inspected `sports-data-provision/util/appconfig/manifest.json`.
Confirmed two facts that fully explain the symptom:

### 1. Prod uses RabbitMQ, not Azure Service Bus

`CommonConfig:Messaging:UseRabbitMq = "true"` is set under the
**base `Prod`** App Config label. None of the mode-specific labels
(`Prod.All`, `Prod.BaseballMlb`, `Prod.FootballNcaa`,
`Prod.FootballNfl`) override that flag. So `ConfigureTransport` in
`MessagingRegistration.cs` takes the RabbitMQ branch for every prod
pod — the `AzureServiceBusConnectionString` keys in the manifest
are configured but unused.

### 2. Prod has a per-sport RabbitMQ split

`CommonConfig:Messaging:RabbitMq:Host` resolves to **different
clusters** depending on which App Config label a service loads:

| Service (effective label stack) | RabbitMq:Host resolves to |
|---|---|
| API (`Prod` + `Prod.All`) | `rabbitmq.messaging.svc.cluster.local` |
| Producer Football NCAA (`Prod` + `Prod.FootballNcaa`) | `rabbitmq.messaging.svc.cluster.local` *(no override; inherits the base)* |
| Producer Football NFL (`Prod` + `Prod.FootballNfl`) | `rabbitmq-nfl.messaging.svc.cluster.local` |
| Producer Baseball MLB (`Prod` + `Prod.BaseballMlb`) | `rabbitmq-mlb.messaging.svc.cluster.local` |

### Why this produces the symptom

- Producer Baseball publishes `BaseballPlayCompleted` and
  `ContestStatusChanged` to the exchange on **rabbitmq-mlb**.
- The API consumer's queue (`baseball-play-completed-handler`) is
  bound to the matching exchange on the **base rabbitmq**.
- There is no shovel or federation between the two RabbitMQ
  clusters. The published message lands on a `rabbitmq-mlb`
  exchange that has no bound queues for it — silently dropped.
- The debug card works because that flow is API → base rabbitmq
  → API consumer → SignalR. Same broker.
- Producer NCAA → API works because Producer NCAA happens to
  inherit the base `rabbitmq` host (no override in
  `Prod.FootballNcaa`). NFL is silently broken the same way MLB
  is, but nobody has noticed because there's no equivalent NFL
  live-replay surface yet.
- Local dev works because docker-compose has a single RabbitMQ
  container and every service connects to it.

The "halt at play 73" symptom is **independent** of the routing
gap. Even if the replay had run all 639 plays, none of them would
have reached the API consumer. The halt is most plausibly a pod
restart during the PR #313 deploy rollout — check the Hangfire
dashboard for the job's terminal state at
`19:58:35Z` on `2026-05-11`.

---

## Architectural constraint — per-sport broker split is load-bearing

Captured from user 2026-05-11. The per-sport RabbitMQ split is a
deliberate isolation decision, not historical scaffolding. Four
reasons it stays:

1. **Symmetry with the per-service database pattern.** Each service
   already owns its own database; the queues follow the same
   ownership boundary.
2. **Blast-radius isolation.** A RabbitMQ outage or misconfiguration
   on one sport's broker does not take down every sport.
3. **Observability.** Per-sport brokers give per-sport queue depth,
   error rates, and topology — easier to reason about than a
   single multiplexed cluster.
4. **Backlog isolation.** Running a historical sourcing pass on one
   sport (e.g. backfilling NCAA 2019) can saturate that sport's
   broker without blocking live MLB ingestion or any cross-service
   flow on a different sport's broker.

**Consequence for the decision below: Option 1 is off the table.**
Any fix must preserve the per-sport split for everything except
the small set of event types that genuinely need to cross service
boundaries.

## Remediation options

Four options analyzed. Options 1 and 3 are documented for
completeness but ruled out by architectural constraints (per-sport
broker isolation is load-bearing; Producer does not connect to
SignalR).

### ~~Option 1 — Consolidate everything on the base RabbitMQ~~ (REJECTED)

**Why rejected:** would collapse the per-sport isolation that
underpins blast-radius separation, backlog isolation, and
per-sport observability. See "Architectural constraint" above.

Documented here only so future readers see the option was
considered and dismissed for a concrete reason.

(Original analysis: drop the `Messaging:RabbitMq:*` overrides from
`Prod.BaseballMlb` / `Prod.FootballNfl`, decommission the
dedicated clusters. One config change, no code change. Trade: the
isolation properties listed in the constraint section.)

### Option 2 — RabbitMQ federation or shovel for cross-service event types

**What:** Keep the per-sport split. Configure RabbitMQ federation
(or a shovel) so that the cross-service event exchanges
(`BaseballPlayCompleted`, `ContestStatusChanged`, and any future
"Producer-publishes / API-consumes" events) are forwarded from
`rabbitmq-mlb` → `rabbitmq` (and same for `rabbitmq-nfl`).

**Implementation:**
- Stand up a federation upstream from `rabbitmq-mlb` to base
  `rabbitmq` for the specific exchanges that need cross-cluster
  delivery. Same for NFL.
- Update RabbitMQ k8s manifests in `sports-data-config`.
- No app code changes.

**Pros:**
- Preserves per-sport isolation for ingestion-volume traffic.
- Cross-service events flow through; intra-sport events stay on
  the dedicated cluster.

**Cons / risks:**
- Adds a moving part. Federation links can break silently; need
  alerts on the federation state.
- Configuration is per-exchange — every new cross-service event
  type has to be added to the federation policy. Easy to forget
  when introducing a new event.
- Slightly higher latency on cross-broker delivery.
- Operationally more complex than option 1; less complex than
  options 3 and 4.

### ~~Option 3 — Colocate the SignalR fan-out with Producer~~ (REJECTED)

**Why rejected:** Producer never connects to SignalR. That
separation of concerns ("Producer ingests + publishes events;
API broadcasts to clients") is a hard architectural boundary
that does not move for this fix. User 2026-05-11: "Full stop."

Documented only so future readers see the option was considered
and dismissed for a concrete reason.

(Original analysis: register `IHubContext` in Producer Baseball,
move `BaseballPlayCompletedHandler` + `ContestStatusChangedHandler`
into Producer, broadcast from there via the shared
`sportdeets-prod` Azure SignalR backplane. Skips the cross-broker
hop entirely. Trade: Producer gains a SignalR dependency, the
fan-out logic gets duplicated per sport, and the producer/consumer
role boundary blurs.)

### Option 4 — Multi-bus in the API

**What:** Use MassTransit's multi-bus support. API hosts the
existing primary bus on the base `rabbitmq` for general events,
plus one additional bus per dedicated per-sport RabbitMQ cluster
that publishes cross-service events the API needs to consume.

**Implementation:**
- Register `AddMassTransit<IBaseballMlbBus>(...)` (or similar
  marker interface) pointing at `rabbitmq-mlb` in API's
  `Program.cs`, with `BaseballPlayCompletedHandler` and
  `ContestStatusChangedHandler` consumers attached to that bus.
- Same for NFL.
- API now opens one connection per per-sport cluster on top of
  its base connection.

**Pros:**
- No publisher-side change. Producer Baseball keeps publishing
  to its own broker as it does today.
- Preserves per-sport broker isolation.

**Cons / risks:**
- Most complex of the four. MassTransit's multi-bus configuration
  is fiddly — separate `IBusControl` instances, separate hosted
  services, separate `IPublishEndpoint` per bus type. Easy to
  cross wires.
- Adds an external dependency to every per-sport RabbitMQ cluster
  from the API pod. Now the API has to be reachable to all of
  them, and a network blip on any one cluster degrades the API.
- Doesn't scale well as more sports are added — every new sport
  with its own broker means another bus registration and the same
  handlers duplicated.
- Confusing on-call story: "which RabbitMQ cluster is API
  consuming from for event X?" answered differently per event.

---

## Recommendation

With Options 1 and 3 ruled out by the architectural constraints,
the remaining choice is between Options 2 and 4. **Option 2
(RabbitMQ shovel) is the call** — both immediate and long-term —
for two reinforcing reasons.

**1. It's the path of least resistance to unblock prod.** No
application code change. Shovel configuration is hours of work,
not days. The shovel plugin is already a first-class RabbitMQ
feature with operator/CRD support in `sports-data-config`.

**2. It's structurally more flexible than the alternatives
against future evolution.** This is the deeper reason. Any
scheme that classifies events at the C# type level (marker
interface, attribute, namespace convention) bakes a routing
commitment into the event record itself. But "is this event
integration?" is really a property of *who currently subscribes*
— which is mutable. Today's intra-Producer event might gain an
API consumer next year; today's API-bound event might gain a
Producer-internal consumer. Every such change ripples awkwardly
through a class-level classification:

- Re-classify domain → integration: existing intra-Producer
  consumers break (they were listening on the wrong broker).
- Re-classify integration → domain: API breaks.
- Add a Producer-internal consumer for an existing integration
  event: dual-publish, or Producer subscribes cross-broker
  (defeats the blast-radius isolation we just paid for), or
  re-classify.

None of those are clean. The smell is the system telling you the
classification is at the wrong abstraction layer. Events should
not know whether they're "integration"; that's a topology
question, not an event-identity question.

**Option 2 sidesteps this entirely.** Events have no class-level
designation. They live on the publisher's broker, period.
Cross-broker delivery is plumbing. Adding a future
Producer-internal consumer for `BaseballPlayCompleted` is
literally "register a consumer in Producer Baseball" — same
broker, same event, no shovel change, no event-class change.
The classification is implicit in *who subscribes*, not in
*what the event is*.

Option 4 is also viable but loses on two counts: it makes the
API depend on every per-sport broker being reachable (broader
blast radius for the API), and the "which broker is API
consuming X from" question grows in complexity per sport.
Shovels keep the API on one broker.

**Shovels to set up:**

Exchange names confirmed 2026-05-12 by inspecting
`messaging-baseballmlb.sportdeets.com` (the prod `rabbitmq-mlb`
management UI):

- `SportsData.Core.Eventing.Events.Contests.Baseball:BaseballPlayCompleted`
  — sport-specific namespace
- `SportsData.Core.Eventing.Events.Contests:ContestStatusChanged`
  — sport-neutral namespace (lives directly under `Contests`,
  not `Contests.Baseball`); same exchange name will be reusable
  when NFL needs a shovel later

Shovel definitions to create:

- Source `rabbitmq-mlb` → Destination `rabbitmq` for both
  exchanges above.
- Same shape from `rabbitmq-nfl` once NFL gets a live-replay
  surface. Currently silently broken in the same way as MLB
  but symptomless because nothing exercises that path yet.
  Only the `BaseballPlayCompleted`-flavored exchange differs
  (it'll be `FootballPlayCompleted` under the Football
  namespace); `ContestStatusChanged` is identical across sports.

**Trade-offs to accept with Option 2:**

- Per-exchange config: every future cross-service event type
  must be added to the shovel policy. Mitigation: keep the
  shovel definitions next to the event definitions in the same
  source-of-truth repo (e.g. `sports-data-config`) and add a
  short checklist item to the event-creation runbook.
- Slight latency on cross-broker delivery (shovel hop). Not
  meaningful for human-facing UI updates.
- Need observability on the shovel state itself — RabbitMQ
  exposes per-shovel forwarded-message and ack rates; surface
  in Grafana alongside the other broker metrics.

---

## Implementation plan — Option 2 (shovels)

Phased rollout. Phase 0 is done; Phase 1 is the next chunk of
real work.

### Phase 0 — Orient on the deployment shape ✅ DONE

Looked at `sports-data-config/app/base/rabbitmq/`. Confirmed:

- **Deployment model:** RabbitMQ Cluster Operator
  (`apiVersion: rabbitmq.com/v1beta1`, `kind: RabbitmqCluster`).
  That operator also ships topology CRDs (`Queue`, `Exchange`,
  `Binding`, `Policy`, `Shovel`, `Federation`) so shovels can be
  defined declaratively as YAML and reconciled by Flux — no
  imperative `rabbitmqctl set_parameter` scripting required.
- **Three clusters in the `messaging` namespace:** `rabbitmq`
  (base), `rabbitmq-mlb`, `rabbitmq-nfl`.
- **Shovel plugin is enabled on all three clusters.** Each
  cluster's `additionalPlugins` list in its `RabbitmqCluster`
  manifest includes `rabbitmq_shovel`,
  `rabbitmq_shovel_management`, and `rabbitmq_federation`.
  Plugin state is not a blocker.

### Phase 1 — Local two-broker reproduction

The point of Phase 1 is to learn the shovel mechanism on
something disposable AND to capture the exact MassTransit
exchange names by inspection (rather than guessing them from
the message-type URN convention).

The current local dev setup (`docker-compose.local.mlb.yml`)
uses a single RabbitMQ that runs on the Docker Desktop host,
shared via `host.docker.internal`. That single-broker shape is
exactly what hides this bug from local dev — both Producer MLB
and API connect to the same broker, so cross-broker delivery is
trivially satisfied. **For Phase 1 we want local to mirror the
prod per-sport topology.**

Concrete plan: extend `docker-compose.local.mlb.yml` to include
**two** RabbitMQ containers — `rabbitmq` (base) and
`rabbitmq-mlb` — both running `rabbitmq:3.13-management` with
the shovel plugins enabled at container start. Wire the
existing services so the topology matches prod:

| Service | RabbitMq host (in compose) | Why |
|---|---|---|
| `api` | `rabbitmq` | API consumer queue lives here. Same as prod. |
| `producer-ncaa-api` | `rabbitmq` | Inherits base broker, no override. Same as prod. |
| `producer-nfl-api` | `rabbitmq` | Same — NFL split surfaces later when NFL gains a live surface. |
| `producer-mlb` | `rabbitmq-mlb` | Service-level env override mirrors prod's `Prod.BaseballMlb` App Config override. |
| `provider-mlb` | `rabbitmq-mlb` | Provider Baseball also follows the sport-specific broker. |

Sketch of the compose additions:

```yaml
services:
  rabbitmq:
    image: rabbitmq:3.13-management
    container_name: rabbitmq
    ports: ["5672:5672", "15672:15672"]
    command: [
      "bash", "-c",
      "rabbitmq-plugins enable --offline rabbitmq_shovel rabbitmq_shovel_management && rabbitmq-server"
    ]

  rabbitmq-mlb:
    image: rabbitmq:3.13-management
    container_name: rabbitmq-mlb
    ports: ["5673:5672", "15673:15672"]
    command: [
      "bash", "-c",
      "rabbitmq-plugins enable --offline rabbitmq_shovel rabbitmq_shovel_management && rabbitmq-server"
    ]
```

Required wiring changes:

- `x-common-env`: flip `CommonConfig__Messaging__RabbitMq__Host`
  from `host.docker.internal` to `rabbitmq` (the base broker via
  the compose network alias).
- `producer-mlb` service block: add a service-specific
  `CommonConfig__Messaging__RabbitMq__Host: rabbitmq-mlb`
  override. This is the in-compose analog of the prod
  `Prod.BaseballMlb` App Config override.
- `provider-mlb` service block: same override, for symmetry.
- `depends_on` on every dependent service so containers wait
  for the broker (`service_started` or `service_healthy` with a
  TCP probe).
- Management UIs reachable on the host at `localhost:15672`
  (base) and `localhost:15673` (mlb).

**Three things to flag before applying the compose change:**

1. **Port conflict with the host's Docker Desktop RabbitMQ.**
   The compose containers want `localhost:5672` and `:15672`.
   Either stop the standalone Docker Desktop RabbitMQ before
   `docker compose up`, or remap the compose ports. Stopping is
   easier.

2. **Persistence.** Default is ephemeral — every
   `docker compose down` wipes broker state. For a Phase 1
   learning environment that's actually a feature (wipe and
   re-test cleanly). Start without named volumes.

3. **Existing `Local` App Config label.** Currently sets
   `RabbitMq:Host = localhost` and `UseRabbitMq = true`. The
   compose env-var override masks the App Config value, so no
   App Config change is needed — just update the env-var
   values from `host.docker.internal` to the new container
   aliases.

### Phase 1 acceptance criteria

Before Phase 1 is "done":

- [ ] `docker compose -f docker-compose.local.mlb.yml up --build`
  brings up both brokers cleanly; their management UIs are
  reachable on `localhost:15672` and `localhost:15673`.
- [ ] Firing a debug-card broadcast against local API
  reproduces a successful flow on the base broker (sanity).
- [ ] Firing a baseball replay against local Producer MLB
  reproduces the **silent drop** seen in prod — replay
  publishes lands in `rabbitmq-mlb`, API consumer on
  `rabbitmq` sees nothing. **This must reproduce before we
  trust Phase 1 to validate the shovel fix.**
- [ ] Confirm the exact exchange names MassTransit creates by
  inspecting `rabbitmq-mlb`'s exchanges via the management UI
  after the replay run. Record those names for Phase 3.

### Phase 2 — Set up the shovel in the local environment

After Phase 1 reproduces the prod symptom, declare a Shovel via
the RabbitMQ Cluster Operator's `Shovel` CRD (or, for the local
compose case, via the management UI / API since the operator
isn't running locally). Verify cross-broker delivery: replay's
plays now land at the local API consumer; the MatchupCard in
the local browser updates.

### Phase 3 — Write the prod Shovel CRDs ✅ DRAFTED 2026-05-12

Files staged in `sports-data-config` working tree (uncommitted):

- **NEW** `app/base/rabbitmq-topology-operator/kustomization.yaml`
  — installs the Messaging Topology Operator (MTO) v1.18.2 via
  Kustomize remote ref. MTO provides the `Shovel` CRD; the
  existing cluster-operator (`2.19.0`, 106 days old) does not.
  Compatible with cluster-operator 2.10+; cert-manager already
  installed in the cluster.
- **NEW** `app/base/rabbitmq/shovels/shovel-baseball-play-completed.yaml`
- **NEW** `app/base/rabbitmq/shovels/shovel-contest-status-changed.yaml`
- **NEW** `app/base/rabbitmq/shovels/kustomization.yaml`
- **NEW** `app/base/rabbitmq/shovels/README.md` — including the
  one-time `kubectl` recipe to construct the credentials Secret
  from the cluster-operator-managed default-user secrets
- **MODIFIED** `app/base/rabbitmq/kustomization.yaml` — adds
  `shovels` to resources
- **MODIFIED** `app/overlays/04_prod/kustomization.yaml` — adds
  `../../base/rabbitmq-topology-operator` to resources

Credentials decision: shovels use a single combined `Secret`
(`shovel-mlb-to-base-credentials`) with `srcUri` + `destUri` keys,
constructed at runtime via `kubectl` from the existing
`rabbitmq-mlb-default-user` and `rabbitmq-default-user` secrets
that the cluster operator already manages. The combined Secret
is annotated `kustomize.toolkit.fluxcd.io/prune: disabled` so
Flux doesn't prune it. Plaintext credentials never enter git;
they're sourced from operator-managed secrets that auto-rotate
with the cluster.

Same shape for NFL deferred until it gains a live surface — see
README.md in the shovels directory for the add-new-sport
recipe.

### Phase 4 — Deploy + observe in prod

Apply the new manifests. Fire one debug-card broadcast and one
replay against the same contest. Confirm in Seq that
`BaseballPlayCompleted consume: received` now fires for the
replay's `CorrelationId`. Confirm the prod browser console
receives the plays on the MatchupCard.

Capture per-shovel metrics in Grafana (forwarded message count,
ack rate, link state) alongside the other broker metrics so
shovel health is visible.

### Phase 5 — Runbook entry

Short ops doc covering: shovel CRD location, expected metrics,
how to add a new cross-service event type to the shovels, what
to check when cross-service events disappear.

---

## Files involved

| File | Role |
|---|---|
| `src/SportsData.Producer/Application/Contests/BaseballContestReplayService.cs` | Where the replay publishes from. Direct-publish in place (PR #313). |
| `src/SportsData.Core/Eventing/EventBus.cs` | `EventBusAdapter` + `MessageDeliveryPolicy` (AsyncLocal scope). |
| `src/SportsData.Core/DependencyInjection/MessagingRegistration.cs` | MassTransit + transport. Picks RabbitMQ vs ASB based on `CommonConfig:Messaging:UseRabbitMq`. |
| `src/SportsData.Api/Application/Events/BaseballPlayCompletedHandler.cs` | API consumer. Has the diagnostic logging from PR #312. |
| `src/SportsData.Api/Application/Events/ContestStatusChangedHandler.cs` | Same — useful because replay also publishes one of these before the play loop. |
| `src/SportsData.Producer/Program.cs` | Where Producer roles register messaging. |
| `src/SportsData.Api/Program.cs` | Where API registers its consumer list — line ~229. |
| `sports-data-provision/util/appconfig/manifest.json` | **The actual root cause lives here** — `Prod.BaseballMlb` and `Prod.FootballNfl` override `Messaging:RabbitMq:Host` to per-sport clusters. |

## Related PRs

- **#312** — `chore(csp,signalr): CSP Report-Only header + consumer diagnostic logging` (merged + deployed; adds the entry/exit log lines on the three API handlers).
- **#313** — `fix(producer): direct-publish from BaseballContestReplayService` (merged + deployed; didn't change anything because the routing problem is broker-side, not delivery-mode-side).

## Memory entries touched in this thread

- `feedback_wait_for_coderabbit.md` — strengthened to "don't push without explicit signal" (after the `80772ec2` mishap).
- `project_signalr_consumer_logs_downgrade.md` — new, captures the
  follow-up to drop the diagnostic Info logs to Debug once the live-
  replay routing is fixed and MLB season is stable.
