# Contest Finalization — Daemon Role + Reconcile Backstop

**Status:** Diagnostic + design draft. Not yet implemented.
**Origin:** 2026-06-14 MLB scoring miss — only ~4 of 14 played games produced `ContestCompleted` events; the other 10 never reached the publish point in `CompetitionStreamerBase`, so enrichment / `ContestFinalized` / pick scoring never ran.

**Scope evolution:** investigation pinned the root cause on a KEDA + long-running-Hangfire-job mismatch. KEDA scale-down of Producer-MLB worker pods mid-game cancels in-flight streamer jobs that span hours. The structural fix is a new `Daemon` role for Producer (4th role alongside `Api` / `Ingest` / `Worker`, mirroring the planned [Producer role split](../memory/...)); streamers move there. The reconcile backstop remains as durable insurance against Daemon-pod death.

This doc covers streamers only. AI generation jobs (`MatchupPreviewGenerator`, `ContestRecapProcessor`) and the DLQ reprocessor are out of scope here despite sharing the same risk profile — they'll get the Daemon treatment in follow-ups.

## Problem

`STATUS_FINAL` detection is bound to a single long-running Hangfire task per competition (`CompetitionStreamerBase.ExecuteAsync`). The publish points for `ContestCompleted` exist only **inside** that task:

- `ExecuteWithStreamAsync` — initial `STATUS_FINAL` arm (`CompetitionStreamerBase.cs:288–295`)
- `ExecuteWithStreamAsync` — `WaitForLiveStartAsync` returned `AlreadyFinal` (`:265–275`)
- `ExecuteWithStreamAsync` — `PollWhileInProgressAsync` returned `Final` (`:317–322`)

If the task ends any other way, no `ContestCompleted` is published. There is no external reconciliation that catches stranded competitions.

`PickScoringJob` (`SportsData.Api/Application/Jobs/PickScoringJob.cs`) is a backstop on the scoring side, but it depends on `Contest.IsFinal` / enrichment having happened, which depends on `ContestCompleted` having fired. It cannot save the case where the streamer never reached publish.

## Step 1 — Diagnostic SQL

Run against the MLB Producer DB. Replace the `INTERVAL` bounds to match the window you're investigating.

```sql
SELECT
  c."Id" AS "ContestId",
  c."StartDateUtc",
  c."IsFinal" AS "Contest_IsFinal",
  c."FinalizedUtc",
  comp."Id" AS "CompetitionId",
  cs."StatusTypeName",
  cs."StatusDescription",
  s."Status" AS "StreamStatus",
  s."StreamStartedUtc",
  s."StreamEndedUtc",
  s."FailureReason",
  s."ModifiedUtc" AS "StreamLastUpdated"
FROM "Contest" c
INNER JOIN "Competition" comp ON comp."ContestId" = c."Id"
LEFT JOIN  "CompetitionStatus" cs ON cs."CompetitionId" = comp."Id"
LEFT JOIN  "CompetitionStream" s  ON s."CompetitionId"  = comp."Id"
WHERE c."StartDateUtc" >= NOW() - INTERVAL '36 hours'
  AND c."StartDateUtc" <  NOW() - INTERVAL '4 hours'
ORDER BY c."StartDateUtc";
```

### How to interpret each row

| `StreamStatus` | What it means | Why no `ContestCompleted` | Source in `CompetitionStreamerBase.cs` |
|----------------|----------------|----------------------------|-----------------------------------------|
| `Active` (still) | Hangfire job died mid-run — process restart, OOM, network, etc. The state machine is purely in-memory in the job; nothing resumes it. | `STATUS_FINAL` only fires from inside the live polling loop, which is no longer running. | `:519–522` is the live-loop detection; never reached after process death. |
| `Failed` w/ `FailureReason = "Stream exceeded max duration without STATUS_FINAL"` | The 5-hour `MaxStreamDuration` cap was hit. MLB extra innings + rain delay can easily cross this. | `Timeout` branch marks Failed without publishing. | `:38` cap; `:494–498` check; `:331–333` Failed branch. |
| `Failed` w/ `FailureReason = "Status polling failed repeatedly"` | 10 consecutive ESPN status fetches returned null. | Throws — no publish. | `:39` cap; `:509–513` throw site. |
| `Failed` w/ `"Initial status fetch failed"` | First status fetch on entry failed. | Marks Failed before reaching any switch arm. | `:234–236`. |
| `Failed` w/ `"Live start not detected within max stream duration"` | `WaitForLiveStartAsync` hit `MaxStreamDuration` without seeing `STATUS_IN_PROGRESS`. | `Timeout` arm marks Failed. | `:277–280`. |
| `Failed` w/ `"Competition re-fetch after live start failed"` | Game went live but the post-live-start parent fetch failed. | Marks Failed before workers spawn. | `:259–261`. |
| `Completed` but **no** `ContestCompleted` log in Seq | publish happened but broker / consumer lost it. | Real messaging-side durability problem. | Publish at `:271`, `:291`, or `:320`. |
| no `CompetitionStream` row at all | Scheduler never started a stream for this competition. | Look at `CompetitionStreamScheduler`, not `CompetitionStreamerBase`. | — |

