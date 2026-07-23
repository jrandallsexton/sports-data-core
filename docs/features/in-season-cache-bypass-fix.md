# In-Season Cache Bypass — Stop Re-Fetching Immutable Documents

Status: **PoC implemented** (PR #549) — `DocumentRequestedHandler.ProcessResourceIndex`
fan-out path, allow-list `{ EventCompetitionPlay }`. **Leaf-path follow-on implemented**
(uncommitted, review pending) — `DocumentRequestedHandler.ProcessResourceIndexItem`, the
single dependency-sourced doc path (e.g. situation `lastPlay.$ref`); see "Leaf path" below.
Follow-ons still deferred (see Scope): `ResourceIndexJob` paged loop, on-final force-bypass
re-validate, in-season cooldown relaxation.
Last updated: 2026-07-22
Scope: MLB live sourcing (in-season). Applies to any current-season sport.

## Problem

During a **live** MLB game, the **same individual play** is fetched from ESPN
**15–20 times** (verified in Seq for a single play URI, e.g.
`…/competitions/401816227/plays/4018162270203990099`). A completed play is
**immutable** — it should be served from Mongo, never re-fetched. This redundant
fetching saturates ESPN's IP rate limiter (ESPN returns **403**, not 429), which
is the direct cause of the **live-slate-freeze / stuck-finalization** issue
(`reference_stuck_live_finalization_rate_limit`) currently worked around with
spaced manual refreshes.

### Amplification math

`CompetitionStreamerBase` polls a live competition **every 30s** for up to a
**5-hour** `MaxStreamDuration`, re-requesting the play index each cycle. A
~3-hour baseball game (~360 cycles) × ~150–300 completed plays =
**tens of thousands of redundant ESPN fetches per game**, none of which return
new data. At the `RequestDelayMs=1000` throttle + 403 retry policy, this
massively exceeds ESPN's tolerance → 403 storm → the live pipeline stalls and
games don't finalize.

## Root cause

Cache-bypass is decided **per-season**, then applied uniformly to **every item**:

- `ShouldBypassCache(seasonYear)` returns `true` when `seasonYear >=
  CurrentSeason` (or `CurrentSeason == 0`). MLB 2026 → `true`. Two identical
  copies: `DocumentRequestedHandler.cs:120` and `ResourceIndexJob.cs:212`.
- The item command inherits that as `command.BypassCache = true`
  (`DocumentRequestedHandler.cs:202`, `ResourceIndexJob.cs:243`) — correctly
  *not* the index's hardcoded bypass, but season-based.
- `ResourceIndexItemProcessor.cs:186` — `if (dbItem is not null &&
  !command.BypassCache)` — with `BypassCache=true`, this short-circuits **past
  the Mongo-serve path**, so a perfectly good cached play is ignored and
  re-fetched from ESPN (`:265→284`).
- The unchanged-content / cooldown suppression that would otherwise stop the
  re-publish (`:210`) is itself gated on `!IsCurrentSeason(...)` → also skipped
  in-season.

**The bug: `season` is the wrong axis for an individual item. The right axis is
`document mutability`.** The index *listing* legitimately bypasses (new plays
appear); an individual completed *play* never should.

## Key principle: mutability ≠ season

| Behavior in-season | Correct for |
|---|---|
| **Bypass cache** (re-fetch, data changes) | the play/competitor **index listings**, and mutable aggregates |
| **Serve from Mongo** (immutable once created) | individual completed **plays / drives** |

### Draft document-type classification (LOAD-BEARING — needs confirmation)

**Immutable once created** (serve from Mongo even in-season):
- `EventCompetitionPlay` — the primary offender (highest volume, re-paged every 30s)
- `EventCompetitionDrive` (football)
- `EventCompetitionCompetitorRoster` (static per game once set) — *confirm*

**Mutable in-season** (keep bypassing — must stay fresh):
- `EventCompetition`, `EventCompetitionStatus`, `EventCompetitionSituation`
- `EventCompetitionCompetitorScore`, `…LineScore`, `…Record`
- `…CompetitorStatistics`, `…AthleteStatistics`, `…Leaders`
- `EventCompetitionOdds`, `…Probability`, `…Prediction`, `…PowerIndex`

The classification is the crux and the main thing to get right — a
mis-classified mutable type would go stale mid-game; a mis-classified immutable
type just wastes an occasional fetch. **Bias the initial allow-list small
(start with `EventCompetitionPlay` only)** — it captures ~all the bleeding —
then expand.

## The one real correctness tension: the finalizing edge

Older plays are immutable, but the **most-recent play may still be finalizing**
(an in-progress at-bat fetched with partial data, then completed). A blanket
"never re-fetch a cached play" would miss that transition and show a stale last
play. Also relevant: rare post-game ESPN **corrections** (scoring changes).

**Resolution (chosen): re-fetch only the live edge, identified as the last index
item.** VERIFIED against a real MLB plays index: `EspnResourceIndexDto.Items` is
ordered **ascending** by play id (639 plays / 26 pages, ids strictly
increasing). So the newest play is deterministically the **last item on the last
page**: `dto.PageIndex >= dto.PageCount && i == dto.Items.Count - 1`. That single
item keeps re-fetching (may still be finalizing); every other cached play serves
from Mongo. Result: **1 ESPN play fetch/cycle** instead of hundreds.

This is cleaner than reading the Situation doc's `lastPlay` ref: the decision is
made *right where the index is already being iterated*, with no cross-document
lookup and no coupling to Situation processing. **Implemented in the PoC for the
`DocumentRequestedHandler.ProcessResourceIndex` path only** — the `ResourceIndexJob`
paged loop is a deferred follow-on (see Scope below).

### Correction coverage for non-edge cached plays — DEFERRED (not in the PoC)

The PoC serves cached non-edge plays from Mongo and re-fetches only the live
edge. It does **not** implement any re-validation of already-cached non-edge
plays, so a rare mid-game **correction** (a scoring/stat change to an older play)
would not be picked up. In particular:

- **The finalization refresh does NOT currently force a full re-validate.**
  `PublishContestRefreshOnFinalAsync` flows through the same enqueue path, so
  post-PoC it will *also* serve plays from cache — corrections are not re-fetched
  at final. Making the on-final pass force-bypass plays is a **required follow-on**
  before broad rollout; it is not implemented here.
- A periodic mid-game full re-validate is a possible later addition, also not
  implemented.

Why this is acceptable for the PoC: ESPN generally only lists a play in the index
once it's complete, so most cached plays are already final; corrections are rare.
The live-edge re-fetch covers the still-finalizing play. But the correction gap
is real and is why the on-final force-bypass is called out as a follow-on rather
than "coverage we already have."

## Leaf path (`ProcessResourceIndexItem`) — follow-on, implemented (review pending)

After #549 deployed, Grafana (`provider-baseball-mlb-worker-metrics`, *MongoDB Cache
Hits vs ESPN Live Fetches*) showed the intended inversion: at deploy the red
(ESPN fetches) plateau collapsed from ~5,000/interval to a ~500 floor and green
(cache hits) went from **zero to dominant**. But a **residual red floor crept
back to ~2,500–3,000/interval** — a second, independent amplification path the
fan-out fix never touched.

