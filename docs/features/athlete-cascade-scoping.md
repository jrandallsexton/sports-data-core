# Athlete cascade scoping — design

**Status:** authorized 2026-09-01; items 1+2 implemented (PR pending)
**Raised:** 2026-08-29, after the first live NCAAFB Saturday queued >1M documents

## The problem, observed

On 2026-08-29 the pipeline accumulated **over a million queued items** across three stages
(Provider Hangfire ~222K, NCAA broker document-requested ~64K, Producer Hangfire ~748K)
while a single pick'em league game sat starved behind it, showing zero plays two hours into
the game. Purging all three restored latency to seconds, but the inflow that produced them
is unchanged and will recur on every Saturday, worse on 09-05/06 with the full slate.

The flood is not one bug. It is four defects that compose into a self-sustaining chain
reaction, each multiplying the next.

### It is a standing backlog, not a game-day spike

The queue does not drain between slates. Sampling at 22:30 UTC on Saturday 08-29 showed the
pipeline still processing plays and athlete cascades for **Friday night's** games:

| ESPN event | Kickoff | Matchup |
|---|---|---|
| 401866616 | 2026-08-28T23:00Z | New Hampshire at UAlbany (CAA) |
| 401868040 | 2026-08-28T22:00Z | William & Mary at Villanova (Patriot League) |
| 401864494 | 2026-08-29T19:00Z | San José State at USC — *the league game* |

More than 24 hours of lag, from a Friday slate consisting of a handful of small-division
games. This is the key severity fact and it changes the risk assessment for 09-05/06 in two
ways:

1. The full FBS Saturday slate will land on a queue that is **already a day behind**, not on
   an empty one.
2. It explains why the league game starved. Live plays for a game users are actively
   watching were queued behind day-old play-by-play for FCS games nobody picked. The
   pipeline has no notion that one of those is worth more than the other.

Which is also why "the queue reached equilibrium" was the wrong read on the night: inflow
matching drain rate at a depth of ~1M is not equilibrium, it is a backlog that never catches
up.

## The chain

```text
Event sourced (all divisions, national slate)
  └─> Competition is new  ──> spawns EventCompetitionPlay + EventCompetitionDrive
        └─> Play processor resolves participants
              └─> AthleteSeason missing ──> PublishDependencyRequest(AthleteSeason)
                    └─> AthleteSeason processor spawns, per athlete:
                          ├─ AthleteImage (headshot)
                          ├─ AthleteSeasonStatistics
                          ├─ AthleteSeasonNote
                          └─ Athlete (if missing)
```

Rough fan-out per game: ~150 plays x ~2-4 participants ≈ several hundred athlete
references, deduping to roughly a roster's worth of unique athletes, each of which spawns
**four more documents**. Multiply by every game in every division — including Division II
and III games no user will ever pick — and the million becomes arithmetic rather than
surprise.

## Defect A — `isNew ||` bypasses the inclusion filter entirely

`EventCompetitionDocumentProcessorBase.cs:372`, `FootballEventCompetitionDocumentProcessor.cs:49`,
`EventCompetitionDriveDocumentProcessor.cs:200`, and the roster/leaders processors all guard
spawning as:

```csharp
if (isNew || ShouldSpawn(DocumentType.EventCompetitionPlay, command))
```

For a **new** entity the filter is not consulted at all. Since bulk sourcing of a fresh
slate means every competition is new, the inclusion filter is unenforceable in precisely
the scenario that generates the load. Any narrowing we set upstream is silently discarded
on first sight of an entity.

## Defect B — a null or empty filter means "spawn everything"

`DocumentProcessorBase.ShouldSpawn` (line 188):

```csharp
if (command.IncludeLinkedDocumentTypes == null || command.IncludeLinkedDocumentTypes.Count == 0)
{
    return true;   // default: spawn all
}
```

The default is fail-open, and **there is no way to express "this document only, no
children."** An empty list is indistinguishable from an absent one. So any request that
doesn't explicitly enumerate its children gets the full subtree.

This matters most at the participant hop. When a play needs an `AthleteSeason` it needs it
**only to satisfy a foreign key** — it has no use for that athlete's headshot, season
statistics, or notes. But `PublishDependencyRequest` cannot currently say so.

