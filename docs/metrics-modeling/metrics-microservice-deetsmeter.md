# Metrics Microservice (deetsMeter) — Design

**Status:** design / not yet implemented
**Owners:** Randall
**Target test bed:** MLB inference, with the explicit caveat that MLB accuracy is not a priority — the goal is to prove the *plumbing*, not the model quality

## TL;DR

The current `src/metrics-modeling/` tree is prototype Python scripts orchestrated by `Generate-Predictions.ps1`. It produces a JSON DTO of `(ContestId, WinnerFranchiseSeasonId, WinProbability, PredictionType)` rows and the operator manually POSTs them to `/api/admin/ai-predictions/{MetricBot-user-id}` weekly. The deetsMeter UI consumes those predictions.

Moving this to a cluster-hosted microservice is genuine new construction. The interesting decisions, in order of how much they shape everything else:

1. **Language/framework** — Python (FastAPI, keep existing code) vs C# (rewrite, integrate with existing service patterns) vs ML.NET (different cost shape)
2. **Inference shape** — batch (current pattern, cheap and fine) vs live HTTP (per-contest, much more service-y) vs hybrid
3. **DB access boundary** — direct Postgres read (current pattern, fast, violates the API/Producer rule) vs API HTTP calls (matches existing service patterns, slower for training)
4. **Training cadence and model artifact storage** — refit weekly? on demand? where does the trained model live?
5. **Per-sport models** — football and MLB share approximately zero features

Estimated effort: 3–4 sprints minimum, mostly because there's genuine new code (the AI provider routing work by contrast is mostly a wiring change).

