# Refactor: dissolve AdminController into resource controllers

**Status: IN PROGRESS.** Decisions below are settled; nothing has moved yet.
First written 2026-09-13; rewritten 2026-10-03 against current `main`.

`Application/Admin/` holds 66 admin-token endpoints:
- `AdminController`: 54 endpoints, 1,343 lines
- `SmackLabController`: 8
- `AdminOpsProxyController`: 2
- the `AdminApiTokenAttribute` itself

It is not a feature. It is a bucket for anything that happens to need an admin
token. This plan moves every endpoint to the feature it serves, until
`Application/Admin/` no longer exists.

---

## Decisions (settled)

| # | Decision |
|---|---|
| D1 | **"Admin" is an authorization concern, not a domain.** It lives on an attribute, never in a folder, controller or route name. |
| D2 | **`admin/` leaves every route.** Admin resources join the existing `api/` namespace: `api/prompts`, `api/models`. |
| D3 | **Controllers are `{Resource}Controller`, like everything else** (`PromptsController`, `ModelsController`). Where the resource already has a controller, admin endpoints **join it**. No `ContestsAdminController`, no `Admin/` subfolder inside a feature. |
| D4 | **Every moved endpoint keeps `[AdminApiToken]`.** In a mixed controller (public + admin actions) it goes on each **action**. In an all-admin controller it goes on the **class**. |
| D5 | **Moves first, auth model later.** Replacing the shared `X-Admin-Token` with role claims is a separate effort (see "Deferred: the auth model"). It is one attribute change per controller after the split, and the snapshot shows every one. |
| D6 | **Slice layout follows the API conventions** used in #807–#813: `{Feature}/Commands/{Name}/`, `{Feature}/Queries/{Name}/`, `{Feature}/Jobs/{Name}/`, a feature with more than one file gets a subfolder, and pieces shared by slices sit at the feature root. |

---

## The guard: route-table snapshot test

`test/unit/SportsData.Api.Tests.Unit/Routing/RouteTableSnapshotTests.cs` pins
every endpoint to `RouteTable.snapshot.txt`: verb, template, and *effective*
authorization (`[AdminApiToken]`, `[Authorize]`, or `(none)`). It currently
covers 170 routes: 66 admin-token, 79 `[Authorize]`, 25 none.

- **It is the review artifact.** A move PR's snapshot diff is the review: old
  route out, new route in, auth column unchanged.
- **It is the auth ledger.** With `admin/` gone from URIs, and admin actions sitting
  beside public ones in shared controllers (D3/D4), nothing else shows which
  endpoints are privileged. A dropped `[AdminApiToken]` causes no compile error and no
  other test failure, only a silently public endpoint. Here it is a one-line
  diff a reviewer is already reading.
- A second test fails on duplicated verb+template, which is easy to introduce
  while re-parenting routes.

Regenerate: `ROUTE_SNAPSHOT_UPDATE=1 dotnet test --filter RouteTable_MatchesSnapshot`.

---

## Rules for every move PR

0. **Keep, delete or merge first.** This is an inventory exercise, not a filing
   exercise. Check every caller (web `adminApi.js`, Bruno, smoke tests, other
   services, scripts) before moving an endpoint. Deleting is cheap and
   reversible (git keeps it); carrying an unused endpoint forever is not.
1. **No behavior change.** Move code and routes; don't improve handlers.
   `Result<T>` cleanup, collapsing near-duplicates and route renames beyond the
   move get their own PRs.
2. **The snapshot diff is the review.** It shows exactly the endpoints this PR
   moves, each with its auth unchanged. An unexpected row stops the PR.
3. **One feature per PR.** Nothing done "while I was in there".
4. **Client follows in the same PR.** Repoint `adminApi.js` (and any admin page)
   so the admin UI works at every step. Bruno collections can lag.
5. **DI moves with the slice, verified by booting the API locally.** Unit tests
   don't catch lifetime mistakes.

---

## Inventory and targets