Credit where due: `PublishDependencyRequest` already propagates the parent's filter down
each hop (added for the Refresh Contest narrowing, `docs/refresh-contest-cascade-narrowing.md`).
That machinery is correct and reusable — it just has nothing to propagate when the seed is
unfiltered, and Defect A discards it anyway on new entities.

## Defect C — no league scoping on the event-child cascade

PR #688 league-scoped *stream scheduling*, so we only stream games backing a pick'em league.
The document cascade never got the same treatment: we still source plays, drives,
probabilities and their athlete subtrees for every game in every division.

**Does #688 already make this moot? No — measured, not assumed.** #688 was deployed hours
before the 2026-08-29 flood and the flood happened anyway. Sampling the backlog that same
night, with league-scoped streaming live, gave:

```text
 20  EventCompetitionPlay
 15  AthleteSeason
  3  Athlete
  2  AthleteSeasonNote
distinct ESPN events: 401866616, 401868040
```

Neither is a league game (the league game that night was 401864494). Both are FCS:
New Hampshire at UAlbany (Coastal Athletic Association) and William & Mary at Villanova
(Patriot League).

The reason is that streaming and sourcing are different paths. The streamer is a *poller*,
and #688 stopped it polling non-league games. The cascade is triggered by *document
processing*: event documents for the full national slate still arrive via bulk/resource-index
sourcing, and `isNew` (Defect A) fires the child spawn the moment one lands, with no league
awareness anywhere in that path. Scoping the poller does not scope the cascade.

## Defect D — SignalR broadcasts every play to every client

`SportsData.Api/Application/Events/FootballPlayCompletedHandler.cs:38`:

```csharp
await _hubContext.Clients
    .All
    .SendAsync("FootballPlayCompleted", msg, context.CancellationToken);
```

`Clients.All`, with no groups and no league filter. Every play of every game in the pipeline
is pushed to every connected browser and mobile client. Confirmed from the field on
2026-08-29: the operator saw Furman (FCS, Southern Conference) play events in the browser
dev console while watching an unrelated game.

This is the surface that makes the cascade a **user-facing** cost rather than an internal
one. It burns mobile data, battery and client CPU parsing messages the client immediately
discards, and it scales as *(games sourced) x (connected clients)* — so it gets worse both
as we add sports and as the user base grows. `BaseballPlayCompletedHandler` and
`ContestStatusChangedHandler` want the same audit.

Fix: broadcast to SignalR groups keyed by contest (or by league), with clients joining only
the contests they are actually displaying. Note this is worth doing **regardless** of how
Defects A-C land: even with the cascade perfectly league-scoped, a client watching one game
should not receive play traffic for every other game in its league, let alone all of them.

## Proposed fix

Three changes, smallest blast radius first. Each is independently valuable and independently
revertible.

### 1. Make "no children" expressible (unblocks everything else)

Add an explicit way for a request to declare it wants the document and nothing beneath it.

**Decision (owner, 2026-09-01): no new flag.** A `SuppressChildSpawning` bool on the
events was prototyped and rejected in review — two mechanisms answering the same
question ("which children may spawn?") invites logical chaos, and
`IncludeLinkedDocumentTypes` was created for precisely this situation. Instead, the
filter's collapsed cases are split apart:

- `null` — no filter, spawn everything (unchanged; in-flight messages unaffected)
- `[]` (empty) — spawn **nothing**: the document is wanted for itself alone
- non-empty — spawn only the listed types (unchanged)

This needs no wire-contract change at all: the filter already round-trips
Producer -> Provider -> Producer and already propagates hop-to-hop, so "empty"
propagates identically. It is also strictly better under mixed versions than a new
field: an old Provider relays `[]` verbatim, where it would silently drop an unknown
bool. An old Producer consuming `[]` treats it as spawn-all — today's behaviour.

Audited before adopting: every existing filter origin is either literal `null`, the
static non-empty `ContestRefreshDocumentTypes` set, or operator HTTP pass-through.
No code path computes a filter list that could collapse to empty. An operator
explicitly posting `[]` to a sourcing endpoint now gets document-only sourcing —
a new, useful capability rather than a hazard.

### 2. Narrow the participant dependency request

`FootballEventCompetitionPlayDocumentProcessor.BuildParticipantsAsync` (line 185) requests
`AthleteSeason` purely for FK resolution. Mark that request with an empty inclusion
filter. This alone removes
the 4x-per-athlete multiplier — the largest single term in the fan-out — without touching
what games we source.