> **Season-launch reality check.** The current operator-driven process is unsustainable through a 13-week NCAAFB + parallel NFL season — the operator already gets confused week-to-week about whether stats generated correctly, which scripts run in what order, which CSVs go where. Failure modes are silent. Before NCAAFB kickoff (Aug 28, 2026), a stripped-down MVP needs to ship that eliminates manual orchestration. See [Season-launch MVP (2026)](#season-launch-mvp-2026) — the full design below remains the long-term target, not the August deliverable.

---

## Season-launch MVP (2026)

The full architecture later in this doc is the **long-term** target. This section is the **near-term** scope that must land before NCAAFB kickoff (Aug 28, 2026).

**MVP goal:** eliminate manual operator orchestration. Do not build the full microservice. Do not solve the service-boundary purity question. Do not stand up FastAPI. Make Tuesday night happen on its own.

Two phases, both within the existing `src/metrics-modeling/` tree (no new service skeleton):

### MVP Phase A — Consolidate-the-prototype (1–2 weeks)

Replace the PowerShell + inter-stage CSVs + Postman handoff with a single Python entry point.

- One CLI: `python -m metricbot run-week` (or similar)
- Internally: detect current week → query Postgres → train → predict SU + ATS → POST results to `/api/admin/ai-predictions/{MetricBot-user-id}` directly via HTTP
- Inter-stage state stays in memory on the happy path; keep a `--dump-intermediate` flag that writes CSVs for debugging when something looks wrong
- Replace `Generate-Predictions.ps1` and the operator's Postman step
- Real `requirements.txt` (or `pyproject.toml`) so the venv is reproducible
- Run from any host with Postgres + API reachability — Bender, dev box, dedicated runner. Placement decision deferred to Phase B.

What this earns you: when something is wrong, it's wrong in one place with one log stream. The "did I run them in the right order? did I generate the stats correctly first?" confusion goes away.

### MVP Phase B — Containerize + schedule (2–3 weeks)

Drop the Phase A CLI into a Docker image and run it as a K3s CronJob.

- Container image built from the `src/metrics-modeling/` tree
- K3s CronJob: schedule for the right pre-slate-lock window per sport (Tuesday night for NCAAFB-week, Wednesday for NFL-week — adjust per league)
- Secrets via existing AppConfig + Kubernetes secret patterns (Postgres connection, API base URL, MetricBot service account credentials)
- Job emits structured logs through the existing OTel + Seq pipeline; failure path raises a visible alert
- Operator's weekly involvement: zero on the happy path; investigate-and-rerun on alerts

What this earns you: operational sustainability through the season. The job runs whether or not the operator remembers it's Tuesday.

### What MVP explicitly does NOT do

These are full-architecture concerns deferred to Q4 / post-season:

- **No Producer-side metrics API.** The MVP keeps reading Postgres directly. The CLAUDE.md "API never hits Producer's DB directly" boundary is technically violated by the MVP path. Building a proper Producer endpoint right now would compete with season-launch work for the same scarce time. Revisit Q4.
- **No FastAPI / live inference endpoint.** Current consumer pattern is batch; no live re-scoring on metric refresh.
- **No artifact storage / model versioning.** Phase A's CLI refits on every run, in process. Acceptable at the operator's volume.
- **No MLB coverage at MVP scope.** Football models port forward as-is. MLB is plumbing-test on the AI side (separate doc) and gets a metrics model in Q4 at the earliest, *not* before NCAAFB. MetricBot has no MLB predictions during the 2026 MLB season. Acceptable.
- **No new `SportsData.MetricBot` service tree.** MVP work lives in `src/metrics-modeling/`. The Phase 2 service skeleton in the full design below is a Q4 move.

### Why this is the right scope

The full design's value proposition is "honor the service boundary + per-sport architectural extensibility." The MVP's value proposition is "the operator stops manually orchestrating during a 22-week sports season." These don't conflict — the MVP is a strict subset of the full design, scoped to the one outcome that's actually season-blocking.

If MVP Phase A alone ships and Phase B slips into September, that's still a substantial sustainability win over the current state — manual orchestration drops from 5+ steps to 1.

---

## Current state

### What the Python tree actually is

```
src/metrics-modeling/
├── sql/
│   ├── competition_metrics_current_week.sql
│   └── competition_metrics_training.sql
├── data/                                  (runtime CSV/JSON outputs)
├── Generate-Predictions.ps1               (PowerShell orchestrator)
├── train_model.py                         (RandomForestClassifier for SU)
├── predict_straightup.py                  (LinearRegression margin → P(win))
├── predict_ats.py                         (P(margin + spread > 0))
├── predict_week11.py                      (week-specific logistic regression)
├── generate_contest_prediction_dtos.py    (JSON export to API)
├── rolling_predict_metrics_spread_logreg.py  (backtest harness)
├── combine_csv.py                         (training + current-week CSV merge)
└── [debug/analysis scripts]
```

There is no `requirements.txt` / `pyproject.toml` at the root — only the `.venv/`. Credentials come from a shell env var (`SPORTDEETS_SECRETS_PATH`) that points at a file with prod DB credentials.

### The end-to-end pattern today

1. Operator runs `Generate-Predictions.ps1` on their workstation, typically once a week
2. The script:
   - Auto-detects the current `SeasonWeek` from Postgres
   - Runs the two `.sql` queries to extract training + current-week metrics into CSV
   - Invokes the Python scripts in sequence (combine → train → predict SU → predict ATS → emit DTO)
3. Output: `data/contest_predictions.json` with rows of `(ContestId, WinnerFranchiseSeasonId, WinProbability, PredictionType)`
4. Operator opens Postman, POSTs the JSON to `/api/admin/ai-predictions/{MetricBot-user-id}`
5. API persists the predictions under the synthetic MetricBot user
6. The deetsMeter component (`src/UI/sd-ui/src/components/matchups/DeetsMeter.jsx`) reads them via the matchup query and renders a confidence indicator

### What deetsMeter actually computes

For each contest:

1. Pull ~40 per-team metrics from Producer Postgres for both teams (offensive: Ypp, SuccessRate, ExplosiveRate, PointsPerDrive, RZ efficiency, etc.; defensive equivalents; special teams; season aggregates; betting line)
2. Train `LinearRegression` on completed games — target is point margin (home − away)
3. Predict each unfinished game's expected margin
4. Convert margin to win probability via the normal distribution tail: `P(win) = P(margin > 0)`
5. Adjust for the betting spread: `P(cover) = P(margin + spread > 0)`
6. Emit `(ContestId, predicted winner, probability, prediction type)`

Backtest accuracy as reported by `rolling_predict_metrics_spread_logreg.py`: **56–62%** week-over-week. Straight linear regression on raw features, no cross-validation, no engineered features, no ensembling. This is a working baseline, not state of the art.

### Maturity honest reckoning

Prototype scripts that work in a single operator's hands on a single workstation. CSV files as state transfer between stages. Hardcoded paths. Manual week-number injection via PowerShell string-replace. No tests. No structured logging. No error handling. No containerization. No model artifact storage — `LinearRegression` refits every run, in-memory only.

This is not a small refactor away from being a microservice. It's a rewrite where the existing scripts serve as the spec.

### MetricBot identity

The API treats the metrics output as if it came from a synthetic user. `IsSynthetic = true` on the `User` row. The POST endpoint authenticates as this user. Service-account pattern. Keep.

---

## Target architecture

The shape that follows from the decisions below. **None of this is final** — see the decisions section.

### Service boundary

`SportsData.MetricBot` — a new top-level service in `src/`, peer to `SportsData.Producer` and `SportsData.Api`. Responsibilities:

- Maintain trained per-sport prediction models
- Refit models on a defined cadence (training pipeline)
- Serve predictions (inference pipeline)
- Publish prediction events into the existing MassTransit/RabbitMQ bus when predictions land

Does *not* own:

- The DTO schema (lives in `SportsData.Core` or `SportsData.Api`)
- The persistence of accepted predictions (`SportsData.Api` continues to own that via the existing `/api/admin/ai-predictions/{userId}` endpoint, or its successor)
- The deetsMeter UI rendering (unchanged)

### Suggested entry points

For batch (sketch):

```
POST /train/{sport}            → retrain a sport's model on latest data
POST /predict/week/{sport}/{seasonYear}/{seasonWeek}
                               → emit predictions for the named week
GET  /models/{sport}           → metadata about the deployed model
                                  (version, training date, backtest accuracy)
```

For live, if Decision 2 lands on a hybrid:

```
POST /predict/contest/{contestId}
                               → on-demand single-contest inference
```

---

## Decisions to make

### Decision 1 — Language / framework

| Option | Pros | Cons |
|---|---|---|
| **Python + FastAPI** | Keeps the existing code investment; pandas/scikit-learn/scipy are the natural tools for this work; small services in K3s are easy | Adds a Python runtime to the cluster; team is otherwise .NET-first; observability story is different (need to wire OpenTelemetry into Python explicitly) |
| **C# + ML.NET** | Single runtime, single tooling chain, matches existing services; OTel + Seq + AppConfig come for free | Throws away the Python code; ML.NET has a steeper feature-engineering learning curve than scikit-learn; less idiomatic for fast iteration on model shape |
| **C# orchestration + Python "computation core" as a sidecar** | Best of both — C# owns HTTP + DI + AppConfig, Python owns the math | Two languages, two deployment artifacts, two dependency stories; more moving parts than either pure option |

**Recommendation:** Python + FastAPI. The metrics work is genuinely a data-science workload and the team's investment in scikit-learn-style models is real. The "adds a Python runtime" concern is real but bounded — one service, one Dockerfile, one Helm release. ML.NET is a worse fit for "I want to try a different feature set" iteration cycles, which this work demands.

Wire OpenTelemetry into the FastAPI app via `opentelemetry-instrumentation-fastapi` so the cluster's observability story includes it.

### Decision 2 — Inference shape

| Option | Description | Fits |
|---|---|---|
| **Pure batch** | Service exposes `POST /predict/week/...`; weekly cron or Hangfire-triggered call regenerates the whole week's predictions | Matches current workflow; cheap; predictable load; doesn't handle late-arriving spreads well |
| **Pure live** | Service exposes `POST /predict/contest/{contestId}`; API calls it on-demand | Cleanest data model; predictions always reflect latest metrics + odds; load profile is unpredictable; cold-start matters |
| **Hybrid** | Batch precomputes on contest creation + week roll-over; live recomputes on metric refresh / odds change | Best UX; most plumbing |

**Recommendation:** start with **pure batch**. The current operator workflow is batch and the deetsMeter UX is set up around weekly predictions. Pure live introduces an inference-latency cost on the hot path (matchup card rendering) for a feature that's currently weekly. Add a live `POST /predict/contest/{contestId}` endpoint later if odds-movement requires it.

This is a "don't build hybrid speculatively" decision per the project conventions.

### Decision 3 — DB access boundary

CLAUDE.md explicitly mandates: **"API never hits Producer's DB directly."** The existing pattern is HTTP-based typed clients between services.

The Python prototype today reads Producer's Postgres directly via raw SQL. Convenient for batch training (~40 features × thousands of games × multiple seasons) but it's the same boundary violation the rest of the system is structured to avoid.

| Option | Pros | Cons |
|---|---|---|
| **Direct Postgres read** | Fast; matches current code; trivial to migrate | Two services own the schema, you've leaked the boundary, schema changes in Producer can silently break MetricBot |
| **API HTTP calls** | Honors the boundary; uniform service-to-service pattern | Aggregate-over-many-contests gets slow if API doesn't expose batched endpoints — would need a `GET /metrics/batch?sport=...&seasonYear=...` |
| **Producer-side HTTP API for metrics** | The "right" boundary — Producer owns its data, MetricBot reads via Producer's HTTP surface | Producer doesn't have those endpoints today; would need to be built; matches existing per-aggregate-root client pattern (`SeasonClient`, `FranchiseClient`, etc.) |

**Recommendation:** **Producer-side HTTP API for metrics**, accepting that this is a separate small piece of Producer work that has to land first. Direct Postgres read is genuinely easier today but it's exactly the kind of "we'll fix it later" that turns into a four-service-deep coupling problem in two years. Match the pattern.

Open question: does the existing `FranchiseClient` already expose enough to assemble the feature set, or does it need a dedicated metrics endpoint? Worth a quick audit before the decision is locked.

### Decision 4 — Training cadence + model artifact storage

Two intertwined questions: how often does the model refit, and where do the trained models live?

**Cadence options:**

- Weekly, automatically, on a fixed day before the slate locks
- On demand via API trigger (operator decides)
- On every prediction request (refit each time — what the prototype does today)
- A mix: scheduled weekly refit + admin override

**Storage options:**

- Container-local file system, regenerate on every container restart (current prototype's behavior)
- Object storage (e.g. MinIO in-cluster, or filesystem PVC) — model artifacts as serialized `joblib` blobs keyed by `(sport, training_date, version)`
- Postgres BLOB — works but Postgres isn't a great large-blob store
- Git LFS — fine for occasional manual updates, wrong for automated refits

**Recommendation:** scheduled weekly refit + admin-trigger override; MinIO (or PVC) for artifact storage with a strict `(sport, version)` key. Model loading is lazy on first inference, cached in memory for the container lifetime. Versions are monotonically increasing; the API stamps `ModelVersion` on the prediction so deetsMeter (or any future model-explainer UI) can attribute a prediction to a specific model run.

### Decision 5 — Per-sport models

All ~40 features in the current pipeline are football-specific (Ypp, ExplosiveRate, RZ efficiency, etc.). MLB has zero overlap — the right baseball features are xwOBA, FIP, BABIP, run expectancy, pitcher matchup context, etc.

This is not a "we'll generalize the feature pipeline" problem. It's "there is a football model and there is a baseball model, and they are different programs sharing infrastructure."

**Recommendation:** explicit per-sport pipelines under a common service.

```
metricbot/
  ingest/
    nfl_features.py         (also covers NCAAFB — same features)
    mlb_features.py
  models/
    football_margin.py      (the current LinearRegression)
    mlb_run_diff.py         (new; design TBD)
  service/
    main.py                 (FastAPI app)
    routes.py
    artifacts.py            (load/save model blobs)
```

The MLB pipeline has placeholder math at first — the user has explicitly said MLB accuracy is not the goal, the plumbing is. A `0.5` flat prediction is fine for MLB during plumbing development as long as it actually traverses the service boundaries correctly.

### Decision 6 — MetricBot user identity / auth

Current pattern: prediction rows are POSTed by a synthetic user with `IsSynthetic = true`. The API endpoint `/api/admin/ai-predictions/{userId}` authenticates as that user.

Two paths:

- **Keep the current pattern.** Service-account user, Firebase token issued for the synthetic user, MetricBot service authenticates as that user when POSTing predictions.
- **Service-to-service auth.** Skip the Firebase user model entirely; introduce a service-to-service auth pattern (mTLS, shared secret, signed JWT) for inter-cluster API calls.

**Recommendation:** keep the current pattern. The synthetic user model is already wired, the predictions belong-to-a-user semantically (they show up in the deetsMeter as "MetricBot's pick"), and introducing service-to-service auth as a one-off for this feature is over-engineering.

### Decision 7 — Where does training data live?

Even after Decision 3 (HTTP-based metric reads), the training pipeline pulls thousands of rows. Two options:

- Stream them via API on every training run (slow but simple)
- Materialize a training-data snapshot in MetricBot's own storage (Postgres or Parquet on PVC), refreshed on the same cadence as the model

**Recommendation:** start with streaming via API. Profile the training run; if it's > a few minutes, introduce a cached training snapshot. Don't build the snapshot pipeline speculatively.

---

## Proposed rollout

> **Note on near-term scope.** The rollout below is the **full-microservice** rollout. For the 2026 season, the [Season-launch MVP (2026)](#season-launch-mvp-2026) section above supersedes Phases 0–2 of this rollout. Treat the phases below as the Q4 / post-season continuation that builds on what the MVP ships.

### Phase 0 — Discovery (no code changes)

- Audit Producer's existing endpoints — what's already exposed that MetricBot would use, what's missing
- Inventory the ~40 features in the current pipeline against Producer's API surface
- Confirm a Python service in K3s is acceptable (Decision 1) and discuss with anyone who'd be on-call for it

### Phase 1 — Producer metrics API

- Producer exposes a `GET /api/metrics/batch?sport=...&seasonYear=...&seasonWeek=...` (shape TBD)
- API-side: the canonical contest model that MetricBot ultimately reads from
- This is genuinely a Producer PR, separate from MetricBot itself

### Phase 2 — MetricBot service skeleton

- New service `src/SportsData.MetricBot/` (Python + FastAPI)
- Dockerfile + Helm chart following the existing service conventions
- One placeholder endpoint (`POST /predict/contest/{contestId}` returning a flat `0.5` for any sport)
- Wired through AppConfig and OTel
- Deployed to dev cluster; verify it's reachable and instrumented before any model code lands

### Phase 3 — Football model port

- Port the existing scripts into the new service
- Same feature set, same LinearRegression baseline (the goal is correctness vs the existing pipeline, not improving accuracy)
- Add `POST /train/{sport}` and `POST /predict/week/{sport}/{year}/{week}`
- Compare output JSON against the current prototype's JSON for several weeks; should match to within numerical tolerance
- When they match, the deetsMeter source-of-truth flips from the operator-run Python to the cluster service

### Phase 4 — MLB plumbing pipeline

- MLB-specific feature pipeline (new code, simple baseline)
- MLB model — at first, intentionally simplistic (run-differential mean reversion or similar). Goal is round-tripping, not accuracy
- Validate the deetsMeter UI renders MLB predictions cleanly

### Phase 5 — Cadence + artifacts

- Scheduled weekly retrain (Hangfire trigger from the API, or k8s CronJob; pick one)
- MinIO or PVC artifact storage; model versioning
- Admin endpoint to trigger an out-of-band retrain

### Phase 6 — Quality work (future, separate planning)

- Improve MLB model
- Possibly improve football model (cross-validation, feature engineering, gradient boosting)
- Possibly add Decision 2's live inference path

---

## Open questions

These should resolve before Phase 1 starts:

1. **Producer audit.** Does Producer already expose enough to read the ~40 features without a dedicated batched endpoint?
2. **GPU/CPU sizing on the cluster.** Are the nodes' CPU + RAM specs comfortable for batch retraining on tens of thousands of rows? (Almost certainly yes for LinearRegression; matters if the model ever upgrades to gradient boosting or neural baselines.)
3. **Acceptable training time.** What's the budget for a full weekly retrain — minutes? An hour?
4. **Owner / on-call.** If MetricBot goes down on a Sunday, what's the impact (deetsMeter renders stale predictions) and who handles it?
5. **MLB feature data availability in Producer.** The current Producer focuses on football; does it source the MLB-specific stats (xwOBA, etc.) the MLB model would need? If not, that's its own Producer workstream.

---

## Out of scope

- **deetsMeter UI changes.** Component is unchanged; it reads prediction rows from the existing API endpoint.
- **Other prediction types.** Currently SU + ATS. Over/Under, prop-bet style, player-level — all future, all separate.
- **Model interpretability / SHAP values.** Worth doing eventually; not part of getting plumbing right.
- **Hyperparameter tuning.** The current LinearRegression has zero hyperparameters; this becomes a real question only when the model upgrades.
- **Live odds integration.** Predictions today are computed against the most recent stored spread. Odds-movement-aware predictions are a Decision 2 hybrid case for later.
- **Multi-tenancy.** Single sportDeets tenancy, no need for per-tenant model isolation.

---

## Files referenced

Production code (existing):

- `src/metrics-modeling/` — entire Python prototype tree
- `src/metrics-modeling/Generate-Predictions.ps1` — current operator workflow
- `src/UI/sd-ui/src/components/matchups/DeetsMeter.jsx` — UI consumer
- `src/UI/sd-ui/src/components/matchups/DeetsMeter.css`
- `src/SportsData.Api/Application/...AiPredictions...` — current ingestion endpoint (`/api/admin/ai-predictions/{userId}`)

Sibling design context:

- `ai-provider-cutover-deepseek-to-ollama.md` (this folder) — the AI-side cutover plan, related but independent

## First graded backtests — 2025 NCAAFB, five weeks (2026-08-10)

Grader: `POST /admin/metricbot/backtest` (shipped #612). Weeks 4, 5, 6,
8, 10; tail=0 per protocol (tail is an early-weeks-only question).
1,439 graded games.

| wk | graded | SU | always-home | favorite (spread games) | ATS | model MAE | market MAE | Brier | climatology |
|---|---|---|---|---|---|---|---|---|---|
| 4 | 285 | 64.2% | 57.9% | 76.5% (98) | 50.5% (97) | 19.60 | 11.47 | 0.2378 | 0.2438 |
| 5 | 264 | 65.9% | 54.9% | 78.3% (106) | 44.3% (106) | 17.47 | 10.22 | 0.2229 | 0.2476 |
| 6 | 291 | 67.3% | 57.0% | 77.9% (95) | 45.3% (95) | 17.53 | 11.22 | 0.2146 | 0.2450 |
| 8 | 295 | 71.5% | 55.2% | 73.2% (108) | 50.9% (108) | 16.63 | 12.20 | 0.1939 | 0.2472 |
| 10 | 304 | 77.0% | 54.6% | 73.6% (110) | 45.5% (110) | 15.43 | 12.44 | 0.1598 | 0.2479 |

Weighted: SU 69.4%, ATS 47.3% (n=516), model MAE 17.30 vs market 11.53.
Denominator note: favorite-baseline games (517) exceed ATS games (516)
by one — a push, excluded from ATS grading per the harness rules but
still SU-decided and so counted for the favorite baseline.

**Conclusions (scope: five sampled weeks of one season, 2025 NCAAFB —
observed results, not a multi-season generalization):**
- SU accuracy rose monotonically across the sampled weeks — 64% (wk4)
  -> 77% (wk10) — consistent with season-aggregate features
  stabilizing as games accumulate. Brier beat climatology in every
  sampled week (per-week values in the table). On this evidence,
  deetsMeter SU picks are defensible; repeat over 2024 (and 2026 as it
  arrives) before treating the trend as a law.
- ATS: no observed edge — 47.3% weighted, below break-even in 4 of 5
  weeks and never above 51%. Frame as entertainment.
- Vegas beats the model by ~6 pts/game on margin, consistently.
  Consistent with the stated goal (calibration, not beating Vegas).
- In-sample MAE 13.9 vs out-of-sample ~17.3: the honest gap; every
  prior 56-62% claim was measured with leaky features.

**Pooled calibration (1,439 games):** buckets 0.1–0.8 healthy (±4pts).
Two defects: 0.8–0.9 runs ~10pts hot (84.4% -> 74.9%, n=167), and
0.0–0.1 is broken (5.6% -> 35.0% actual, n=40) while 0.9–1.0 is
near-perfect (94.5% -> 94.3%).

**Autopsy of the 0.0–0.1 bucket (40 games):** the neutral-site theory
is not supported — 2/40 games were off the home team's venue and none
carried an event note (caveat: both signals depend on VenueId/EventNote
completeness, which was not separately audited). Leading explanation,
consistent with all the evidence: **no opponent-strength adjustment in
the features.** All metrics are raw
season averages; schedule-inflated stats are indistinguishable from
real ones. Smoking guns: the model gave >=90% to LOWER-DIVISION road
teams that then lost 47-14 (fcs @ fbs) and 35-9 (d2 @ fcs) — hard to
explain by any rival theory. Extreme
away-favorite predictions concentrate the inflation failure because a
90% road win requires stats overwhelming home field — inflated stats
are exactly the kind that do. The asymmetry (0.9–1.0 fine) exists
because predicted HOME blowouts are mostly payday games where strong
stats and home field agree. A global residual-std inflation CANNOT fix
this (it would wreck the healthy 0.9–1.0 bucket); this is bias, not
variance.

**Agreed next steps (in order):**
1. ~~Grader enhancement: model SU accuracy restricted to the SAME
   spread games as the favorite baseline~~ DONE (2026-08-11), sweep
   re-run same day. **Verdict: on market-priced games the favorite
   baseline beats the model in every sampled week — weighted 75.8% vs
   65.6% (n=517).** Per week (model/favorite): wk4 64.3/76.5, wk5
   57.6/78.3, wk6 69.5/77.9, wk8 65.7/73.2, wk10 70.9/73.6 — the gap
   narrows late but never closes. Spreadless games: model 71.5%
   weighted (n=922), rising to 80.4% by wk10 — confirming empirically
   that the overall 69.4% was propped up by easier unpriced matchups.
   These results led directly to the v1.1 design below.
2. ~~MetricBot-v1.1: opponent-adjusted features~~ SUPERSEDED
   2026-08-11 by the v1.1 design section below: v1.1 is market-prior +
   scope; opponent-adjusted features and division indicators moved to
   v1.2 (one change per version so the grader can attribute). Point-in-time caveat: GroupSeasonMap is
   SEASON-scoped (a FranchiseSeason field), which is the right
   granularity for division indicators — divisions don't change
   mid-season — but the field is mutable if the hierarchy is ever
   rebuilt/backfilled, so backtests assume the stored per-season value
   reflects that season's actual membership. Acceptable for v1.1;
   revisit if hierarchy rewrites become routine.
3. Until v1.1: sub-10% home probabilities really mean ~1-in-3.
4. Experiment results durable store (ExperimentRun tables) once the
   report shape settles. Until then the interim record is the HTTP
   RESPONSE the operator saves by hand (e.g. from Bruno) into
   docs/metrics-modeling/output/ (gitignored) — distinct from the CLI's
   `--dump-intermediate`, which writes CSV/JSON artifacts to
   `src/metrics-modeling/data/` (also gitignored; ephemeral when run in
   the service container). Retention owner: the operator's local
   checkout (acceptable: every backtest is deterministic and
   reproducible from the same request, so lost artifacts are
   re-derivable, not lost evidence).

## MetricBot-v1.1 design (decided 2026-08-11)

> **STATUS 2026-08-13: v1.1.0 sweep ran; v1.1.1 patches the finding.**
> The 2025 five-week sweep passed both formal gates (same-games SU
> 66.8% vs v1.0's 65.6%; ATS Brier 0.301 vs 0.324) and ATS accuracy
> reached 53.1% (n=277 — not yet signal). But calibration exposed a
> defect: probabilities used the correction model's IN-SAMPLE fit std
> (~13) where the honest out-of-sample error is ~16-17 — saturating
> everything away from 0.5 (bucket 0.0-0.1: predicted 4.5%, actual
> 25.6%; ATS Brier worse than always-50%). v1.1.1 computes the scale
> from forward-only walk-forward residuals instead (CR review: KFold
> would validate blocks with models trained partly on future games).
> Re-run the sweep before acceptance — exact protocol:
>
> **v1.1.1 acceptance protocol:** five requests via
> `POST /admin/metricbot/backtest`, body
> `{"sport":"FootballNcaa","seasonYear":2025,"week":W}` for W in
> {4,5,6,8,10} (priorSeasonTail omitted = 0). Aggregate weighted by
> per-week denominators (`baseline_favorite.games_with_spread` for
> same-games SU; `ats.decided` for ATS). PASS iff ALL of:
> (a) weighted same-games SU >= 65.6% (the v1.0 floor);
> (b) weighted ATS Brier < 0.25 (better than always-50%);
> (c) pooled SU calibration within 10pts in every bucket with n >= 20.
> FAIL on any -> v1.1 stays off the live weekly job; findings feed
> v1.2. Result JSONs are hand-saved and gitignored (prod-derived);
> every run is deterministic and re-derivable from these requests —
> that determinism is the reproducibility guarantee.
>
> Known-and-deferred: the pure-stats fallback still uses
> its own in-sample std (same defect family, tiny slate share —
> fold into v1.2); the home-underdog asymmetry is v1.2 SOS material.
>
> Original v1.1.0 note:** predict_market_prior in model.py;
> is_priced predicate; FBS slate filter via psql var (fbs_scope from
> per-sport config — NFL unfiltered); FbsParticipant column carves the
> residual corpus in python; ATS DTOs priced-only (NaN defect fixed);
> MIN_RESIDUAL_ROWS = 3x features guards thin corpora (early-2022
> backtests fall back whole-slate). Acceptance per this design: 2022+
> sweep must show no same-games SU regression vs v1.0 and improved ATS
> calibration.

Decisions from the post-sweep review (decision owner: Randall; all
resolved same day):

**Scope — what deetsMeter covers:**
- NCAAFB: games with **at least one FBS participant** (payday games
  included — they appear in real pick'em slates). Filter:
  `split_part(FranchiseSeason.GroupSeasonMap,'|',3) = 'fbs'` on either
  side. Predicate note: existing consumers
  (`GetCompletedFbsContestIds.sql`, `MatchupScheduleProcessor`) use
  substring matching (`LIKE '%fbs%'` / case-insensitive contains);
  v1.1 adopts segment-3 equality WITHIN MetricBot's SQL only — it is
  stricter (immune to 'fbs' appearing in another path segment) and the
  two agree on every current value. Existing consumers are
  intentionally out of scope; harmonizing them on the segment predicate
  is a separate cleanup candidate.
- NFL: **every game.**
- Matchup previews are UNAFFECTED and remain universal ("data-driven
  insights for every NCAAFB and NFL matchup") — previews are the LLM
  pipeline; MetricBot never touches them.

**Architecture — market-prior with a residual model:**
- The spread becomes an INPUT (decided: yes). For priced games,
  `predicted_margin = -Spread + correction(features)` where the model
  is trained to predict the RESIDUAL against the closing line. This
  admits the market's information (injuries, weather, context —
  invisible to box-score aggregates) at full strength instead of
  making 64 noisy features compete with it.
- Unpriced games (a handful of FBS-participant games per season) fall
  back to the existing pure-stats model, unchanged. Explicit contract:
  priced games use the residual path (feature set decided at
  implementation: the 64 stats columns, with Spread entering through
  the prior term, not FEATURE_COLS); unpriced games use the 64-column
  fallback and emit an SU prediction ONLY — no ATS DTO, since there is
  no line to pick against. (Today's pipeline computes NaN cover
  probabilities for spreadless rows and still builds ATS DTOs from
  them — a latent defect v1.1 removes.)
- **Canonical `is_priced` predicate (one definition, used everywhere):**
  `is_priced := Spread IS NOT NULL`. A pick'em line (`Spread == 0`) IS
  priced — it is a real market opinion (prior = 0 + correction), the
  residual path applies, and an ATS pick is decidable (home covers iff
  it wins). This one predicate gates the residual-vs-fallback split,
  ATS DTO emission, and ATS grading. The favorite BASELINE's existing
  `Spread != 0` exclusion is a different question — a pick'em has no
  favorite to name — and stays as a baseline-only sub-rule, not a
  pricing rule. (Current code is inconsistent on exactly this:
  baseline excludes 0, ATS grading includes it, DTO emission ignores
  the question — v1.1 unifies on the predicate above.)
- ATS consequence: the cover probability becomes the model's measured
  DISAGREEMENT with the line. A correction model with nothing to say
  predicts ~0 residual, yielding ~50% ATS picks at low confidence —
  the truthful output given measured ATS of 47.3%, replacing today's
  false confidence.
- SU consequence: the favorite baseline (~76%) becomes the natural
  benchmark — a ZERO correction reproduces it exactly, but a trained
  correction can flip favorite-side picks, so matching or beating the
  baseline is a grader-verified outcome, not a construction guarantee.
  The 2022+ backtest sweep is the acceptance test: v1.1 ships only if
  same-games SU does not regress below the pure-stats v1.0 number and
  ATS calibration improves.
- Rejected alternatives: raw Spread in FEATURE_COLS (fillna(0) teaches
  the model that unpriced games are pick'ems, poisoning the mismatches
  it handles well); two fully separate models (doubles maintenance,
  splits the corpus).

**Corpus reality (verified against prod 2026-08-11):**
- Odds exist from 2022 onward only. Residual-model training corpus =
  FBS-participant + priced + metrics: 849 (2022) + 895 (2023) + 874
  (2024) + 922 (2025) ≈ **3,540 NCAAFB games**, plus the NFL's own
  corpus. Adequate for a linear correction; rules out data-hungry
  approaches. Residual-model backtests are therefore 2022+ only.
- Priced ≠ FBS: books price hundreds of FCS games (2025: 1,583 priced
  total vs 932 FBS-participant; 922 of the 932 priced). DECIDED
  training scope: the residual model trains on the INTERSECTION —
  FBS-participant AND priced AND metrics (the ~3,540-game corpus in
  the table above) — matching the product scope and keeping the
  training distribution homogeneous. The ~660 priced-FCS games per
  season are deliberately excluded: more rows, but they reintroduce
  the cross-division mixing v1.2 exists to fix. Revisit if 3,540 rows
  prove too thin (the grader will say so).
- GroupSeasonMap: backfilled prod-wide 2026-08-11 (was 2025-only — an
  earlier run had only reached a local DB; 2026 was fully empty until
  then and is a pre-season onboarding dependency worth a checklist
  entry). Division labels: `fbs`, `fcs`, and `yy` (ESPN's abbreviation
  for BOTH D2 and D3 — treat as one below-FCS bucket).

**Sequencing (one change per version so the grader can attribute):**
- v1.1: market-prior + scope filter. Bar: close the same-games gap
  toward the 75.8% favorite baseline; grader unchanged (#614 already
  measures everything needed).
- v1.2: SOS-adjusted features + division indicators — matters MOST in
  v1.1's world (the correction model and unpriced fallback are where
  schedule-blindness still lives, including the broken 0.0-0.1
  bucket).

## MetricBot-v1.1.2 sweep on the recomputed corpus (2026-08-15)

The v1.1.1 acceptance failure was autopsied to formula defects in
`CompetitionMetric` itself (see
`docs/audit/competition-metrics-formula-audit.md`). Fixes C1/C2 (#624),
H1/H2/H4 (#625), H3+M-round with FormulaVersion/InputsHash stamping
(#626), and the recompute enablers (#627, #629) landed, then a full
unattended recompute of BOTH sports, all seasons (49 season-runs,
~11.5h, 2026-08-14→15) rebuilt every CompetitionMetric and
FranchiseSeasonMetric row at vintage `2026.08`. NCAAFB per-game FBS
PPD by season is now 1.495 / 1.470 / 1.500 / 1.512 (2022–2025) —
the 1.31→5.93 cross-vintage drift is gone. v1.1.2 also removed
NetPunt, PenaltyYardsPerPlay, and TurnoverMarginPerDrive from
FEATURE_COLS (58 features; audit M4/H3/M1 — M1 failed box-score
verification at 72.5% exact vs the 95% gate).

Sweep: same five requests (weeks 4/5/6/8/10, 2025, tail 0), results
`output/ncaaf-2025-Wk*-backtest-v5.json`.

| wk | SU same-games | favorite | ATS acc | ATS Brier | model MAE | market MAE |
|----|---------------|----------|---------|-----------|-----------|------------|
| 4  | .7705 | .7705 | .4426 | .2775 | 12.90 | 11.89 |
| 5  | .7736 | .8113 | .4906 | .2680 | 10.21 |  9.41 |
| 6  | .7647 | .7647 | .5098 | .2620 | 10.61 | 10.23 |
| 8  | .7333 | .7500 | .5500 | .2495 | 11.82 | 12.05 |
| 10 | .7115 | .6731 | .5192 | .2457 | 12.62 | 12.90 |

Weighted: same-games SU **75.1%** (gate ≥65.6% — PASS by 9.5pts);
ATS Brier **0.2608** (gate <0.25 — FAIL, from 0.2990); calibration:
3 violating buckets at n=20–24 (FAIL).

**Verdict: 1 of 3 gates — NOT accepted, but the failure mode changed
class.** The −5.5pt uniform home bias is eliminated: the calibration
curve's top half is near-perfect (0.9–1.0 bucket: pred .962 vs actual
.960), SU Brier 0.184 vs climatology 0.224, and violations are
scattered small-n noise in both directions, not systematic. Week 10
beat the favorite baseline outright (.7115 vs .6731) and the model's
margin MAE beat the MARKET in weeks 8 and 10. ATS Brier improves
monotonically with season depth (.2775→.2457; weeks 8 and 10
individually pass the gate) — the residual error is concentrated in
weeks 4–6 where entering-season as-of aggregates rest on 3–5 games.

**v1.2 therefore targets early-season feature quality** (this was
already the plan; the sweep confirms it): consistent as-of AGGREGATE
features on both the training and prediction sides (training currently
uses per-game rows, prediction uses entering-week aggregates —
aligning them helps most where aggregates are thinnest), plus
prior-season tail blending for early weeks, then the deferred SOS /
division indicators. One change per version still applies.

Corpus caveats recorded during the recompute campaign (none affect
the 2022+ model corpus): ESPN's play feed has a vintage boundary at
2013/2014 in BOTH sports (PPD +0.3, SuccessRate −8pts stepping into
2013); pre-2005 data degrades below the gate floors — exclude from
historical analysis; NCAA 2010 has no play data at all; ~1.5–2% of
rows per season are permanent zeros (games ESPN never published drive
data for). NFL FranchiseSeasonMetric rows now exist for the first
time (all seasons 2001+; the FBS-scoping bug #629 had prevented ANY
NFL season aggregation).

## Local container smoke test

Docker's `--env-file` takes the same `KEY=VALUE` format as
`_metricbot.env`, so it can be pointed at directly.

```powershell
cd C:\Projects\sports-data
docker build -f src/metrics-modeling/Dockerfile -t sportsdatametricbot:local .

docker run --rm -p 8080:8080 `
  --env-file "D:\Dropbox\Code\sports-data-provision\_secrets\_metricbot.env" `
  sportsdatametricbot:local
```

Then from another terminal:

```powershell
Invoke-RestMethod -Uri http://localhost:8080/health

# Dry-run experiment: no POST, returns run metadata only.
$body = @{
  sport             = "FootballNcaa"
  season_year       = 2026
  week              = 1
  prior_season_tail = 5
  dry_run           = $true
  publish           = $false
} | ConvertTo-Json

Invoke-RestMethod -Uri http://localhost:8080/run-week `
  -Method Post -ContentType "application/json" -Body $body
```

`Invoke-RestMethod` is the reliable form on Windows PowerShell 5.1. The
`curl` alias resolves to `Invoke-WebRequest` (different parameters), and
even `curl.exe` needs backslash-escaped inner quotes because PS 5.1
strips them when handing arguments to native executables:

```powershell
curl.exe -X POST http://localhost:8080/run-week `
  -H "Content-Type: application/json" `
  -d '{\"sport\":\"FootballNcaa\",\"season_year\":2026,\"week\":1,\"prior_season_tail\":5,\"dry_run\":true}'
```

### Two gotchas

1. **`--env-file` does not strip quotes** — unlike MetricBot's own config
   parser. `METRICBOT_PG_HOST="somehost"` reaches the process with the
   quote characters included and the connection fails. Keep the values
   unquoted (the CLI accepts either).
2. **`localhost` inside a container is the container.** If
   `METRICBOT_PG_HOST` or `METRICBOT_API_BASE_URL` point at `localhost`,
   override them for the container run — Docker Desktop exposes the host
   as `host.docker.internal`. Flags after `--env-file` win, so only the
   host-relative values need patching:

```powershell
docker run --rm -p 8080:8080 `
  --env-file "D:\Dropbox\Code\sports-data-provision\_secrets\_metricbot.env" `
  -e METRICBOT_PG_HOST=host.docker.internal `
  -e METRICBOT_API_BASE_URL=http://host.docker.internal:5262 `
  sportsdatametricbot:local
```

Neither applies in-cluster: the `metricbot-secrets` values are the real
Postgres host and the internal API service address, both resolvable from
any pod.