Distribution of the 10 stranded games across these rows tells us which failure mode is dominant and whether one backstop covers most of them.

### Findings — 2026-06-13 MLB slate

Of the 14 played games:

- **4** → `StreamStatus = Completed`. These produced the 8 `ContestCompleted` Seq events (publish-log + consume-log = 2 per contest × 4).
- **10** → `StreamStatus = Failed`, `FailureReason = "Cancelled by external request"`. All identical.

That `FailureReason` string only originates in **one place** — the `OperationCanceledException` catch in `CompetitionStreamerBase.ExecuteAsync`:

```csharp
// CompetitionStreamerBase.cs:180–188
catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
{
    _logger.LogWarning("Streaming cancelled by external request");
    if (stream != null)
    {
        stream.StreamEndedUtc = _dateTimeProvider.UtcNow();
        await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Failed, CancellationToken.None, "Cancelled by external request");
    }
}
```

The `cancellationToken` here is the one **Hangfire** passes into the job. It trips when:

1. The Hangfire worker / Producer pod is shutting down (deployment, KEDA scale-down, OOM kill, pod eviction, host reboot).
2. The job is cancelled from the Hangfire dashboard.
3. The job is rescheduled / deleted out from under itself.

10 identical cancellations clustered on one day are unlikely to be #2 (admin action) — that would be deliberate and you'd remember. Almost certainly **one** Producer-MLB pod event killed all in-flight streamer jobs simultaneously.

### Confirm with logs — actual findings 2026-06-13

Seq queries `@Message = 'Streaming cancelled by external request'` and `@Properties.SourceContext like '%CompetitionStreamer%' and @Level = 'Warning'` returned **11 cancellation log lines** spread across **5 distinct time clusters** on the MLB Producer:

| Timestamp (UTC) | Count | Shape |
|-----------------|-------|-------|
| 14:00:40.704 | 1 | single |
| 15:00:27.319 | 1 | single |
| 15:55:08.480 | 3 | simultaneous (same ms) — multi-job event |
| **16:04:51.043–046** | **5** | **simultaneous — multi-job event (largest)** |
| 21:56:16.069 | 1 | single |

11 log lines for 10 stranded streams from Step 1 is consistent: most likely one doubleheader Contest hosts 2 Competitions that each got cancelled, or one stream was cancelled across a retry.

**Key interpretation:** this is **not** a single bad pod event. Multi-job clusters at 15:55 and 16:04 strongly suggest two distinct Producer-MLB pod / deployment / KEDA events. Singletons at 14:00, 15:00, 21:56 are either job-level cancellations or windows where only one stream happened to be in flight.

This reframes the problem: cancellations are **recurring background noise** the streamer needs to be resilient against, not a one-off anomaly we can engineer out by fixing a single pod stability issue.

Cross-reference to confirm trigger for the 15:55 and 16:04 clusters:

- Kubernetes: `kubectl get events` won't go back that far, but check pod restart-count and the Producer-MLB deployment's `kubectl rollout history`.
- Hangfire dashboard → Servers tab: worker registration/deregistration timestamps around 15:55 and 16:04.
- Seq: search for the Producer startup banner (`SourceContext` containing program/host startup) at ~15:55 and ~16:04 to confirm pod boots.
- Flux: any image / replica change pushed to `sports-data-config` for `producer-mlb-worker` that day.

### Sidebar: `EventCompetitionProbability` URI is null

The same `@Level = 'Warning'` query surfaced 4 additional warnings on 2026-06-13:

- 19:10:08.754
- 19:10:08.775
- 22:06:06.019
- 22:07:35.383

