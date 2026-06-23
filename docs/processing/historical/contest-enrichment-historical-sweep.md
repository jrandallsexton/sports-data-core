# Contest Enrichment Historical Sweep

**Status:** Implementing
**Date:** 2026-06-16
**Driver:** ContestEnrichmentJob's season-week filter has been silently excluding ~44K historical contests (NCAAFB 19,208 + NFL 1,048 + MLB 24,430) that have `STATUS_FINAL` ready for enrichment but never had `FinalizedUtc` stamped.

## Problem

`ContestEnrichmentJob` is registered as a daily cron. Its steady-state code path (`BackfillCurrentSeason = false`) only considers contests whose `SeasonWeekId` matches the current two season weeks. Its backfill path (`BackfillCurrentSeason = true`, currently always on) widens to "current season only." Neither catches historical contests.

After the season-week filter dropped, the actual blocker on enrichment is just status availability — the sport-specific enrichment processors already short-circuit cleanly when `CompetitionStatus.StatusTypeName != "STATUS_FINAL"`. The job's filter has been turning away contests that the processor would have happily finalized.

A subset of those 44K contests are *truly cancelled* (ESPN status = `STATUS_CANCELED`). Once the job stops excluding them, they would re-enqueue every nightly run because the enrichment processor never stamps `FinalizedUtc` on cancelled games — they're not final. We need a separate marker.

## Decisions

### D1. Add `CancelledUtc DateTime?` to `ContestBase`

Sport-agnostic, single nullable column. Inherited by `FootballContest` and `BaseballContest`. Two EF migrations (one per sport DbContext) add the column. No index — the unbounded query runs at most daily and the column participates in a single `IS NULL` predicate.

### D2. `EventCompetitionStatusDocumentProcessor` is the only `CancelledUtc` setter

When the status processors (`EventCompetitionStatusDocumentProcessor<TDataContext>` for Football, `BaseballEventCompetitionStatusDocumentProcessor<TDataContext>` for Baseball) see `StatusTypeName == "STATUS_CANCELED"`:

- Load the parent `Contest` via the projected `ContestId`.
- If `Contest.CancelledUtc is null`, stamp it with `_dateTimeProvider.UtcNow()`.
- If already non-null, leave it alone — preserves the "first observed" timestamp.

If the status transitions *away from* `STATUS_CANCELED` (rare: a cancelled game gets uncancelled), log a `LogWarning` but do not clear `CancelledUtc`. Treat cancellation as irrevocable; require a manual override if a real bad case appears.

The Football processor doesn't currently inject `IDateTimeProvider`; add it. Baseball already has it.

### D3. Only `STATUS_CANCELED` qualifies as terminal for now

`STATUS_FORFEIT` is deferred. Real forfeits are vanishingly rare in NFL/NCAAFB and effectively extinct in MLB. If a forfeit game appears and someone complains, revisit. `STATUS_POSTPONED`, `STATUS_SUSPENDED`, `STATUS_DELAYED` are all transient — game eventually resumes/plays. Status processor already publishes `ContestStatusChanged` on these; nothing else to do.

### D4. ContestEnrichmentJob runs an unbounded query

```csharp
var contests = await _dataContext.Contests
    .AsNoTracking()
    .Where(c => c.FinalizedUtc == null
             && c.CancelledUtc == null
             && c.StartDateUtc < _dateTimeProvider.UtcNow())
    .OrderBy(c => c.StartDateUtc)
    .ToListAsync();
```

- `FinalizedUtc IS NULL` — not yet enriched.
- `CancelledUtc IS NULL` — not terminal-cancelled. Once D2's setter fires, those are permanently excluded.
- `StartDateUtc < now` — past games only. Future-scheduled games can't be enriched.

Delete the `BackfillCurrentSeason` flag, the `SeasonWeeks` query, and the per-season-week loop. The 3h grace window (`< now + 3h`) goes away — including future games never made sense for enrichment.

### D5. Daily cadence (`Cron.Daily(8)`) stays

The 44K initial sweep is a one-shot, triggered manually via the Hangfire dashboard's "Trigger now" on the recurring job. After that, daily runs see only the small day's-worth of newly-finalized games (~hundreds during MLB regular season, ~dozens NCAAFB on game days, ~zero NFL most days). Event-driven path (`ContestCompleted` → `ContestCompletedHandler` schedules `EnrichContestCommand` with 30s delay) handles live churn; the daily cron is cheap insurance for events lost in transit.

### D6. No backfill for existing cancelled contests

After the column ships, only newly-observed cancellations get `CancelledUtc` stamped. Historical cancelled games already in the DB stay non-cancelled-from-our-perspective and re-enqueue every run. Cost: a fast no-op per re-enqueue (enrichment processor short-circuits on non-FINAL). We accept the noise and revisit once we see what surfaces from the sweep.

### D7. No `ContestCancelled` event published

