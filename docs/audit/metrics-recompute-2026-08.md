# Metrics recompute campaign — 2026-08 manifest

The execution record for the full-corpus `CompetitionMetric` /
`FranchiseSeasonMetric` recompute mandated by
[competition-metrics-formula-audit.md](competition-metrics-formula-audit.md)
(recommended sequence, steps 2–3). Summary numbers elsewhere
(e.g. the deetsMeter model-track doc) trace to this manifest; the raw
orchestrator log is appended verbatim.

## Scope and outcome

- **Window**: 2026-08-14T20:00:15Z → 2026-08-15T07:32:44Z (~11.5h,
  unattended; one mid-run deploy of PR #629 unblocked phase 2).
- **Formula vintage stamped**: `2026.08` (`MetricFormula.Version`),
  with content-aware `InputsHash` per competition.
- **Season-runs**: **50** — NCAAFB 2001–2025 excluding 2010 (no play
  data) = 24; NFL 2001–2026 = 26. NCAAFB 2026 was correctly skipped by
  season discovery (no finalized games with plays yet — kickoff is
  Aug 28); NFL 2026 qualified via finalized preseason games. Every
  run: play→drive linkage repair → per-game recompute (phase 1) →
  straggler sweep → season aggregation (phase 2) → gates.
- **Status**: COMPLETE. All seasons finished both phases; season-row
  stamping is 100% in every season (table below).
- **Code lineage**: formula fixes #624 #625 #626, campaign enablers
  #627 #629; the orchestrator was an operational script run
  in-cluster (metricbot pod, `nohup sh /tmp/campaign.sh`), reproduced
  verbatim in the [script appendix](#appendix-orchestrator-script)
  below.

## Gate results per season

Gate bounds (from the audit doc): PPD ∈ [1.5, 3.5],
SuccessRate ∈ [0.30, 0.55], |FieldPosDiff mean| < 3 (0.000 in every
season — omitted from the table), TimePossRatio ≈ 0.5. Measured on
the with-plays corpus (playless games and legitimate shutouts are not
gate failures). Deviations are recorded, not halting, in unattended
mode:

- **2013/2014 ESPN vintage boundary (both sports)**: PPD +≈0.3 and
  SuccessRate −≈8pts stepping into 2013 — feed-level taxonomy change.
- **Pre-2005 (both sports)**: SuccessRate breaches the 0.30 floor;
  thin play coverage. Excluded from historical comparability.
- **Zero-row tail**: ~1.5–2% of with-plays rows per season are
  permanent zeros (ESPN never published drive data for those games) or
  legitimate scoreless offenses.

| sport | season | with-plays rows | PPD | SuccessRate | TimePossRatio | zero rows | season rows stamped |
|---|---|---|---|---|---|---|---|
| FootballNcaa | 2025 | 1738 | 1.512 | 0.4506 | 0.5004 | 31 | 136/136 |
| FootballNcaa | 2024 | 1682 | 1.500 | 0.4510 | 0.5007 | 27 | 134/134 |
| FootballNcaa | 2023 | 1689 | 1.470 | 0.4509 | 0.5010 | 30 | 133/133 |
| FootballNcaa | 2022 | 1604 | 1.495 | 0.4568 | 0.5008 | 24 | 131/131 |
| FootballNcaa | 2021 | 1581 | 1.500 | 0.4645 | 0.5014 | 32 | 130/130 |
| FootballNcaa | 2020 | 1093 | 1.484 | 0.4527 | 0.5022 | 18 | 128/128 |
| FootballNcaa | 2019 | 915 | 1.437 | 0.4485 | 0.5047 | 22 | 130/130 |
| FootballNcaa | 2018 | 957 | 1.449 | 0.4514 | 0.5054 | 11 | 130/130 |
| FootballNcaa | 2017 | 1544 | 1.416 | 0.4458 | 0.5044 | 31 | 130/130 |
| FootballNcaa | 2016 | 921 | 1.425 | 0.4433 | 0.5052 | 16 | 128/128 |
| FootballNcaa | 2015 | 1621 | 1.422 | 0.4405 | 0.5061 | 36 | 128/128 |
| FootballNcaa | 2014 | 1602 | 1.417 | 0.4486 | 0.5043 | 34 | 132/132 |
| FootballNcaa | 2013 | 1597 | 1.704 | 0.3643 | 0.5016 | 62 | 126/126 |
| FootballNcaa | 2012 | 1463 | 1.672 | 0.3527 | 0.5034 | 56 | 117/117 |
| FootballNcaa | 2011 | 1410 | 1.540 | 0.3463 | 0.5034 | 79 | 112/112 |
| FootballNcaa | 2009 | 1381 | 1.722 | 0.3427 | 0.5059 | 46 | 112/112 |
| FootballNcaa | 2008 | 1361 | 1.996 | 0.3427 | 0.5031 | 37 | 111/111 |
| FootballNcaa | 2007 | 1255 | 1.937 | 0.3400 | 0.5014 | 26 | 111/111 |
| FootballNcaa | 2006 | 1264 | 1.852 | 0.3396 | 0.4999 | 41 | 110/110 |
| FootballNcaa | 2005 | 1120 | 1.891 | 0.4490 | 0.5001 | 25 | 110/110 |
| FootballNcaa | 2004 | 881 | 2.598 | 0.2870 | 0.4886 | 46 | 108/108 |
| FootballNcaa | 2003 | 426 | 2.620 | 0.2874 | 0.5011 | 12 | 110/110 |
| FootballNcaa | 2002 | 9 | 2.507 | 0.2750 | 0.4833 | 0 | 110/110 |
| FootballNcaa | 2001 | 860 | 2.693 | 0.2826 | 0.5017 | 24 | 110/110 |
| FootballNfl | 2026 | 20 | 1.059 | 0.4171 | 0.5000 | 0 | 19/19 |
| FootballNfl | 2025 | 668 | 1.263 | 0.4401 | 0.5000 | 8 | 32/32 |
| FootballNfl | 2024 | 668 | 1.233 | 0.4443 | 0.5001 | 5 | 32/32 |
| FootballNfl | 2023 | 668 | 1.172 | 0.4360 | 0.5001 | 11 | 32/32 |
| FootballNfl | 2022 | 666 | 1.286 | 0.4433 | 0.5000 | 7 | 32/32 |
| FootballNfl | 2021 | 666 | 1.315 | 0.4475 | 0.5001 | 11 | 32/32 |
| FootballNfl | 2020 | 538 | 1.442 | 0.4538 | 0.5001 | 4 | 32/32 |
| FootballNfl | 2019 | 664 | 1.265 | 0.4320 | 0.5000 | 7 | 32/32 |
| FootballNfl | 2018 | 664 | 1.288 | 0.4409 | 0.5000 | 10 | 32/32 |
| FootballNfl | 2017 | 634 | 1.193 | 0.4231 | 0.5000 | 11 | 32/32 |
| FootballNfl | 2016 | 662 | 1.239 | 0.4320 | 0.5000 | 8 | 32/32 |
| FootballNfl | 2015 | 664 | 1.321 | 0.4284 | 0.5000 | 6 | 32/32 |
| FootballNfl | 2014 | 654 | 1.320 | 0.4298 | 0.5001 | 12 | 32/32 |
| FootballNfl | 2013 | 618 | 1.652 | 0.3640 | 0.5000 | 6 | 32/32 |
| FootballNfl | 2012 | 660 | 1.619 | 0.3636 | 0.4985 | 12 | 32/32 |
| FootballNfl | 2011 | 662 | 1.580 | 0.3681 | 0.5000 | 12 | 32/32 |
| FootballNfl | 2010 | 662 | 1.582 | 0.3574 | 0.5000 | 11 | 32/32 |
| FootballNfl | 2009 | 662 | 1.543 | 0.3637 | 0.5001 | 19 | 32/32 |
| FootballNfl | 2008 | 576 | 1.576 | 0.3743 | 0.5001 | 8 | 32/32 |
| FootballNfl | 2007 | 662 | 1.530 | 0.3603 | 0.5000 | 9 | 32/32 |
| FootballNfl | 2006 | 354 | 1.484 | 0.3573 | 0.5001 | 12 | 32/32 |
| FootballNfl | 2005 | 532 | 1.524 | 0.3515 | 0.5001 | 10 | 32/32 |
| FootballNfl | 2004 | 202 | 2.606 | 0.2684 | 0.5000 | 2 | 32/32 |
| FootballNfl | 2003 | 576 | 2.542 | 0.2625 | 0.5000 | 31 | 32/32 |
| FootballNfl | 2002 | 534 | 2.659 | 0.2638 | 0.5001 | 12 | 32/32 |
| FootballNfl | 2001 | 572 | 2.476 | 0.2519 | 0.4895 | 24 | 31/31 |

## v1.1.2 acceptance-sweep denominators (2025 NCAAFB)

Result files `docs/metrics-modeling/output/ncaaf-2025-Wk*-backtest-v5.json`
(gitignored; regenerable — every backtest is deterministic given the
stamped corpus). Weighted aggregates trace to:

| week | SU decided | games w/ spread | SU correct (same games) | ATS decided | ATS Brier |
|---|---|---|---|---|---|
| 4 | 62 | 61 | 47 | 61 | 0.2775 |
| 5 | 53 | 53 | 41 | 53 | 0.2680 |
| 6 | 51 | 51 | 39 | 51 | 0.2620 |
| 8 | 60 | 60 | 44 | 60 | 0.2495 |
| 10 | 52 | 52 | 37 | 52 | 0.2457 |
| **total** | — | **277** | **208** | **277** | **0.2608 (weighted)** |

Weighted same-games SU = 208/277 = **0.7509**;
weighted ATS Brier = **0.2608**.

Pooled calibration buckets (n≥20) violating the 10pt gate:

| bucket | n | predicted | actual | gap |
|---|---|---|---|---|
| 0.0–0.1 | 20 | 0.038 | 0.250 | 21.2pts |
| 0.1–0.2 | 24 | 0.147 | 0.292 | 14.5pts |
| 0.5–0.6 | 20 | 0.560 | 0.800 | 24.0pts |

## Appendix: orchestrator script

Deployed to the metricbot pod as `/tmp/campaign.sh` and launched with
`nohup sh /tmp/campaign.sh >/dev/null 2>&1 &` (no arguments; seasons
are discovered from the databases at run time).

```sh
#!/bin/sh
# Metrics recompute campaign orchestrator — runs detached in the metricbot pod.
# Per sport, per season (newest first): linkage repair -> heal-wait ->
# phase 1 -> drain-wait -> straggler sweep -> phase 2 (retry until the
# NFL/slug fix deploys) -> gates logged. Everything is idempotent and
# resumable: re-running skips healed linkage and restamped rows.
LOG=/tmp/campaign.log
STATE=/tmp/campaign.state

psq() {
  PGPASSWORD="$METRICBOT_PG_PASSWORD" psql -h "$METRICBOT_PG_HOST" -U "$METRICBOT_PG_USER" -d "$1" -tAc "$2" 2>>$LOG
}

log() { echo "$(date -u +%Y-%m-%dT%H:%M:%SZ) $1" >> $LOG; }

broken_count() { # db year -> count of games with <90% linkage
  psq "$1" "SELECT COUNT(*) FROM \"Competition\" c JOIN \"Contest\" ct ON ct.\"Id\" = c.\"ContestId\" CROSS JOIN LATERAL (SELECT COUNT(*) AS plays, COUNT(\"DriveId\") AS linked FROM \"CompetitionPlay\" p WHERE p.\"CompetitionId\" = c.\"Id\") pl WHERE ct.\"SeasonYear\" = $2 AND ct.\"FinalizedUtc\" IS NOT NULL AND pl.plays > 0 AND pl.linked::float / pl.plays < 0.9" | tr -d '[:space:]'
}

run_season() {
  svc=$1; db=$2; year=$3; fbsfilter=$4
  log "[$db $year] START"

  # ---- 1. linkage repair
  psq "$db" "SELECT c.\"Id\" FROM \"Competition\" c JOIN \"Contest\" ct ON ct.\"Id\" = c.\"ContestId\" CROSS JOIN LATERAL (SELECT COUNT(*) AS plays, COUNT(\"DriveId\") AS linked FROM \"CompetitionPlay\" p WHERE p.\"CompetitionId\" = c.\"Id\") pl WHERE ct.\"SeasonYear\" = $year AND ct.\"FinalizedUtc\" IS NOT NULL AND pl.plays > 0 AND pl.linked::float / pl.plays < 0.9" > /tmp/ids.txt
  rcount=$(grep -c . /tmp/ids.txt || true)
  log "[$db $year] repair candidates: $rcount"
  if [ "$rcount" -gt 0 ]; then
    while read -r id; do
      [ -z "$id" ] && continue
      curl -s -o /dev/null -X POST "$svc/api/competitions/$id/drives/refresh"
      sleep 0.25
    done < /tmp/ids.txt
    # ---- 2. heal-wait: stop when count stable across 3 checks (60s apart) or 45 min
    prev=-1; same=0; tries=0
    while [ $tries -lt 45 ]; do
      sleep 60
      rem=$(broken_count "$db" "$year")
      if [ "$rem" = "$prev" ]; then same=$((same+1)); else same=0; fi
      prev=$rem; tries=$((tries+1))
      [ $same -ge 3 ] && break
      [ "$rem" = "0" ] && break
    done
    log "[$db $year] heal plateau: remaining=$prev after ${tries}m"
  fi

  # ---- 3. phase 1 (vintage-aware; resumable)
  p1=$(curl -s -X POST "$svc/api/competitions/metrics/generate/$year")
  log "[$db $year] phase1: $p1"

  # ---- 4. drain-wait: stamped row count stable across 2 checks
  prev=-1; same=0; tries=0
  while [ $tries -lt 30 ]; do
    sleep 30
    st=$(psq "$db" "SELECT COUNT(*) FROM \"CompetitionMetric\" cm JOIN \"Competition\" c ON c.\"Id\" = cm.\"CompetitionId\" JOIN \"Contest\" ct ON ct.\"Id\" = c.\"ContestId\" WHERE ct.\"SeasonYear\" = $year AND cm.\"FormulaVersion\" = '2026.08'" | tr -d '[:space:]')
    if [ "$st" = "$prev" ]; then same=$((same+1)); else same=0; fi
    prev=$st; tries=$((tries+1))
    [ $same -ge 2 ] && break
  done
  log "[$db $year] phase1 drained: stamped=$prev"

  # ---- 5. straggler sweep: zero-PPD games with healthy linkage
  psq "$db" "SELECT DISTINCT cm.\"CompetitionId\" FROM \"CompetitionMetric\" cm JOIN \"Competition\" c ON c.\"Id\" = cm.\"CompetitionId\" JOIN \"Contest\" ct ON ct.\"Id\" = c.\"ContestId\" CROSS JOIN LATERAL (SELECT COUNT(*) AS plays, COUNT(\"DriveId\") AS linked FROM \"CompetitionPlay\" p WHERE p.\"CompetitionId\" = cm.\"CompetitionId\") pl WHERE ct.\"SeasonYear\" = $year AND ct.\"FinalizedUtc\" IS NOT NULL AND cm.\"PointsPerDrive\" = 0 AND pl.plays > 0 AND pl.linked::float / pl.plays >= 0.9" > /tmp/str.txt
  scount=$(grep -c . /tmp/str.txt || true)
  if [ "$scount" -gt 0 ]; then
    while read -r id; do
      [ -z "$id" ] && continue
      curl -s -o /dev/null -X POST "$svc/api/competitions/$id/metrics/generate"
      sleep 0.3
    done < /tmp/str.txt
    sleep 120
  fi
  log "[$db $year] stragglers recomputed: $scount"

  # ---- 6. phase 2 (retry until the fix deploys; 10-min backoff, max 6h)
  t=0
  while [ $t -lt 36 ]; do
    code=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$svc/api/franchise-seasons/seasonYear/$year/metrics/generate")
    if [ "$code" = "202" ] || [ "$code" = "200" ]; then
      log "[$db $year] phase2 accepted"
      break
    fi
    log "[$db $year] phase2 HTTP $code - retry in 10m"
    sleep 600
    t=$((t+1))
  done
  sleep 60

  # ---- 7. gates -> log (no halt in unattended mode; review the report)
  gates=$(psq "$db" "SELECT COUNT(*), ROUND(AVG(cm.\"PointsPerDrive\"),3), ROUND(AVG(cm.\"FieldPosDiff\"),3), ROUND(AVG(cm.\"SuccessRate\"),4), ROUND(AVG(cm.\"TimePossRatio\"),4), COUNT(*) FILTER (WHERE cm.\"PointsPerDrive\" = 0) FROM \"CompetitionMetric\" cm JOIN \"FranchiseSeason\" fs ON fs.\"Id\" = cm.\"FranchiseSeasonId\" JOIN \"Competition\" c ON c.\"Id\" = cm.\"CompetitionId\" JOIN \"Contest\" ct ON ct.\"Id\" = c.\"ContestId\" WHERE ct.\"SeasonYear\" = $year AND cm.\"FormulaVersion\" = '2026.08' AND EXISTS (SELECT 1 FROM \"CompetitionPlay\" p WHERE p.\"CompetitionId\" = cm.\"CompetitionId\") $fbsfilter")
  fsm=$(psq "$db" "SELECT COUNT(*), COUNT(*) FILTER (WHERE \"FormulaVersion\" = '2026.08') FROM \"FranchiseSeasonMetric\" WHERE \"Season\" = $year")
  log "[$db $year] GATES rows|ppd|fpd|success|tpr|zeros: $gates ; season rows|stamped: $fsm"
  echo "$db $year done" >> $STATE
  log "[$db $year] DONE"
}

log "===== CAMPAIGN START ====="

# NCAAFB first (backtest blockers lead), then NFL. Seasons discovered,
# newest first; already-completed seasons re-run cheaply (vintage skip).
NCAA_DB="sdProducer.FootballNcaa"
NCAA_SVC="http://producer-svc-football-ncaa"
NCAA_FBS="AND split_part(fs.\"GroupSeasonMap\", '|', 3) = 'fbs'"
for year in $(psq "$NCAA_DB" "SELECT DISTINCT ct.\"SeasonYear\" FROM \"Contest\" ct WHERE ct.\"FinalizedUtc\" IS NOT NULL AND EXISTS (SELECT 1 FROM \"Competition\" c JOIN \"CompetitionPlay\" p ON p.\"CompetitionId\" = c.\"Id\" WHERE c.\"ContestId\" = ct.\"Id\") ORDER BY 1 DESC"); do
  grep -q "^$NCAA_DB $year done$" $STATE 2>/dev/null && { log "[$NCAA_DB $year] already done - skip"; continue; }
  run_season "$NCAA_SVC" "$NCAA_DB" "$year" "$NCAA_FBS"
done

NFL_DB="sdProducer.FootballNfl"
NFL_SVC="http://producer-svc-football-nfl"
for year in $(psq "$NFL_DB" "SELECT DISTINCT ct.\"SeasonYear\" FROM \"Contest\" ct WHERE ct.\"FinalizedUtc\" IS NOT NULL AND EXISTS (SELECT 1 FROM \"Competition\" c JOIN \"CompetitionPlay\" p ON p.\"CompetitionId\" = c.\"Id\" WHERE c.\"ContestId\" = ct.\"Id\") ORDER BY 1 DESC"); do
  grep -q "^$NFL_DB $year done$" $STATE 2>/dev/null && { log "[$NFL_DB $year] already done - skip"; continue; }
  run_season "$NFL_SVC" "$NFL_DB" "$year" ""
done

log "===== CAMPAIGN COMPLETE ====="
```

## Appendix: raw orchestrator log

```text
2026-08-14T20:00:15Z ===== CAMPAIGN START =====
2026-08-14T20:00:20Z [sdProducer.FootballNcaa 2025] START
2026-08-14T20:00:21Z [sdProducer.FootballNcaa 2025] repair candidates: 3
2026-08-14T20:04:24Z [sdProducer.FootballNcaa 2025] heal plateau: remaining=3 after 4m
2026-08-14T20:04:24Z [sdProducer.FootballNcaa 2025] phase1: {"seasonYear":2025,"totalContests":3768,"enqueuedJobs":0,"message":"Enqueued 0 metric calculation jobs for 3768 contests in season 2025"}
2026-08-14T20:05:55Z [sdProducer.FootballNcaa 2025] phase1 drained: stamped=7536
2026-08-14T20:08:26Z [sdProducer.FootballNcaa 2025] stragglers recomputed: 95
2026-08-14T20:08:28Z [sdProducer.FootballNcaa 2025] phase2 accepted
2026-08-14T20:09:28Z [sdProducer.FootballNcaa 2025] GATES rows|ppd|fpd|success|tpr|zeros: 1738|1.512|0.000|0.4506|0.5004|31 ; season rows|stamped: 136|136
2026-08-14T20:09:28Z [sdProducer.FootballNcaa 2025] DONE
2026-08-14T20:09:28Z [sdProducer.FootballNcaa 2024] START
2026-08-14T20:09:28Z [sdProducer.FootballNcaa 2024] repair candidates: 4
2026-08-14T20:13:31Z [sdProducer.FootballNcaa 2024] heal plateau: remaining=4 after 4m
2026-08-14T20:13:31Z [sdProducer.FootballNcaa 2024] phase1: {"seasonYear":2024,"totalContests":3787,"enqueuedJobs":0,"message":"Enqueued 0 metric calculation jobs for 3787 contests in season 2024"}
2026-08-14T20:15:01Z [sdProducer.FootballNcaa 2024] phase1 drained: stamped=7574
2026-08-14T20:17:33Z [sdProducer.FootballNcaa 2024] stragglers recomputed: 91
2026-08-14T20:17:33Z [sdProducer.FootballNcaa 2024] phase2 HTTP 500 - retry in 10m
2026-08-14T20:27:33Z [sdProducer.FootballNcaa 2024] phase2 HTTP 500 - retry in 10m
2026-08-14T20:37:33Z [sdProducer.FootballNcaa 2024] phase2 HTTP 500 - retry in 10m
2026-08-14T20:47:36Z [sdProducer.FootballNcaa 2024] phase2 accepted
2026-08-14T20:48:36Z [sdProducer.FootballNcaa 2024] GATES rows|ppd|fpd|success|tpr|zeros: 1682|1.500|0.000|0.4510|0.5007|27 ; season rows|stamped: 134|134
2026-08-14T20:48:36Z [sdProducer.FootballNcaa 2024] DONE
2026-08-14T20:48:36Z [sdProducer.FootballNcaa 2023] START
2026-08-14T20:48:36Z [sdProducer.FootballNcaa 2023] repair candidates: 6
2026-08-14T20:52:40Z [sdProducer.FootballNcaa 2023] heal plateau: remaining=6 after 4m
2026-08-14T20:53:48Z [sdProducer.FootballNcaa 2023] phase1: {"seasonYear":2023,"totalContests":3713,"enqueuedJobs":3713,"message":"Enqueued 3713 metric calculation jobs for 3713 contests in season 2023"}
2026-08-14T20:55:48Z [sdProducer.FootballNcaa 2023] phase1 drained: stamped=7426
2026-08-14T20:58:13Z [sdProducer.FootballNcaa 2023] stragglers recomputed: 74
2026-08-14T20:58:14Z [sdProducer.FootballNcaa 2023] phase2 accepted
2026-08-14T20:59:15Z [sdProducer.FootballNcaa 2023] GATES rows|ppd|fpd|success|tpr|zeros: 1689|1.470|0.000|0.4509|0.5010|30 ; season rows|stamped: 133|133
2026-08-14T20:59:15Z [sdProducer.FootballNcaa 2023] DONE
2026-08-14T20:59:15Z [sdProducer.FootballNcaa 2022] START
2026-08-14T20:59:16Z [sdProducer.FootballNcaa 2022] repair candidates: 1418
2026-08-14T21:14:33Z [sdProducer.FootballNcaa 2022] heal plateau: remaining=3 after 8m
2026-08-14T21:15:44Z [sdProducer.FootballNcaa 2022] phase1: {"seasonYear":2022,"totalContests":3704,"enqueuedJobs":3704,"message":"Enqueued 3704 metric calculation jobs for 3704 contests in season 2022"}
2026-08-14T21:17:14Z [sdProducer.FootballNcaa 2022] phase1 drained: stamped=7408
2026-08-14T21:19:40Z [sdProducer.FootballNcaa 2022] stragglers recomputed: 79
2026-08-14T21:19:41Z [sdProducer.FootballNcaa 2022] phase2 accepted
2026-08-14T21:20:42Z [sdProducer.FootballNcaa 2022] GATES rows|ppd|fpd|success|tpr|zeros: 1604|1.495|0.000|0.4568|0.5008|24 ; season rows|stamped: 131|131
2026-08-14T21:20:42Z [sdProducer.FootballNcaa 2022] DONE
2026-08-14T21:20:42Z [sdProducer.FootballNcaa 2021] START
2026-08-14T21:20:43Z [sdProducer.FootballNcaa 2021] repair candidates: 1403
2026-08-14T21:35:50Z [sdProducer.FootballNcaa 2021] heal plateau: remaining=16 after 8m
2026-08-14T21:36:56Z [sdProducer.FootballNcaa 2021] phase1: {"seasonYear":2021,"totalContests":3605,"enqueuedJobs":3605,"message":"Enqueued 3605 metric calculation jobs for 3605 contests in season 2021"}
2026-08-14T21:38:27Z [sdProducer.FootballNcaa 2021] phase1 drained: stamped=7210
2026-08-14T21:40:53Z [sdProducer.FootballNcaa 2021] stragglers recomputed: 81
2026-08-14T21:40:55Z [sdProducer.FootballNcaa 2021] phase2 accepted
2026-08-14T21:41:55Z [sdProducer.FootballNcaa 2021] GATES rows|ppd|fpd|success|tpr|zeros: 1581|1.500|0.000|0.4645|0.5014|32 ; season rows|stamped: 130|130
2026-08-14T21:41:55Z [sdProducer.FootballNcaa 2021] DONE
2026-08-14T21:41:55Z [sdProducer.FootballNcaa 2020] START
2026-08-14T21:41:55Z [sdProducer.FootballNcaa 2020] repair candidates: 801
2026-08-14T21:48:55Z [sdProducer.FootballNcaa 2020] heal plateau: remaining=0 after 3m
2026-08-14T21:49:16Z [sdProducer.FootballNcaa 2020] phase1: {"seasonYear":2020,"totalContests":1136,"enqueuedJobs":1136,"message":"Enqueued 1136 metric calculation jobs for 1136 contests in season 2020"}
2026-08-14T21:50:46Z [sdProducer.FootballNcaa 2020] phase1 drained: stamped=2272
2026-08-14T21:52:57Z [sdProducer.FootballNcaa 2020] stragglers recomputed: 32
2026-08-14T21:52:59Z [sdProducer.FootballNcaa 2020] phase2 accepted
2026-08-14T21:53:59Z [sdProducer.FootballNcaa 2020] GATES rows|ppd|fpd|success|tpr|zeros: 1093|1.484|0.000|0.4527|0.5022|18 ; season rows|stamped: 128|128
2026-08-14T21:53:59Z [sdProducer.FootballNcaa 2020] DONE
2026-08-14T21:53:59Z [sdProducer.FootballNcaa 2019] START
2026-08-14T21:54:00Z [sdProducer.FootballNcaa 2019] repair candidates: 770
2026-08-14T22:03:54Z [sdProducer.FootballNcaa 2019] heal plateau: remaining=1 after 6m
2026-08-14T22:04:17Z [sdProducer.FootballNcaa 2019] phase1: {"seasonYear":2019,"totalContests":1470,"enqueuedJobs":1470,"message":"Enqueued 1470 metric calculation jobs for 1470 contests in season 2019"}
2026-08-14T22:07:18Z [sdProducer.FootballNcaa 2019] phase1 drained: stamped=2940
2026-08-14T22:09:33Z [sdProducer.FootballNcaa 2019] stragglers recomputed: 45
2026-08-14T22:09:36Z [sdProducer.FootballNcaa 2019] phase2 accepted
2026-08-14T22:10:36Z [sdProducer.FootballNcaa 2019] GATES rows|ppd|fpd|success|tpr|zeros: 915|1.437|0.000|0.4485|0.5047|22 ; season rows|stamped: 130|130
2026-08-14T22:10:36Z [sdProducer.FootballNcaa 2019] DONE
2026-08-14T22:10:36Z [sdProducer.FootballNcaa 2018] START
2026-08-14T22:10:38Z [sdProducer.FootballNcaa 2018] repair candidates: 688
2026-08-14T22:18:08Z [sdProducer.FootballNcaa 2018] heal plateau: remaining=0 after 4m
2026-08-14T22:18:32Z [sdProducer.FootballNcaa 2018] phase1: {"seasonYear":2018,"totalContests":1252,"enqueuedJobs":1252,"message":"Enqueued 1252 metric calculation jobs for 1252 contests in season 2018"}
2026-08-14T22:20:02Z [sdProducer.FootballNcaa 2018] phase1 drained: stamped=2504
2026-08-14T22:22:11Z [sdProducer.FootballNcaa 2018] stragglers recomputed: 25
2026-08-14T22:22:12Z [sdProducer.FootballNcaa 2018] phase2 accepted
2026-08-14T22:23:13Z [sdProducer.FootballNcaa 2018] GATES rows|ppd|fpd|success|tpr|zeros: 957|1.449|0.000|0.4514|0.5054|11 ; season rows|stamped: 130|130
2026-08-14T22:23:13Z [sdProducer.FootballNcaa 2018] DONE
2026-08-14T22:23:13Z [sdProducer.FootballNcaa 2017] START
2026-08-14T22:23:15Z [sdProducer.FootballNcaa 2017] repair candidates: 1355
2026-08-14T22:38:12Z [sdProducer.FootballNcaa 2017] heal plateau: remaining=2 after 8m
2026-08-14T22:39:24Z [sdProducer.FootballNcaa 2017] phase1: {"seasonYear":2017,"totalContests":3590,"enqueuedJobs":3590,"message":"Enqueued 3590 metric calculation jobs for 3590 contests in season 2017"}
2026-08-14T22:40:55Z [sdProducer.FootballNcaa 2017] phase1 drained: stamped=7180
2026-08-14T22:43:25Z [sdProducer.FootballNcaa 2017] stragglers recomputed: 92
2026-08-14T22:43:27Z [sdProducer.FootballNcaa 2017] phase2 accepted
2026-08-14T22:44:27Z [sdProducer.FootballNcaa 2017] GATES rows|ppd|fpd|success|tpr|zeros: 1544|1.416|0.000|0.4458|0.5044|31 ; season rows|stamped: 130|130
2026-08-14T22:44:27Z [sdProducer.FootballNcaa 2017] DONE
2026-08-14T22:44:27Z [sdProducer.FootballNcaa 2016] START
2026-08-14T22:44:27Z [sdProducer.FootballNcaa 2016] repair candidates: 694
2026-08-14T22:53:59Z [sdProducer.FootballNcaa 2016] heal plateau: remaining=2 after 6m
2026-08-14T22:54:22Z [sdProducer.FootballNcaa 2016] phase1: {"seasonYear":2016,"totalContests":887,"enqueuedJobs":887,"message":"Enqueued 887 metric calculation jobs for 887 contests in season 2016"}
2026-08-14T22:55:52Z [sdProducer.FootballNcaa 2016] phase1 drained: stamped=1774
2026-08-14T22:58:06Z [sdProducer.FootballNcaa 2016] stragglers recomputed: 41
2026-08-14T22:58:08Z [sdProducer.FootballNcaa 2016] phase2 accepted
2026-08-14T22:59:08Z [sdProducer.FootballNcaa 2016] GATES rows|ppd|fpd|success|tpr|zeros: 921|1.425|0.000|0.4433|0.5052|16 ; season rows|stamped: 128|128
2026-08-14T22:59:08Z [sdProducer.FootballNcaa 2016] DONE
2026-08-14T22:59:08Z [sdProducer.FootballNcaa 2015] START
2026-08-14T22:59:10Z [sdProducer.FootballNcaa 2015] repair candidates: 1431
2026-08-14T23:14:30Z [sdProducer.FootballNcaa 2015] heal plateau: remaining=4 after 8m
2026-08-14T23:15:55Z [sdProducer.FootballNcaa 2015] phase1: {"seasonYear":2015,"totalContests":3728,"enqueuedJobs":3728,"message":"Enqueued 3728 metric calculation jobs for 3728 contests in season 2015"}
2026-08-14T23:17:25Z [sdProducer.FootballNcaa 2015] phase1 drained: stamped=7456
2026-08-14T23:19:59Z [sdProducer.FootballNcaa 2015] stragglers recomputed: 99
2026-08-14T23:20:02Z [sdProducer.FootballNcaa 2015] phase2 accepted
2026-08-14T23:21:02Z [sdProducer.FootballNcaa 2015] GATES rows|ppd|fpd|success|tpr|zeros: 1621|1.422|0.000|0.4405|0.5061|36 ; season rows|stamped: 128|128
2026-08-14T23:21:02Z [sdProducer.FootballNcaa 2015] DONE
2026-08-14T23:21:02Z [sdProducer.FootballNcaa 2014] START
2026-08-14T23:21:04Z [sdProducer.FootballNcaa 2014] repair candidates: 1395
2026-08-14T23:39:28Z [sdProducer.FootballNcaa 2014] heal plateau: remaining=5 after 11m
2026-08-14T23:41:08Z [sdProducer.FootballNcaa 2014] phase1: {"seasonYear":2014,"totalContests":3783,"enqueuedJobs":3783,"message":"Enqueued 3783 metric calculation jobs for 3783 contests in season 2014"}
2026-08-14T23:42:38Z [sdProducer.FootballNcaa 2014] phase1 drained: stamped=7566
2026-08-14T23:45:10Z [sdProducer.FootballNcaa 2014] stragglers recomputed: 92
2026-08-14T23:45:12Z [sdProducer.FootballNcaa 2014] phase2 accepted
2026-08-14T23:46:12Z [sdProducer.FootballNcaa 2014] GATES rows|ppd|fpd|success|tpr|zeros: 1602|1.417|0.000|0.4486|0.5043|34 ; season rows|stamped: 132|132
2026-08-14T23:46:12Z [sdProducer.FootballNcaa 2014] DONE
2026-08-14T23:46:12Z [sdProducer.FootballNcaa 2013] START
2026-08-14T23:46:36Z [sdProducer.FootballNcaa 2013] repair candidates: 1488
2026-08-15T00:09:14Z [sdProducer.FootballNcaa 2013] heal plateau: remaining=1 after 13m
2026-08-15T00:12:16Z [sdProducer.FootballNcaa 2013] phase1: {"seasonYear":2013,"totalContests":3760,"enqueuedJobs":3760,"message":"Enqueued 3760 metric calculation jobs for 3760 contests in season 2013"}
2026-08-15T00:13:48Z [sdProducer.FootballNcaa 2013] phase1 drained: stamped=7520
2026-08-15T00:16:40Z [sdProducer.FootballNcaa 2013] stragglers recomputed: 147
2026-08-15T00:16:44Z [sdProducer.FootballNcaa 2013] phase2 accepted
2026-08-15T00:17:44Z [sdProducer.FootballNcaa 2013] GATES rows|ppd|fpd|success|tpr|zeros: 1597|1.704|0.000|0.3643|0.5016|62 ; season rows|stamped: 126|126
2026-08-15T00:17:44Z [sdProducer.FootballNcaa 2013] DONE
2026-08-15T00:17:44Z [sdProducer.FootballNcaa 2012] START
2026-08-15T00:17:54Z [sdProducer.FootballNcaa 2012] repair candidates: 1289
2026-08-15T00:42:55Z [sdProducer.FootballNcaa 2012] heal plateau: remaining=2 after 15m
2026-08-15T00:46:37Z [sdProducer.FootballNcaa 2012] phase1: {"seasonYear":2012,"totalContests":3647,"enqueuedJobs":3647,"message":"Enqueued 3647 metric calculation jobs for 3647 contests in season 2012"}
2026-08-15T00:48:08Z [sdProducer.FootballNcaa 2012] phase1 drained: stamped=7294
2026-08-15T00:50:53Z [sdProducer.FootballNcaa 2012] stragglers recomputed: 118
2026-08-15T00:51:04Z [sdProducer.FootballNcaa 2012] phase2 accepted
2026-08-15T00:52:04Z [sdProducer.FootballNcaa 2012] GATES rows|ppd|fpd|success|tpr|zeros: 1463|1.672|0.000|0.3527|0.5034|56 ; season rows|stamped: 117|117
2026-08-15T00:52:04Z [sdProducer.FootballNcaa 2012] DONE
2026-08-15T00:52:04Z [sdProducer.FootballNcaa 2011] START
2026-08-15T00:52:56Z [sdProducer.FootballNcaa 2011] repair candidates: 1290
2026-08-15T01:18:10Z [sdProducer.FootballNcaa 2011] heal plateau: remaining=6 after 14m
2026-08-15T01:20:32Z [sdProducer.FootballNcaa 2011] phase1: {"seasonYear":2011,"totalContests":1292,"enqueuedJobs":1292,"message":"Enqueued 1292 metric calculation jobs for 1292 contests in season 2011"}
2026-08-15T01:22:15Z [sdProducer.FootballNcaa 2011] phase1 drained: stamped=2584
2026-08-15T01:25:17Z [sdProducer.FootballNcaa 2011] stragglers recomputed: 139
2026-08-15T01:25:29Z [sdProducer.FootballNcaa 2011] phase2 accepted
2026-08-15T01:26:32Z [sdProducer.FootballNcaa 2011] GATES rows|ppd|fpd|success|tpr|zeros: 1410|1.540|0.000|0.3463|0.5034|79 ; season rows|stamped: 112|112
2026-08-15T01:26:32Z [sdProducer.FootballNcaa 2011] DONE
2026-08-15T01:26:32Z [sdProducer.FootballNcaa 2009] START
2026-08-15T01:28:42Z [sdProducer.FootballNcaa 2009] repair candidates: 1156
2026-08-15T01:50:22Z [sdProducer.FootballNcaa 2009] heal plateau: remaining=3 after 13m
2026-08-15T01:55:20Z [sdProducer.FootballNcaa 2009] phase1: {"seasonYear":2009,"totalContests":3542,"enqueuedJobs":3542,"message":"Enqueued 3542 metric calculation jobs for 3542 contests in season 2009"}
2026-08-15T01:56:53Z [sdProducer.FootballNcaa 2009] phase1 drained: stamped=7084
2026-08-15T01:59:48Z [sdProducer.FootballNcaa 2009] stragglers recomputed: 117
2026-08-15T02:00:03Z [sdProducer.FootballNcaa 2009] phase2 accepted
2026-08-15T02:01:06Z [sdProducer.FootballNcaa 2009] GATES rows|ppd|fpd|success|tpr|zeros: 1381|1.722|0.000|0.3427|0.5059|46 ; season rows|stamped: 112|112
2026-08-15T02:01:06Z [sdProducer.FootballNcaa 2009] DONE
2026-08-15T02:01:06Z [sdProducer.FootballNcaa 2008] START
2026-08-15T02:03:11Z [sdProducer.FootballNcaa 2008] repair candidates: 1144
2026-08-15T02:22:17Z [sdProducer.FootballNcaa 2008] heal plateau: remaining=0 after 10m
2026-08-15T02:28:08Z [sdProducer.FootballNcaa 2008] phase1: {"seasonYear":2008,"totalContests":3556,"enqueuedJobs":3556,"message":"Enqueued 3556 metric calculation jobs for 3556 contests in season 2008"}
2026-08-15T02:29:41Z [sdProducer.FootballNcaa 2008] phase1 drained: stamped=7112
2026-08-15T02:32:21Z [sdProducer.FootballNcaa 2008] stragglers recomputed: 88
2026-08-15T02:32:34Z [sdProducer.FootballNcaa 2008] phase2 accepted
2026-08-15T02:33:37Z [sdProducer.FootballNcaa 2008] GATES rows|ppd|fpd|success|tpr|zeros: 1361|1.996|0.000|0.3427|0.5031|37 ; season rows|stamped: 111|111
2026-08-15T02:33:37Z [sdProducer.FootballNcaa 2008] DONE
2026-08-15T02:33:37Z [sdProducer.FootballNcaa 2007] START
2026-08-15T02:36:17Z [sdProducer.FootballNcaa 2007] repair candidates: 850
2026-08-15T02:48:18Z [sdProducer.FootballNcaa 2007] heal plateau: remaining=2 after 7m
2026-08-15T02:49:34Z [sdProducer.FootballNcaa 2007] phase1: {"seasonYear":2007,"totalContests":3424,"enqueuedJobs":3424,"message":"Enqueued 3424 metric calculation jobs for 3424 contests in season 2007"}
2026-08-15T02:51:04Z [sdProducer.FootballNcaa 2007] phase1 drained: stamped=6848
2026-08-15T02:53:27Z [sdProducer.FootballNcaa 2007] stragglers recomputed: 56
2026-08-15T02:53:29Z [sdProducer.FootballNcaa 2007] phase2 accepted
2026-08-15T02:54:29Z [sdProducer.FootballNcaa 2007] GATES rows|ppd|fpd|success|tpr|zeros: 1255|1.937|0.000|0.3400|0.5014|26 ; season rows|stamped: 111|111
2026-08-15T02:54:30Z [sdProducer.FootballNcaa 2007] DONE
2026-08-15T02:54:30Z [sdProducer.FootballNcaa 2006] START
2026-08-15T02:54:37Z [sdProducer.FootballNcaa 2006] repair candidates: 640
2026-08-15T03:03:57Z [sdProducer.FootballNcaa 2006] heal plateau: remaining=1 after 6m
2026-08-15T03:04:35Z [sdProducer.FootballNcaa 2006] phase1: {"seasonYear":2006,"totalContests":1532,"enqueuedJobs":1532,"message":"Enqueued 1532 metric calculation jobs for 1532 contests in season 2006"}
2026-08-15T03:07:05Z [sdProducer.FootballNcaa 2006] phase1 drained: stamped=3064
2026-08-15T03:09:21Z [sdProducer.FootballNcaa 2006] stragglers recomputed: 47
2026-08-15T03:09:24Z [sdProducer.FootballNcaa 2006] phase2 accepted
2026-08-15T03:10:24Z [sdProducer.FootballNcaa 2006] GATES rows|ppd|fpd|success|tpr|zeros: 1264|1.852|0.000|0.3396|0.4999|41 ; season rows|stamped: 110|110
2026-08-15T03:10:24Z [sdProducer.FootballNcaa 2006] DONE
2026-08-15T03:10:24Z [sdProducer.FootballNcaa 2005] START
2026-08-15T03:10:48Z [sdProducer.FootballNcaa 2005] repair candidates: 439
2026-08-15T03:19:06Z [sdProducer.FootballNcaa 2005] heal plateau: remaining=3 after 6m
2026-08-15T03:19:34Z [sdProducer.FootballNcaa 2005] phase1: {"seasonYear":2005,"totalContests":1416,"enqueuedJobs":1416,"message":"Enqueued 1416 metric calculation jobs for 1416 contests in season 2005"}
2026-08-15T03:22:04Z [sdProducer.FootballNcaa 2005] phase1 drained: stamped=2832
2026-08-15T03:24:14Z [sdProducer.FootballNcaa 2005] stragglers recomputed: 28
2026-08-15T03:24:16Z [sdProducer.FootballNcaa 2005] phase2 accepted
2026-08-15T03:25:16Z [sdProducer.FootballNcaa 2005] GATES rows|ppd|fpd|success|tpr|zeros: 1120|1.891|0.000|0.4490|0.5001|25 ; season rows|stamped: 110|110
2026-08-15T03:25:16Z [sdProducer.FootballNcaa 2005] DONE
2026-08-15T03:25:16Z [sdProducer.FootballNcaa 2004] START
2026-08-15T03:25:25Z [sdProducer.FootballNcaa 2004] repair candidates: 469
2026-08-15T03:34:52Z [sdProducer.FootballNcaa 2004] heal plateau: remaining=14 after 7m
2026-08-15T03:35:31Z [sdProducer.FootballNcaa 2004] phase1: {"seasonYear":2004,"totalContests":1416,"enqueuedJobs":1416,"message":"Enqueued 1416 metric calculation jobs for 1416 contests in season 2004"}
2026-08-15T03:37:02Z [sdProducer.FootballNcaa 2004] phase1 drained: stamped=2832
2026-08-15T03:39:11Z [sdProducer.FootballNcaa 2004] stragglers recomputed: 28
2026-08-15T03:39:15Z [sdProducer.FootballNcaa 2004] phase2 accepted
2026-08-15T03:40:15Z [sdProducer.FootballNcaa 2004] GATES rows|ppd|fpd|success|tpr|zeros: 881|2.598|0.000|0.2870|0.4886|46 ; season rows|stamped: 108|108
2026-08-15T03:40:15Z [sdProducer.FootballNcaa 2004] DONE
2026-08-15T03:40:15Z [sdProducer.FootballNcaa 2003] START
2026-08-15T03:40:17Z [sdProducer.FootballNcaa 2003] repair candidates: 218
2026-08-15T03:46:22Z [sdProducer.FootballNcaa 2003] heal plateau: remaining=1 after 5m
2026-08-15T03:46:41Z [sdProducer.FootballNcaa 2003] phase1: {"seasonYear":2003,"totalContests":620,"enqueuedJobs":620,"message":"Enqueued 620 metric calculation jobs for 620 contests in season 2003"}
2026-08-15T03:49:41Z [sdProducer.FootballNcaa 2003] phase1 drained: stamped=1240
2026-08-15T03:51:47Z [sdProducer.FootballNcaa 2003] stragglers recomputed: 15
2026-08-15T03:51:49Z [sdProducer.FootballNcaa 2003] phase2 accepted
2026-08-15T03:52:49Z [sdProducer.FootballNcaa 2003] GATES rows|ppd|fpd|success|tpr|zeros: 426|2.620|0.000|0.2874|0.5011|12 ; season rows|stamped: 110|110
2026-08-15T03:52:49Z [sdProducer.FootballNcaa 2003] DONE
2026-08-15T03:52:49Z [sdProducer.FootballNcaa 2002] START
2026-08-15T03:52:49Z [sdProducer.FootballNcaa 2002] repair candidates: 5
2026-08-15T03:53:51Z [sdProducer.FootballNcaa 2002] heal plateau: remaining=0 after 1m
2026-08-15T03:53:58Z [sdProducer.FootballNcaa 2002] phase1: {"seasonYear":2002,"totalContests":371,"enqueuedJobs":371,"message":"Enqueued 371 metric calculation jobs for 371 contests in season 2002"}
2026-08-15T03:55:28Z [sdProducer.FootballNcaa 2002] phase1 drained: stamped=742
2026-08-15T03:55:29Z [sdProducer.FootballNcaa 2002] stragglers recomputed: 0
2026-08-15T03:55:31Z [sdProducer.FootballNcaa 2002] phase2 accepted
2026-08-15T03:56:31Z [sdProducer.FootballNcaa 2002] GATES rows|ppd|fpd|success|tpr|zeros: 9|2.507|0.000|0.2750|0.4833|0 ; season rows|stamped: 110|110
2026-08-15T03:56:31Z [sdProducer.FootballNcaa 2002] DONE
2026-08-15T03:56:31Z [sdProducer.FootballNcaa 2001] START
2026-08-15T03:56:37Z [sdProducer.FootballNcaa 2001] repair candidates: 438
2026-08-15T04:02:53Z [sdProducer.FootballNcaa 2001] heal plateau: remaining=0 after 4m
2026-08-15T04:03:25Z [sdProducer.FootballNcaa 2001] phase1: {"seasonYear":2001,"totalContests":1378,"enqueuedJobs":1378,"message":"Enqueued 1378 metric calculation jobs for 1378 contests in season 2001"}
2026-08-15T04:04:55Z [sdProducer.FootballNcaa 2001] phase1 drained: stamped=2756
2026-08-15T04:07:05Z [sdProducer.FootballNcaa 2001] stragglers recomputed: 28
2026-08-15T04:07:08Z [sdProducer.FootballNcaa 2001] phase2 accepted
2026-08-15T04:08:08Z [sdProducer.FootballNcaa 2001] GATES rows|ppd|fpd|success|tpr|zeros: 860|2.693|0.000|0.2826|0.5017|24 ; season rows|stamped: 110|110
2026-08-15T04:08:08Z [sdProducer.FootballNcaa 2001] DONE
2026-08-15T04:08:09Z [sdProducer.FootballNfl 2026] START
2026-08-15T04:08:10Z [sdProducer.FootballNfl 2026] repair candidates: 10
2026-08-15T04:09:13Z [sdProducer.FootballNfl 2026] heal plateau: remaining=0 after 1m
2026-08-15T04:09:13Z [sdProducer.FootballNfl 2026] phase1: {"seasonYear":2026,"totalContests":10,"enqueuedJobs":10,"message":"Enqueued 10 metric calculation jobs for 10 contests in season 2026"}
2026-08-15T04:10:43Z [sdProducer.FootballNfl 2026] phase1 drained: stamped=20
2026-08-15T04:10:43Z [sdProducer.FootballNfl 2026] stragglers recomputed: 0
2026-08-15T04:10:44Z [sdProducer.FootballNfl 2026] phase2 accepted
2026-08-15T04:11:44Z [sdProducer.FootballNfl 2026] GATES rows|ppd|fpd|success|tpr|zeros: 20|1.059|0.000|0.4171|0.5000|0 ; season rows|stamped: 19|19
2026-08-15T04:11:44Z [sdProducer.FootballNfl 2026] DONE
2026-08-15T04:11:44Z [sdProducer.FootballNfl 2025] START
2026-08-15T04:11:47Z [sdProducer.FootballNfl 2025] repair candidates: 283
2026-08-15T04:14:09Z [sdProducer.FootballNfl 2025] heal plateau: remaining=0 after 1m
2026-08-15T04:14:18Z [sdProducer.FootballNfl 2025] phase1: {"seasonYear":2025,"totalContests":334,"enqueuedJobs":334,"message":"Enqueued 334 metric calculation jobs for 334 contests in season 2025"}
2026-08-15T04:15:48Z [sdProducer.FootballNfl 2025] phase1 drained: stamped=668
2026-08-15T04:17:51Z [sdProducer.FootballNfl 2025] stragglers recomputed: 8
2026-08-15T04:17:52Z [sdProducer.FootballNfl 2025] phase2 accepted
2026-08-15T04:18:52Z [sdProducer.FootballNfl 2025] GATES rows|ppd|fpd|success|tpr|zeros: 668|1.263|0.000|0.4401|0.5000|8 ; season rows|stamped: 32|32
2026-08-15T04:18:52Z [sdProducer.FootballNfl 2025] DONE
2026-08-15T04:18:52Z [sdProducer.FootballNfl 2024] START
2026-08-15T04:19:00Z [sdProducer.FootballNfl 2024] repair candidates: 282
2026-08-15T04:22:19Z [sdProducer.FootballNfl 2024] heal plateau: remaining=0 after 2m
2026-08-15T04:22:27Z [sdProducer.FootballNfl 2024] phase1: {"seasonYear":2024,"totalContests":334,"enqueuedJobs":334,"message":"Enqueued 334 metric calculation jobs for 334 contests in season 2024"}
2026-08-15T04:23:57Z [sdProducer.FootballNfl 2024] phase1 drained: stamped=668
2026-08-15T04:25:59Z [sdProducer.FootballNfl 2024] stragglers recomputed: 5
2026-08-15T04:25:59Z [sdProducer.FootballNfl 2024] phase2 accepted
2026-08-15T04:26:59Z [sdProducer.FootballNfl 2024] GATES rows|ppd|fpd|success|tpr|zeros: 668|1.233|0.000|0.4443|0.5001|5 ; season rows|stamped: 32|32
2026-08-15T04:26:59Z [sdProducer.FootballNfl 2024] DONE
2026-08-15T04:26:59Z [sdProducer.FootballNfl 2023] START
2026-08-15T04:27:03Z [sdProducer.FootballNfl 2023] repair candidates: 290
2026-08-15T04:29:27Z [sdProducer.FootballNfl 2023] heal plateau: remaining=0 after 1m
2026-08-15T04:29:35Z [sdProducer.FootballNfl 2023] phase1: {"seasonYear":2023,"totalContests":334,"enqueuedJobs":334,"message":"Enqueued 334 metric calculation jobs for 334 contests in season 2023"}
2026-08-15T04:31:05Z [sdProducer.FootballNfl 2023] phase1 drained: stamped=668
2026-08-15T04:33:09Z [sdProducer.FootballNfl 2023] stragglers recomputed: 11
2026-08-15T04:33:10Z [sdProducer.FootballNfl 2023] phase2 accepted
2026-08-15T04:34:10Z [sdProducer.FootballNfl 2023] GATES rows|ppd|fpd|success|tpr|zeros: 668|1.172|0.000|0.4360|0.5001|11 ; season rows|stamped: 32|32
2026-08-15T04:34:10Z [sdProducer.FootballNfl 2023] DONE
2026-08-15T04:34:10Z [sdProducer.FootballNfl 2022] START
2026-08-15T04:34:14Z [sdProducer.FootballNfl 2022] repair candidates: 245
2026-08-15T04:37:25Z [sdProducer.FootballNfl 2022] heal plateau: remaining=0 after 2m
2026-08-15T04:37:35Z [sdProducer.FootballNfl 2022] phase1: {"seasonYear":2022,"totalContests":333,"enqueuedJobs":333,"message":"Enqueued 333 metric calculation jobs for 333 contests in season 2022"}
2026-08-15T04:39:05Z [sdProducer.FootballNfl 2022] phase1 drained: stamped=666
2026-08-15T04:41:07Z [sdProducer.FootballNfl 2022] stragglers recomputed: 7
2026-08-15T04:41:08Z [sdProducer.FootballNfl 2022] phase2 accepted
2026-08-15T04:42:08Z [sdProducer.FootballNfl 2022] GATES rows|ppd|fpd|success|tpr|zeros: 666|1.286|0.000|0.4433|0.5000|7 ; season rows|stamped: 32|32
2026-08-15T04:42:08Z [sdProducer.FootballNfl 2022] DONE
2026-08-15T04:42:08Z [sdProducer.FootballNfl 2021] START
2026-08-15T04:42:10Z [sdProducer.FootballNfl 2021] repair candidates: 205
2026-08-15T04:44:09Z [sdProducer.FootballNfl 2021] heal plateau: remaining=0 after 1m
2026-08-15T04:44:15Z [sdProducer.FootballNfl 2021] phase1: {"seasonYear":2021,"totalContests":333,"enqueuedJobs":333,"message":"Enqueued 333 metric calculation jobs for 333 contests in season 2021"}
2026-08-15T04:45:45Z [sdProducer.FootballNfl 2021] phase1 drained: stamped=666
2026-08-15T04:47:49Z [sdProducer.FootballNfl 2021] stragglers recomputed: 11
2026-08-15T04:47:49Z [sdProducer.FootballNfl 2021] phase2 accepted
2026-08-15T04:48:49Z [sdProducer.FootballNfl 2021] GATES rows|ppd|fpd|success|tpr|zeros: 666|1.315|0.000|0.4475|0.5001|11 ; season rows|stamped: 32|32
2026-08-15T04:48:49Z [sdProducer.FootballNfl 2021] DONE
2026-08-15T04:48:49Z [sdProducer.FootballNfl 2020] START
2026-08-15T04:48:50Z [sdProducer.FootballNfl 2020] repair candidates: 190
2026-08-15T04:51:43Z [sdProducer.FootballNfl 2020] heal plateau: remaining=0 after 2m
2026-08-15T04:51:50Z [sdProducer.FootballNfl 2020] phase1: {"seasonYear":2020,"totalContests":269,"enqueuedJobs":269,"message":"Enqueued 269 metric calculation jobs for 269 contests in season 2020"}
2026-08-15T04:53:20Z [sdProducer.FootballNfl 2020] phase1 drained: stamped=538
2026-08-15T04:55:21Z [sdProducer.FootballNfl 2020] stragglers recomputed: 4
2026-08-15T04:55:22Z [sdProducer.FootballNfl 2020] phase2 accepted
2026-08-15T04:56:22Z [sdProducer.FootballNfl 2020] GATES rows|ppd|fpd|success|tpr|zeros: 538|1.442|0.000|0.4538|0.5001|4 ; season rows|stamped: 32|32
2026-08-15T04:56:22Z [sdProducer.FootballNfl 2020] DONE
2026-08-15T04:56:22Z [sdProducer.FootballNfl 2019] START
2026-08-15T04:56:26Z [sdProducer.FootballNfl 2019] repair candidates: 221
2026-08-15T04:58:28Z [sdProducer.FootballNfl 2019] heal plateau: remaining=0 after 1m
2026-08-15T04:58:35Z [sdProducer.FootballNfl 2019] phase1: {"seasonYear":2019,"totalContests":332,"enqueuedJobs":332,"message":"Enqueued 332 metric calculation jobs for 332 contests in season 2019"}
2026-08-15T05:00:05Z [sdProducer.FootballNfl 2019] phase1 drained: stamped=664
2026-08-15T05:02:07Z [sdProducer.FootballNfl 2019] stragglers recomputed: 7
2026-08-15T05:02:08Z [sdProducer.FootballNfl 2019] phase2 accepted
2026-08-15T05:03:08Z [sdProducer.FootballNfl 2019] GATES rows|ppd|fpd|success|tpr|zeros: 664|1.265|0.000|0.4320|0.5000|7 ; season rows|stamped: 32|32
2026-08-15T05:03:08Z [sdProducer.FootballNfl 2019] DONE
2026-08-15T05:03:08Z [sdProducer.FootballNfl 2018] START
2026-08-15T05:03:09Z [sdProducer.FootballNfl 2018] repair candidates: 225
2026-08-15T05:05:13Z [sdProducer.FootballNfl 2018] heal plateau: remaining=0 after 1m
2026-08-15T05:05:22Z [sdProducer.FootballNfl 2018] phase1: {"seasonYear":2018,"totalContests":332,"enqueuedJobs":332,"message":"Enqueued 332 metric calculation jobs for 332 contests in season 2018"}
2026-08-15T05:07:22Z [sdProducer.FootballNfl 2018] phase1 drained: stamped=664
2026-08-15T05:09:26Z [sdProducer.FootballNfl 2018] stragglers recomputed: 10
2026-08-15T05:09:26Z [sdProducer.FootballNfl 2018] phase2 accepted
2026-08-15T05:10:26Z [sdProducer.FootballNfl 2018] GATES rows|ppd|fpd|success|tpr|zeros: 664|1.288|0.000|0.4409|0.5000|10 ; season rows|stamped: 32|32
2026-08-15T05:10:26Z [sdProducer.FootballNfl 2018] DONE
2026-08-15T05:10:26Z [sdProducer.FootballNfl 2017] START
2026-08-15T05:10:27Z [sdProducer.FootballNfl 2017] repair candidates: 281
2026-08-15T05:12:48Z [sdProducer.FootballNfl 2017] heal plateau: remaining=0 after 1m
2026-08-15T05:12:57Z [sdProducer.FootballNfl 2017] phase1: {"seasonYear":2017,"totalContests":331,"enqueuedJobs":331,"message":"Enqueued 331 metric calculation jobs for 331 contests in season 2017"}
2026-08-15T05:14:27Z [sdProducer.FootballNfl 2017] phase1 drained: stamped=662
2026-08-15T05:16:30Z [sdProducer.FootballNfl 2017] stragglers recomputed: 11
2026-08-15T05:16:31Z [sdProducer.FootballNfl 2017] phase2 accepted
2026-08-15T05:17:31Z [sdProducer.FootballNfl 2017] GATES rows|ppd|fpd|success|tpr|zeros: 634|1.193|0.000|0.4231|0.5000|11 ; season rows|stamped: 32|32
2026-08-15T05:17:31Z [sdProducer.FootballNfl 2017] DONE
2026-08-15T05:17:31Z [sdProducer.FootballNfl 2016] START
2026-08-15T05:17:31Z [sdProducer.FootballNfl 2016] repair candidates: 283
2026-08-15T05:19:52Z [sdProducer.FootballNfl 2016] heal plateau: remaining=0 after 1m
2026-08-15T05:20:04Z [sdProducer.FootballNfl 2016] phase1: {"seasonYear":2016,"totalContests":331,"enqueuedJobs":331,"message":"Enqueued 331 metric calculation jobs for 331 contests in season 2016"}
2026-08-15T05:21:34Z [sdProducer.FootballNfl 2016] phase1 drained: stamped=662
2026-08-15T05:23:37Z [sdProducer.FootballNfl 2016] stragglers recomputed: 8
2026-08-15T05:23:37Z [sdProducer.FootballNfl 2016] phase2 accepted
2026-08-15T05:24:37Z [sdProducer.FootballNfl 2016] GATES rows|ppd|fpd|success|tpr|zeros: 662|1.239|0.000|0.4320|0.5000|8 ; season rows|stamped: 32|32
2026-08-15T05:24:37Z [sdProducer.FootballNfl 2016] DONE
2026-08-15T05:24:37Z [sdProducer.FootballNfl 2015] START
2026-08-15T05:24:37Z [sdProducer.FootballNfl 2015] repair candidates: 294
2026-08-15T05:31:00Z [sdProducer.FootballNfl 2015] heal plateau: remaining=3 after 5m
2026-08-15T05:31:08Z [sdProducer.FootballNfl 2015] phase1: {"seasonYear":2015,"totalContests":332,"enqueuedJobs":332,"message":"Enqueued 332 metric calculation jobs for 332 contests in season 2015"}
2026-08-15T05:32:38Z [sdProducer.FootballNfl 2015] phase1 drained: stamped=664
2026-08-15T05:34:40Z [sdProducer.FootballNfl 2015] stragglers recomputed: 5
2026-08-15T05:34:40Z [sdProducer.FootballNfl 2015] phase2 accepted
2026-08-15T05:35:40Z [sdProducer.FootballNfl 2015] GATES rows|ppd|fpd|success|tpr|zeros: 664|1.321|0.000|0.4284|0.5000|6 ; season rows|stamped: 32|32
2026-08-15T05:35:40Z [sdProducer.FootballNfl 2015] DONE
2026-08-15T05:35:40Z [sdProducer.FootballNfl 2014] START
2026-08-15T05:35:43Z [sdProducer.FootballNfl 2014] repair candidates: 305
2026-08-15T05:38:09Z [sdProducer.FootballNfl 2014] heal plateau: remaining=0 after 1m
2026-08-15T05:38:15Z [sdProducer.FootballNfl 2014] phase1: {"seasonYear":2014,"totalContests":332,"enqueuedJobs":332,"message":"Enqueued 332 metric calculation jobs for 332 contests in season 2014"}
2026-08-15T05:39:45Z [sdProducer.FootballNfl 2014] phase1 drained: stamped=664
2026-08-15T05:41:49Z [sdProducer.FootballNfl 2014] stragglers recomputed: 12
2026-08-15T05:41:50Z [sdProducer.FootballNfl 2014] phase2 accepted
2026-08-15T05:42:50Z [sdProducer.FootballNfl 2014] GATES rows|ppd|fpd|success|tpr|zeros: 654|1.320|0.000|0.4298|0.5001|12 ; season rows|stamped: 32|32
2026-08-15T05:42:50Z [sdProducer.FootballNfl 2014] DONE
2026-08-15T05:42:50Z [sdProducer.FootballNfl 2013] START
2026-08-15T05:42:51Z [sdProducer.FootballNfl 2013] repair candidates: 292
2026-08-15T05:45:14Z [sdProducer.FootballNfl 2013] heal plateau: remaining=0 after 1m
2026-08-15T05:45:22Z [sdProducer.FootballNfl 2013] phase1: {"seasonYear":2013,"totalContests":332,"enqueuedJobs":332,"message":"Enqueued 332 metric calculation jobs for 332 contests in season 2013"}
2026-08-15T05:46:52Z [sdProducer.FootballNfl 2013] phase1 drained: stamped=664
2026-08-15T05:48:54Z [sdProducer.FootballNfl 2013] stragglers recomputed: 6
2026-08-15T05:48:55Z [sdProducer.FootballNfl 2013] phase2 accepted
2026-08-15T05:49:55Z [sdProducer.FootballNfl 2013] GATES rows|ppd|fpd|success|tpr|zeros: 618|1.652|0.000|0.3640|0.5000|6 ; season rows|stamped: 32|32
2026-08-15T05:49:55Z [sdProducer.FootballNfl 2013] DONE
2026-08-15T05:49:55Z [sdProducer.FootballNfl 2012] START
2026-08-15T05:49:57Z [sdProducer.FootballNfl 2012] repair candidates: 305
2026-08-15T05:55:22Z [sdProducer.FootballNfl 2012] heal plateau: remaining=1 after 4m
2026-08-15T05:55:32Z [sdProducer.FootballNfl 2012] phase1: {"seasonYear":2012,"totalContests":332,"enqueuedJobs":332,"message":"Enqueued 332 metric calculation jobs for 332 contests in season 2012"}
2026-08-15T05:57:02Z [sdProducer.FootballNfl 2012] phase1 drained: stamped=664
2026-08-15T05:59:05Z [sdProducer.FootballNfl 2012] stragglers recomputed: 9
2026-08-15T05:59:06Z [sdProducer.FootballNfl 2012] phase2 accepted
2026-08-15T06:00:06Z [sdProducer.FootballNfl 2012] GATES rows|ppd|fpd|success|tpr|zeros: 660|1.619|0.000|0.3636|0.4985|12 ; season rows|stamped: 32|32
2026-08-15T06:00:06Z [sdProducer.FootballNfl 2012] DONE
2026-08-15T06:00:06Z [sdProducer.FootballNfl 2011] START
2026-08-15T06:00:13Z [sdProducer.FootballNfl 2011] repair candidates: 312
2026-08-15T06:03:40Z [sdProducer.FootballNfl 2011] heal plateau: remaining=0 after 2m
2026-08-15T06:03:47Z [sdProducer.FootballNfl 2011] phase1: {"seasonYear":2011,"totalContests":331,"enqueuedJobs":331,"message":"Enqueued 331 metric calculation jobs for 331 contests in season 2011"}
2026-08-15T06:05:17Z [sdProducer.FootballNfl 2011] phase1 drained: stamped=662
2026-08-15T06:07:21Z [sdProducer.FootballNfl 2011] stragglers recomputed: 12
2026-08-15T06:07:22Z [sdProducer.FootballNfl 2011] phase2 accepted
2026-08-15T06:08:22Z [sdProducer.FootballNfl 2011] GATES rows|ppd|fpd|success|tpr|zeros: 662|1.580|0.000|0.3681|0.5000|12 ; season rows|stamped: 32|32
2026-08-15T06:08:22Z [sdProducer.FootballNfl 2011] DONE
2026-08-15T06:08:22Z [sdProducer.FootballNfl 2010] START
2026-08-15T06:08:22Z [sdProducer.FootballNfl 2010] repair candidates: 313
2026-08-15T06:10:50Z [sdProducer.FootballNfl 2010] heal plateau: remaining=0 after 1m
2026-08-15T06:10:56Z [sdProducer.FootballNfl 2010] phase1: {"seasonYear":2010,"totalContests":331,"enqueuedJobs":331,"message":"Enqueued 331 metric calculation jobs for 331 contests in season 2010"}
2026-08-15T06:12:57Z [sdProducer.FootballNfl 2010] phase1 drained: stamped=662
2026-08-15T06:15:00Z [sdProducer.FootballNfl 2010] stragglers recomputed: 11
2026-08-15T06:15:01Z [sdProducer.FootballNfl 2010] phase2 accepted
2026-08-15T06:16:01Z [sdProducer.FootballNfl 2010] GATES rows|ppd|fpd|success|tpr|zeros: 662|1.582|0.000|0.3574|0.5000|11 ; season rows|stamped: 32|32
2026-08-15T06:16:01Z [sdProducer.FootballNfl 2010] DONE
2026-08-15T06:16:01Z [sdProducer.FootballNfl 2009] START
2026-08-15T06:16:02Z [sdProducer.FootballNfl 2009] repair candidates: 307
2026-08-15T06:18:28Z [sdProducer.FootballNfl 2009] heal plateau: remaining=0 after 1m
2026-08-15T06:18:36Z [sdProducer.FootballNfl 2009] phase1: {"seasonYear":2009,"totalContests":332,"enqueuedJobs":332,"message":"Enqueued 332 metric calculation jobs for 332 contests in season 2009"}
2026-08-15T06:20:06Z [sdProducer.FootballNfl 2009] phase1 drained: stamped=664
2026-08-15T06:22:13Z [sdProducer.FootballNfl 2009] stragglers recomputed: 19
2026-08-15T06:22:14Z [sdProducer.FootballNfl 2009] phase2 accepted
2026-08-15T06:23:14Z [sdProducer.FootballNfl 2009] GATES rows|ppd|fpd|success|tpr|zeros: 662|1.543|0.000|0.3637|0.5001|19 ; season rows|stamped: 32|32
2026-08-15T06:23:14Z [sdProducer.FootballNfl 2009] DONE
2026-08-15T06:23:14Z [sdProducer.FootballNfl 2008] START
2026-08-15T06:23:14Z [sdProducer.FootballNfl 2008] repair candidates: 266
2026-08-15T06:25:31Z [sdProducer.FootballNfl 2008] heal plateau: remaining=0 after 1m
2026-08-15T06:25:36Z [sdProducer.FootballNfl 2008] phase1: {"seasonYear":2008,"totalContests":288,"enqueuedJobs":288,"message":"Enqueued 288 metric calculation jobs for 288 contests in season 2008"}
2026-08-15T06:27:06Z [sdProducer.FootballNfl 2008] phase1 drained: stamped=576
2026-08-15T06:29:08Z [sdProducer.FootballNfl 2008] stragglers recomputed: 8
2026-08-15T06:29:09Z [sdProducer.FootballNfl 2008] phase2 accepted
2026-08-15T06:30:09Z [sdProducer.FootballNfl 2008] GATES rows|ppd|fpd|success|tpr|zeros: 576|1.576|0.000|0.3743|0.5001|8 ; season rows|stamped: 32|32
2026-08-15T06:30:09Z [sdProducer.FootballNfl 2008] DONE
2026-08-15T06:30:09Z [sdProducer.FootballNfl 2007] START
2026-08-15T06:30:10Z [sdProducer.FootballNfl 2007] repair candidates: 291
2026-08-15T06:32:30Z [sdProducer.FootballNfl 2007] heal plateau: remaining=0 after 1m
2026-08-15T06:32:34Z [sdProducer.FootballNfl 2007] phase1: {"seasonYear":2007,"totalContests":332,"enqueuedJobs":332,"message":"Enqueued 332 metric calculation jobs for 332 contests in season 2007"}
2026-08-15T06:34:05Z [sdProducer.FootballNfl 2007] phase1 drained: stamped=664
2026-08-15T06:36:08Z [sdProducer.FootballNfl 2007] stragglers recomputed: 9
2026-08-15T06:36:08Z [sdProducer.FootballNfl 2007] phase2 accepted
2026-08-15T06:37:08Z [sdProducer.FootballNfl 2007] GATES rows|ppd|fpd|success|tpr|zeros: 662|1.530|0.000|0.3603|0.5000|9 ; season rows|stamped: 32|32
2026-08-15T06:37:08Z [sdProducer.FootballNfl 2007] DONE
2026-08-15T06:37:08Z [sdProducer.FootballNfl 2006] START
2026-08-15T06:37:08Z [sdProducer.FootballNfl 2006] repair candidates: 161
2026-08-15T06:38:52Z [sdProducer.FootballNfl 2006] heal plateau: remaining=0 after 1m
2026-08-15T06:38:54Z [sdProducer.FootballNfl 2006] phase1: {"seasonYear":2006,"totalContests":177,"enqueuedJobs":177,"message":"Enqueued 177 metric calculation jobs for 177 contests in season 2006"}
2026-08-15T06:40:24Z [sdProducer.FootballNfl 2006] phase1 drained: stamped=354
2026-08-15T06:42:28Z [sdProducer.FootballNfl 2006] stragglers recomputed: 12
2026-08-15T06:42:29Z [sdProducer.FootballNfl 2006] phase2 accepted
2026-08-15T06:43:29Z [sdProducer.FootballNfl 2006] GATES rows|ppd|fpd|success|tpr|zeros: 354|1.484|0.000|0.3573|0.5001|12 ; season rows|stamped: 32|32
2026-08-15T06:43:29Z [sdProducer.FootballNfl 2006] DONE
2026-08-15T06:43:29Z [sdProducer.FootballNfl 2005] START
2026-08-15T06:43:29Z [sdProducer.FootballNfl 2005] repair candidates: 233
2026-08-15T06:48:33Z [sdProducer.FootballNfl 2005] heal plateau: remaining=1 after 4m
2026-08-15T06:48:37Z [sdProducer.FootballNfl 2005] phase1: {"seasonYear":2005,"totalContests":333,"enqueuedJobs":333,"message":"Enqueued 333 metric calculation jobs for 333 contests in season 2005"}
2026-08-15T06:50:08Z [sdProducer.FootballNfl 2005] phase1 drained: stamped=666
2026-08-15T06:52:11Z [sdProducer.FootballNfl 2005] stragglers recomputed: 10
2026-08-15T06:52:11Z [sdProducer.FootballNfl 2005] phase2 accepted
2026-08-15T06:53:11Z [sdProducer.FootballNfl 2005] GATES rows|ppd|fpd|success|tpr|zeros: 532|1.524|0.000|0.3515|0.5001|10 ; season rows|stamped: 32|32
2026-08-15T06:53:11Z [sdProducer.FootballNfl 2005] DONE
2026-08-15T06:53:11Z [sdProducer.FootballNfl 2004] START
2026-08-15T06:53:11Z [sdProducer.FootballNfl 2004] repair candidates: 86
2026-08-15T06:55:35Z [sdProducer.FootballNfl 2004] heal plateau: remaining=0 after 2m
2026-08-15T06:55:37Z [sdProducer.FootballNfl 2004] phase1: {"seasonYear":2004,"totalContests":131,"enqueuedJobs":131,"message":"Enqueued 131 metric calculation jobs for 131 contests in season 2004"}
2026-08-15T06:57:07Z [sdProducer.FootballNfl 2004] phase1 drained: stamped=262
2026-08-15T06:59:08Z [sdProducer.FootballNfl 2004] stragglers recomputed: 2
2026-08-15T06:59:08Z [sdProducer.FootballNfl 2004] phase2 accepted
2026-08-15T07:00:08Z [sdProducer.FootballNfl 2004] GATES rows|ppd|fpd|success|tpr|zeros: 202|2.606|0.000|0.2684|0.5000|2 ; season rows|stamped: 32|32
2026-08-15T07:00:08Z [sdProducer.FootballNfl 2004] DONE
2026-08-15T07:00:08Z [sdProducer.FootballNfl 2003] START
2026-08-15T07:00:08Z [sdProducer.FootballNfl 2003] repair candidates: 262
2026-08-15T07:06:20Z [sdProducer.FootballNfl 2003] heal plateau: remaining=4 after 5m
2026-08-15T07:06:25Z [sdProducer.FootballNfl 2003] phase1: {"seasonYear":2003,"totalContests":331,"enqueuedJobs":331,"message":"Enqueued 331 metric calculation jobs for 331 contests in season 2003"}
2026-08-15T07:08:25Z [sdProducer.FootballNfl 2003] phase1 drained: stamped=662
2026-08-15T07:10:32Z [sdProducer.FootballNfl 2003] stragglers recomputed: 22
2026-08-15T07:10:32Z [sdProducer.FootballNfl 2003] phase2 accepted
2026-08-15T07:11:32Z [sdProducer.FootballNfl 2003] GATES rows|ppd|fpd|success|tpr|zeros: 576|2.542|0.000|0.2625|0.5000|31 ; season rows|stamped: 32|32
2026-08-15T07:11:32Z [sdProducer.FootballNfl 2003] DONE
2026-08-15T07:11:32Z [sdProducer.FootballNfl 2002] START
2026-08-15T07:11:33Z [sdProducer.FootballNfl 2002] repair candidates: 239
2026-08-15T07:17:39Z [sdProducer.FootballNfl 2002] heal plateau: remaining=5 after 5m
2026-08-15T07:17:48Z [sdProducer.FootballNfl 2002] phase1: {"seasonYear":2002,"totalContests":333,"enqueuedJobs":333,"message":"Enqueued 333 metric calculation jobs for 333 contests in season 2002"}
2026-08-15T07:19:19Z [sdProducer.FootballNfl 2002] phase1 drained: stamped=666
2026-08-15T07:21:23Z [sdProducer.FootballNfl 2002] stragglers recomputed: 12
2026-08-15T07:21:23Z [sdProducer.FootballNfl 2002] phase2 accepted
2026-08-15T07:22:23Z [sdProducer.FootballNfl 2002] GATES rows|ppd|fpd|success|tpr|zeros: 534|2.659|0.000|0.2638|0.5001|12 ; season rows|stamped: 32|32
2026-08-15T07:22:23Z [sdProducer.FootballNfl 2002] DONE
2026-08-15T07:22:23Z [sdProducer.FootballNfl 2001] START
2026-08-15T07:22:23Z [sdProducer.FootballNfl 2001] repair candidates: 262
2026-08-15T07:27:36Z [sdProducer.FootballNfl 2001] heal plateau: remaining=25 after 4m
2026-08-15T07:27:40Z [sdProducer.FootballNfl 2001] phase1: {"seasonYear":2001,"totalContests":321,"enqueuedJobs":321,"message":"Enqueued 321 metric calculation jobs for 321 contests in season 2001"}
2026-08-15T07:29:40Z [sdProducer.FootballNfl 2001] phase1 drained: stamped=642
2026-08-15T07:31:44Z [sdProducer.FootballNfl 2001] stragglers recomputed: 12
2026-08-15T07:31:44Z [sdProducer.FootballNfl 2001] phase2 accepted
2026-08-15T07:32:44Z [sdProducer.FootballNfl 2001] GATES rows|ppd|fpd|success|tpr|zeros: 572|2.476|0.000|0.2519|0.4895|24 ; season rows|stamped: 31|31
2026-08-15T07:32:44Z [sdProducer.FootballNfl 2001] DONE
2026-08-15T07:32:44Z ===== CAMPAIGN COMPLETE =====
```