All four are `"Skipping worker for EventCompetitionProbability - URI is null"` from `StartPollingWorkers` at `CompetitionStreamerBase.cs:465`. It means the parent `EventCompetition` payload came back from ESPN without a `probability` ref link, so the polling worker for that document type is silently skipped.

This is **unrelated to the cancellation issue** but worth tracking separately:

- If ESPN's MLB `EventCompetition` documents do not include `probability` refs at all (likely — win-probability streams are a football/basketball construct), the warning is meaningless noise. Fix shape: sport-gate the polling targets so `BaseballCompetitionStreamer.GetPollingTargets` doesn't return Probability.
- If the link is sometimes present and sometimes missing, it's a real upstream-data anomaly worth investigating.

Either way, it does not explain the missed `ContestCompleted` publishes — those streams were cancelled before `STATUS_FINAL` was reached.

## Step 2A — Why these 10 didn't auto-recover (the bug)

When the `OperationCanceledException` catch above runs, it logs, persists `Failed`, and **returns normally**. The exception is not rethrown.

From Hangfire's perspective, the job completed successfully — no retry is queued. After the Producer pod comes back up, nothing re-runs the streamer for those 10 competitions. They sit in `Failed` forever; `Contest.IsFinal` never flips; enrichment never runs; `ContestFinalized` never fires; picks never score.

If the catch **rethrew** `OperationCanceledException`, Hangfire would (by default) treat the job as cancelled-during-shutdown and re-queue it for execution on a healthy worker. On retry, `ExecuteAsync` would:

1. Load the `CompetitionStream` (in `Failed` state).
2. Overwrite its status to `AwaitingStart` (`CompetitionStreamerBase.cs:158`).
3. Re-fetch competition + status from ESPN.
4. If game is now `STATUS_FINAL` → switch arm at `:288–295` → publishes `ContestCompleted`.

That would have caught all 10 of yesterday's misses automatically — no backstop required.

**Caveat:** Hangfire's exact behavior on `OperationCanceledException` depends on attribute configuration (`AutomaticRetry`, etc.) and Hangfire-server settings. Worth verifying empirically with a deliberate pod restart against a known-live stream before committing to "rethrow alone is enough."

## Step 2B — KEDA cooldown tune (tactical stopgap)

The dominant trigger for the cancellations on 2026-06-13 is **KEDA scaling Producer-MLB worker pods down mid-game**. KEDA's queue-depth-driven scaling and Hangfire long-running streamer jobs are a fundamental architectural mismatch: KEDA sees the message queue drained and decides the pod is idle; SIGTERM follows; Hangfire's `BackgroundJobServer` signals shutdown to in-flight jobs; the streamer's `OperationCanceledException` catch trips. K8s has zero visibility into Hangfire's in-flight work.

> **Not the strategic answer.** This tune buys time but doesn't fix the architectural mismatch — KEDA still owns the streamer-hosting pod's lifecycle. The real fix is splitting streamers onto a non-KEDA-scaled role (see Step 2D). Use this only as an interim before Step 2D ships.

**Mitigation:** raise the `cooldownPeriod` on the MLB Producer worker's KEDA `ScaledObject` to comfortably exceed the longest plausible MLB game (e.g. 6 hours, vs the typical 5-minute default). KEDA will not scale a pod down until it's been idle for that duration, so a pod running a multi-hour streamer stays alive even after the message queue drains.

This is the cheapest possible fix:

- Pure config in `sports-data-config` — no code change.
- No application-level retry semantics to verify.
- Independent of Hangfire behavior on rethrown cancellation.

**Tradeoffs:**

- Higher pod hours during quiet windows — at minimum one worker pod stays up for `cooldownPeriod` after the last message even if there's no streamer running. For a single MLB pod that's negligible.
- Doesn't help if a pod actually crashes (OOM, eviction). The reconcile backstop still catches that case.
- The setting is sport-specific — football streamers have shorter game durations (~3.5h), so a smaller cooldown is appropriate there. Per-sport ScaledObject tuning is already the pattern.

**Empirical check before tuning:** confirm KEDA is the trigger.

```
kubectl get pods -n default -l app=producer-mlb -o wide   # pod ages
kubectl describe scaledobject -n default | grep -A 20 producer-mlb
kubectl get hpa -n default | grep producer-mlb
```

If current pod ages are hours-not-days and `cooldownPeriod` is the default, KEDA is the trigger and this tune lands the mitigation immediately.

## Step 2D — Producer `Daemon` role (strategic fix)

