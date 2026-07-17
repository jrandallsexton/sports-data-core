# Live MLB streaming: per-tick ESPN fetch amplification → rate-limit starvation → stuck-Live slate

Status: **DRAFT for review** (author: Claude, 2026-06-27). Randall has not yet agreed with this analysis — the "Open questions / how to falsify" section exists specifically to be attacked before we commit to a fix.

## Symptom

2026-06-26 MLB slate: 15/15 contests left stuck on **Live** overnight. Most game overviews showed **≤6 innings** of data — i.e. live data stopped landing mid-game, not at finalization. Manual per-contest **Refresh Contest** the next morning finalized all 15 and scored picks.

## Update 2026-06-28 — counter-evidence (weakens the load-cliff claim)

The 2026-06-27 slate was **also 15 games**, ran with **no code change deployed**, and **all 15 finalized + scored cleanly**. Same nominal load, opposite outcome. This argues against "15 concurrent games deterministically saturates the limiter" as the sole trigger and shifts weight toward an **aggravator specific to 06-26** (e.g. KEDA scale-down / reduced Provider capacity, a postponed-game retry storm, transient ESPN slowness, or a deploy). The amplification mechanism in section D is still real and worth fixing as hardening, but it may be a *contributing condition*, not *the* cause. Resolve open question #1 and #5 before treating section D as the root cause. (Worth comparing 06-26 vs 06-27: limiter-saturation volume, Provider pod/worker counts, stream cancel/requeue frequency, and game-end clustering.)

## TL;DR claim (to be challenged)

The live **Plays** streaming worker re-requests the *entire* plays collection every 30s per game. For current-season games that fans out into a **live ESPN fetch of every play (and every index page) on every tick**, with no cache and no cool-down. Load scales with `concurrent games × plays-per-game`, which climbs through the game. Around the middle innings of a full 15-game slate it saturates the Provider's ESPN rate limiter (`ESPN rate limiter: max wait 30000ms exceeded, failing open`), starving the lightweight fetches that actually matter (`EventCompetitionStatus`, competitor scores). Games freeze mid-game; status never reaches `STATUS_FINAL`; enrichment never finalizes; the slate is stuck Live.

Finalization is a **victim**, not the trigger.

## Evidence

### A. The stuck state is a status/enrichment gate (proven)
- UI "Live" = `started && FinalizedUtc == null`. `ContestBase.IsFinal => FinalizedUtc.HasValue`.
- `FinalizedUtc` is set in exactly one place: `BaseballContestEnrichmentProcessor.cs:262`, gated at `:171` on `CompetitionStatus.StatusTypeName == "STATUS_FINAL"`. On a non-final status it **logs and returns with no retry** — nothing re-runs it.
- For example contest `d1dcbd8f…` / competition `73daaa32…` (ESPN event `401815920`): `ContestCompleted` published 04:24:59Z, enrichment ran 04:25:33Z, and skipped — status was not FINAL.

### B. Status processing stopped mid-slate (proven)
- `BaseballEventCompetitionStatusDocumentProcessor` persisted **nothing after 01:30Z** (`…like '%Persisted%'` → empty). Games ran ~01:45–04:30Z. So no game's status row reached `STATUS_FINAL`.
- Last observed status writes were a 00:32–00:35Z batch (games going `SCHEDULED → IN_PROGRESS`).

### C. ESPN rate limiter saturated during the games (proven)
- `ApplicationName='SportsData.Provider' AND @Message like 'ESPN rate limiter%'`, 01:30–04:30Z: returned the 500-row cap spanning **03:06–04:28Z** — i.e. heavy, sustained saturation *during* games (games started ~01:45; 03:00–03:30 ≈ inning 6). A second burst 05:11–05:59Z is the finalization/refresh aftershock.
- Message verbatim: `ESPN rate limiter: max wait 30000ms exceeded, failing open`.

### D. The fan-out re-fetches everything, live, for current season (proven in code)
- Streamer (`BaseballCompetitionStreamer.GetPollingTargets`) polls `Details.Ref` (the plays **collection**) every 30s. It does **not** poll `EventCompetitionStatus` at all.
- `DocumentRequestedHandler.ProcessResourceIndex` (`:212`) walks **every page** of that collection, fetching each page from ESPN with `bypassCache: true` (`:225`), and enqueues one `ProcessResourceIndexItemCommand` **per play** with `BypassCache: ShouldBypassCache(SeasonYear)` (`:342`).
- `ShouldBypassCache` (`:120`) returns **true** for `seasonYear >= CurrentSeason` → current-season (live) always bypasses cache.
- `ResourceIndexItemProcessor.HandleValid`: with `BypassCache=true`, the Mongo cache read (`:186`) is skipped and it goes straight to `_espnApi.GetResource(..., bypassCache: true)` (`:284`) — a live ESPN call per play.
- The publish cool-down (`DocumentPublishCooldownMinutes`, 90 in per-sport Prod labels) is gated on `!IsCurrentSeason(...)` (`ResourceIndexItemProcessor.cs:210`) → **does not apply to live games at all**. It is a historical-sourcing optimization.

**Per-tick cost, per game ≈ (plays-index pages) + (plays-in-game) ESPN fetches, every 30s.** A mid/late-game has ~hundreds of plays; ×15 games sustained is on the order of ~100 ESPN req/s and rising — well past the limiter envelope.

