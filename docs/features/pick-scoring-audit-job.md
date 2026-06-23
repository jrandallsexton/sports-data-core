# Pick Scoring Audit Job

**Status:** Proposed
**Date:** 2026-06-16
**Driver:** Historical pick-scoring corruption from the pre-2026-06-16 race where `PickScoringJob` enqueued unfinalized contests and `PickScoringService` silently scored picks against `Guid.Empty` winners (see [contest-finalization-event-restructure](contest-finalization-event-restructure.md) for the upstream fix).

## Problem

The race fixed today only blocks **future** corruption. Picks that were already scored against pre-enrichment data carry permanent wrong values:

- `IsCorrect = false` (compared against `Guid.Empty`)
- `PointsAwarded = 0` (a confidence-points pick worth 12 awarded 0)
- `ScoredAt != null` — so no future `PickScoringJob` cron will revisit them

The downstream effect cascades to `PickemGroupWeekResult` aggregates, which the leaderboard reads from.

Two corruption modes that should both be detectable post-fact:

1. **Scored-then-finalized:** pick scored against an unfinalized contest, which later enriched. The contest now has valid `WinnerFranchiseId` / `FinalizedUtc`, but the stored pick result is the bad pre-enrichment computation. **This is invisible to any "is the contest finalized?" check at audit time** because by then it is.
2. **Scored-and-still-unfinalized:** pick scored against a contest that hasn't enriched. Edge case but possible if upstream is broken. Detection signal: `GetMatchupResult` returns `NotFound` (the new SQL filter rejects unfinalized rows).

Plus a third concern: future scoring-logic bugs we don't yet know about. A periodic re-score-and-compare audit catches **any** divergence between current PickScoringService output and what's stored, regardless of root cause.

User expectation: "I _highly_ imagine there are likely some NCAAFB picks that were scored incorrectly last season." Long-term value is unclear; near-term value is repairing back-to-2024 corruption.

## Proposed flow

```text
PickScoringAuditJob(Sport)   [CRON, one schedule per active sport, staggered]
  │
  ├─ Load distinct ContestIds where any UserPick.ScoredAt != null
  │   AND the pick's PickemGroup.Sport == this job's Sport
  │   (no time-window filter — audit is exhaustive by design)
  │
  ├─ For each ContestId:
  │   ├─ Sport is known up-front from the job parameter — no per-contest resolution needed
  │   ├─ HTTP GET Producer.GetMatchupResult(contestId)  via ContestClientFactory.Resolve(sport)
  │   │
  │   ├─ IF NotFound  (contest not finalized — stuck-pick mode)
  │   │   ├─ Load all scored picks for the contest
  │   │   ├─ Reset ScoredAt = null, IsCorrect = null, PointsAwarded = 0
  │   │   ├─ LogError per pick: "Pick scored against unfinalized contest"
  │   │   └─ Track affected (LeagueId, SeasonYear, SeasonWeek) for fan-out
  │   │
  │   └─ IF Success   (contest finalized — re-score and compare)
  │       ├─ Load all scored picks for the contest (with PickemGroup + matchup)
  │       ├─ For each pick:
  │       │   ├─ Capture stored (IsCorrect, PointsAwarded)
  │       │   ├─ Re-run PickScoringService.ScorePick on a working copy
  │       │   ├─ Compare against stored values
  │       │   └─ IF mismatch:
  │       │       ├─ Overwrite IsCorrect / PointsAwarded in place
  │       │       ├─ LogError with before/after snapshot
  │       │       └─ Track affected (LeagueId, SeasonYear, SeasonWeek)
  │       └─ SaveChangesAsync (one commit per contest)
  │
  └─ For each distinct affected (LeagueId, SeasonYear, SeasonWeek):
      └─ Enqueue IScoreLeagueWeeks  (Hangfire — same fan-out PickScoringProcessor uses)
```

## Decisions

### D1. Audit re-runs `PickScoringService.ScorePick` directly

No re-implementation of scoring logic. The audit calls the same service the live path uses, on a throwaway clone of the pick, then compares the result to the stored row. This guarantees by construction that audit and live scoring stay in sync — if scoring rules change, the audit picks up the new rules without a code change.

The clone is necessary because `ScorePick` mutates the pick in place (sets `IsCorrect`, `PointsAwarded`, `ScoredAt`, `WasAgainstSpread`). Cloning lets us read the proposed result and decide whether to apply it.

### D2. Correct in place, do not reset+rescore

For finalized-contest mismatches: overwrite `IsCorrect` and `PointsAwarded` directly. `ScoredAt` stays at the original timestamp (it correctly marks "when scoring decided this pick") and gets a sibling `ScoredAt` ModifiedUtc update for audit-trail.

Alternatives considered: reset `ScoredAt = null` and let `PickScoringJob` re-score on its next run. Rejected because it (a) adds latency to user-visible correction, (b) loses the audit's point-of-comparison, and (c) splits responsibility — the audit job already loaded the data and computed the answer; deferring the write is wasteful.