The architectural mismatch is that the streamer is a daemon (multi-hour, indefinite-lifetime, daemon-shaped) running inside a job-queue worker (KEDA-scaled, queue-depth-driven, optimized for fast turnover). The fix is to host daemon-shaped Hangfire jobs on a separate K8s role that is **not** managed by KEDA.

### Role split

Producer gets a 4th role alongside the planned `Api` / `Ingest` / `Worker` split: **`Daemon`**.

- **Self-explanatory naming.** Unix-conventional; tells future-you exactly what's hosted there: long-running background processes that shouldn't be interrupted.
- **Not limited to streamers.** Same role will later host AI generation jobs (costly retries), the DLQ reprocessor (lose-progress-on-cancel), and anything else daemon-shaped. Out of scope for this doc but informs the naming.

### Wiring

- Producer image is unchanged; role is selected via a CLI flag (mirroring the `project_role_split` plan): `--role=Daemon`.
- In `Program.cs`, the role gates Hangfire's `BackgroundJobServerOptions.Queues`:
  - `Api`: no `BackgroundJobServer` registered.
  - `Ingest`: as planned.
  - `Worker`: `Queues = ["default", ...]` — **excludes** `daemon`.
  - `Daemon`: `Queues = ["daemon"]` — exclusively this queue.
- Streamer enqueue sites (`CompetitionStreamScheduler` and any direct `Enqueue<ICompetitionBroadcastingJob>` call sites) specify `[Queue("daemon")]` or pass the queue name explicitly.

### K8s manifests (per-sport)

In `sports-data-config`, add per-sport Daemon Deployments:

- `producer-mlb-daemon`, `producer-ncaafb-daemon`, `producer-nfl-daemon`, etc. — same pattern as existing `producer-{sport}-worker`.
- **No KEDA `ScaledObject`** on these. Stable replica count.
- **2 replicas per sport during in-season** — one is a SPOF (planned node drain, OS update, OOM). Three is overkill.
- **Out-of-season:** scale to 0 manually via Flux. Resumes via Flux when the season starts.
- **PodDisruptionBudget** with `minAvailable: 1` — protects against voluntary disruption killing both replicas at once.
- **Resource shape:** I/O-bound (sleep, HTTP, sleep, HTTP). Start with `requests: 100m / 256Mi`, `limits: 500m / 512Mi`, tune from observed.
- **`terminationGracePeriodSeconds`:** generous enough that Hangfire's `ShutdownTimeout` (raise to e.g. 60s) has room to cancel cleanly on a planned shutdown. Streamers still won't finish in 60s, but they'll trip the `OperationCanceledException` correctly (with the swallow bug fixed) and Hangfire will re-queue to the other replica.

### What Daemon pods do NOT do

- Do not handle MassTransit consumer subscriptions for broker-driven work. Those stay on `Ingest`-role pods.
- Do not serve HTTP. Those stay on `Api`.
- Do not run KEDA-scaled queue workers. Those stay on `Worker`.

The Daemon role is purely a Hangfire `BackgroundJobServer` on a dedicated queue.

## Step 2E — Why a backstop is still the right shape

Even with the Daemon role hosting streamers, a reconcile backstop earns its keep:

- **Daemon pods can still die.** OOM, node failure, K8s eviction, voluntary disruption. PodDisruptionBudget reduces but does not eliminate this.
- **Hangfire retries are best-effort.** Retry count caps, Hangfire-server outage, or the surviving pod dying too can still leave streams stranded.
- **The 5-hour `MaxStreamDuration` cap is unrelated to scheduling** — a long MLB game with extra innings + rain delay still hits `Timeout`, marks Failed, and never publishes.
- **`>10 consecutive status fetch failures`** throws `InvalidOperationException`, which Hangfire may or may not retry depending on its config.
- **The reconcile job is sport-scoped, low-frequency, and idempotent.** Negligible cost; provides a uniform safety net independent of Hangfire semantics. Runs on the Daemon pod itself (recurring job on the same `daemon` queue).

Options considered (orthogonal — final design uses 1+2+3):

1. **Daemon role split (Step 2D)** — strategic structural fix.
2. **Stop swallowing the cancellation (Step 2A)** — tiny diff; lets Hangfire re-queue on the rare Daemon-pod loss so the surviving replica picks up.
3. **External reconciliation backstop (Step 3)** — durable safety net for everything else.

## Step 3 — Proposed `FinalizationReconcileJob`

**Cadence:** every 15 minutes during game hours (or always; ESPN polling cost is bounded by the number of stranded streams).

