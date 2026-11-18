# War Room Analytics — Coverage Audit (Phase 0b)

> Point-in-time record: this audit describes the system as of its date.
> Open items and action lists reflect that day, not the current state. See
> [war-room-analytics.md](../war-room-analytics.md) for current status.

Date: 2026-11-01. Database: `sdProducer.FootballNcaa` (local, restored from
prod; data current to 2026-11-01 01:34 local). Queries:
`2026-11-01-coverage-audit.sql` (sections 1-4) plus the drill-downs quoted
below.

Population: non-cancelled contests with at least one FBS participant
(`FranchiseSeason.GroupSeasonMap ILIKE '%|fbs%'`). See Finding 3: that
predicate is itself unreliable before 2011, but teams wrongly tagged FBS in
seasons they had no program have no contests, so the population is not
inflated by them.

## Summary: effective floors by data family

| Family | Usable from | Holes / caveats |
|---|---|---|
| Schedule | 2000 | 2010: only 205 of ~380 contests have a `Competition` row (and ~760 expected); 2016: 125 contests without a `Competition` row |
| Results (scores) | 2000 *after roll-up* | Score rows exist but never rolled up to `Contest` for most of 2000, 2002, 2003, 2016, 2019 (Finding 1). 2018: ~330 games have no score rows (most have plays) |
| Kickoff time | 2001 | 2000 is date-only (99.4% at midnight ET). 2001+ shows a realistic kickoff-hour distribution |
| Venue coordinates | 2001 (86-95%), 2014 (100%) | Needed for local time; no time zone column yet |
| Conference membership | 2011 | Pre-2011 `GroupSeasonMap` is wrong for realigned teams (Finding 3) |
| Conference-game flag | 2014 (ESPN) | 0% before 2014. 2014+ agrees with map-derived 98-99% |
| Rank at game time | 2001-2025 | Via `CompetitionCompetitor.CuratedRankCurrent`. 2026 all frozen at 99 (Finding 2). Weekly poll tables only cover 2025-2026 |
| Spread | 2012 (97-100%) | Not 2022 as earlier notes said. ATS results track the finalization gaps |
| Team box | 2004 | 2011 39%, 2016 36%, 2018 10% |
| Athlete box | 2004 | 2018 28%, **2025 42%** |
| Plays | 2005 (86%+), 2008 (94%+) | 2001-2004 60-66%, 2002 4%, 2000 0% |

**The LSU yardstick today** (night + November + conference + both ranks +
result): answerable for **2011-2025**, minus the 2016/2018/2019 result gaps.
After the score roll-up (Finding 1) and the conference fix (Finding 3) it
extends to 2001-2025.

## Per-season results

