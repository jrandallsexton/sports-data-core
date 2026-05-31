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