For unfinalized-contest stuck picks: do reset `ScoredAt = null` and clear `IsCorrect` / `PointsAwarded`. The pick should not have been scored at all; clearing it lets the normal path re-score once enrichment lands.

### D3. Fan out league-week rescoring

After per-contest correction, audit collects a `HashSet<(Guid LeagueId, int SeasonYear, int SeasonWeek)>` of affected league-weeks (same idiom `PickScoringProcessor` uses today, lines 117 + 175). At the tail of the job, enqueue `IScoreLeagueWeeks.Process` for each via `IProvideBackgroundJobs.Enqueue` — Hangfire absorbs the burst and `DisableConcurrentExecution` collapses duplicates per (league, year, week).

If the enqueue itself throws, log the failure and continue — `LeagueWeekScoringJob`'s nightly cron (D6 fallback) catches anything we missed.

### D4. Daily schedule at 02:00 UTC, before `PickScoringJob` at 09:00 UTC

Audit runs first so unfinalized-contest resets land in time for the daily `PickScoringJob` cron to re-score them via the normal path. Off-peak for the cluster either way.

`DisableConcurrentExecution(timeoutInSeconds: 0)` per-sport-job to prevent overlap with itself if a previous run runs long. With ~tens of thousands of picks at MLB+NCAAFB+NFL scale, expected runtime is dominated by Producer HTTP roundtrips — one per distinct contest. Estimated <5 min per sport at current scale; flag for re-evaluation if any single sport grows past 30 min.

Per-sport stagger so the three audits don't all hammer different Producer pods at the same instant: FootballNcaa 02:00, FootballNfl 02:15, BaseballMlb 02:30 UTC. The pods are separate so concurrent execution wouldn't actually collide, but staggering keeps Seq output cleaner and makes "which sport's audit is misbehaving" trivial to identify.

### D4a. One job class, one registration per sport

`PickScoringAuditJob.ExecuteAsync(Sport sport)` takes the sport as a parameter. `IRecurringJobManager.AddOrUpdate<PickScoringAuditJob>(...)` registers it once per active sport with a distinct job name (`PickScoringAudit-FootballNcaa`, etc.). Hangfire serializes the `Sport` enum into the recurring job definition and re-injects it on each fire.

Doesn't implement `IAmARecurringJob` because that interface requires a parameterless `ExecuteAsync()`. The marker has no other consumers in the codebase (only declared, never queried), so dropping it costs nothing.

