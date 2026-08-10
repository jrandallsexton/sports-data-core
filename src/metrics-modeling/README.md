# MetricBot — deetsMeter prediction pipeline

Season-launch MVP Phase A (design:
`docs/metrics-modeling/metrics-microservice-deetsmeter.md`). One command
replaces the old `Generate-Predictions.ps1` + inter-stage CSVs + manual
Postman POST.

All commands below assume you are in this directory
(`C:\Projects\sports-data\src\metrics-modeling`) and use the venv's
python directly — no activation needed:

```powershell
cd C:\Projects\sports-data\src\metrics-modeling
.venv\Scripts\python.exe -m metricbot run-week --help
```

## Cookbook

```powershell
# ── LIVE MODE (the weekly production run) ────────────────────────────
# Live mode auto-resolves NOW -> (season, week) — current open window or
# the next upcoming week — and uses the SAME as-of extraction as
# experiments (entering-week features, leak-free training).

# Safest first contact: full pipeline, no POST, artifacts written to .\data
.venv\Scripts\python.exe -m metricbot run-week --sport FootballNcaa --dry-run --dump-intermediate

# The real Tuesday-night run: predict the current NCAAFB week and publish.
# In weeks 1-2 (little/no current-season data) add the tail or the slate
# will be empty/thin:
.venv\Scripts\python.exe -m metricbot run-week --sport FootballNcaa --prior-season-tail 5

# Mid-season, once teams have games, the tail is optional:
.venv\Scripts\python.exe -m metricbot run-week --sport FootballNcaa

# Same for the NFL week (its own database, same model)
.venv\Scripts\python.exe -m metricbot run-week --sport FootballNfl --prior-season-tail 5

# Something looks off? Re-run with artifacts + debug logging
.venv\Scripts\python.exe -m metricbot run-week --sport FootballNcaa --dry-run --dump-intermediate --verbose

# ── AS-OF MODE (backtests / experiments — NEVER posts without --publish) ─

# Backtest 2025 week 6 exactly as the model would have seen it
.venv\Scripts\python.exe -m metricbot run-week --sport FootballNcaa --season-year 2025 --week 6 --dump-intermediate

# Early-season experiment: week 2 with prior-season tail on...
.venv\Scripts\python.exe -m metricbot run-week --sport FootballNcaa --season-year 2025 --week 2 --prior-season-tail 5 --dump-intermediate

# ...and the same week with it off, to compare
.venv\Scripts\python.exe -m metricbot run-week --sport FootballNcaa --season-year 2025 --week 2 --dump-intermediate

# NFL backtest
.venv\Scripts\python.exe -m metricbot run-week --sport FootballNfl --season-year 2025 --week 10 --dump-intermediate

# Sweep a season's weeks (PowerShell loop; artifacts pile up in .\data)
4..14 | ForEach-Object {
  .venv\Scripts\python.exe -m metricbot run-week --sport FootballNcaa --season-year 2025 --week $_ --dump-intermediate
}

# Deliberately publish an as-of run's predictions (rare; know why first)
.venv\Scripts\python.exe -m metricbot run-week --sport FootballNcaa --season-year 2025 --week 6 --publish

# ── BACKTEST (predict as-of + grade against final scores; never posts) ─

# Grade a single historical week
.venv\Scripts\python.exe -m metricbot backtest --sport FootballNcaa --season-year 2025 --week 6 --prior-season-tail 5

# The tail experiment, graded: same week with and without
.venv\Scripts\python.exe -m metricbot backtest --sport FootballNcaa --season-year 2025 --week 2 --prior-season-tail 5
.venv\Scripts\python.exe -m metricbot backtest --sport FootballNcaa --season-year 2025 --week 2

# Sweep and grade a season
4..14 | ForEach-Object {
  .venv\Scripts\python.exe -m metricbot backtest --sport FootballNcaa --season-year 2025 --week $_ --prior-season-tail 5
}
```