Same treatment for the `AthletePosition` request on line 203.

**Consequence to accept explicitly:** athlete headshots, season statistics and notes will no
longer arrive as a side effect of play processing. Anything that genuinely needs them
(player pick'em scoring, rosters) must source them through a deliberate path rather than
inheriting them by accident. Worth confirming against the player pick'em requirements before
merge — this is the one place the change could take something away that a feature quietly
depends on.

### 3. Honour the filter for new entities, and league-scope the event children

- Change `isNew || ShouldSpawn(...)` to consult the filter for new entities too. The `isNew`
  short-circuit exists so first-time sourcing gets a complete tree; that intent is right for
  a league game and wrong for the national slate, which is exactly what scoping decides.
- Gate the event-child spawn on league membership using `IProvideApi.GetContestIdsInLeagues`
  (the #688 machinery). Non-league events keep the cheap documents — event, status, score —
  and skip plays, drives, probabilities and every athlete subtree beneath them.

### 4. Scope the SignalR broadcast to groups

Replace `Clients.All` with per-contest (or per-league) groups in the play and status
handlers, and have clients join only what they display. Independent of 1-3 and separately
shippable — it is the only item with a directly user-visible payoff, and it stands on its
own merits even if the cascade were perfectly scoped.

## Sequencing

1 and 2 are a small, self-contained PR that removes most of the backend volume and can ship
first. 4 is independent and can go in parallel — it is the one users would feel. 3 is the
larger change and wants its own PR plus a plan for what we do about non-league history
(see open questions).

### 5. Priority separation for league work (follow-on)

Even with volume cut, bulk sourcing and live league games sharing one FIFO queue means a
league game can always end up behind a backfill. Scoping makes that unlikely; a separate
queue (or priority) for contests backing a pick'em league makes it impossible. Worth doing
once 1-3 land, since the 24-hour lag above shows the failure is not hypothetical.

## Open questions for the owner

- **Do we ever want non-league play-by-play?** Metrics/backtesting may want plays for games
  outside any league. If so, scoping should route that work to a deliberate low-priority
  backfill rather than the live path — not drop it.
- **Does player pick'em depend on the athlete children arriving via the play cascade?**
  **ANSWERED 2026-09-01 (from source): no.** The evidence chain:
  1. Scoring consumes per-game `AthleteCompetitionStatistic` rows, produced by
     `EventCompetitionAthleteStatisticsDocumentProcessor` from
     `EventCompetitionAthleteStatistics` documents. Those documents are spawned from
     three places -- the competitor roster processor, the leaders processor, and the
     play processor itself (`participant.Statistics`) -- none of which are children
     of `AthleteSeason`. Suppressing the AthleteSeason subtree cannot touch them.
  2. The stats processor resolves the `AthleteSeason` row itself and publishes its
     own dependency request when the row is missing. Item 2 still delivers the row;
     only the enrichment children (image, season statistics, notes) are dropped.
  3. Inside `AthleteSeasonDocumentProcessor`, the `Athlete` parent is a blocking FK
     dependency (`PublishDependencyRequest` + throw/retry), not a ShouldSpawn-gated
     child -- so suppression cannot orphan the row.
  4. `AthleteImage` sources headshots the product is permanently barred from showing
     (app-store constraint), and season statistics arriving via this accidental path
     are the same corpus the fabrication audit slated for purge and rebuild through a
     deliberate path. Losing accidental arrival is alignment, not regression.

  Baseball's play processor resolves participants to null when missing and publishes
  no dependency requests -- football is the only spawner on this hop.
- **Replay of purged work:** the ~1M purged items were mostly non-league canonical data and
  athlete crawl. Documents persist in Provider's Mongo, so this is replayable by choice, not
  lost. Decide whether any of it is worth replaying or whether scoping makes it moot.

## Implementation notes (items 1+2, as built)

No event or command shape changed. The entire implementation is three behaviours in
`DocumentProcessorBase`, two call sites in the football play processor, and comments.

**`ShouldSpawn`** now distinguishes the three filter states: `null` -> spawn all
(default, unchanged); empty -> spawn nothing, logged as a document-only request;
non-empty -> only the listed types (unchanged).

**`PublishChildDocumentRequest`** additionally refuses to publish when the command's
filter is empty. This second check is what defends against the `isNew ||` call sites
(Defect A): they bypass ShouldSpawn, but every child publish funnels through this
choke point. It deliberately enforces ONLY the empty case — enforcing a non-empty
filter there would implicitly decide the isNew-bypass question (item 3), which is a
separate behavioural change. A unit test pins that boundary.

**`PublishDependencyRequest`** gains an optional `includeLinkedDocumentTypes`
override: when given, it replaces the command's filter on that one published request;
otherwise the parent's filter propagates unchanged (existing behaviour). FK
resolution is never blocked — a document-only AthleteSeason command still requests
its missing Athlete parent, or the row could never persist.

**Propagation is sticky downhill.** A document-only command publishes its own empty
filter onto every FK request it makes, so the chain beneath a lean request stays
lean: play -> AthleteSeason -> Athlete are all document-only. Nothing downstream can
widen a filter it was handed.

**The play processor** marks its two participant requests
(`AthleteSeason`, `AthletePosition`) with `Array.Empty<DocumentType>()`.

**Retry behaviour:** the filter already round-trips the
`ExternalDocumentNotSourcedException` retry loop via `ToDocumentCreated`
(pre-existing), so a document-only request stays document-only across retries.

**Not affected (deliberately):** operator replay paths
(`PublishDocumentEventsProcessor`, the ops endpoints) and all seed jobs pass their
filters through untouched; none of them construct an empty list today, so their
behaviour is unchanged. `DocumentJobDefinition`'s doc comment was corrected — it
claimed "null or empty" both meant spawn-all.

## Item 5 as built (2026-09-02): the "live" Hangfire queue

Starvation is now structurally impossible rather than merely unlikely.

**The insight that made it cheap:** post-#688 the competition streamer polls
ONLY contests backing a pick'em league — so "streamer-originated" equals
"league-live" by construction, and priority is a tag riding the existing
pipeline instead of a per-document league lookup.

- `DocumentRequested`/`DocumentCreated` carry an appended optional
  `Priority` flag (wire-safe both directions: mixed versions degrade to
  single-queue behaviour, never misroute).
- `CompetitionStreamerBase` publishes with `Priority: true`; the Provider
  relay carries it; and BOTH Hangfire hops route on it — Provider's
  `DocumentRequestedHandler` (the fetch hop: ~222K jobs deep on
  2026-08-29) and Producer's `DocumentCreatedHandler` (the processing
  hop), each on both the immediate and retry/backoff paths. Provider
  Workers listen `["00-live", "default"]` too. (The Provider hop was an
  operator catch in review — the first cut only prioritized Producer.)
- The queue is named **`00-live`** because priority in Hangfire.PostgreSql
  is ALPHABETICAL — its dequeue orders by `"fetchedat" NULLS FIRST,
  "queue", "jobid"`; the configured array only filters. (Array-order
  priority is Hangfire.SqlServer semantics and does not apply. Caught by
  CodeRabbit on PR #709: the first cut's `"live"` sorts after `"default"`
  and would have INVERTED the priority.) Workers listen
  `["00-live", "default"]`; every worker empties live before touching
  bulk. Daemons unchanged.
- Priority is sticky downhill exactly like the inclusion filter: a live
  play's FK dependencies (AthleteSeason -> Athlete -> AthletePosition) ride
  the live queue too, and `ToDocumentCreated` keeps retries prioritized.
  Hangfire 1.8 sticky queues keep even exception retries and scheduled
  backoffs on the queue they were born on.
- KEDA deliberately untouched: its scaler counts `default` only — bulk
  depth is what should drive replicas; the live queue is small (~3-6
  publishes/min per active streamer) and drains first regardless.

**Admission rule (owner, 2026-09-02): if everything is a priority, nothing
is.** Exactly one code path sets `Priority: true` — the competition
streamer, whose scope is already narrowed to league-backing contests by
PR #688. Propagation may only INHERIT priority from a live parent, never
originate it. Expanding admission (Refresh Contest, operator replays,
anything) requires the same scrutiny this design got, and the burden of
proof is on the new admission: the live queue's value is precisely its
emptiness. Default-false is pinned by never-style tests in both services.

Net effect on a 2026-08-29-shaped day: the backlog can be a million deep
and the play a user is watching still processes within seconds of arrival.

## What this does not fix

Independent of scoping, the dependency retry has no attempt cap (logs "attempt 1" forever).
That is a separate queued fix; it makes a flood durable but does not create one.
