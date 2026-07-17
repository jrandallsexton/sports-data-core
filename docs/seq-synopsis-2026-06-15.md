# Seq 24h Synopsis - 2026-06-15

Window: 2026-06-14 ~12:00Z -> 2026-06-15 ~12:00Z

## 1. Headline

Quiet day overall. Producer redeploy to `:4730` rolled cleanly, MLB Daemon pods are up and healthy, and the new `CompetitionStreamerBase` rethrow path has not surfaced any `OperationCanceledException` errors. Two things to look at: (a) **`FinalizationReconcileJob` has zero log output in 24h** - either not firing or silent; (b) API pod hit `Max Pool Size=10` connection-pool exhaustion in a brief 09:00Z burst that knocked out 7 league-week scoring enqueues.

## 2. Top error patterns (24h)

| # | Source | Message / Exception | Count | First seen (Z) | Last seen (Z) |
|---|---|---|---|---|---|
| 1 | Producer `DocumentCreatedHandler` (Role=Ingest, Sport=BaseballMlb) | `HANDLER_MAX_RETRIES (10)` for `EventCompetitionSituation` doc `f838bba5...` - lastPlay not found on event 401815756 | ~140+ | 00:05:23 | 04:00:12 |
| 2 | Producer `DocumentCreatedProcessor` / `EventCompetitionAthleteStatisticsDocumentProcessor` | `DOC_CREATED_PROCESSOR_FAILED` -> `DbUpdateConcurrencyException` (rows affected 0/1) | ~70 | 03:55:57 | 04:06:49 |
| 3 | Provider `ResourceIndexItemProcessor` + `EspnHttpClient` | ESPN `ServiceUnavailable` on probabilities `4018157131301990057` (event 401815713) | ~75 | 03:52:12 | 04:13:34 |
| 4 | API `Hangfire.AutomaticRetryAttribute` + `Microsoft.EntityFrameworkCore.Update` | `NpgsqlException: connection pool has been exhausted (Max Pool Size=10)` -> downstream `DbUpdateException` on `PickemGroupWeekResult` unique-constraint duplicate during retry | 16 | 09:00:17 | 09:00:40 |
| 5 | API `PickScoringProcessor` | `Failed to enqueue league-week scoring. LeagueId=aa7a482f-...` (caused by #4 pool exhaustion) | 7 | 09:00:17 | 09:00:20 |
| 6 | Producer EF startup probe | `Failed executing DbCommand (30,030ms) SELECT __EFMigrationsHistory` - 30s timeout during new image cold-start | 5 | 11:25:46 | 11:28:37 |
| 7 | API `NotificationHub` | SignalR `OperationCanceledException: Client hasn't sent a message/ping within ClientTimeoutInterval` (known noise) | several | continuous | continuous |

## 3. Streamer workstream signals

- **`CompetitionStreamerBase` rethrow (PR A):** No `OperationCanceledException` from any `*CompetitionStreamer*` SourceContext in 24h. Pre-redeploy MLB game finished cleanly at **02:14:52Z**: `Polling loop exited with outcome Final` -> `Publishing ContestCompleted` -> `Stopping 3 active workers` -> `Worker for X cancelled gracefully` x3 -> `All workers stopped successfully` -> `Stream status updated to Completed`. The graceful-cancel path is exercising correctly.
- **Daemon role (PR C):** MLB Daemon pods are up. First Daemon log lines at **11:39:49Z** show `EnsureQueuesHostedService` binding `document-dead-letter` queue to its exchange under `Role=Daemon, Sport=BaseballMlb`. Zero errors, zero warnings from Daemon role in 24h.
- **`FinalizationReconcileJob` (PR E):** **Zero log events in 24h** at any level - no `Reconcile` or `Finalization` string matches. Two possibilities: (a) job hasn't fired since the redeploy yet (15-min cadence, redeploy ~04:33Z, Daemon-up ~11:39Z; first scheduled run could legitimately be pending), or (b) it's running but emitting nothing - either way, worth a deliberate check.
- **Streamer DLQ pressure:** The 140+ `HANDLER_MAX_RETRIES` on `EventCompetitionSituation` for a single MLB doc (event 401815756, the same game that finalized at 02:14Z) is the documented "ESPN lastPlay eventual consistency" pattern, not a regression. It happened on Role=Ingest, pre-redeploy.

## 4. Suggested next investigation

1. **Did the reconcile job run at all?** `SourceContext like '%FinalizationReconcile%' or @MessageTemplate like '%Reconcile%'` over the last 6h with no level filter. If still empty, check Hangfire dashboard for `FinalizationReconcileJob` last execution.
2. **API pool-exhaustion blast radius:** `ApplicationName = 'SportsData.Api' and RenderedMessage like '%connection pool%'` over 24h - was 09:00Z a one-off (probable cron collision) or a recurring squeeze? `Max Pool Size=10` is low for an API pod also running Hangfire.
3. **Streamer rethrow confirmation:** `SourceContext like '%CompetitionStreamer%' and @Level in ['Error','Warning']` over the next 24h to confirm the new rethrow stays clean across the next MLB game cycle on the new image.

## 5. Things deliberately skipped

- ESPN `ServiceUnavailable` / `NotFound` on individual play and probability refs - the documented ESPN data-sparsity / eventual-consistency pattern; provider retries handle it.
- `DbUpdateConcurrencyException` on `EventCompetitionAthleteStatistics` - the known parallel-fan-out contention; processor retry recovers.
- SignalR `OperationCanceledException` from `NotificationHub` - mobile-client idle disconnects, baseline noise.
- 30s `__EFMigrationsHistory` timeout during cold-start - one-off startup probe slowness on the new image; pod came up successfully.