The grade report covers: SU accuracy vs always-home and
favorite-by-spread baselines; ATS accuracy vs the 52.4% break-even
(pushes excluded and counted); out-of-sample margin MAE/RMSE for the
model AND for the spread itself on the same games (model vs market —
the honest head-to-head); Brier scores; calibration deciles
(predicted 70% should win ~70%). O/U is absent by design — the model
predicts margin, not totals. In prod, the same report comes from
`POST /admin/metricbot/backtest` (see Deployment below).

Flag notes:

- `--dry-run` — everything except the POST; inspect logs/DTO counts first.
- `--dump-intermediate` — writes CSV/JSON artifacts to `.\data`
  (gitignored — contains prod-derived data). As-of artifacts are named
  `*_FootballNcaa_asof_2025_wk6_tail5.*`; live ones
  `*_FootballNcaa_live_2026_wk1_tail5.*`.
- Live mode has no week flag: `sql/detect_current_season_week.sql`
  resolves NOW to (season, week) — the open window, or the next upcoming
  non-preseason week.
- `--legacy-extraction` runs the original prototype SQL (live
  FranchiseSeasonMetric joins + leaky training) for parity comparison
  only. It cannot run before metrics exist for the season.

## As-of mode semantics (Option B, full as-of — decided 2026-08-09)

Predicts a historical week using ONLY information available entering it:

- **Slate features**: entering-week aggregates computed from per-game
  `CompetitionMetric` rows (weeks < N), formula-parity with the live
  `ComputeFranchiseSeasonMetric` (including its SafeAvg→0 quirk).
- **Training set**: all completed games strictly before
  (season-year, week) — prior seasons fully, target season weeks < N,
  later seasons never. The 12 Pts/Margin columns are ENTERING-GAME
  windows per team (the live flow leaks these from the current
  FranchiseSeason row — a known finding this mode corrects).
- **Preseason excluded everywhere** (system-testing data, never signal).
- `--prior-season-tail N`: tops up thin early-week windows with the
  team's most recent N prior-season (regular/post) games — an
  experiment axis; run early weeks with and without it and compare.
- **As-of runs NEVER post** to the predictions endpoint unless
  `--publish` is passed — a backtest must not touch live deetsMeter
  data.

Expect as-of accuracy to differ from the prototype's 56–62% backtests:
those numbers were computed with leaky features. Lower-but-honest is
the correct outcome, not a regression.

## Setup

```powershell
python -m venv .venv
.venv\Scripts\pip install -r requirements.txt
```

`psql` must be on PATH (extraction shells out to it — no Python DB
driver by design; the Phase B container installs postgresql-client).

Tests (offline — no DB, no network):

```powershell
.venv\Scripts\pip install -r requirements-dev.txt
.venv\Scripts\python.exe -m pytest tests\ -q
```

## Configuration

Environment variables, or a `_metricbot.env` (plain KEY=VALUE lines;
leading underscore per the secrets-file convention; quotes around values
optional) placed BESIDE the existing `_common-variables.ps1` —
`SPORTDEETS_SECRETS_PATH` may point at that file or its directory;
either works. Never committed, repo is public:

| Variable | Purpose |
|---|---|
| `METRICBOT_PG_HOST` / `METRICBOT_PG_PORT` | Producer Postgres (port defaults 5432) |
| `METRICBOT_PG_USER` / `METRICBOT_PG_PASSWORD` | credentials |
| `METRICBOT_API_BASE_URL` | API base — same base the web app's API client uses; the CLI appends `/admin/ai-predictions/{userId}` |
| `METRICBOT_ADMIN_TOKEN` | X-Admin-Token for the ingestion endpoint |
| `METRICBOT_USER_ID` | optional GUID; defaults to the MetricBot synthetic user (`b210d677-…`) |

The sport flag takes the platform's Sport enum name (case-insensitive)
and picks the matching database — deliberately ONE vocabulary across
C# and Python, no translation layer.

## Layout