**Selection predicate:**

```csharp
var stranded = await _dbContext.CompetitionStreams
    .Include(s => s.Competition).ThenInclude(c => c!.Contest)
    .Include(s => s.Competition).ThenInclude(c => c!.ExternalIds)
    .Where(s =>
        s.StreamStartedUtc != null &&
        s.StreamStartedUtc > now - TimeSpan.FromHours(48) &&    // window of concern
        (s.Status == CompetitionStreamStatus.Active ||
         s.Status == CompetitionStreamStatus.Failed) &&
        s.Competition!.Contest!.IsFinal == false)
    .ToListAsync();
```

**Per stranded row:**

1. Resolve ESPN status URI from `CompetitionExternalIds[Espn].SourceUrl` via `EspnUriMapper.CompetitionRefToCompetitionStatusRef` — same path the streamer uses.
2. GET status; if `STATUS_FINAL`:
   - `_eventBus.Publish(new ContestCompleted(...))` with `DeliveryMode.Direct` — mirror `CompetitionStreamerBase.PublishContestCompletedAsync`.
   - `_eventBus.Publish(new DocumentRequested(... DocumentType=Event ...))` for the parent Event refresh — mirror `PublishContestRefreshOnFinalAsync`.
   - Mark the `CompetitionStream` row `Completed` with `StreamEndedUtc = now`. Don't clobber `FailureReason` — leave it as evidence of what originally failed.
3. If not `STATUS_FINAL` and `StreamStartedUtc > now - 12h`, leave it alone (game still in progress; streamer or next reconcile pass will handle it). Outside that window, log a warning — game has been "in progress" for >12h, which is anomalous.

**Why `DeliveryMode.Direct`:** same reason the streamer uses it — stateless publish, no entity write inside the publish call, the EF outbox interceptor has no `SaveChangesAsync` anchor to flush against. See `CompetitionStreamerBase.cs:640–646`.

**Idempotency:** the existing consumer-side guards already handle this:
- `ContestCompletedHandler` → enrichment processors check `Contest.IsFinal` or status before doing work.
- `ContestFinalizedHandler` → `PickScoringProcessor` short-circuits when no unscored picks for the contest.

So at-least-once redelivery from a reconcile pass that races a slow streamer is safe.

## Step 4 — Scope notes

- **Sport gating:** the reconcile job should be sport-scoped the same way the streamer is. Producer pod for MLB reconciles MLB streams; Football pod reconciles Football. Keeps ESPN URI resolution sport-agnostic at the call site without cross-sport DbContext reach.
- **No new ESPN polling pressure during games:** the predicate only touches streams that are stranded (`Active` past expected end, or `Failed`). A healthy in-progress game's stream is in `Active` with a recent `ModifiedUtc`; we want to skip those unless they're past a sane upper bound (e.g. `StreamStartedUtc > now - 6h` is suspect for MLB regardless of streamer state).
- **Observability:** log `Reconciled stranded stream {StreamId} → published ContestCompleted` at Info so a single Seq query proves the backstop ran.
- **No replacement of the streamer:** the streamer remains the primary path. The backstop only fires when it failed. This preserves the low-latency in-game updates the streamer provides.

## Step 4 — Sequencing plan

The three fixes (Daemon role, swallow-bug fix, reconcile backstop) ship across multiple PRs spanning `sports-data` and `sports-data-config`. Order matters — if streamers are routed to the `daemon` queue before a Daemon pod exists to process them, nothing runs.

| PR | Repo | Description | Risk |
|----|------|-------------|------|
| **A** | `sports-data` | Add `Daemon` to the Producer role enum + CLI flag. `Program.cs` branches `BackgroundJobServerOptions.Queues` by role. **In transition:** existing `Worker` role keeps listening on both `default` AND `daemon` so streamer routing in PR C doesn't strand anything before PR B lands. Includes the swallow-bug fix (rethrow in `CompetitionStreamerBase.cs:180`) — low-risk and benefits even the pre-Daemon world. | Low — additive. |
| **B** | `sports-data-config` | Per-sport `producer-{sport}-daemon` Deployment manifests (replicas=2, no ScaledObject, PodDisruptionBudget). Start with MLB only since that's the active sport; NCAAFB / NFL can be templated and held disabled until in-season. | Low — new manifests, no existing-deployment changes. |
| **C** | `sports-data` | Streamer enqueue specifies `[Queue("daemon")]`. From this PR on, new streamer jobs land on Daemon pods. | Medium — verify enqueue site exhaustively. |
| **D** | `sports-data` | `Worker` role stops listening on `daemon` queue. After this, KEDA-scaled workers cannot pick up streamers at all. Wait until PR B + PR C have been in prod long enough to confirm Daemon pods are healthy. | Low (after C is stable). |
| **E** | `sports-data` | `FinalizationReconcileJob` per Step 3. Recurring on the `daemon` queue. | Low — read-then-publish, idempotent consumers. |
| **F** | One-shot | Recover yesterday's 10 stranded contests. Either a one-shot admin endpoint that runs the reconcile logic immediately, or a SQL script that flips `Contest.IsFinal = true` and re-publishes the events. Decide based on how PRs A–E land. | Medium — touches live data. |