The status processor already publishes `ContestStatusChanged` on transitions. Downstream subscribers can branch on `StatusTypeName == "STATUS_CANCELED"` if they need to react. No new event surface.

### D8. No changes to enrichment processors

`FootballContestEnrichmentProcessor` and `BaseballContestEnrichmentProcessor` stay as-is. They already short-circuit cleanly on `StatusTypeName != "STATUS_FINAL"`, log informationally, and return. A cancelled contest that survives to the processor (e.g., first nightly run after deploy, before its status doc re-fires) will hit this short-circuit. Wasted enqueue, fast no-op.

### D9. Picks on cancelled games — deferred

Audit job will see `GetMatchupResult → NotFound` for cancelled contests and reset pick `ScoredAt = null`. `PickScoringJob` will then re-enqueue, get NotFound, short-circuit. Cycle repeats nightly with small per-pick cost. Real fix is probably an `IsVoid` flag on `PickemGroupUserPick` plus a "skip cancelled" branch in `PickScoringJob`, but defer until volume warrants — see how big the cancelled-game pick pool actually is post-sweep.

## File-by-file plan

| File | Change |
|---|---|
| `src/SportsData.Producer/Infrastructure/Data/Entities/ContestBase.cs` | Add `public DateTime? CancelledUtc { get; set; }`. No EF config — nullable column with default mapping is sufficient. |
| `src/SportsData.Producer/Migrations/Football/{timestamp}_AddContestCancelledUtc.cs` | **New** EF migration — adds `CancelledUtc` nullable timestamptz column. |
| `src/SportsData.Producer/Migrations/Baseball/{timestamp}_AddContestCancelledUtc.cs` | **New** EF migration — mirror for Baseball context. |
| `src/SportsData.Producer/Application/Documents/Processors/Providers/Espn/Common/EventCompetitionStatusDocumentProcessor.cs` | Inject `IDateTimeProvider`. After determining the new status, call a small inline helper that loads the parent Contest and stamps/logs `CancelledUtc`. |
| `src/SportsData.Producer/Application/Documents/Processors/Providers/Espn/Baseball/BaseballEventCompetitionStatusDocumentProcessor.cs` | Same `CancelledUtc` stamp logic. Uses existing `IDateTimeProvider`. |
| `src/SportsData.Producer/Application/Contests/ContestEnrichmentJob.cs` | Delete `BackfillCurrentSeason` flag, `ExecuteBackfillCurrentSeason` method, and the season-week query path. Inject `IDateTimeProvider`. New `ExecuteAsync` is the unbounded query. |
| `test/unit/SportsData.Producer.Tests.Unit/Application/Documents/Processors/Providers/Espn/Football/EventCompetitionStatusDocumentProcessorTests.cs` | Add tests for: STATUS_CANCELED stamps `CancelledUtc`; second observation with CancelledUtc already set is no-op; transition away from CANCELED logs warning, doesn't clear. |
| `test/unit/SportsData.Producer.Tests.Unit/Application/Documents/Processors/Providers/Espn/Baseball/BaseballEventCompetitionStatusDocumentProcessorTests.cs` | Same set of tests for the Baseball processor (create file if it doesn't exist). |
| `test/unit/SportsData.Producer.Tests.Unit/Application/Contests/ContestEnrichmentJobTests.cs` | **New** test file. Coverage: unfinalized + uncancelled + past start → enqueued; cancelled → skipped; future-start → skipped; finalized → skipped. |

## Operational rollout

1. Test EF migrations locally (per CLAUDE.md migration rule).
2. Local E2E: seed a few unfinalized contests of varying statuses in a copied prod DB snapshot, run the job, watch them finalize / skip appropriately.
3. Open PR. Land via normal CR cycle.
4. Deploy.
5. Verify Hangfire dashboard shows three recurring jobs registered correctly.
6. Hit "Trigger now" on `ContestEnrichmentJob` during a quiet window. Watch Seq for the new `JobRunId`-scoped logs.
7. After sweep settles (~8-10 hours expected at current worker pool size), query Postgres for the residual unfinalized count per sport. Compare to the 44K baseline to gauge what surfaced.

## Test plan

- [ ] `dotnet build` clean.
- [ ] `dotnet test` clean on `SportsData.Producer.Tests.Unit`.
- [ ] Migration applies cleanly on a local Postgres copy.
- [ ] Local E2E: cancelled contest stays out of the enrichment queue after status doc fires.
- [ ] Local E2E: previously-stuck NCAAFB contest from 2018 with `STATUS_FINAL` finalizes on first run of the new job.

## Out of scope

- Backfill of `CancelledUtc` on already-cancelled-in-DB contests (D6).
- Picks on cancelled games — `IsVoid` flag, audit-job branch (D9).
- Forfeit handling (D3).
- `ContestCancelled` event (D7).
- Sourcing-incomplete contests (no `CompetitionStatus` row): these will repeatedly no-op through enrichment. A separate sourcing audit / Provider re-fetch tool is the right venue.