```
season games both_fbs final_scored time_valid midnight_et venue_coords conf_flag conf_map conf_agree ranked_appear spread ats team_box athlete_box plays
2000    711   662      0.0         0.0       99.4        0.0          0.0       37.2     62.8         0       0.0    0.0   0.0    0.0       0.0
2001    753   620     91.6       100.0        0.0       86.1          0.0       51.0     49.0       267       0.0    0.0   0.0    0.0      59.9
2002    762   628     27.4       100.0        0.0       86.5          0.0       51.0     49.0       334       0.0    0.0   0.0    0.0       4.1
2003    759   625     44.4       100.0        0.0       89.5          0.0       51.2     48.8       310       0.0    0.0   0.0    0.0      63.4
2004    700   590     98.6       100.0        0.0       86.4          0.0       55.4     44.6       296       0.0    0.0  97.6   98.0      66.0
2005    688   606     98.3       100.0        0.0       88.5          0.0       62.2     37.8       301       0.0    0.0  97.2   97.2      85.8
2006    748   643     99.6       100.0        0.1       89.8          0.0       58.0     42.0       324       0.0    0.0  99.9   99.9      90.2
2007    754   653     99.7       100.0        0.0       89.4          0.0       57.6     42.4       321       0.0    0.0  98.5   99.2      88.5
2008    765   649     99.7       100.0        0.0       93.6          0.2       58.1     41.8       327       0.1    0.1  99.3   99.5      96.2
2009    766   642     99.9       100.0        0.0       94.3          0.0       60.0     40.0       323       0.0    0.0  99.9   99.9      97.9
2010    205   178      0.0       100.0        0.0       89.3          0.0       57.9     42.1       110       0.0    0.0 100.0  100.0      98.5
2011    780   646     98.7       100.0        0.0       93.6          0.0       66.6     33.4       338       0.0    0.0  38.7   90.0      98.8
2012    819   683     99.8       100.0        0.2       95.0          0.0       67.2     32.8       336      98.8   97.3  98.9   99.0      97.2
2013    858   745     99.8       100.0        0.3       95.7          0.0       66.4     33.6       341      99.0   97.8  99.7   99.7      99.7
2014    873   765     99.7       100.0        0.3      100.0         66.4       67.8     98.6       340      99.7   97.6  99.5   99.7      97.8
2015    873   766     99.7       100.0        0.0      100.0         66.7       67.9     98.8       339     100.0   97.5  99.7   99.7      98.9
2016    751   651     66.2       100.0        0.0      100.0         66.4       67.4     98.9       256      96.3   64.3  36.4   96.1      94.4
2017    856   759     96.3        99.8        0.2      100.0         66.1       67.6     98.6       298      97.4   93.1  95.1   96.5      95.9
2018    895   780     56.2       100.0        0.0      100.0         66.2       68.1     98.1       334      97.5   54.1   9.9   28.0      97.7
2019    889   775     54.7       100.0        0.0       99.0         66.5       68.6     97.8       339      99.6   51.7  99.9   99.9      99.8
2020    691   650     82.3        98.8        1.2       99.3         82.6       84.5     98.2       294      97.7   74.5  82.5   82.5      81.6
2021    893   774     98.9       100.0        0.0      100.0         66.0       68.3     97.7       291      99.4   96.5  99.3   99.3      94.0
2022    900   780     99.6        99.9        0.1      100.0         66.0       68.5     97.6       321      99.8   99.3  99.6   99.6      95.2
2023    910   792    100.0       100.0        0.0      100.0         67.7       69.2     98.5       342     100.0   98.4 100.0  100.0      99.1
2024    920   799     99.8       100.0        0.0      100.0         66.1       68.0     98.1       346      99.1   95.0  99.9   99.9      97.9
2025    934   808     99.5       100.0        0.0      100.0         67.7       69.7     98.0       340      99.5   99.0  95.4   42.3      99.9
2026    846   716     70.2        78.1       21.9      100.0         74.4       73.3     98.9         0      74.0   69.1  56.5   62.1      70.2
```

All columns except `games`, `both_fbs`, `ranked_appear` are percentages of
`games` (conference columns: of `both_fbs`). 2026 is in progress; its
time/odds/box gaps are mostly unplayed games. `conf_agree` before 2014 is low
only because ESPN's flag is absent (0%), not because the map disagrees.

## Findings

### 1. Most "missing" scores exist but were never rolled up (fixable without ESPN)

`Contest.HomeScore`/`AwayScore`/`FinalizedUtc` are null for large chunks of
some seasons, but competitor score rows exist:

| Season | Unscored | No `Competition` row | Has `CompetitionCompetitorScores` | Has plays |
|---|---|---|---|---|
| 2000 | 711 | 0 | 711 | 0 |
| 2002 | 553 | 0 | 553 | 26 |
| 2003 | 420 | 0 | 420 | 257 |
| 2010 | 380 | 175 | 205 | 202 |
| 2016 | 379 | 125 | 217 | 219 |
| 2018 | 392 | 0 | 64 | 373 |
| 2019 | 403 | 0 | 403 | 401 |

