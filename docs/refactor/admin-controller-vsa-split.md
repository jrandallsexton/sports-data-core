# Refactor: dissolve AdminController into resource controllers

**Status: IN PROGRESS.** First written 2026-09-13; rewritten 2026-10-03 against current `main`.

| Slice | PR | State |
|---|---|---|
| Snapshot test, Bruno secrets, this plan | #755 | merged |
| Prompts → `PromptsController` | #814 | merged |
| Models, ModelProviders, Model Lab → three controllers | #815 | merged |
| MetricBot → `MetricBotController` (+ ingestion, weekly job) | #816 | merged |
| Notifications → `NotificationsController` (test push + reminder backfill) | #817 | merged |
| SmackLab → `SmackLab/SmackLabController` | #818 | merged |
| Ops proxy → `Ops/OpsController` | #819 | merged |
| Synthetic picks + `ai-refresh` → `Synthetics/SyntheticsController` | #820 | merged |
| Security fix: preview approve/reject admin-only; 403 for non-admin callers | #821 | merged |
| Previews → `PreviewsController` at `api/previews` (+ 7 contest-keyed routes) | #822 | merged |
| Diagnostics → `DiagnosticsController`, `SignalRDebugController`, `LoadTestsController` | #823 | merged |
| Matchups → `MatchupsController` at `api/matchups` | #824 | merged |
| Contests → join `ContestsController` (`api/{sport}/{league}/contests`) | #825 | merged |
| Security fix: `ui/contest` refresh, media refresh, finalize admin-only | #826 | merged |
| Leagues + Picks → `LeaguesController` (`api/leagues`), `PicksController` (`api/picks`) | #827 | merged |
| Franchise season sourcing → join `FranchisesController` | #828 | merged |
| `ai-audit` → `PreviewsController` at `api/previews/audit` | this PR | open |

**Deploy hold (2026-10-04):** nothing in this refactor deploys until every
slice has landed. Then the API, the web app (admin routes in `adminApi.js`)
and **MetricBot** (its ingestion URL, below) deploy together.

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

### Model Lab → `Models/` + `ModelLabController` *(done, #815; Q2 resolved)*
| GET | `model-lab/matrix` | handler `Admin/Queries/GetModelLabMatrix` |
|---|---|---|

### Previews → `PreviewsController` at `api/previews` *(done, #822; Q1 resolved)*
`PreviewController` (`preview/`) is renamed `PreviewsController` and moves to
`api/previews`. Its approve/reject (`api/previews/{previewId}/approve|reject`) became
admin-only in #821. The seven contest-keyed admin routes join it under
`contests/{contestId}`, so a contest id and a preview id never share a position:

| Verb | Today | New route | Handler |
|---|---|---|---|
| GET | `matchup/preview/{contestId}` | `api/previews/contests/{contestId}` | `Previews/Queries/GetMatchupPreview` (moved from `Admin/Queries/`) |
| POST | `matchup/preview/{contestId}` | `api/previews/contests/{contestId}` | `Previews/Commands/UpsertMatchupPreview` (moved) |
| POST | `matchup/preview/{contestId}/reset` | `api/previews/contests/{contestId}/reset` | inline (enqueues generation) |
| POST | `matchup/preview/{contestId}/capture` | `api/previews/contests/{contestId}/capture` | inline |
| GET | `matchup/preview/{contestId}/captures` | `api/previews/contests/{contestId}/captures` | `Previews/Queries/GetMatchupPreviewCaptures` (moved) |
| POST | `matchup/preview/{contestId}/experiment` | `api/previews/contests/{contestId}/experiment` | inline |
| POST | `matchup/preview/{contestId}/experiment/panel` | `api/previews/contests/{contestId}/experiment/panel` | inline |