Today's routes are shown without the `admin/` prefix. The "new route" column
is the default (`api/` + today's path) unless a note says otherwise. Handlers
under `Admin/Commands/*` and `Admin/Queries/*` are already slice-shaped, so
moving them is a folder move.

### Prompts → `Prompts/` + `PromptsController` *(new; all admin, class-level token)*
| Verb | Today | New route | Handler today |
|---|---|---|---|
| GET | `prompts` | `api/prompts` | `Admin/Prompts/GetPromptsQueryHandler` |
| GET | `prompts/{promptId}` | `api/prompts/{promptId}` | same |
| POST | `prompts` | `api/prompts` | `Admin/Prompts/CreatePrompt*` |
| PUT | `prompts/{promptId}` | `api/prompts/{promptId}` | `Admin/Prompts/UpdatePromptCommand` |
| POST | `prompts/{promptId}/set-default` | `api/prompts/{promptId}/set-default` | `Admin/Prompts/SetDefaultPromptCommand` |
| POST | `prompts/import-blob` | `api/prompts/import-blob` | `Admin/Prompts/ImportPromptFromBlobCommand` |

Handlers are flat today. Slice them into `Prompts/Commands/{CreatePrompt,UpdatePrompt,SetDefaultPrompt,ImportPromptFromBlob}/`
and `Prompts/Queries/GetPrompts/`, with `LogSanitizer` at the feature root if
shared. **First slice; it establishes the pattern.** Supersedes #756, whose
flat layout predates D6.

### Models → `Models/` + `ModelsController`, `ModelProvidersController` *(new; all admin)*
| Verb | Today | New route |
|---|---|---|
| GET/POST | `models` | `api/models` |
| GET/PUT | `models/{modelId}` | `api/models/{modelId}` |
| POST | `models/{modelId}/set-default` | `api/models/{modelId}/set-default` |
| GET/POST | `model-providers` | `api/model-providers` |

Handlers today: `Admin/Models/*` (flat). `model-providers` is its own resource,
so per D3 it gets its own controller in the same feature folder.

### Model Lab *(1 endpoint; placement open, Q2)*
| GET | `model-lab/matrix` | handler `Admin/Queries/GetModelLabMatrix` |
|---|---|---|

### Previews → join `PreviewController` *(exists at `preview/`; Q1)*
| Verb | Today | Handler today |
|---|---|---|
| GET | `matchup/preview/{contestId}` | `UI/Matchups/Queries/GetMatchupPreview` |
| POST | `matchup/preview/{contestId}` | `Admin/Commands/UpsertMatchupPreview` |
| POST | `matchup/preview/{contestId}/reset` | inline |
| POST | `matchup/preview/{contestId}/capture` | inline (enqueues generation) |
| GET | `matchup/preview/{contestId}/captures` | `Admin/Queries/GetMatchupPreviewCaptures` |
| POST | `matchup/preview/{contestId}/experiment` | inline (enqueues generation) |
| POST | `matchup/preview/{contestId}/experiment/panel` | inline |
| POST | `ai-refresh` | `Admin/Commands/RefreshAiExistence` |
| POST | `ai-audit` | `Admin/Queries/AuditAi` |

### Contests → join `ContestsController` *(exists at `api/{sport}/{league}/contests`; Q3)*
| Verb | Today | Handler today |
|---|---|---|
| POST | `contest/{contestId}/reenrich` | `Admin/Commands/ReenrichContest` |
| POST | `contests/refresh` | Producer client proxy |
| POST | `contest/{contestId}/score` | inline (enqueues pick scoring) |
| POST | `ai/game-recap` | `Contests/Commands/GenerateGameRecap` |
| GET | `football/contests/{contestId}/matchup` | `Admin/Queries/GetMatchupForContest` |
| GET | `baseball/contests/{contestId}/matchup` | same |
| POST | `football/contests/{contestId}/replay` | Producer client proxy |
| POST | `baseball/contests/{contestId}/replay` | Producer client proxy |

`contests/refresh` is also reachable through `AdminOpsProxyController`'s
allowlist (`producer` → `contests/refresh`). Two doors, one room: apply rule 0.

### Matchups → `Matchups/` + `MatchupsController` *(new; all admin)*
| Verb | Today | Handler today |
|---|---|---|
| POST | `matchups/refresh` | `Admin/Commands/RefreshWeekMatchups` |
| POST | `matchups/audit-records` | `Matchups/Jobs/MatchupRecordAudit` (enqueue) |
| POST | `backfill-matchup-odds-pricing` | `Admin/Commands/BackfillMatchupOddsPricing` |
| POST | `notifications/matchups/backfill` | inline |

### Scoring / Leagues *(Q4)*
| Verb | Today | Handler today |
|---|---|---|
| POST | `backfill-league-scores/{seasonYear}` | `Admin/Commands/BackfillLeagueScores` |
| POST | `backfill-user-pick-bet-points` | `Admin/Commands/BackfillUserPickBetPoints` |
| POST | `leagues/{leagueId}/weeks/{week}/replay` | `Admin/Queries/GetLeagueWeekContests` + Producer proxy |

### MetricBot → `MetricBot/` + `MetricBotController` *(new; all admin)*
| Verb | Today | Notes |
|---|---|---|
| POST | `metricbot/run-week` | MetricBot client proxy |
| POST | `metricbot/backtest` | MetricBot client proxy |
| GET | `metricbot/health` | MetricBot client proxy |
| POST | `ai-predictions/{syntheticId}` | synthetic bulk picks (`UI/Contest/Commands/SubmitContestPredictions`) |

Also moves here: `Admin/SyntheticPicks/*`, which includes a `SyntheticPickService`
(retire as a service per VSA). `StatBotPickWriter` is used by
`MatchupPreviewApprovedConsumer`, so it may belong in Previews. The other move is
`Application/Jobs/MetricBotWeeklyJob.cs`, deferred from the VSA pass to here.

### Notifications → `Notifications/` + `NotificationsController` *(new; all admin)*
| POST | `notifications/test-push` | `Admin/Commands/SendTestPushNotification` |
|---|---|---|

### Franchises → join `FranchisesController` *(exists, sport-scoped; Q3)*
| POST | `sourcing/franchise-seasons/{sport}/{seasonYear}` | Producer client proxy |
|---|---|---|

`FranchisesController` already carries two admin actions with per-action
`[AdminApiToken]` (`.../seasons/{seasonYear}/enrich` and `/source`), so this is
the D4 pattern in existing code.

### Diagnostics → `Diagnostics/` *(new; controller naming open, Q5)*
| Verb | Today | Handler today |
|---|---|---|
| GET | `errors/competitions-without-{competitors,plays,drives,metrics}` (4) | `Admin/Queries/GetCompetitionsWithout*` |
| POST | `generate-url-identity` | `Admin/GenerateUrlIdentityCommand` |
| POST | `ai-test` | `Admin/Queries/GetAiResponse` |
| POST | `keda/load-test` | `Admin/Commands/GenerateLoadTest` + `Admin/Jobs/PublishLoadTestEventsJob` |
| POST | `signalr-debug/{contest-status,football-play,baseball-play}` (3) | inline, request DTOs in `Admin/SignalRDebug/` |

### SmackLab → `SmackLab/` + `SmackLabController` *(moves out of Admin; 8 endpoints; all admin)*
`smack-lab/{leagues, leagues/{id}/picks, leagues/{id}/ratings, phrases, phrases/{id}, preview, ratings}` → `api/smack-lab/...`. Handlers in
`Admin/SmackLab/` move with it.

### Ops proxy → `Ops/` + `OpsController` *(2 endpoints; all admin)*
`GET/POST ops/{service}/{sport}/{league}/{**opPath}` → `api/ops/...`. This is the
allowlisted relay to Producer/Provider operations.

### The auth primitive
`AdminApiTokenAttribute` → `Infrastructure/Auth/`. It is infrastructure, and
moving it is what lets `Application/Admin/` be deleted.

---

## Suggested order

1. **This PR (#755):** snapshot test, Bruno secrets, this plan.
2. **Prompts** (pattern end to end) → **Models** (+ Model Lab per Q2).
3. **MetricBot**, **Notifications**, **SmackLab**, **Ops**: self-contained, new controllers.
4. **Diagnostics**: after Q5.
5. **Matchups**, **Previews**, **Scoring/Leagues**: join or create controllers per Q1/Q4.
6. **Contests**, **Franchises**: join sport-scoped controllers per Q3.
7. Move `AdminApiTokenAttribute`; delete `Application/Admin/`.

Steps 2–6 are independent after Prompts and can interleave with feature work.

---

## Open questions (settle per slice, before its PR)

- **Q1. Previews and `preview/`.** `PreviewController` lives at `preview/`, not
  `api/`. Admin preview endpoints joining it would land at `preview/...`. Either
  accept that, or move `PreviewController` to `api/previews` in the same PR
  (that is a user-facing route change, so the web/mobile approve/reject calls follow).
- **Q2. Model Lab.** Is it its own slice, or part of Models or Previews? It reads
  preview captures across models.
- **Q3. Joining sport-scoped controllers.** `ContestsController` and
  `FranchisesController` sit at `api/{sport}/{league}/...`. Joining them puts
  admin routes under `{sport}/{league}`. That replaces today's
  `football/...` / `baseball/...` route prefixes, but means handlers take sport
  from the route.
- **Q4. Where do league-score backfill, bet-points backfill and league-week
  replay live?** Candidates: a `LeaguesController` under `api/` (the existing
  `LeagueController` is the UI surface at `ui/leagues`), or `Scoring/`.
- **Q5. Diagnostics controller naming.** D3 says `{Resource}Controller`, but these
  are tools rather than a resource. Options: `DiagnosticsController`, or split by
  resource (`CompetitionsController` for the integrity queries, `SignalRDebugController`, `LoadTestsController`).
- **Q6. Route renames.** `contest/{id}` vs `contests/...` (singular vs plural),
  `ai-*` pseudo-namespaces, the four `errors/competitions-without-*` routes that
  are one question with a parameter. Rename during each move (the clients
  change anyway) or in follow-ups? Rule 1 says follow-ups unless decided
  otherwise.

---

## Deferred: the auth model

The shared `X-Admin-Token` header is the weaker primitive:
- actions taken with it are unattributable
- it is one secret for every admin endpoint
- the filter checks it before authentication

The web admin UI already runs entirely on Firebase claims (`ClaimTypes.Role = "Admin"`,
from `User.IsAdmin`); mobile calls no admin route. The header's remaining users are
**Bruno** (hand-driven) and **`SmokeTestFixture`**, which runs after every production
deploy. Retiring the header means migrating the smoke tests first: a service
account, or a break-glass key scoped to the smoke-tested endpoints.

Per D5 this comes after the split. It is then one attribute change per
controller, and the snapshot asserts the new policy per endpoint.

---

## What success looks like

- `Application/Admin/` no longer exists.
- No route template contains `admin/`.
- No controller is a catch-all; each serves one resource.
- The snapshot shows every formerly-admin endpoint under its new route, each
  still `[AdminApiToken]`. No endpoint lost its auth across the whole refactor.
- The admin pages work at the end, and at every step in between.