Contest finalization/enrichment never ran for these historical games. A
targeted re-enrichment (the existing contest reenrich path) should roll the
scores up, set the winner, and compute ATS where odds exist. 2010 and 2016
also need their missing `Competition` documents sourced; 2018 needs score
documents re-sourced (or derived from final play scores).
`CompetitionCompetitor.Points` is null everywhere, including 2024, so it is
unused and not a fallback.

### 2. Bug: `CuratedRankCurrent` is frozen at first ingestion

`EventCompetitionCompetitorDocumentProcessorBase.ProcessUpdate` syncs
`HomeAway`, `Order`, `Winner` and sport-specific extras, but never
`CuratedRankCurrent`; it is set only in `CreateEntity`. 2026 competitors were
created before the preseason poll, so all 5,694 are 99 (unranked) despite
2,153 finalized games. Historical seasons are correct because they were
created from settled documents. Fix: sync the rank on update (football
override), then re-process 2026 competitor documents. Also affects anything
else reading the column for live seasons.

### 3. Pre-2011 conference (and FBS) membership is fabricated for realigned teams

`FranchiseSeason.GroupSeasonId` / `GroupSeasonMap` for many teams before 2011
point at their *current* conference:

| Team | 2000-2010 map | Reality |
|---|---|---|
| USC | fbs/big10 (to 2010), pac12 from 2011 | Pac-10 |
| TCU | fbs/big12 throughout | WAC / C-USA / MWC until 2012 |
| Kennesaw St | fbs/usa until 2014 | no program until 2015 (then FCS) |
| Charlotte | fbs/American until 2012 | no program until 2013 |
| Old Dominion | fbs/belt until 2008 | no program until 2009 |

Teams whose history ESPN models correctly look right (Missouri big12 to 2011,
sec from 2012; the 2005 SEC roster is exact; 2011+ samples are correct). The
pattern resembles the ESPN NFL roster fabrication (current state rendered
under historical paths), but the root cause (ESPN document vs our
GroupSeason resolution) is **not yet established**. Impact:

- Conference splits before 2011 are wrong for any realigned team.
- The FBS predicate tags no-program teams as FBS; harmless for contest
  counts (they have no contests) but wrong for team lists.

Remedy options, to decide after root cause: fix the resolution if it is ours;
otherwise a curated historical membership table (publicly documented, ~130
teams x 10 seasons, realignment changes only).

Full analysis, affected team list, and remedy options for decision:
[2026-11-06-conference-membership.md](2026-11-06-conference-membership.md).

**Root cause (2026-11-06): ESPN's TeamSeason document, not our resolution.**
`TeamSeasonDocumentProcessor` resolves `GroupSeasonId` from the document's
`groups` $ref, and that $ref is fabricated: ESPN's live 2005 TCU document
(`seasons/2005/teams/2628`) links `types/3/groups/4` (Big 12), USC 2005 links
`groups/5` (Big Ten). We store exactly what ESPN serves.
`GroupSeasonMap` is not written by any service; it was built once from the
`GroupSeasonId` -> `ParentId` chain by `sql/pgsql/groupSeasonHierarchy.sql`,
so it inherits the same error.

ESPN's conference-side lists are honest:
`seasons/{y}/types/2/groups/{conf}/teams`. 2005 Big 12 lists 12 teams without
TCU, 2005 Mountain West includes TCU, 2005 Pac-10 includes USC. Pulled for
every FBS and FCS conference 2001-2025 and compared at conference level
(division-aware) against prod, team-seasons with at least one contest:

| Seasons | Wrong conference per season | Notes |
|---|---|---|
| 2001-2004 | 31-32 | Pac-10 schools as Big Ten/Big 12/ACC, C-USA as American/Big 12, A-10 FCS as CAA/Patriot |
| 2005-2010 | 13 | Pac-10 (8), MWC (TCU, BYU, Utah, SDSU, CSU) |
| 2011-2012 | 4 | MWC: TCU (2011), SDSU, CSU, Boise St, Fresno St (2012) |
| 2013-2025 | 0 | except 2020: FAMU, Bethune-Cookman (MEAC, not SWAC) |