Seq isolated it to a single immutable play fetched repeatedly *after* the deploy
(e.g. `…/competitions/401816230/plays/4018162301401020033`, 3+ `ESPN live fetch`
events, same `CorrelationId`), all with `SourceContext =
ResourceIndexItemProcessor` and `BypassCache = true` — the **leaf** branch of
`DocumentRequestedHandler.ConsumeInternal`, not the index fan-out.

**Root cause.** Producer's live `EventCompetitionSituation` processor is mutable
and re-runs every cycle. Each run resolves `dto.LastPlay.$ref`; when that play
isn't in the canonical DB yet it calls `PublishDependencyRequest(...,
EventCompetitionPlay)` and throws `ExternalDocumentNotSourcedException` to retry.
That publish (`DocumentProcessorBase`, `SeasonYear: command.SeasonYear`) lands on
the Provider **leaf path**, which still hardcoded `BypassCache:
ShouldBypassCache(evt.SeasonYear)` → in-season → fetch. So the same immutable play
was re-fetched from ESPN on **every situation retry** — the leaf-path twin of the
live-slate storm.

**Fix.** The leaf path now honors the same immutable-in-season policy, with **two
deliberate differences** from the fan-out path:

1. **No live-edge carve-out.** A single leaf has no positional "newest item"
   signal and doesn't need one: a cache **miss** still fetches (a never-seen play
   is sourced exactly once), and the fan-out path keeps the current game's edge
   play fresh every cycle. Serving the leaf from Mongo is at most one cycle stale,
   then immutable. It also breaks the situation retry loop *faster* — the play is
   republished from Mongo, persists canonically, and the situation stops asking.
2. **Gate on feature-enabled (`CurrentSeason != 0`), NOT exact current season.**
   Dependency-sourced leaf requests (e.g. `lastPlay`) do **not** reliably carry a
   usable `SeasonYear`, so an "`== current season`" gate would silently no-op on
   exactly this traffic. Immutability is season-independent: a cache hit for a
   play from any season is correct, and historical seasons already serve from
   cache via `ShouldBypassCache` regardless.

```csharp
// ProcessResourceIndexItem — leaf path
var serveImmutableFromCache = _commonConfig.CurrentSeason != 0
    && InSeasonDocumentPolicy.IsImmutableInSeason(evt.DocumentType);