**Reasonable shipping cadence:**

- A + B together (code + manifests land same window so transition mode is brief).
- C after the Daemon pods are running and healthy.
- D one game-day later, after observation.
- E parallel to D — independent.
- F as soon as we have either reconcile job (E) or one-shot script ready.

## Open questions

1. **Does the daily `PickScoringJob` re-enqueue scoring once `Contest.IsFinal` flips?** It enumerates by `UserPicks.ScoredAt == null`, not by `Contest.IsFinal`, so yes — but worth confirming the processor doesn't refuse to score until `Contest.IsFinal = true`. If it does refuse, the backstop chain works (reconcile → ContestCompleted → enrichment → ContestFinalized → scoring). If it doesn't, we may need a small fix on the processor side.
2. **Should the streamer's Failed branches also publish `ContestCompleted` directly?** Tempting, but two issues: (a) on `"Stream exceeded max duration"`, we don't actually know the game is final — we just know we stopped looking; publishing would create false finalizations. (b) on Hangfire-killed-the-process, no code runs to publish anyway. The reconcile backstop handles both cleanly because it re-checks ESPN before publishing.
3. **Does `CompetitionStreamScheduler` enqueue streamers via Hangfire `Enqueue<T>` or a different path?** Need to confirm the actual enqueue mechanism before deciding how to specify the queue in PR C. If it's a recurring job registered via `IRecurringJobManager.AddOrUpdate`, the queue is specified differently than a one-shot `Enqueue<T>`.

## Next steps

### Investigation (mostly done)

- [x] Run Step 1 SQL against MLB prod; record the distribution of `StreamStatus` values for the 14 played games from 2026-06-13. → 4 `Completed`, 10 `Failed` with `"Cancelled by external request"`.
- [x] Run Seq queries for the cancellation warnings. → 11 log lines across 5 distinct time clusters; **not** a single pod event.
- [x] Confirm KEDA scale-down is the cancellation trigger: 18h baseline pod + 112s extra pod confirms KEDA is recycling extras; baseline survives, extras don't.
- [ ] Verify Hangfire's behavior on rethrown `OperationCanceledException` empirically — useful both for PR A's swallow-bug fix and PR D's confidence. Cheap version: search Seq for `"Broadcasting job started for"` against the 10 cancelled streams' CompetitionIds — if each has only one, Hangfire never retried (confirming the swallow bug killed retry).
- [ ] Confirm `PickScoringProcessor`'s gating logic re: `Contest.IsFinal` (Open question #1).
- [ ] Confirm the streamer enqueue mechanism in `CompetitionStreamScheduler` (Open question #3).

### Implementation (Step 4 PRs)

- [ ] **PR A** — Daemon role + swallow-bug fix.
- [ ] **PR B** — `producer-mlb-daemon` Deployment in `sports-data-config`.
- [ ] **PR C** — Streamer enqueue → `daemon` queue.
- [ ] **PR D** — `Worker` role stops listening on `daemon`.
- [ ] **PR E** — `FinalizationReconcileJob` (Step 3).
- [ ] **PR F** — One-shot recovery for the 10 stranded contests.

### Out of scope (tracked separately)

- [ ] Sport-gate or otherwise resolve the `EventCompetitionProbability - URI is null` warning (4 occurrences on 2026-06-13). Separate from the cancellation issue but flagged for follow-up.
- [ ] Add a Seq dashboard / alert for "reconcile published ContestCompleted" — if this fires often, the streamer needs a deeper look even with the Daemon role.
- [ ] Migrate AI generation jobs (`MatchupPreviewGenerator`, `ContestRecapProcessor`) and DLQ reprocessor to the `daemon` queue. Same risk profile, different urgency.