## Why the morning manual refresh worked
One contest at a time, against a calm pipeline (no concurrent live streams, limiter not saturated). The status doc re-sourced, landed `STATUS_FINAL`, the status processor's defensive `ContestCompleted` (`EventCompetitionStatusProcessorBase.cs:152`) fired, enrichment finalized. Firing all 15 at once would have re-created the flood.

## Root cause (claim)

Not a single bad commit — a **load cliff**. The live-play sourcing design re-fetches immutable, already-known plays from ESPN on every poll. Cheap for NCAAFB (fewer concurrent games, fewer "plays") and for any single game; catastrophic for a full MLB slate because cost grows with `games × plays`. The cool-down that would have damped this is current-season-exempt, and `ShouldBypassCache` forces a live fetch for exactly the in-season case that needs protection.

## Proposed fixes

Primary (attacks the amplifier):

1. **Stop re-fetching immutable plays.** A completed baseball play does not change. For current-season play sourcing, serve already-stored plays from Mongo and only live-fetch the **tail** (the in-progress / most-recent play, plus any index entries not yet seen). Turns per-tick cost from `O(all plays)` to `O(new plays + 1)`.
   - Implementation options to evaluate:
     - (1a) At the streamer level: track high-water mark (last play sequence/ordinal or last page index seen) per competition; request only new pages + re-request only the last page each tick.
     - (1b) At the Provider fan-out level: for `EventCompetitionPlay` + current season, set `BypassCache=false` for plays already in Mongo and only bypass for the newest item(s). Requires a notion of "which play is still live."
   - 1a keeps the cache policy untouched and is the most surgical; 1b is more general but touches the shared sourcing path.

Secondary (make finalization survive a load spike even if (1) is imperfect):

2. **Enrichment self-retry.** The `status != STATUS_FINAL` branch should reschedule a bounded delayed retry (Hangfire) instead of returning permanently. This alone would have auto-recovered the slate without manual refresh.
3. **Status on its own cheap cadence / priority.** Either have the streamer poll `EventCompetitionStatus` directly at a slow interval (it's one tiny doc), or give status/score fetches a priority lane in the rate limiter so they never starve behind play pagination.

Tertiary:

4. **Bound concurrent-stream ESPN load.** Shared token budget or a concurrent-stream cap so a big slate can't self-DoS.
5. **Narrow `PublishContestRefreshOnFinalAsync`** to `{Event, EventCompetition, EventCompetitionStatus, EventCompetitionCompetitor, EventCompetitionCompetitorScore}` so finalization doesn't re-page the full plays index again.

Recommended sequencing: **(1a) + (2)** first — (1a) removes the cause, (2) guarantees recovery. (3)/(4)/(5) as hardening.

## Open questions / how to falsify this analysis

I want these answered before committing — they're where I could be wrong:

1. **Is the limiter saturation the cause or a side effect?** Counter-hypothesis: a KEDA scale-down reduced Provider worker/pod capacity at ~03:00 and the limiter saturation is downstream of fewer consumers, not raw request volume. Test: correlate Provider pod count / Hangfire worker availability and the `SourcingJobOrchestrator can't be scheduled` warnings against the saturation onset.
2. **Does the limiter "fail open" actually drop the request, or let it through to a 403?** If it lets through, ESPN 403s may be tripping the circuit breaker — a different failure than pure queue starvation. Test: search for 403 / circuit-breaker-open events in 03:00–04:30Z (note: bare `%403%`/`%429%` matches play-id digits — must match on status/circuit log templates, not substrings).
3. **Did the plays workers actually keep ticking, or did the streams die (cancel/requeue)?** We saw a KEDA cancel+requeue for `401815920` at 01:32Z. If many streams were Failed and not re-queued, "≤6 innings" could be dead streams, not starvation. Test: per-competition `CompetitionStream.Status` timeline + "Streaming cancelled / re-queue" frequency during the slate.
4. **Is `EventCompetitionPlay` really the dominant fetch volume?** Validate with the `espn.live.fetch` meter by DocumentType during the window, or count `ESPN LIVE` lines by resource type. If plays aren't the bulk, fix (1) is misaimed.
5. **Frequency/severity:** is this every full slate, or did 06-26 have an aggravator (slate size, a postponed-game retry storm, a deploy)? Determines urgency vs. a tuning knob.

## Files referenced
- `src/SportsData.Producer/Application/Competitions/BaseballCompetitionStreamer.cs` (polling targets; no status poll)
- `src/SportsData.Producer/Application/Competitions/CompetitionStreamerBase.cs` (`PublishContestRefreshOnFinalAsync`, poll loop)
- `src/SportsData.Provider/Application/Documents/DocumentRequestedHandler.cs` (`ProcessResourceIndex`, `ShouldBypassCache`)
- `src/SportsData.Provider/Application/Processors/ResourceIndexItemProcessor.cs` (cache/cool-down gates, `:186` `:210` `:284`)
- `src/SportsData.Producer/Application/Contests/BaseballContestEnrichmentProcessor.cs` (`:171` status gate, `:262` finalize)
- `src/SportsData.Producer/.../Espn/Common/EventCompetitionStatusProcessorBase.cs` (`:152` defensive ContestCompleted)