- `metricbot/` — the pipeline package (extract → model → dtos → api →
  pipeline). Math is ported VERBATIM from the prototype scripts; any
  intentional math change bumps `MODEL_VERSION` in `__init__.py`.
- `sql/` — extraction queries. The two `competition_metrics_*.sql` files
  are unchanged from the prototype (the spec); the two `*_asof_*.sql`
  files take `psql -v` variables (a client-runnable copy of the as-of
  week query with inlined params lives at
  `sql/pgsql/competition_metrics_asof_week.sql` in the repo root).
- Legacy prototype scripts (`predict_*.py`, `Generate-Predictions.ps1`,
  etc.) remain untouched for output-parity comparison; delete them once
  the CLI has produced matching output for a real week.

## Verification (first run)

1. `.venv\Scripts\python.exe -m metricbot run-week --sport FootballNcaa --season-year 2026 --week 1 --prior-season-tail 5 --dump-intermediate`
   (runs pre-season: with the tail flag, week-1 features come from the
   prior season's final games, so no current-season data is required)
2. Parity (mid-season, once 2026 metrics exist): run with and without
   `--legacy-extraction` for the same week and compare predictions —
   entering-week aggregates should match the live FranchiseSeasonMetric
   values; training deltas reflect the leak fixes, documented in the
   design doc.
3. Live publish when happy: `run-week --sport FootballNcaa --prior-season-tail 5`.
4. First as-of sanity check: pick a team you know in
   `data\current_FootballNcaa_asof_2025_wk6_tail0.csv` — its feature values
   should reflect ONLY weeks 1–5 (they should NOT match the live
   `FranchiseSeasonMetric` row, which includes the full season).

   .venv\Scripts\python.exe -m metricbot run-week --sport FootballNfl --season-year 2026 --week 1 --prior-season-tail 5 --dump-intermediate

## Deployment (Phase B)

MetricBot runs as an **internal** service — no public ingress, same
posture as Producer/Provider. The API's **Hangfire** owns scheduling and
manual triggering; MetricBot is a stateless executor.

```
Hangfire (API pod)                    MetricBot (this service)
  MetricBotWeekly-FootballNcaa  ──┐
    cron: Tue 03:00 UTC           │   POST /run-week  →  extract → train
  MetricBotWeekly-FootballNfl   ──┤                       → predict → POST
    cron: Wed 03:00 UTC           │                         predictions back
                                  │                         to the API
POST /admin/metricbot/run-week  ──┘   (admin-token gated proxy; the
  {sport, seasonYear?, week?,          on-demand + experiment entry point)
   priorSeasonTail, publish,
   includeDtos}
```

Why Hangfire instead of a K8s CronJob: the jobs dashboard already gives
manual re-triggering, run history, and failure visibility — and a
CronJob cannot take parameters, which is the entire experiment
workflow.

### Image

```powershell
# from the repo root (build context = repo root)
docker build -f src/metrics-modeling/Dockerfile -t sportsdatametricbot:local .
docker run --rm -p 8080:8080 --env-file <your env file> sportsdatametricbot:local
curl http://localhost:8080/health
```

### Cluster

- Manifests: `app/base/apps/metricbot/` in **sports-data-config**;
  prod overlay patch `app/overlays/04_prod/metricbot-patch.yaml`.
- Secret `metricbot-secrets` (namespace `default`) with keys:
  `pg-host`, `pg-port`, `pg-user`, `pg-password`, `api-base-url`,
  `admin-token`.
- API config key `CommonConfig:MetricBot:BaseUrl` →
  `http://metricbot:8080/` (defaults to that if unset).
- Deploy workflow input: `metricbot_tag`.

### Ad-hoc / experiment runs in prod

```
POST /admin/metricbot/run-week      (X-Admin-Token)
{ "sport": "FootballNcaa", "seasonYear": 2025, "week": 6,
  "priorSeasonTail": 5, "includeDtos": true }
```

Explicit `seasonYear`/`week` never publishes unless `"publish": true` —
the same guard as the CLI. Omit both for a live run of the current week.