"Matchup" was a misnomer: the preview is the resource. `GetAiPreview` uses the
admin `GetMatchupPreview` slice, which returns the raw preview string. The
same-named `UI/Matchups/Queries/GetMatchupPreview` (returns `MatchupPreviewDto`)
is the user-facing one and stays for the UI pass. `ai-refresh` went to
Synthetics (#820).

**`ai-audit` (this PR):** `POST admin/ai-audit` → `POST api/previews/audit`;
`Admin/Queries/AuditAi` → `Previews/Queries/AuditAi`, moved verbatim. The
handler is read-only: it logs previews whose predicted winner is not one of
the matchup's two FranchiseSeasonIds. It is enqueued as
`IAuditAiQueryHandler`, so the namespace move strands only audit jobs not
finished at deploy (manual, rarely run). With it gone, `AdminController`'s
constructor (background jobs, logger) had no users and is removed.
`ai/game-recap` is the last action in `AdminController`.

### Contests → join `ContestsController` at `api/{sport}/{league}/contests` *(done, #825; Q3 resolved for contests)*
The first mixed controller: its GETs are public, and every moved action carries
`[AdminApiToken]` on the **action**.

| Verb | Today | New route | Notes |
|---|---|---|---|
| POST | `contest/{contestId}/score` | `api/{sport}/{league}/contests/{contestId}/score` | scoring is keyed by contest id; sport/league only place the route |
| POST | `contest/{contestId}/reenrich?sport=&league=` | `api/{sport}/{league}/contests/{contestId}/reenrich` | `Contests/Commands/ReenrichContest` (moved) |
| POST | `contests/refresh?sport=&league=&seasonYear=` | `api/{sport}/{league}/contests/refresh?seasonYear=` | the literal `refresh` cannot clash with `{contestId:guid}` |
| GET | `baseball/contests/{id}/matchup` + `football/contests/{id}/matchup?league=` | `api/{sport}/{league}/contests/{contestId}/matchup` | **merged**; `Contests/Queries/GetMatchupForContest` (moved) |
| POST | `baseball/contests/{id}/replay` + `football/contests/{id}/replay?league=` | `api/{sport}/{league}/contests/{contestId}/replay` | **merged** |

The baseball/football pairs **had** to merge. Under a sport-scoped prefix they
would share one template. The merged actions resolve the sport with
`ModeMapper.ResolveMode(sport, league)`, as the football actions already did;
`("baseball","mlb")` maps to the `Sport.BaseballMlb` the baseball actions
hard-coded. Sport/league move from query string (default football/ncaa) into
the route, so callers must now name them.

`contest/{id}/score` deliberately lives here, not in a scoring controller.
`ai/game-recap` is not a contest route and is still in `AdminController`.
### Matchups → `MatchupsController` at `api/matchups` *(done, #824)*
| Verb | Today | New route | Handler |
|---|---|---|---|
| POST | `matchups/refresh` | `api/matchups/refresh` | `Matchups/Commands/RefreshWeekMatchups` (moved from `Admin/Commands/`) |
| POST | `matchups/audit-records` | `api/matchups/audit-records` | `Matchups/Jobs/MatchupRecordAudit` (already in Matchups) |
| POST | `backfill-matchup-odds-pricing` | `api/matchups/backfill-odds-pricing` | `Matchups/Commands/BackfillMatchupOddsPricing` (moved, incl. the per-contest `ApplyMatchupOddsPricingHandler` job) |

The new admin `MatchupsController` sits beside the user-facing
`UI/Matchups/MatchupController` (`ui/matchup`). The backfill route drops the
"matchup" that is redundant under the matchups resource.
`notifications/matchups/backfill` went to Notifications (#817).
### Leagues + Picks → `LeaguesController` at `api/leagues`, `PicksController` at `api/picks` *(done, #827; Q4 resolved)*
Both new, both all-admin (`[AdminApiToken]` on the class). The user-facing
surfaces stay where they are: `UI/Leagues/LeagueController` (`ui/leagues`) and
`UI/Picks/PicksController` (`ui/picks`).

| Verb | Today | New route | Handler (moved) |
|---|---|---|---|
| POST | `leagues/{leagueId}/weeks/{week}/replay` | `api/leagues/{leagueId}/weeks/{week}/replay` | `Leagues/Queries/GetLeagueWeekContests` + Producer proxy |
| POST | `backfill-league-scores/{seasonYear}` | `api/leagues/scores/backfill?seasonYear=` | `Leagues/Commands/BackfillLeagueScores` |
| POST | `backfill-user-pick-bet-points` | `api/picks/bet-points/backfill` | `Picks/Commands/BackfillUserPickBetPoints` |

The league is the resource for the replay and the score backfill (it writes
league week results). The bet-points backfill writes UserPick columns, so it
goes to picks. `seasonYear` moves to the query string, as on
`contests/refresh`. Hangfire stores `IApplyUserPickBetPoints` and
`ApplyUserPickBetPointsCommand` by type name, so the namespace move strands
any bet-point job not finished at deploy: enqueued, scheduled, or awaiting
retry (Hangfire retries failed jobs for hours). Before deploying, confirm the
Hangfire dashboard shows none of these jobs in Enqueued, Scheduled or
Retries. It is a one-off backfill, so this is normally empty.

### MetricBot → `MetricBot/` + `MetricBotController` *(new; all admin)*
| Verb | Today | New route | Notes |
|---|---|---|---|
| POST | `metricbot/run-week` | `api/metricbot/run-week` | MetricBot client proxy |
| POST | `metricbot/backtest` | `api/metricbot/backtest` | MetricBot client proxy |
| GET | `metricbot/health` | `api/metricbot/health` | MetricBot client proxy |
| POST | `ai-predictions/{syntheticId}` | `api/metricbot/predictions/{syntheticId}` | **MetricBot's ingestion endpoint** |

`ai-predictions` is not a web-app endpoint: the Python MetricBot service POSTs
each run's predictions there with `X-Admin-Token` (`src/metrics-modeling/metricbot/api.py`).
It moves under MetricBot's own controller, and the Python client changes in the same PR.
That is why MetricBot is part of the deploy hold. The handler
(`UI/Contest/Commands/SubmitContestPredictions`) stays put until the UI pass.

`Application/Jobs/MetricBotWeeklyJob.cs` moves to `MetricBot/Jobs/`. That
removes the catch-all `Application/Jobs/` folder.

### Synthetic picks → own slice *(not MetricBot)*
`Admin/SyntheticPicks/` (`SyntheticPickService`, `StatBotPickWriter`) was first
grouped with MetricBot, but MetricBot never uses it. It is StatBot and
synthetic-user pick generation:
- `SyntheticPickService`: metric-based picks for synthetic users; used only by
  `RefreshAiExistence` (`ai-refresh`).
- `StatBotPickWriter`: StatBot's preview-derived picks; used by `RefreshAiExistence`,
  `MatchupPreviewApprovedConsumer`, `PreviewGeneratedConsumer`, and the pick advisor.

**Landed (#820):** feature `Application/Synthetics/`, using the codebase's own
vocabulary (`IsSynthetic`, `SyntheticUsersConfig`, `SyntheticPickStyle`).
- `SyntheticsController` at `api/synthetics`. `ai-refresh` → `POST api/synthetics/refresh`.
  This drops the `ai-` pseudo-namespace (Q6) because the route changes anyway.
- `Synthetics/Commands/RefreshAiExistence/`: command, handler, validator, and
  `SyntheticPickService`, whose only user is this handler. It is now a
  slice-internal collaborator rather than a top-level service; renaming or
  folding it is a follow-up.
- `Synthetics/StatBotPickWriter` sits at the feature root because several features
  share it (refresh, two preview consumers, the pick advisor).

### Notifications → `Notifications/` + `NotificationsController` *(new; all admin)*
| Verb | Today | New route | Handler today |
|---|---|---|---|
| POST | `notifications/test-push` | `api/notifications/test-push` | `Admin/Commands/SendTestPushNotification` |
| POST | `notifications/matchups/backfill` | `api/notifications/matchups/backfill` | inline (publishes `PickemGroupMatchupsRequested`) |

The backfill was first filed under Matchups, but it is the Notification
service's reminder backfill: it re-drives the projection that schedules
pick-deadline and contest-start reminders. Both `notifications/...` routes are
one resource. The backfill's logic is inline in the action and moves verbatim
(rule 1). Making it a command slice is a follow-up.

### Franchises → join `FranchisesController` at `api/{sport}/{league}/franchises` *(done, #828; Q3 resolved for franchises)*
| Verb | Today | New route | Notes |
|---|---|---|---|
| POST | `sourcing/franchise-seasons/{Sport}/{seasonYear}` | `api/{sport}/{league}/franchises/seasons/{seasonYear}/source` | Producer client proxy, inline |

The all-franchises sibling of the existing per-franchise
`.../franchises/{franchiseIdOrSlug}/seasons/{seasonYear}/source`: same verb and
shape, minus the franchise segment. The literal `seasons` cannot be taken for a
slug (literal segments outrank parameters, and no POST template has that
shape). The sport moves from the `Sport` enum in the route (`FootballNcaa`) to
`{sport}/{league}` resolved by `ModeMapper`, as on every other action here.
`[AdminApiToken]` sits on the action, beside the existing `enrich` and
`source` (D4). It stays an inline proxy, as `GetTeamRoster` in the same
controller already is.

### Diagnostics → three controllers *(done, #823; Q5 resolved)*
Split along tool lines, so no controller is a smaller catch-all:

| Verb | Today | New route | Controller | Handler |
|---|---|---|---|---|
| GET | `errors/competitions-without-{competitors,plays,drives,metrics}` (4) | `api/diagnostics/competitions-without-*` | `DiagnosticsController` | `Diagnostics/Queries/GetCompetitionsWithout*` |
| POST | `ai-test` | `api/diagnostics/ai-test` | `DiagnosticsController` | `Diagnostics/Queries/GetAiResponse` |
| POST | `generate-url-identity` | `api/diagnostics/generate-url-identity` | `DiagnosticsController` | inline + `Diagnostics/Commands/GenerateUrlIdentity` |
| POST | `signalr-debug/{contest-status,football-play,baseball-play}` (3) | `api/signalr-debug/*` | `SignalRDebugController` | inline; request DTOs in `SignalRDebug/` |
| POST | `keda/load-test` | `api/load-tests` | `LoadTestsController` | `LoadTests/Commands/GenerateLoadTest` + `LoadTests/Jobs/PublishLoadTestEventsJob` |

- **SignalR debug gets its own controller.** Each call broadcasts to every
  connected client, which makes it the most dangerous admin tool.
- **Load tests rename the route.** The old `keda/` named the autoscaler, not
  the action.
- **`generate-url-identity` is a deletion candidate.** No web, Bruno, script or
  doc caller was found. It was moved, not deleted, pending the operator's review
  of intent (rule 0).
### SmackLab → `SmackLab/` + `SmackLabController` *(done, #818)*
`smack-lab/{leagues, leagues/{id}/picks, leagues/{id}/ratings, phrases, phrases/{id}, preview, ratings}` → `api/smack-lab/...`. Handlers in
`Admin/SmackLab/` move with it.

### Ops proxy → `Ops/` + `OpsController` *(done, #819 — renamed from AdminOpsProxyController)*
`GET/POST ops/{service}/{sport}/{league}/{**opPath}` → `api/ops/...`. This is the
allowlisted relay to Producer/Provider operations.

### The auth primitive
`AdminApiTokenAttribute` → `Infrastructure/Auth/`. It is infrastructure, and
moving it is what lets `Application/Admin/` be deleted.

---

## Suggested order

1. **This PR (#755):** snapshot test, Bruno secrets, this plan.
2. ~~**Prompts**~~ (#814) → ~~**Models** + **Model Lab**~~ (#815).
3. **MetricBot** (#816), **Notifications**, **SmackLab**, **Ops**: self-contained, new controllers. **Synthetic picks** + `ai-refresh`: own slice.
4. **Diagnostics**: after Q5.
5. **Matchups**, **Previews**, **Scoring/Leagues**: join or create controllers per Q1/Q4.
6. **Contests**, **Franchises**: join sport-scoped controllers per Q3.
7. Move `AdminApiTokenAttribute`; delete `Application/Admin/`.

Steps 2–6 are independent after Prompts and can interleave with feature work.

---

## Open questions (settle per slice, before its PR)

- ~~**Q1. Previews and `preview/`.**~~ Resolved: renamed `PreviewsController`, moved to `api/previews` (approve/reject follow; mobile never called them).
- ~~**Q2. Model Lab.**~~ Resolved: `ModelLabController` in `Models/` (#815).
- **Q3. Joining sport-scoped controllers.** `ContestsController` and
  `FranchisesController` sit at `api/{sport}/{league}/...`. Joining them puts
  admin routes under `{sport}/{league}`. That replaces today's
  `football/...` / `baseball/...` route prefixes, but means handlers take sport
  from the route.
- ~~**Q4. Where do league-score backfill, bet-points backfill and league-week replay live?**~~ Resolved: `LeaguesController` at `api/leagues` (replay, score backfill) and `PicksController` at `api/picks` (bet-points backfill).
- ~~**Q5. Diagnostics controller naming.**~~ Resolved: three controllers (Diagnostics, SignalRDebug, LoadTests). Original question: D3 says `{Resource}Controller`, but these
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
from `User.IsAdmin`); mobile calls no admin route. The header's remaining users are:
- **Bruno**: hand-driven.
- **`SmokeTestFixture`**: runs after every production deploy.
- **MetricBot**: the Python service authenticates its prediction ingestion
  (`api/metricbot/predictions/{userId}`) with `METRICBOT_ADMIN_TOKEN`. It is
  unattended, like the smoke tests.

Retiring the header means migrating both unattended callers first: a service
account, or a break-glass key scoped to exactly the endpoints they touch.

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
