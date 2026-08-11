# Per-SeasonWeek Franchise Metrics: Point-in-Time Inputs for StatBot Regression Testing

Status: **discovery / design — snapshot table not authorized; the core
insight is now IMPLEMENTED in query form** (2026-08-09): MetricBot's
as-of extraction (`src/metrics-modeling/sql/competition_metrics_asof_*.sql`)
computes point-in-time aggregates on the fly from `CompetitionMetric`,
with formula-parity to `ComputeFranchiseSeasonMetric` and no new
tables. **Cutoff semantics note:** the implementation uses
ENTERING-week windows (`sw."Number" < :week` — "the state of the world
before week N kicks off"), because its consumer predicts week N. This
doc's proposal below uses `week <= N` ("after week N") for the
materialized rows; the two are off-by-one views of the same derivation
and the convention decision in "Open decisions" #1 remains open for the
table. Any future `FranchiseSeasonWeekMetric` must pin ONE convention
and document the mapping to the as-of queries. A materialized table
remains future work and this doc remains its design basis.
Date: 2026-07-29 (status updated 2026-08-11)
Surfaces: SportsData.Producer (primary), SportsData.Api (preview provenance), football only

## Problem

StatBot outputs (DeetsMeter scores, matchup previews) cannot be regression-tested
because their inputs are destroyed on every recompute. A preview generated before
week 8 consumed the metrics as they stood after week 7 — but once week 8 games
process, that state is gone. There is no way to ask "what did the model see?"

The ask: capture franchise metrics on a per-SeasonWeek basis, calculate the
rolling values, and store them, so any historical model run can be replayed
against the exact inputs it had.

## Current state (verified against source, 2026-07-29)

The metric pipeline is entirely in-house, computed from play-by-play — **not**
sourced from ESPN's season-stats documents:

```
ESPN plays/drives (immutable per game)
  └─ FootballCompetitionPlay / CompetitionDrive
       └─ CalculateCompetitionMetricsCommandHandler
            └─ CompetitionMetric            ← 2 rows per game (one per team), PERSISTS
                 └─ CalculateFranchiseSeasonMetricsCommandHandler
                      └─ FranchiseSeasonMetric   ← 1 row per franchise-season, DELETE+REPLACE
                           └─ MatchupPreviewProcessor (API, via FranchiseClient)
                                └─ prompt → preview
```

Key facts, with sources:

- **`CompetitionMetric`** (`Infrastructure/Data/Entities/Metrics/CompetitionMetric.cs`)
  is per-competition per-team: YPP, success rate, explosive rate, points/drive,
  3rd/4th conversion, RZ rates, possession ratio, the `Opp*` mirror set, and
  ST/discipline fields. Carries `InputsHash` and `ComputedUtc`. Recomputed
  delete+replace *per competition*, but a final game's plays don't change, so in
  practice these rows are stable once a game is final.
- **`FranchiseSeasonMetric`** (same folder) is the season-to-date aggregate.
  **Unique index on `FranchiseSeasonId`** — structurally one row, no history.
  `CalculateFranchiseSeasonMetricsCommandHandler` deletes and re-adds it from all
  of that team's `CompetitionMetric` rows.
- **Trigger** is the audit-job idiom: `FootballCompetitionMetricsAuditJob` sweeps
  for competitions ≥3h past their date with no metrics and enqueues calculation.
  Franchise-season aggregation is enqueued via `FranchiseSeasonController`.
- **Previews consume the live row**: `MatchupPreviewProcessor.cs:79-82` pulls
  current `FranchiseSeasonMetric` for both teams at generation time. A
  `UsedMetrics` flag records *whether* metrics were available, not *which values*.
- **Week mapping exists**: `ContestBase` has `SeasonYear`, `Week (int?)`, and
  `SeasonWeekId → SeasonWeek`.
- **Regression scaffolding partially exists**: `ModelRun` (name/version/params)
  and `CompetitionModelOutput` (per-run per-competition per-team scores with
  `ExplainerJson`) are already in the Metrics folder. What's missing is exactly
  the point-in-time *inputs* side.

### Corrections to the working assumptions

Two premises in the original framing don't match the code, and both are good news:

1. **"We get FranchiseSeasonMetric data from ESPN"** — no. ESPN's season-stats
   document feeds `FranchiseSeasonStatistic` (a different entity). The metrics
   that drive StatBot are computed in-house from plays/drives. We are not
   dependent on capturing ESPN's weekly overwrites on a schedule; we own the
   entire derivation.
2. **"We get per-contest data via CompetitionCompetitorStatistic"** — that entity
   exists (ESPN's per-game box score, category/stat tree), but it is **not in the
   metric chain today**. Nothing reads it for metrics. See "Where it fits" below.

## The key insight: snapshots are derivable retroactively

Because `CompetitionMetric` is per-game and persists, and contests map to weeks,
**"metrics as of week N" is a pure function of data we already have**:

> filter that team's `CompetitionMetric` rows to contests with week ≤ N
> (or `< N` for entering-week semantics — see the status note above;
> MetricBot's shipped as-of queries use `< N`),
> aggregate exactly as `ComputeFranchiseSeasonMetric` does today.

Nothing needs to be captured going forward that isn't already captured. This
means the entire historical corpus — NCAA seasons back through the historical
sourcing effort — can be backfilled into per-week snapshots without touching
ESPN. The regression-test dataset is as large as the play-by-play archive, not
as large as "weeks since we started snapshotting."

The one thing that is *not* reconstructible is what a **previously generated
preview** actually saw (its inputs may predate correct week mapping, late plays,
or metric-formula changes). Provenance has to be captured at generation time
going forward; history before that is best-effort.

## Proposed shape (for discussion, not authorized)

### 1. `FranchiseSeasonWeekMetric` — the snapshot entity

Same metric columns as `FranchiseSeasonMetric`, plus:

- `SeasonYear`, `SeasonType`, `WeekNumber` (and/or `SeasonWeekId`)
- `GamesPlayed` (through the cutoff)
- `ThroughUtc` — the actual cutoff instant used
- `InputsHash` — hash of the contributing `CompetitionMetric` ids+hashes, so a
  regression run can verify its SOURCE INPUTS are byte-identical to the
  original. Scope limit: this detects source-row drift only; a change to
  the aggregation formula itself leaves InputsHash unchanged. Pair it
  with an `AggregationVersion` (bumped whenever
  `ComputeFranchiseSeasonMetric` changes) for full replayability.
- Production wrinkle the snapshot must model or disclaim: MetricBot's
  early-season runs use a prior-season tail (production
  `MetricBotWeeklyJob` passes PriorSeasonTail=5 — each team's window is
  topped up with its most recent prior-season regular/post games until
  it has 5 of its own). Plain week-N snapshot rows cannot reproduce
  those runs; either model the tail rule (source-season provenance per
  contributing game) or state that snapshots reproduce tail-less runs
  only.
- unique index `(FranchiseSeasonId, SeasonType, WeekNumber)`

**Semantic decision needed:** does the week-N row mean *entering* week N (games
through N-1) or *after* week N (games through N)? Proposal: **after** — "the
state of the world once week N completed" — because that's the input to
anything generated during week N+1, and week 0/preseason falls out naturally as
`GamesPlayed = 0`. But this must be pinned down before any row is written;
it's the kind of off-by-one that poisons a regression corpus silently.

### 2. Computation: parameterize, don't fork

Extract the aggregation in `CalculateFranchiseSeasonMetricsCommandHandler.ComputeFranchiseSeasonMetric`
so the same code produces both the live row (no cutoff) and any week snapshot
(cutoff at week N). One formula, two callers — the live row and the snapshots
can never drift from each other.

### 3. Trigger: extend the audit-job idiom

A `FranchiseSeasonWeekMetricsAuditJob` that finds `(franchiseSeason, week)`
pairs whose week is complete but which lack a snapshot row, and enqueues
computation. Self-healing, idempotent, and it **is** the backfill mechanism —
pointed at 2024, it fills 2024. No separate one-shot backfill path to write and
then throw away. Matches `FootballCompetitionMetricsAuditJob` exactly.

"Week is complete" needs a definition (all contests in that SeasonWeek final?
`SeasonWeek.EndDate` passed? both?) — open question below.

### 4. Preview provenance (API side)

When `MatchupPreviewProcessor` generates a preview, persist *which* inputs it
saw — either the two `FranchiseSeasonWeekMetric` ids, or an inline JSON snapshot
of the metric values on the preview record. Replaces the boolean `UsedMetrics`
with something replayable. Without this, the snapshot table lets us reconstruct
what a preview *should have* seen, but not prove what it *did* see.

### 5. Where `CompetitionCompetitorStatistic` fits

Not as the primary source — plays/drives already win on granularity and are the
established chain. Two real roles:

- **Cross-validation**: ESPN's box-score totals vs our play-derived numbers per
  game. A cheap audit that catches play-ingestion gaps (missing plays understate
  yardage silently; a box-score diff surfaces it).
- **Gap-filling**: metrics not derivable from our play data. `NetPunt` is
  literally `0m // TODO` in `CalculateCompetitionMetricsCommandHandler` today —
  punting stats may be exactly what the box score provides cheaply.

## Open decisions

1. **Week-N semantics** — entering vs after (proposal: after; see §1).
2. **"Week complete" definition** for the audit job — all contests final vs
   week end-date passed vs both.
3. **Bye weeks** — write a carry-forward row (identical values, `GamesPlayed`
   unchanged) or no row? Carry-forward makes consumers dumber (every week has a
   row); gaps make the table smaller and "played that week" explicit. Proposal:
   carry-forward — regression harnesses joining "preview generated in week N"
   to "snapshot N-1" shouldn't need bye-week special cases.
4. **Rolling-value flavors** — season-to-date average only (what exists), or
   also recency-weighted / last-3 forms? If multiple flavors are plausible, the
   schema needs a discriminator (or a `ModelRun`-style versioning of the
   aggregation itself) *now*, even if only one flavor ships first.
5. **Metric-formula versioning** — when a formula bug is fixed (e.g. NetPunt
   gets implemented), do historical snapshots get recomputed (corpus changes
   under the model) or frozen (corpus is stable but wrong)? `InputsHash` detects
   source-input drift only — formula changes require the
   `AggregationVersion` field proposed in §1; policy for handling either
   kind of drift is a product decision.
6. **Postseason** — SeasonType handling: continuous week numbering or
   type-scoped; bowl/playoff games in season-to-date aggregates or excluded.
7. **Scope** — football-only (both contexts are `FootballDataContext` today).
   MLB's shape (162 games, no meaningful "week") suggests per-*date* snapshots
   there eventually; out of scope here but worth not designing against.

## Verify before trusting the backfill claim

The retroactive-derivability claim is structural (verified from code); the
**coverage** claim is empirical and unverified. The dataset is only as large as
the play-by-play archive *actually is* — ESPN's PBP coverage thins with age,
`CompetitionMetric` only exists where plays computed cleanly, and
`Contest.Week`/`SeasonWeekId` are nullable. Run this against the football DB
before sizing any backfill:

```sql
-- Per-season coverage: how much of the backfill corpus actually exists?
SELECT
    c."SeasonYear",
    COUNT(DISTINCT c."Id")                                            AS contests,
    COUNT(DISTINCT c."Id") FILTER (WHERE c."SeasonWeekId" IS NOT NULL) AS with_week_mapping,
    COUNT(DISTINCT comp."Id")                                         AS competitions,
    COUNT(DISTINCT p."CompetitionId")                                  AS with_plays,
    COUNT(DISTINCT cm."CompetitionId")                                 AS with_metrics
FROM "Contest" c
JOIN "Competition" comp ON comp."ContestId" = c."Id"
LEFT JOIN LATERAL (
    SELECT fp."CompetitionId" FROM "FootballCompetitionPlay" fp
    WHERE fp."CompetitionId" = comp."Id" LIMIT 1
) p ON TRUE
LEFT JOIN LATERAL (
    SELECT m."CompetitionId" FROM "CompetitionMetric" m
    WHERE m."CompetitionId" = comp."Id" LIMIT 1
) cm ON TRUE
GROUP BY c."SeasonYear"
ORDER BY c."SeasonYear";
```

-- NFL Results --
SeasonYear	contests	with_week_mapping	competitions	with_plays	with_metrics
1999	248	248	248	0	248
2000	324	324	324	0	324
2001	323	323	323	286	323
2002	333	333	333	267	333
2003	333	333	333	290	333
2004	333	333	333	267	333
2005	333	333	333	266	333
2006	332	332	332	332	332
2007	332	332	332	331	332
2008	332	332	332	332	332
2009	332	332	332	331	332
2010	332	332	332	332	332
2011	332	332	332	331	332
2012	332	332	332	330	332
2013	332	332	332	309	332
2014	333	333	333	327	333
2015	332	332	332	332	332
2016	332	332	332	331	332
2017	333	333	333	317	333
2018	332	332	332	332	332
2019	332	332	332	332	332
2020	334	334	334	269	334
2021	334	334	334	333	334
2022	334	334	334	333	334
2023	334	334	334	334	334
2024	334	334	334	334	334
2025	334	334	334	334	334
2026	321	321	321	0	0

-- NCAAFB Results --
SeasonYear	contests	with_week_mapping	competitions	with_plays	with_metrics
1999	699	699	699	0	699
2000	713	713	713	0	713
2001	1498	1498	1498	456	1498
2002	1505	1505	1505	31	1505
2003	1526	1526	1526	484	1526
2004	1428	1428	1428	475	1428
2005	1438	1438	1438	597	1438
2006	1535	1535	1535	692	1535
2007	3495	3495	3495	866	3495
2008	3587	3587	3587	1147	3587
2009	3546	3546	3546	1232	3546
2010	2276	2276	2276	211	2276
2011	3616	3616	3616	1297	3616
2012	3657	3657	3657	1318	3657
2013	3772	3772	3772	1534	3772
2014	3798	3798	3798	1472	3798
2015	3748	3748	3748	1503	3748
2016	3213	3213	3213	1117	3213
2017	3699	3699	3699	1376	3699
2018	3776	3776	3776	1441	3776
2019	3791	3791	3791	1555	3791
2020	2123	2123	2123	801	2123
2021	3702	3702	3702	1408	3702
2022	3724	3724	3724	1420	3724
2023	3734	3734	3734	1519	3734
2024	3802	3802	3802	1594	3802
2025	3833	3833	3833	1679	3833
2026	1481	1481	1481	0	0

Reading it: `with_metrics ≈ competitions` in a season → that season backfills
cleanly. `with_plays` well below `competitions` → ESPN never had (or we never
sourced) PBP there, and that season's floor is set by ESPN, not by us.
`with_week_mapping` gaps → fixable on our side from `StartDateUtc` + the
season-week calendar. (Table/column names should be sanity-checked against the
live schema before running — written from the EF configurations, not psql.)

### Coverage-query caveats (2026-08-11 review — apply before sizing)

The recorded query and result tables above are kept as the historical
record; a rerun for actual backfill sizing must address five
limitations found in review:

1. **Metrics without plays exist** (visible in the tables: 1999/2000
   show `with_metrics = contests` with `with_plays = 0`).
   `CalculateCompetitionMetricsCommandHandler` does not reject an empty
   play collection, so a metric row is NOT proof of play-derived data.
   Identify the provenance of pre-2001 metric rows before counting them
   as regression corpus; until then the "backfills cleanly" reading
   only holds for seasons where `with_plays ≈ with_metrics`.
2. **Per-database execution, not a Sport predicate**: the two result
   tables were produced by running the query separately against
   `sdProducer.FootballNfl` and `sdProducer.FootballNcaa` (the
   platform's per-sport database split) — that is why no `Sport` filter
   appears. A rerun must preserve that execution model.
3. **Inner join drops contest-only rows**: `contests` counts contests
   having at least one Competition row (1:1 in practice, but a LEFT
   JOIN or a renamed column would make the scope explicit).
4. **No `FinalizedUtc` filter**: current-season rows (2026: 321
   contests, zero plays/metrics — unplayed games) inflate denominators.
   Sizing reruns should add `c."FinalizedUtc" IS NOT NULL` or report
   the in-progress season separately.
5. **`LIMIT 1` laterals count half-covered games**: one team's metric
   row marks the competition covered; the entity is two rows per game.
   Sizing reruns should require both franchise keys.

## Sizing (rough)

Snapshot rows ≈ franchise-seasons × weeks ≈ (~800 NCAA + 32 NFL) × ~16 × years.
For 25 years of NCAA history that's on the order of 300k rows of ~25 decimal
columns — trivial for PostgreSQL, and dwarfed by the existing plays tables. The
compute cost of a full backfill is bounded by the same aggregation the live path
already runs, driven off indexed `CompetitionMetric` reads.

## Out of scope

- The prediction-model work itself (`metrics-microservice-deetsmeter.md`)
- AI provider routing (`ai-provider-routing-per-sport.md`)
- MLB / non-football metrics
- Backfilling provenance for previews generated before provenance capture exists