Also wrong: 12 team-seasons 2001-2004 tagged FBS that ESPN lists as FCS
(Delaware, UMass, James Madison), and South Alabama 2009-2010 tagged FBS with
no ESPN membership. 2000 has no membership lists at all (every 2000 group
`teams` index returns 0), so 2000 cannot be verified from ESPN.

A SQL correction would stick: `ProcessUpdateEntity` never re-resolves
`GroupSeasonId`. Note `GroupSeason` holds one row per season type (types
1-4) for each group; team seasons point at the `types/3` row (2000-2024) or
the `types/2` row (2025, all 797), so a correction must target the variant
the rest of that season uses to keep conference equality checks (record
enrichment's conference W/L) consistent.

### 4. Weekly poll history exists only for 2025-2026

`SeasonPoll*` covers 2025-2026 only. `FranchiseSeasonRanking` for AP
2000-2024 holds one 25-team snapshot per season with no week or date (2010:
12 teams, 2017: 21). "Rank going in" must therefore come from
`CuratedRankCurrent` (ESPN's rank on the game's competitor), not poll tables,
for history. Open question: whether ESPN's curatedRank switches from AP to
CFP rankings once those publish (2014+); verify against a known November game
before labeling the column "AP rank".

### 5. 2025 athlete box scores at 42%

Team box is 95% for 2025 but athlete box is 42%, the lowest of any season
from 2004 except 2018. This is the most recent complete season, so it
matters for current leaders-by-game work. Cause not investigated
(`AthleteCompetition.StatsRequestedUtc` suggests athlete stats are requested
separately and may not have been requested for all games).

### 6. Spread coverage is 2012+, not 2022+

Spreads are present for 97-100% of FBS-involved games from 2012. The
"values ~2022" note in earlier docs/memory is wrong for this DB. ATS results
follow the finalization gaps in Finding 1.

### 7. Finalized home/away flips split Contest from its competitors (deferred)

Found during the curated rank backfill (see
[2026-11-02-curated-rank-backfill-run.md](2026-11-02-curated-rank-backfill-run.md)).
ESPN re-designates home/away on already-finalized games. The two processors
disagree on what to do:

- `EventDocumentProcessorBase` (line ~471) **refuses** the swap on a finalized
  contest, reverts `Contest.Home/AwayTeamFranchiseSeasonId`, and logs
  "refusing to swap".
- `EventCompetitionCompetitorDocumentProcessorBase` (line ~384) has no
  finalized guard and **applies** the new `HomeAway`/`Order`.
- `FootballContestEnrichmentProcessor` (lines 72-73, 257-277) derives
  `HomeScore`/`AwayScore` from the **competitor** sides.

So every refresh of such a game leaves Contest team ids pointing one way, and
re-enrich then writes scores the other way: any surface pairing
`HomeTeamFranchiseSeasonId` with `HomeScore` shows the score inverted.
Winner and ATS are franchise-keyed and survive. Re-enrich alone does not fix
it (verified 2026-11-02: 8 contests re-sourced and re-enriched, all still
split).

Remediated 2026-11-02 for the 9 affected 2025 contests (all small-college,
mostly neutral-site): `sql/pgsql/_remediation_finalized_homeaway_2026_11_02.sql`
(untracked) aligned Contest to the competitor rows and swapped `Name`/
`ShortName`, followed by re-enrichment. Executed in production.

Open design decision (deferred; design note before code):

- (a) Add the finalized guard to the competitor processor too: consistent
  data, but a manual queue every time ESPN flips a finished game.
- (b) Let the Event processor apply the swap on finalized contests and
  trigger re-enrichment. Leaning (b): enrichment already follows the
  competitor sides, and the refusal is what creates the split. Needs a
  decision on downstream pick re-scoring before it ships.

Until decided, any new "refusing to swap" error in Seq means a split contest;
rerun the remediation SQL with that ContestId, then re-enrich.

## Recommended remediation order

Status 2026-11-02: item 1 done (#853 fix, #854 narrowed refresh, backfill run
for 2001-2026). Finding 7 remediated for the 9 known contests; design deferred.

1. Fix `CuratedRankCurrent` update sync and re-process 2026 competitors
   (current-season correctness; small code change).
2. Re-enrich unscored historical contests with existing score rows
   (2000, 2002, 2003, 2016, 2019; no ESPN refetch expected).
3. Root-cause Finding 3 before Phase 2 relies on conference columns.
4. Source the missing 2010/2016 `Competition` documents and 2018 scores.
5. Investigate 2025 athlete box coverage (Phase 3 dependency, not Phase 2).

Items 1-3 gate Phase 2. Phase 1 (season leaders from ESPN season totals) is
unaffected.

Status 2026-11-05: item 2 diagnosed. The scores were never rolled up because
`ContestEnrichmentJob` was pinned to `SeasonYear == 2026` (#426). The data
can be derived for nearly every final game. See
[2026-11-05-results-rollup-diagnosis.md](2026-11-05-results-rollup-diagnosis.md)
and the runbook below.

Status 2026-11-06: item 2 done. All eight seasons finalized (9,177 contests);
FBS `pct_final_scored` is 96.5-100 for every target season. The 2011 (1,417)
and 2018 (1,508) residuals are lower-division games with no scoring plays and
no score rows on at least one side; the runbook's "Will finalize" figures for
those two seasons counted STATUS_FINAL, not derivable scores. They need an
ESPN re-source (folds into item 4). Team records were also backfilled for
2000-2025 (4,163 mismatched, now 0); audit SQL section 5 tracks drift.
Next gate for Phase 2: item 3.

## Runbook 2026-11-05: roll up historical results (item 2)

Operator steps. Prod writes go through the Producer API only; no SQL writes.

### 0. Merge and deploy the PR

Branch `fix/contest-enrichment-sweep-window`:

- Replaces the 2026 pin in `ContestEnrichmentJob` with a 14-day lookback.
- Makes the season floor inclusive (`>= 2000`) in the finalize and refresh
  validators.

Deploy Producer for all three sports. Steps 2-3 for 2001+ do not depend on
the deploy, but run them **after** it: the finalize handler enqueues inside a
Hangfire job, and a pod roll mid-loop would cut a season short. A cut-short
season is harmless (rerunning it only picks up what is still unfinalized), but
it wastes a pass. **Season 2000 requires the deploy** (the old validator
rejects it).

### 1. Baseline

Run section 1 of `2026-11-01-coverage-audit.sql` against prod (read-only)
and keep `pct_final_scored` / `pct_ats_result` for comparison.

### 2. Finalize NCAA, one season at a time

Request: `bruno/api/contests-finalize-seasonYear.yml` (API ops proxy:
`POST {{baseUrlApi}}/api/ops/producer/football/ncaa/contests/finalize`;
Producer is not public), prod API environment. Change only `seasonYear` between runs; keep
`reprocessEnriched: false` (`true` re-enqueues every contest in the season).

```json
{
  "sport": 2,
  "seasonYear": 2002,
  "reprocessEnriched": false
}
```

Leave `correlationId` out. The endpoint generates one (the request's trace
id) and returns it in the 202 body (`{ correlationId, sport, seasonYear }`);
copy it from the response for the Seq filters in step 3. The same id is
stamped on every enrichment the season enqueues.
Order, smallest enqueue first (counts are all-division `STATUS_FINAL`
contests that will finalize; the enqueue is slightly larger):

| # | Season | Will finalize | FBS-involved | CorrelationId
|---|---|---|---|---|
| 1 | 2000 (after deploy) | 713 | 711 | 67dd4405-f6a1-fe1b-f6fb-084c08e40a40
| 2 | 2003 | 904 | 422 | ad99ca2a-5820-5bf7-39e4-e3664d8eade6
| 3 | 2002 | 1,128 | 553 | 029a5f3c-f964-b1e1-2a9a-30a46d6e9562
| 4 | 2019 | 2,310 | 402 | 66ea529e-934f-ef8b-2955-e4d561ea1c2b
| 5 | 2011 | 2,318 | ~10 (rest non-FBS) | b0d2faec-e4ef-31ad-6edc-cf0db1c16dcc
| 6 | 2016 | 2,147 | ~228 | e675bae6-4a3a-d8e5-dbbb-a48629c23cc1
| 7 | 2018 | 2,325 | 373 | 71f3fab7-3178-ca07-eb56-f16ab4b9e322
| 8 | 2010 | 283 (enqueue ~2,745) | 205 | 357a946f-358f-9514-1510-9aab2ddb2849
| 9 | 2001, 2004-2009, 2013, 2015, 2017, 2021, 2022, 2024, 2025 | 1-79 each | small |

Run each one off-peak: each finalized contest sends one SignalR broadcast to
connected clients. No push notifications are sent and no picks are touched.

### 3. Per season: wait for it to drain, then check

In Seq (filter on the run's correlation id):

- `CorrelationId = '<guid>' and @MessageTemplate like 'Finalization queueing complete%'`
  means the enqueue loop finished. `'Failed to finalize contests%'` means it
  died; rerun the same season (idempotent).
- `CorrelationId = '<guid>' and @MessageTemplate like 'Contest enrichment completed%'`:
  the count should approach the "Will finalize" column.
- `@Level = 'Error' and @Exception like '%Npgsql%'`: connection pool
  exhaustion (seen with ~3k enqueues on 11-02). If it appears, wait for the
  Hangfire queue to drain, then rerun the season.

Move to the next season when the Hangfire `default` queue is back near
empty. Each finalization also enqueues record enrichment for both teams, so
the queue trails the enrichment jobs.

### 4. Verify

Rerun section 1 of the audit SQL. Expected `pct_final_scored`:

- About 99% for 2000, 2002, 2003, 2019.
- 2016 up from 66% to ~95%.
- 2018 up from 56% to ~98%.
- 2010 stays at 0% for the 175 contests with no `Competition` row; the
  other 205 finalize.

`pct_ats_result` should follow for 2016, 2018, 2019 (spreads exist 2012+).
Spot-check one team's historical W/L (record enrichment runs off
`ContestFinalized`).

### Expected noise (not failures)

- `Audit deferred — no competitor score rows` warnings increase. 2018 adds
  634 contests that finalize from plays but have no score rows. This is the
  pre-existing audit starvation (see the 11-05 diagnosis); a fix is pending a
  decision.
- About 11 contests whose play feed is missing a late score will log
  `Audit mismatch`, then `Audit FLAGGED` after the retry allowance.
  `FinalizedUtc` is kept. Review those later.
- `Contest status is not yet final ... Skipping enrichment` for postponed /
  cancelled contests in the small seasons.

### Optional: NFL (environment `prod-footballnfl`, `sport: 3`)

Same procedure. Recoverable: 2000 (324, after deploy), 2004 (202), 2006 (156),
2008 (44), single digits in 2001-2003 and 2009. 1999 (248) is below the
validator floor; out of scope.

### Not today

- MLB has ~1,700 recoverable across 2015-2025 (686 in 2025), but each season
  carries ~2,000 non-final unfinalized contests, so a finalize enqueues ~2k
  no-ops per season. Look at those first.
- 2010/2016 missing `Competition` documents, 2020 uncancelled COVID games,
  2025 athlete box, and the audit starvation decision.
