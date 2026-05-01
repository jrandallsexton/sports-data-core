# Event Surface Overview

A complete map of integration and saga events emitted across the sports-data platform: who publishes them, who consumes them, and the wired/orphaned/broken status of each.

> **Generated 2026-05-01** by code-grep across `src/` (Producer, API, Provider, Venue, Core). The per-event docs under `docs/events/` and the catalog at `docs/INTEGRATION_EVENTS.md` predate this and are partially stale — see [§7](#7-existing-event-documentation) for what to trust.

> **Want to follow a full chain across multiple events and services?** See `docs/events/flows/` — currently:
> - [Competitor Score Update → Live UI Push](events/flows/competitor-score-flow.md)

---

## 1. Health summary

- **47 event types defined** in `SportsData.Core/Eventing/Events/` (43 integration + 3 root-level test/infra + 1 API-local stub) plus 1 Provider-local saga event.
- **24 of 47 are actively published** somewhere in production code paths (after the verification pass — the initial publisher-grep agent missed several Provider-side and API-side publishers).
- **18 of 47 have a registered consumer.**
- **23 events are defined but never published** (~half the catalog) — stubs from when the architecture was sketched.
- **1 remaining critical wiring problem** (see [§2](#2-critical-issues--likely-bugs)). Two others (§2.1 and §2.2) were resolved 2026-05-01 — the live-score broadcast chain is now end-to-end.

| Status | Count | Meaning |
|---|---|---|
| ✅ Published + consumed (registered) | 15 | Wired end-to-end |
| 📤 Published, no consumer | 14 | Fire-and-forget; intentional or dead-letter waiting for a feature |
| ⚠️ Published, consumer exists but **NOT registered** | 0 | None remain after 2026-05-01 fix |
| 🔇 Published, consumer **disabled** (commented out) | 1 | Intentional disable |
| 📥 Consumed, no publisher | 0 | After verification, none |
| 💀 Defined but never published or consumed | 17 | Dead stubs |

---

## 2. Critical issues (likely bugs)

These are the items most worth attention. None of these is a code change request — flagging them so an informed decision can be made.

### 2.1 ✅ RESOLVED 2026-05-01 — `CompetitorScoreUpdatedConsumer` was not registered

Refactored to a thin Ingest shim that enqueues `CompetitorScoreUpdatedConsumerHandler` (new Hangfire job class on Worker). Consumer is now registered in `Program.cs:122`. The handler does the `Contest.HomeScore`/`AwayScore` update and publishes `ContestScoreChanged` — preserving the publish-before-save outbox order. See [Competitor Score Flow](events/flows/competitor-score-flow.md) for the end-to-end trace and [`feedback_ingest_consumer_thin_shim`](../../../Users/Randall/.claude/projects/C--Projects-sports-data/memory/feedback_ingest_consumer_thin_shim.md) memory for the rule that drove the shim shape.

### 2.2 ✅ RESOLVED 2026-05-01 — `ContestScoreChangedHandler` was not registered

Now registered in API's `Program.cs` consumer list. SignalR `Clients.All.SendAsync(...)` fires when a `ContestScoreChanged` event lands. With §2.1 resolved upstream, the live-score broadcast chain is wired end-to-end for the first time.

### 2.3 `DocumentDeadLetterConsumer` is disabled

- **Defined:** `src/SportsData.Producer/Application/Documents/DocumentDeadLetterConsumer.cs:11`
- **Disabled at:** `src/SportsData.Producer/Program.cs:124` (commented out of the consumer registration list).
- **What it would do:** log error-level observation when a document is dead-lettered after max retries.
- **Note:** per CLAUDE.md, "DLQ is intentional" — documents land there when dependencies aren't sourced yet. This consumer was monitoring-only, not retrying. The disable may be deliberate (signal-to-noise) but means there is no automatic alert on dead-lettered documents; everything is manual replay via endpoint.

### 2.4 Three events bypass the modern shape convention

- `ConferenceUpdated`, `ConferenceSeasonUpdated`, and `Heartbeat` are old-style `class` types with stringly-typed properties (`Id`, `Name`) instead of `record` inheritors of `EventBase` with `(payload, Ref, Sport, SeasonYear, CorrelationId, CausationId)`. None of the three is currently published, so this is dead code, but if Conference events ever come back to life they should be re-shaped to match the rest of the catalog.

---

## 3. Event matrix

Bucket → Event → Publisher(s) → Consumer(s) → Status. `file:line` references throughout.

### Athletes

| Event | Publisher(s) | Consumer(s) | Status |
|---|---|---|---|
| `AthleteCreated` | `FootballAthleteDocumentProcessor:109`, `BaseballAthleteDocumentProcessor:97` (Producer) | — | 📤 emit-only |
| `AthletePositionCreated` | `AthletePositionDocumentProcessor:141` (Producer) | — | 📤 emit-only |
| `AthletePositionUpdated` | `AthletePositionDocumentProcessor:213` (Producer) | — | 📤 emit-only |

### Conferences

| Event | Publisher(s) | Consumer(s) | Status |
|---|---|---|---|
| `ConferenceCreated` | — | — | 💀 dead stub |
| `ConferenceSeasonCreated` | — | — | 💀 dead stub |
| `ConferenceSeasonUpdated` | — | — | 💀 dead stub (also old-shape, see §2.4) |
| `ConferenceUpdated` | — | — | 💀 dead stub (also old-shape, see §2.4) |

### Contests

| Event | Publisher(s) | Consumer(s) | Status |
|---|---|---|---|
| `CompetitionPlayCompleted` | `EventCompetitionPlayDocumentProcessor:175`, `BaseballEventCompetitionPlayDocumentProcessor:133` (Producer) | — | 📤 emit-only |
| `CompetitionStatusChanged` | `EventCompetitionStatusDocumentProcessor:110`, `BaseballEventCompetitionStatusDocumentProcessor:144`, `ContestReplayService:67,84` (Producer) | — | 📤 emit-only |
| `CompetitionWinProbabilityChanged` | `EventCompetitionProbabilityDocumentProcessor:126` (Producer) | — | 📤 emit-only |
| `CompetitorScoreUpdated` | `EventCompetitionCompetitorScoreDocumentProcessor:133,168` (Producer) | `CompetitorScoreUpdatedConsumer` (Producer Ingest, thin shim) → `CompetitorScoreUpdatedConsumerHandler` (Producer Worker, Hangfire job) | ✅ wired (updates `Contest.HomeScore/AwayScore`; publishes `ContestScoreChanged`) |
| `ContestCreated` | `EventDocumentProcessorBase:114` (Producer) | — | 📤 emit-only |
| `ContestEnrichmentCompleted` | `FootballContestEnrichmentProcessor:173`, `BaseballContestEnrichmentProcessor:146` (Producer) | — | 📤 emit-only |
| `ContestOddsCreated` | `EventCompetitionOddsDocumentProcessor:163`, `BaseballEventCompetitionOddsDocumentProcessor:256` (Producer) | — | 📤 emit-only |
| `ContestOddsUpdated` | `EventCompetitionOddsDocumentProcessor:173`, `BaseballEventCompetitionOddsDocumentProcessor:245` (Producer) | `ContestOddsUpdatedHandler` (API) | ✅ wired (SignalR broadcast) |
| `ContestRecapArticlePublished` | `ContestRecapProcessor:121` (API) | `ContestRecapArticlePublishedHandler` (API) | ✅ wired (SignalR broadcast) |
| `ContestScoreChanged` | `CompetitorScoreUpdatedConsumerHandler` (Producer Worker) | `ContestScoreChangedHandler` (API Ingest) | ✅ wired (SignalR broadcast — see [flow doc](events/flows/competitor-score-flow.md)) |
| `ContestStartTimeUpdated` | `EventCompetitionDocumentProcessorBase:202` (Producer) | `ContestStartTimeUpdatedHandler` (API) | ✅ wired (updates `PickemGroupMatchup.StartDateUtc`) |
| `ContestStatusChanged` | `ContestUpdateProcessor:106` (Producer) | `ContestStatusChangedHandler` (API) | ✅ wired (SignalR broadcast) |

### Documents

| Event | Publisher(s) | Consumer(s) | Status |
|---|---|---|---|
| `DocumentCreated` | `ResourceIndexItemProcessor:348,404,445`, `PublishDocumentEventsProcessor:121` (Provider); also `OutboxTestController` test endpoints; `ProcessDocumentCommandExtensions:9` (Producer wrapper) | `DocumentCreatedHandler` (Producer) | ✅ wired (cross-service: Provider → Producer; enqueues Hangfire processing) |
| `DocumentDeadLetter` | `DocumentCreatedHandler:80` (Producer, after max-retry) | `DocumentDeadLetterConsumer` (Producer) | 🔇 **consumer disabled (§2.3)** |
| `DocumentProcessingCompleted` | `DocumentProcessorBase:387` (Producer, when `NotifyOnCompletion=true`) | — | 📤 emit-only |
| `DocumentRequested` | `DocumentProcessorBase:358` (Producer base, ~47 processors), `FootballCompetitionStreamer:492`, `EventCompetitionDriveDocumentProcessor:199`, `RefreshCompetitionDrivesCommandHandler:110` (Producer) | `DocumentRequestedHandler` (Provider) | ✅ wired (cross-service: Producer → Provider; spawns sourcing jobs) |
| `DocumentSourcingStarted` | — | — | 💀 dead stub |
| `DocumentUpdated` | — | — | 💀 dead stub (inherits `DocumentCreated`) |
| `RequestedDependency` | — (not an event; sub-record used as a property on `DocumentCreated`) | — | n/a |

### Franchise

| Event | Publisher(s) | Consumer(s) | Status |
|---|---|---|---|
| `FranchiseCreated` | `FranchiseDocumentProcessor:167` (Producer) | — | 📤 emit-only |
| `FranchiseSeasonCreated` | `TeamSeasonDocumentProcessor:159` (Producer) | — | 📤 emit-only |
| `FranchiseSeasonEnrichmentCompleted` | `EnrichFranchiseSeasonHandler:78` (Producer) | — | 📤 emit-only |
| `FranchiseSeasonRecordCreated` | `TeamSeasonRecordDocumentProcessor:89` (Producer) | — | 📤 emit-only |
| `FranchiseUpdated` | `FranchiseDocumentProcessor:275` (Producer) | — | 📤 emit-only |

### Images

| Event | Publisher(s) | Consumer(s) | Status |
|---|---|---|---|
| `ProcessImageRequest` | `FootballAthleteDocumentProcessor:83,303`, `BaseballAthleteDocumentProcessor:81,263`, `AthleteSeasonDocumentProcessor:236`, `BaseballAthleteSeasonDocumentProcessor:277`, `VenueDocumentProcessor:90`, `TeamSeasonDocumentProcessor:328`, `FranchiseDocumentProcessor:95` (Producer, several via `PublishBatch`) | `ProcessImageRequestedHandler` (Producer) | ✅ wired (intra-service via bus; enqueues `ImageRequestedProcessor` Hangfire job) |
| `ProcessImageResponse` | `VenueImageRequestProcessor`, `GroupSeasonLogoRequestProcessor`, `FranchiseSeasonLogoRequestProcessor`, `FranchiseLogoRequestProcessor`, `AthleteImageRequestProcessor` (Producer; 9 sites total) | `ProcessImageResponseHandler` (Producer) | ✅ wired (intra-service; enqueues `ImageProcessedProcessor` Hangfire job) |

### PickemGroups

| Event | Publisher(s) | Consumer(s) | Status |
|---|---|---|---|
| `PickemGroupCreated` | `CreateFootballNcaaLeagueCommandHandler:144`, `CreateFootballNflLeagueCommandHandler:139`, `CreateBaseballMlbLeagueCommandHandler:139` (API) | `PickemGroupCreatedHandler` (API) | ✅ wired (intra-service; creates `PickemGroupWeek` + enqueues schedule job) |
| `PickemGroupMatchupAdded` | `AddMatchupCommandHandler:149` (API) | `PickemGroupMatchupAddedHandler` (API) | ✅ wired (intra-service; enqueues `MatchupPreviewProcessor`) |
| `PickemGroupWeekMatchupsGenerated` | `MatchupScheduleProcessor:195` (API) | `PickemGroupWeekMatchupsGeneratedHandler` (API) | ✅ wired (intra-service; per-contest preview enqueue) |
| `PickemGroupWeekContestsGenerated` (API-local stub) | — | — | 💀 dead stub (empty class in `SportsData.Api.Application.Events`) |

### Positions

| Event | Publisher(s) | Consumer(s) | Status |
|---|---|---|---|
| `PositionCreated` | — | — | 💀 dead stub |

### Previews

| Event | Publisher(s) | Consumer(s) | Status |
|---|---|---|---|
| `PreviewGenerated` | `MatchupPreviewProcessor:207` (API) | `PreviewGeneratedHandler` (API) | ✅ wired (intra-service; SignalR broadcast) |

### Venues

| Event | Publisher(s) | Consumer(s) | Status |
|---|---|---|---|
| `VenueCreated` | `VenueDocumentProcessor:102` (Producer) | `VenueCreatedHandler` (Venue service) | ✅ wired (cross-service: Producer → Venue; persists `Venue` entity) |
| `VenueUpdated` | `VenueDocumentProcessor:189` (Producer) | — | 📤 emit-only |

### Test / infrastructure

| Event | Publisher(s) | Consumer(s) | Status |
|---|---|---|---|
| `Heartbeat` | — | — | 💀 dead stub (also old-shape) |
| `OutboxTestEvent` | `OutboxTestController` (Producer/Provider — manual triggers) | `OutboxTestEventHandler` (Producer) | ✅ wired (logs only — outbox self-test) |
| `LoadTestProducerEvent` | `OutboxTestController` (Producer manual trigger) | `LoadTestProducerEventConsumer` (Producer) | ✅ wired (KEDA autoscale validation; enqueues `ProcessLoadTestJob`) |
| `LoadTestProviderEvent` | `OutboxTestController` (Provider manual trigger) | `LoadTestProviderEventConsumer` (Provider) | ✅ wired (KEDA autoscale validation; enqueues `ProcessLoadTestJob`) |

---

## 4. Saga-internal events

These do not live in `Core/Eventing/` and are not part of the platform-wide event vocabulary. They are listed here so the catalog is complete.

| Event | Defined | Publisher(s) | Consumer(s) | Status |
|---|---|---|---|---|
| `TriggerTierSourcing` (Provider-local) | `HistoricalSeasonSourcingSaga.cs:293` | `HistoricalSeasonSourcingSaga` (Provider, 4 publish sites at lines 73, 126, 172, 218) | `TriggerTierSourcingConsumer` (Provider) | ✅ wired (saga state-machine internal — consumer kicks off `ResourceIndexJob` inline for the next tier) |

If other sagas grow event-shaped messages, they belong in this section.

---

## 5. Conventions observed

**Modern shape** (~95% of events):

```csharp
public record EventName(
    PayloadDto Canonical,
    Uri? Ref,
    Sport Sport,
    int? SeasonYear,
    Guid CorrelationId,
    Guid CausationId
) : EventBase;
```

`CorrelationId` + `CausationId` propagate through the bus for distributed tracing — see `docs/MESSAGE_TRACING_IDENTIFIERS.md`. The `Sport` and `SeasonYear` fields enable per-sport/per-season filtering at the consumer side (none currently filter, but the data is there).

**Producer triggers fall into a small set of patterns:**

1. **`DocumentProcessorBase<T>` lifecycle hooks** — base class publishes `DocumentRequested` (for child docs), `DocumentProcessingCompleted` (when requested), and gives subclasses access to `_publishEndpoint` for domain events. ~47 concrete processors flow through here.
2. **Sport-specific `EventCompetition*DocumentProcessor` pairs** — one Football impl + one Baseball impl publish the same canonical event (e.g., `ContestOddsCreated` is published by both `EventCompetitionOddsDocumentProcessor` and `BaseballEventCompetitionOddsDocumentProcessor`). When adding a new sport, both halves of these pairs typically need a new sport-specific processor.
3. **Enrichment processors** publish `*EnrichmentCompleted` events at end-of-pipeline.
4. **Job/handler endpoints** (`ContestUpdateProcessor`, `RefreshCompetitionDrivesCommandHandler`, etc.) publish `DocumentRequested` to trigger sourcing.

**Consumer behaviors fall into a small set:**

| Behavior | Count | Examples |
|---|---|---|
| Enqueue Hangfire job | 7 | `DocumentCreatedHandler`, `ProcessImageRequestedHandler`, `PickemGroupCreatedHandler` |
| SignalR broadcast (API → web clients) | 5 | `ContestOddsUpdatedHandler`, `ContestStatusChangedHandler`, `PreviewGeneratedHandler` |
| Direct DB write | 2 | `ContestStartTimeUpdatedHandler`, `VenueCreatedHandler` |
| Log only (monitoring) | 1 | `OutboxTestEventHandler` |
| Inline orchestration | 1 | `TriggerTierSourcingConsumer` (saga-driven) |

**Cross-service vs. intra-service publishes:**

- **Cross-service:** `DocumentRequested` (Producer→Provider), `DocumentCreated` (Provider→Producer), `VenueCreated` (Producer→Venue). These are the only true cross-service event flows.
- **Intra-service over the bus:** The bulk of consumed events are published *and* consumed within the same service (Producer-internal image pipeline; API-internal pickem flows; API-internal SignalR fan-out). The bus is acting as an in-process workflow engine in those cases — could be replaced by direct method calls, but the bus gives idempotency + outbox guarantees per CLAUDE.md.

---

## 6. Recommendations

Surfaced for discussion, not as a decided plan:

1. **Decide the fate of §2.1 + §2.2 (live-score broadcast).** Either register the two consumers and turn on real-time score push to web clients, or delete the consumer classes and the `ContestScoreChanged` event so the catalog reflects reality. Half-states are misleading.
2. **Decide the fate of §2.3 (`DocumentDeadLetterConsumer`).** If the manual-replay model from CLAUDE.md is the intent, delete the disabled consumer instead of leaving commented-out code. If alerts are wanted, register it (and route logs to Seq/alerting).
3. **Delete the 17 dead-stub events** (Conference quartet, Position, AthletePosition pair, FranchiseUpdated/SeasonCreated/SeasonRecordCreated/SeasonEnrichmentCompleted, DocumentSourcingStarted, DocumentUpdated, Heartbeat, PickemGroupWeekContestsGenerated, the `Venue*` updates if not coming back). Or pick the ones that have an imminent consumer story and keep them. The current half-populated `Eventing/Events/` folder is hard to read because half the files are aspirational.
4. **Consider whether the 14 emit-only events with no consumer are intentional.** Many of them (`AthleteCreated`, `FranchiseCreated`, `CompetitionPlayCompleted`, `ContestEnrichmentCompleted`, etc.) look like they were designed for a future feature (read-model projector? notification trigger?). If those features aren't on the roadmap soon, the events are pure overhead — every one is serialized to the outbox, transmitted via RabbitMQ/Service Bus, and discarded.
5. **Revisit the `ConferenceUpdated`/`ConferenceSeasonUpdated`/`Heartbeat` shape** if Conference events are revived. Bring them in line with the modern record + `EventBase` convention.

---

## 7. Existing event documentation

What already exists in `docs/`:

- **`docs/INTEGRATION_EVENTS.md`** — catalog of ~40 events organized by domain. **Stale**: missing `LoadTestProducerEvent`, `LoadTestProviderEvent`, `PickemGroupWeekContestsGenerated`. Doesn't list publishers or consumers — just names.
- **`docs/events/{domain}/{Event}.md`** — per-event docs with Mermaid sequence diagrams. Each ends with a "(No Consumer Configured)" or analogous note. **Mostly stale** — they were generated when the event was added and not refreshed since. Several `(No Consumer Configured)` notes are wrong (consumers have been added since), and some new events have no per-event doc at all.

This document is the canonical source until the per-event docs catch up. When updating an event's wiring (registering a consumer, adding a publisher, deleting a stub), update both this doc and the relevant `docs/events/` file.

---

## 8. How this was generated

Three parallel exploration agents:

1. **Inventory agent** — grep for class/record declarations under `src/SportsData.Core/Eventing/Events/` and any service-local equivalents.
2. **Publisher agent** — grep for `Publish<T>`, `_bus.Publish`, `IPublishEndpoint.Publish`, `context.Init<T>`, and `new EventName(` across all services. Initial pass missed several cross-service publishers (Provider's `DocumentCreated`, API's `PickemGroupCreated` from league-create handlers, etc.); a follow-up `new EventName(` grep filled the gaps.
3. **Consumer agent** — grep for `IConsumer<T>` implementations across all services + read each service's `Program.cs` consumer registration list to detect unregistered/disabled consumers.

If anything in this doc is wrong, the most likely cause is that a new publisher or consumer has been added since 2026-05-01. Re-run the same three agents.