var bypassCache = ShouldBypassCache(evt.SeasonYear) && !serveImmutableFromCache;
```

Tests: `Leaf_ImmutablePlay_ServesFromCache_WhenFeatureEnabled_RegardlessOfSeason`
covers current-season, the null-season `lastPlay` case, historical, future,
feature-disabled, and the mutable-type control.

## Design options (the actual fix)

1. **Item-mutability cache honesty (core fix).** At the item cache decision, for
   immutable-type items, serve from Mongo when `dbItem` is present **regardless
   of `BypassCache`**. Also relax the `IsCurrentSeason` gate on the cooldown/
   unchanged suppression for immutable types so we don't re-publish an unchanged
   cached play every cycle (reduces Producer + Rabbit churn too). Collapses
   15–20 → ~1.
2. **Reduce demand at the source (follow-on).** Have the streamer / index
   processing only request plays it doesn't already have (presence-diff against
   Mongo), instead of re-enqueueing the whole index each cycle. Cuts the request
   volume before it reaches the cache. Higher leverage on the limiter, but
   touches the live worker + has the same finalizing-edge caveat.
3. **Short in-season read-cache TTL (stopgap).** Type-agnostic: cache in-season
   docs for ~30–60s so rapid re-requests inside the window serve from Mongo.
   Simple, broad, no per-type knowledge — but blunter (delays legitimately
   mutable updates by up to the TTL). Good as a fast relief valve if needed
   before the classification work lands.

## Recommendation

**#1 (core) + the "re-fetch only the live edge" correctness handling**, with **#2
as a follow-on** once #1 proves out. #1 makes the cache honest so *every* caller
benefits (streamer, manual refresh, re-source), and it's a contained change to
one decision point plus a mutability classifier. Keep an initial allow-list of
just `EventCompetitionPlay`.

## Bonus: faster live updates (not just fewer calls)

This is not only a cost fix. Today, a brand-new play must queue behind tens of
thousands of redundant immutable re-fetches and the resulting 403 back-off — so
new plays land **late**. Once immutable plays serve from Mongo, the rate limiter
has headroom and the only ESPN calls are the mutable aggregates + the live-edge
play → **new plays land in ~one fetch instead of waiting out the storm.** Better
logic → lower ESPN load **and** faster live play latency.

## Implementation sketch (refined — decide bypass at enqueue, keep the item processor dumb)

The key insight: all the context needed (document type, page position, whether
this is the live edge) exists **at the point the index enqueues items**, not in
the item processor. So compute the *effective* bypass there and keep
`ResourceIndexItemProcessor` honoring a single `BypassCache` flag — **no change
to its core decision** (`:186` already serves from Mongo when
`BypassCache == false`).

- **Mutability classifier** — a small `IsImmutableInSeason(DocumentType)` /
  static allow-list in Provider, single source of truth. **PoC = {
  `EventCompetitionPlay` }.**
- **`ProcessResourceIndex` (`DocumentRequestedHandler`) and `ResourceIndexJob`**
  — the two item-enqueue loops. Replace the flat
  `BypassCache: ShouldBypassCache(seasonYear)` with a per-item computation:

  ```csharp
  var isLiveEdge = dto.PageIndex >= dto.PageCount && i == dto.Items.Count - 1;
  var bypass = ShouldBypassCache(seasonYear)
               && (!IsImmutableInSeason(docType) || isLiveEdge);
  ```

  So: mutable types + the live-edge play → bypass (unchanged); every other
  immutable item → `false` → served from Mongo by the existing item path. This
  is the whole core fix.
- **`ResourceIndexItemProcessor`** — no change required for the ESPN-call
  reduction. *Optional churn reduction*: relax the `IsCurrentSeason` gate on the
  cooldown/unchanged suppression (`:210`) for immutable types so an unchanged
  cached play isn't re-published to Producer every cycle either.
- **Finalization** — confirm `PublishContestRefreshOnFinalAsync` re-sources plays
  with bypass (full re-validate at game end). If it flows through the same
  enqueue path, it will treat plays as immutable and skip them — so it needs an
  explicit "force bypass" for the final pass (or exempt the on-final refresh from
  `IsImmutableInSeason`).
- **`ShouldBypassCache` itself is untouched** — the season predicate stays; we
  only *gate* it per-item by mutability + edge at enqueue time.

## Testing

- Unit: `IsImmutableInSeason` per DocumentType (locks the classification).
- Unit: in-season + `EventCompetitionPlay` + doc in Mongo → served from cache,
  no ESPN fetch, `espn.cache.hit` increments, `espn.live.fetch` does not.
- Unit: in-season + `EventCompetitionStatus` (mutable) → still bypasses.
- Unit: unchanged cached play within cooldown → suppressed re-publish.

## Rollout / risk

- Guard behind a config flag (allow-list is itself the flag — empty = today's
  behavior).
- Success metric: for a live MLB game, `espn.cache.hit` goes from ~0 to
  **dominant**, `espn.live.fetch` drops by ~2 orders of magnitude, 403s
  disappear, games finalize without manual spacing.
- Risk if mis-classified: a mutable type served stale mid-game (visible, quickly
  correctable by shrinking the allow-list). Starting with `EventCompetitionPlay`
  only makes this near-zero-risk.

## Related

- `reference_stuck_live_finalization_rate_limit` — the symptom this fixes.
- `project_in_season_completed_cache` — the deferred item this concretizes
  (per-season → per-mutability).
- `docs/processing/include-linked-document-types.md` — the refresh-narrowing
  mechanism (orthogonal but adjacent).
