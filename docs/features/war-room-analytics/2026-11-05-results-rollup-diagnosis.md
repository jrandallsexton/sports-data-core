# Results Roll-up Diagnosis (2026-11-05)

> Point-in-time record: this audit describes the system as of its date.
> Open items and action lists reflect that day, not the current state. See
> [war-room-analytics.md](../war-room-analytics.md) for current status.

Follow-up to Finding 1 of [2026-11-01-coverage-audit.md](2026-11-01-coverage-audit.md)
(historical scores never rolled up to `Contest`). Measured against **prod**
`sdProducer.FootballNcaa`, read-only. Section 1 of the coverage audit re-run
first: unchanged since 11-01 except 2026 ranked appearances (0 -> 301, the
#853/#854 backfill landed).

## Why the scores were never rolled up

> Status: this section is the 11-05 snapshot. #856 removed the pin and gave
> the sweep a 14-day `LookbackDays` window (decision 5 below).

`ContestEnrichmentJob` is the sweep meant to finalize backfilled historical
contests, but its query has been pinned to the current season since #426
(2026-06-17):

```csharp
&& c.SeasonYear == 2026 // TODO: Remove after testing
```

So no historical contest has ever been swept. Nothing is wrong with the data
underneath.

## The data is there

Unfinalized, non-cancelled, FBS-involved contests, 2000-2025:

| Season | Unscored | No `Competition` | `STATUS_FINAL` | Final via scoring plays | Final via score rows only | Not derivable |
|---|---|---|---|---|---|---|
| 2000 | 711 | 0 | 711 | 0 | 711 | 0 |
| 2002 | 553 | 0 | 553 | 26 | 527 | 0 |
| 2003 | 422 | 0 | 422 | 259 | 163 | 0 |
| 2010 | 380 | 175 | 205 | 202 | 3 | 0 |
| 2016 | 379 | 125 | 229 | 219 | 9 | 1 |
| 2018 | 392 | 0 | 374 | 373 | 0 | 1 |
| 2019 | 403 | 0 | 402 | 401 | 1 | 0 |

Other seasons have single- or double-digit counts, mostly `STATUS_POSTPONED` /
`STATUS_CANCELED`. Across all divisions about 12,400 `STATUS_FINAL` contests
are unfinalized (2011 alone has 2,318, nearly all non-FBS).

Source agreement: where a contest has both scoring plays and competitor
score rows, the two finals disagree on 11 of ~1,300 (plays lower on 8; a late
scoring play is missing from the feed). Enrichment prefers plays, so those few
may be finalized a few points low. The audit then flags them after its retry
allowance; it does not loop.

2018 is not a re-sourcing problem after all: 373 of its 374 final contests
have scoring plays, so enrichment derives the score without score rows.

## Downstream effect of finalizing historical contests

`ContestFinalized` consumers: API pick scoring, audit invalidation and Player
Pick'em lineups are no-ops (no picks reference these contests); no push
notifications. Producer `ContestFinalizedHandler` enqueues record enrichment
for both teams (wanted: historical W/L recomputed). The only cost is one
SignalR `ContestFinalized` broadcast per contest to connected clients, so run
off-peak.

## Pre-existing: the enrichment audit backlog is starved

`ContestEnrichmentAuditJob` takes the 500 newest-finalized unaudited contests
every 6h. `ContestEnrichmentAuditProcessor` returns "Audit deferred — no
competitor score rows" when **either** side lacks score rows, without
stamping, counting or flagging. Prod now:

- 16,356 contests pending audit (finalized, not audited, not flagged).
- **490 of the 500 in the window** have a side with no score rows (seasons
  2004, 2011, 2017). They are re-deferred every run; the 06:00 UTC run on
  11-05 logged 299+ of these warnings for NCAA alone.
- The 15,856 behind them can never be reached. Fresh finals still audit
  because they sort newest.

Finalizing 2018 adds 634 more such contests (plays, no score rows) at the head
of the window. That does not make things worse in kind, but it is more noise,
and the starvation needs a decision regardless:

- (a) A deferral increments `AuditAttemptCount`, so these flag after the
  allowance. Cheap, but "flagged" would then mean two different things.
- (b) When one side has no score rows, audit against the scoring plays
  instead. The scores then match by construction (same source as enrichment),
  so the result is "consistent", not "verified".
- (c) A separate `AuditUnverifiableUtc` stamp that takes the row out of the
  window. This is the most explicit option.

## Plan

Operator (prod writes):

1. `POST {{baseUrlProducer}}/api/contests/finalize`
   (`bruno/producer/contests-finalize-seasonYear.yml`) with
   **`"reprocessEnriched": false`** (the bruno file defaults to `true`, which
   enqueues every contest in the season). One season at a time, off-peak,
   largest last: 2002, 2003, 2019, 2011, 2016, 2010, 2018, then the small
   seasons. The largest enqueue is about 2,700. The refresh handler exhausted
   the Hangfire pool at roughly this size on 11-02 (reran clean), so watch Seq
   for `Npgsql` pool errors per season.
2. Re-run section 1 of the coverage audit; expect `pct_final_scored` near 99%
   for those seasons, with ATS following for 2012+.

Code (small PR):

3. DONE in #856. `GreaterThan(2000)` -> `GreaterThanOrEqualTo(2000)` in both
   `FinalizeContestsBySeasonYearCommandValidator` and
   `RefreshContestsBySeasonYearCommandValidator`, with tests; then finalize 2000.

Decisions:

4. Audit starvation: (a), (b) or (c) above.
5. DECIDED in #856 (bounded 14-day window). The `SeasonYear == 2026` pin in
   `ContestEnrichmentJob`. Removing it makes
   the weekly sweep re-enqueue every unfinalizable historical contest
   (postponed, missing `Competition`) every week. A bounded window (e.g.
   start date within the last N days) keeps the sweep's real job, which is
   catching lost live finalizations. Step 1 handles the history.

## Still missing after the roll-up (needs ESPN sourcing)

- 2010: about 2,460 contests (all divisions; 175 FBS-involved) have no
  `Competition` row.
- 2016: 125 FBS-involved contests have no `Competition` row; 22 have no status.
- 2020: 991 unfinalized, only 4 `STATUS_FINAL`. These are COVID
  cancellations and postponements with no `CancelledUtc`, so they count as
  scheduled games in any "games played" denominator. Needs a cancelled
  roll-up, not scores.
- Athlete box 2025 at 42% (Phase 3 dependency, not yet investigated).
