# Curated Rank Backfill Run (2026-11-02)

> Point-in-time record: this audit describes the system as of its date.
> Open items and action lists reflect that day, not the current state. See
> [war-room-analytics.md](../war-room-analytics.md) for current status.

Seq review of the first season-wide narrowed refresh after #854 deployed, and
the resulting action list. Request: `bruno/api/contests-refresh-seasonYear-competitors.yml`
(`IncludeLinkedDocumentTypes=EventCompetition,EventCompetitionCompetitor`), NCAA,
seasons 2001-2026. Background: [2026-11-01-coverage-audit.md](2026-11-01-coverage-audit.md),
[../war-room-analytics.md](../war-room-analytics.md).

## What ran

- 2026 kicked off alone at 13:37 UTC (2,860 contests); 2001-2025 at 13:47 UTC.
- Per-season contest counts: ~1,430-1,530 for 2001-2006, ~3,500-3,830 for
  2007-2025, **298 for 2010** (consistent with the audit's missing 2010
  Competition rows, so the 2010 backfill covered only those 298).
- 2000 was rejected by `RefreshContestsBySeasonYearCommandValidator`
  (`GreaterThan(2000)`, message "must be greater than 2000"). Intentional in
  #550, but it conflicts with the War Room 2000-to-present scope. (Since fixed:
  #856 made the floor inclusive.)

## Errors attributable to the run

| # | Error | Count | Assessment |
|---|---|---|---|
| 1 | `Failed to refresh contests by season year`: Hangfire `BackgroundJobClientException`, Npgsql pool exhausted (`Max Pool Size` 22, timeout 15s) | 2 (seasons **2026**, **2008**) | Season job died mid enqueue loop. An unknown number of that season's contests were enqueued; the rest never were. 2026 failed 15s in while running alone. |
| 2 | `ESPN home/away changed on a FINALIZED contest; refusing to swap` | 8 (all 2025) | #661 guard working as designed. Remediation is re-enrich, not refresh. |
| 3 | `ESPN API returned NotFound` for NCAA events 401773930, 401806686 | 2 | ESPN no longer serves these events (likely cancelled/removed). No action. |

Correlation IDs for the failed season jobs: 2026 `fcc63b22-5ec8-f606-c59c-3ca8b7cc0893`,
2008 `012f16ba-1d6c-bdfa-31de-1935724874f6`.

Finalized 2025 contests refused a home/away swap (ContestId, ESPN event):

- `ee5eea3e-5ef7-9624-f80d-f30e5f0117ca` (401773720)
- `544d9c21-2a61-1afa-4b86-39cee25b04e4` (401767351)
- `9a8515fb-d9ea-397c-1820-e612c004cfc1` (401774177)
- `d2628798-7870-1050-5fe0-8f0ccd9f0578` (401773086)
- `97db8a95-8345-bd4d-10ed-9ec309e1df78` (401766659)
- `40d6678c-b636-0c78-6cf9-6f2fe0ef75f4` (401773520)
- `9457edc1-ff8b-0eee-d654-86b81d275cbd` (401773072)
- `4ce0e9d9-04a9-0fcd-17e8-248020cd0238` (401774011)

## Errors in the same window, not caused by the run

| Error | Count | Window (UTC) | Notes |
|---|---|---|---|
| NFL `EventCompetitionPlay` `DbUpdateConcurrencyException` (expected 1 row, affected 0) at `EventCompetitionPlayDocumentProcessorBase.cs:181` | 245 | 01:48-04:22 | Sunday night live game. Concurrent updates to the same play row. All at AttemptCount 0; retry outcome not yet checked. |
| NCAA duplicate key `IX_CompetitionCompetitor_CompetitionId_HomeAway` | 4 | 03:53-12:04 | One 2026 competition (`3dca4055-b0df-85b9-ba02-5bd2fe9872a7`), Hangfire job 13660388. A competitor arrived as `home` while the other row still held `home`; looks like an ESPN home/away swap on a **non-finalized** game, which the #661 guard does not cover. |
| NFL situation DLQ, NFL play/odds 404, worker `TaskCanceledException` on pod shutdown | 5 | 04:19-04:42 | Normal live-game noise. |

## Actions

Status 2026-11-02: 2026 and 2008 reran cleanly at 14:04 UTC (2,860 and 3,587
contests, no pool exhaustion), so items 1-2 are done and item 4 is not needed.
Item 6: re-enrich alone did not fix the split; resolved by
`sql/pgsql/_remediation_finalized_homeaway_2026_11_02.sql` plus re-enrich
(9 contests, including `96682bb5-134b-1a3b-9979-68e2049d1e63`, event
401767394). Root cause and the deferred design decision are in Finding 7 of
the coverage audit.

### Now (operator, no code) - DONE 2026-11-02, do not rerun

1. **Rerun 2026** alone. DONE (14:04 UTC, no pool exhaustion).
2. **Rerun 2008** after 2026 completes. DONE.
3. **Verify** with section 3 of `2026-11-01-coverage-audit.sql`: ~25 ranked
   appearances per week for 2026, and no seasons left mostly at 99.

### Small PR (only if 2026 fails again; otherwise just item 5)

4. **Batch the enqueue in `RefreshContestsBySeasonYearHandler`**: a few hundred
   contests per batch with a short delay, so one season cannot exhaust the
   Hangfire storage pool.
5. **Inclusive season floor**: `GreaterThan(2000)` to `GreaterThanOrEqualTo(2000)`
   plus a test, then backfill 2000. DONE in #856.

### Data fixes

6. **Re-enrich the 8 finalized 2025 contests** listed above. DONE 2026-11-02
   (remediation SQL plus re-enrich of 9 contests; see the status above).
7. **Identify the 2026 game behind the duplicate-key error** (competition
   `3dca4055-…`). If it is a non-finalized home/away swap, the #661 guard has a
   gap: unfinalized games trip the unique index instead of swapping. Investigate
   before changing code.

### Separate investigation (unrelated to this work)

8. **NFL play concurrency** (245 during the live game). First check whether
   retries succeeded. Retries succeeded: log noise worth quieting. Retries
   failed: plays are being dropped during live games.

### Already tracked (from the coverage audit)

9. Missing 2010/2016 Competition rows, missing 2018 score rows, historical
   scores never rolled up, fabricated pre-2011 conference membership, 2025
   athlete box coverage at 42%.