User requirement: "I don't want MLB audits trying to review NCAAFB or NFL." Sport-scoping aligns with the per-sport Producer pod boundary, gives operational isolation (a failing MLB audit doesn't block NCAAFB), and makes Seq queries naturally sport-segmented.

### D5. Exhaustive scope, no time-window filter

`UserPick.ScoredAt != null` is the only filter. The corruption could date back to early-MLB testing or NCAAFB 2025; a sliding-window filter would silently let old corruption persist as it ages out.

Cost analysis: one HTTP `GetMatchupResult` per distinct ContestId regardless of pick count on that contest. At MLB+NCAAFB+NFL season scale this is bounded by the number of finalized contests, not by pick count. Acceptable.

### D6. Tail safety net

The audit job is itself a backstop. If audit fails partway through, the next nightly run picks up where it left off — no resumption state needed because the detection logic is idempotent (re-runs of audit produce the same correction or no correction). Existing `LeagueWeekScoringJob` cron at 09:15 UTC catches any fan-out enqueue that didn't fire.

### D7. Structured logging schema

Every correction event:

```text
LogError "PickScoringAudit correction: ContestId={ContestId} PickId={PickId}
         StoredIsCorrect={StoredIsCorrect} ComputedIsCorrect={ComputedIsCorrect}
         StoredPoints={StoredPoints} ComputedPoints={ComputedPoints}
         LeagueId={LeagueId} SeasonYear={SeasonYear} Week={Week}
         Mode={Mode}"
```

`Mode = "Mismatch"` or `"Unfinalized"`. Single message per pick keeps Seq aggregations simple — count by Mode, group by LeagueId, etc.

Per-contest summary as LogInformation; per-run summary at the end (LogInformation): contests audited, picks examined, mismatches corrected, unfinalized resets, league-weeks enqueued.

## File-by-file plan

| File | Change |
|---|---|
| `src/SportsData.Api/Application/Jobs/PickScoringAuditJob.cs` | **New** — cron entry point. Loads distinct ContestIds, delegates per-contest work to a processor. |
| `src/SportsData.Api/Application/Scoring/PickScoringAuditProcessor.cs` | **New** — per-contest audit. Mirrors `PickScoringProcessor` shape: resolve sport, fetch MatchupResult, iterate picks, write corrections, collect fan-out targets. Constructor takes `AppDataContext`, `IContestClientFactory`, `IPickScoringService`, `IProvideBackgroundJobs`, `IDateTimeProvider`, `ILogger`. |
| `src/SportsData.Api/Application/Scoring/IPickScoringAudit.cs` | **New** — interface for Hangfire enqueueing (mirrors `IScorePicks`). Single `Process(AuditContestCommand)` method. |
| `src/SportsData.Api/Application/Scoring/AuditContestCommand.cs` | **New** — record `(Guid ContestId, Sport Sport, Guid CorrelationId)`. Sport is captured up-front so the processor doesn't repeat the resolution join. |
| `src/SportsData.Core/Common/CausationId.cs` | Add `Api.PickScoringAuditProcessor` GUID so audit-corrected `ModifiedBy` writes are traceable. |
| `src/SportsData.Api/DependencyInjection/ServiceRegistration.cs` | Wire `IPickScoringAudit` → `PickScoringAuditProcessor`. Register **one** recurring cron per active sport: `PickScoringAudit-FootballNcaa` at `0 2 * * *`, `PickScoringAudit-FootballNfl` at `15 2 * * *`, `PickScoringAudit-BaseballMlb` at `30 2 * * *`. |
| `test/unit/SportsData.Api.Tests.Unit/Application/Jobs/PickScoringAuditJobTests.cs` | **New** — cover the enqueue-per-contest behavior. |
| `test/unit/SportsData.Api.Tests.Unit/Application/Scoring/PickScoringAuditProcessorTests.cs` | **New** — cover: (a) finalized + matches → no write, (b) finalized + mismatch → correct in place + enqueue league-week, (c) NotFound → reset ScoredAt + log, (d) finalized + no scored picks → no-op, (e) sport unresolvable → log + skip. |

Total: ~7 file touches. No DB migration. No DTO/contract change.

## Sequencing

The audit job is non-coupled to any other deploy. Ship after the today's bug-fix PR (SQL filter + nullable DTO + service guards) so the audit's NotFound-detection path matches the new SQL semantics. Roll out, watch first nightly run.

## Operating procedure

After first deploy:

1. Watch the 02:00 UTC run on day 1. Expect a large initial sweep of corrections — back-corruption from MLB testing and NCAAFB 2025.
2. Aggregate `LogError "PickScoringAudit correction"` in Seq, group by `Mode`. Sanity-check sample picks manually.
3. After steady state (subsequent nightly runs should find few or zero mismatches), reassess D5 — could narrow scope to last N days if cost grows. Reassess every quarter; deprecate the job entirely if it goes 90 consecutive days with zero corrections AND we have higher confidence in the upstream scoring path.

## Test plan

- [ ] Unit: `PickScoringAuditJob` enqueues `IPickScoringAudit.Process` once per distinct ContestId with `ScoredAt != null` picks.
- [ ] Unit: `PickScoringAuditProcessor` correctly identifies mismatch and corrects in place when finalized contest stored result differs from re-computed.
- [ ] Unit: `PickScoringAuditProcessor` resets `ScoredAt`, `IsCorrect`, `PointsAwarded` when `GetMatchupResult` returns `NotFound`.
- [ ] Unit: `PickScoringAuditProcessor` enqueues `IScoreLeagueWeeks` exactly once per affected `(LeagueId, SeasonYear, Week)` tuple per run.
- [ ] Unit: `PickScoringAuditProcessor` does NOT write when stored values match re-computed values (no churn on healthy picks).
- [ ] Manual E2E (local with prod DB snapshot): one bad pick, one healthy pick, observe correction + enqueue + LogError.
- [ ] Prod monitoring after first run: Seq query for `PickScoringAudit correction` count by Mode + LeagueId. Verify leaderboard for affected leagues reflects corrections within minutes (LeagueWeekScoringProcessor fanout).

## Out of scope

- **Configurable schedule** via Azure App Config. Hardcode `Cron.Daily(2)` initially; only externalize if we end up tuning it frequently.
- **Audit history table**. The LogError stream IS the audit trail. Adding a `PickemGroupUserPickAuditLog` table is a separate decision (would also need retention policy, indexing strategy, etc.).
- **Self-deprecation**. The job stays forever in code; we just stop scheduling it when we trust the upstream path. Don't build automation for that until we know the cadence of trust.
- **Cross-contest checks** (e.g. consistency between contest score and a pick's IsCorrect across the schema). Per-pick audit only.

## Open items

- Confirm `IProvideBackgroundJobs.Enqueue<IPickScoringAudit>(p => p.Process(cmd))` works with the existing Hangfire job activator the API uses (same as `IScorePicks` enqueue path).
- Decide whether `PickScoringAuditJob` should `DisableConcurrentExecution` at the job level or at the per-contest processor level. Job-level is sufficient given the cron only runs once nightly.
- Confirm correlation ID propagation through the audit → service → enqueue chain matches the live scoring path so Seq traces stay readable.
