# Refactor: dissolve AdminController into domain slices

**Status: PROPOSED — for discussion, nothing built.**
Author: 2026-09-13.

`AdminController` is 1,215 lines and 50 endpoints spanning eleven unrelated
domains. It is not an admin *feature*; it is a bucket for anything that
happens to need an admin token. This plan breaks it up by the domain each
endpoint actually serves, leaving `Application/Admin/` holding nothing but
the auth primitive.

---

## The core claim

**"Admin" is an authorization concern, not a domain.** It belongs on an
attribute, not in a folder name. Everything in the controller today is really
about prompts, models, contests, previews, notifications, or scoring — each of
which already has (or deserves) a slice of its own.

Two things follow, and keeping them separate is what makes this safe:

| Concern | Decision |
|---|---|
| Where the **code** lives | Changes. That is the refactor. |
| What the **routes** are | **Also changes.** `admin/` comes out of every URI. |

**Operator decision, 2026-09-13:** the `admin/` prefix dies. It is an
authorization fact leaking into the URI, and the clients are ours to fix.
Mobile calls no admin route at all; the web admin pages may break temporarily
and be repaired as each slice lands.

---

## Recommendation 1 — strip `admin/`, and verify there is nothing under it

**Collision check: zero.** All 159 routes in the API were enumerated and every
admin route was re-tested with its prefix removed against the 100 public
routes. **No exact collisions.**

That result is structural rather than lucky. The public API is already
namespaced by first segment:

| Segment | Controllers |
|---|---|
| `ui/` | 16 — the app-facing view-model surface |
| `api/` | 5 — Athletes, Contests, Franchises, Messageboard, Venues |
| `preview/` | 1 |
| `system/` | 1 — internal league contests |
| `[controller]` | Auth, User |

Admin routes, de-prefixed, land at the **root** — a namespace nothing else
occupies. Which raises the one real question this decision creates, below.

### The consequence to decide: root, or join a namespace?

Stripping `admin/` makes these ~59 endpoints the only root-level routes in the
service. `/prompts`, `/models`, `/contests/refresh`. That is clean, but it
makes admin the *least* namespaced surface in an API where everything else is
namespaced — arguably trading one organizational oddity for another.

Three options:

- **A — bare root.** `/prompts`, `/models`. Shortest. Admin becomes the
  implicit default namespace, which is a strange thing for the most privileged
  surface to be.
- **B — join `api/`.** `/api/prompts`, `/api/models`. Consistent with the
  existing resource API, and `api/` already holds Contests and Franchises,
  which several admin endpoints are about. Reads as "the resource API,
  some of which requires an admin token."
- **C — a new neutral namespace** (`ops/`, `internal/`, `manage/`). Honest
  about the surface without naming the *token*. Still a prefix, so it only
  half-satisfies the goal.

I lean **B**. It removes `admin/` as demanded, puts admin resources in the
namespace their public siblings already occupy, and leaves the root free.
But this is the decision that changes all ~59 routes, so it should be settled
before anything moves.

### The thing we lose, and what replaces it

`admin/` told a reader "this needs an admin token" before they opened a file.
After this change, `/api/prompts` and `/api/contests/{id}` sit side by side
under completely different auth. Nothing in the URI distinguishes them.

That is an acceptable trade, but it is **not free**, and it is exactly why
Recommendation 2 matters more now than it did under the keep-the-routes plan.

---

## Recommendation 1b — fix the AUTH model first; it is the real defect

**Operator observation, 2026-09-13:** a shared admin API key is the wrong
primitive; endpoint access should come from claims on the caller's token.
Correct — and the work is far smaller than it looks, because the claims model
already exists and is already carrying the web app.

### What is already built

`User.IsAdmin` (a column) → `FirebaseAuthenticationMiddleware` issues
`ClaimTypes.Role = "Admin"` on the authenticated identity (it also issues
`permission = ReadOnly`, and knows about `IsSynthetic`) →
`AdminApiTokenAttribute` already honours that claim.

**The web admin UI sends no `X-Admin-Token` at all.** Zero occurrences in
`src/UI`. It is already running entirely on claims. So is mobile, which calls
no admin route whatsoever.

### Who actually uses the shared key

Only **Bruno collections** — ~20 `.yml` files of operator tooling — plus one
stray reference in a debug SQL comment. Nothing in any shipped client.

### Why the key is the defect, concretely

Read the filter's order of operations: the `X-Admin-Token` header is checked
**first and returns before authentication is considered**.

- **Actions taken with it are unattributable.** There is no user identity on
  the request, so a contest reenrich, a league-score backfill, or a SignalR
  broadcast performed through Bruno cannot be traced to a person in any log.
- **One secret, no per-user revocation, no rotation story.** Compromise is
  total and silent.
- **It is all-or-nothing.** The same header that reads `metricbot/health`
  also fires `signalr-debug/football-play`, which broadcasts to every
  connected client, and `keda/load-test`.

### Why claims beat it beyond the obvious

`IsAdmin` is a boolean, but these 50 endpoints have wildly different blast
radii. Claims let that be expressed — `previews:write`, `models:write`,
`ops:replay`, `diagnostics:read` — where a shared key never can. Not required
on day one, but it is the reason to prefer the model, not just the hygiene.

### The sequencing consequence — this inverts part of the plan

**Do the auth change before the slice moves, not after.**

1. All 50 endpoints currently sit in one file under one class-level attribute.
   Changing auth there is a handful of lines. After the split it is eleven
   controllers and eleven edits.
2. It changes what the route-snapshot test asserts — policy per endpoint
   rather than "has `[AdminApiToken]`". Better to write the test against the
   final shape once.
3. It settles the route-prefix argument properly. `admin/` exists in the URI
   largely because the auth requirement was not legible any other way. Replace
   the bespoke filter with a standard `[Authorize(Roles = "Admin")]` or a named
   policy and the requirement becomes declarative, discoverable in Swagger, and
   enforced by the framework — at which point dropping the prefix costs
   nothing, which is exactly the conclusion the operator reached by instinct.

### The one real cost — and why it is smaller than it looks

The reason the key survives is ergonomic, not architectural: running an admin
route from Bruno with claims auth means capturing a bearer token first, and
Firebase ids expire in about an hour. That is a real annoyance and a fair
reason to have kept the key.

It is also a one-time setup problem. Bruno supports collection-level
pre-request scripts and variables, and Firebase exposes a REST sign-in
(`identitytoolkit.googleapis.com/v1/accounts:signInWithPassword`) that returns
an `idToken`. One login request whose script stashes the token in a collection
variable, and every other request sends `Bearer {{idToken}}` without further
thought. Re-run the login when it expires, or script the refresh.

Other options if that is still not worth it:

- A long-lived service account with the Admin role, used by Bruno only.
- Keep a break-glass key, but scope it to a **narrow, named** set of endpoints
  rather than the whole admin surface — and log loudly whenever it is used.

### Adjacent hazard found while checking this — fix regardless

`bruno/environments/*.yml` **are tracked**, and each carries an admin-token
variable. Today every one of them holds `value: ""`, so **nothing is currently
leaked**. But `.gitignore` only covers `bruno/**/.env`, not these files — so
the moment the real token is pasted in to actually use Bruno, the file goes
dirty and a single careless `git add bruno/` puts a production admin token in
a **public** repository.

This is worth fixing on its own schedule, independent of the refactor:
untrack the environment files, or move the secret into a `bruno/**/.env` that
is already ignored. Note this is precisely the failure mode the shared-key
model invites and a claims model does not — there is no long-lived secret to
misplace.

---

## Recommendation 2 — land a route-table snapshot test FIRST

Before a single endpoint moves, add a test that enumerates the application's
route table (`EndpointDataSource` under a `WebApplicationFactory`) and asserts
it against a committed snapshot: verb, template, and **whether
`[AdminApiToken]` is applied** to each.

Worth its own PR, ahead of everything else.

Under the original keep-the-routes plan this was a *regression* gate — the
snapshot had to stay byte-identical. Now that routes are changing deliberately,
its job changes:

1. **It is the review artifact.** Every move PR diffs the snapshot on purpose,
   and that diff — old route out, new route in, auth flag unchanged — is the
   thing a reviewer actually reads. Far better than inferring route changes
   from a pile of moved files.
2. **It is the auth guard we just gave up in the URI.** With `admin/` gone,
   the snapshot becomes the only mechanised statement of which endpoints are
   privileged. A dropped `[AdminApiToken]` during a move would otherwise be
   invisible — no compile error, no failing test, a silently public admin
   endpoint. That is the single worst outcome available in this refactor, and
   this test is what prevents it.
3. It still catches routes re-parented by a class-level `[Route]` and
   templates that silently shadow one another.

Good news on the gate itself: the API PR pipeline now runs an explicit build
before `dotnet test` (fixed since the `--no-build` era), so compile errors and
snapshot drift both fail the PR rather than surfacing post-merge.

---

## Naming cleanup — do it in the same breath

Since the clients break anyway, this is the one cheap moment to fix names that
are bad for reasons other than the prefix. A sample of what is there today:

| Today | Problem |
|---|---|
| `contest/{id}/score` vs `contests/refresh` | singular and plural, same resource |
| `ai-predictions/{syntheticId}` | posts bulk *synthetic picks*; nothing to do with a prediction resource |
| `ai-refresh`, `ai-audit`, `ai-test` | three unrelated actions sharing a fake `ai-` namespace |
| `errors/competitions-without-{competitors,plays,drives,metrics}` | four routes asking one question with a parameter |
| `keda/load-test` | names the autoscaler, not the action |
| `matchup/preview/{contestId}` | preview is a property of a *contest*; `contests/{id}/preview` says so |
| `prompts/{id}/set-default` | a state change expressible as `PUT .../default` |

Proposed principles: plural resource nouns; nest by owning resource
(`contests/{id}/preview`, not `matchup/preview/{id}`); verbs only where the
thing genuinely is an action (`replay`, `backfill`, `reenrich`); no invented
prefixes (`ai-`) standing in for a namespace.

**Deliverable before any code moves: a full old→new route table** for all ~59
endpoints, reviewed in one sitting. It cannot be written until the namespace
question above is settled, since that choice rewrites every row.

---

## Endpoint inventory and target slices

All 50 endpoints, grouped by the domain they actually serve. The Route column
is **today's** path after `admin/` — these are what the old→new table will
rewrite once the namespace is chosen. Folder targets below are unaffected by
that choice.

### Prompts → `Application/Prompts/` *(new)*
| Verb | Route | Method |
|---|---|---|
| POST | `prompts` | CreatePrompt |
| GET | `prompts` | GetPrompts |
| GET | `prompts/{promptId}` | GetPromptById |
| PUT | `prompts/{promptId}` | UpdatePrompt |
| POST | `prompts/{promptId}/set-default` | SetDefaultPrompt |
| POST | `prompts/import-blob` | ImportPromptFromBlob |

Handlers already live together under `Application/Admin/Prompts/`. This is the
cleanest slice in the set — self-contained CRUD, no cross-domain reads.
**Start here.**

### Models → `Application/Models/` *(new)*
| Verb | Route | Method |
|---|---|---|
| POST | `model-providers` | CreateModelProvider |
| GET | `model-providers` | GetModelProviders |
| POST | `models` | CreateModel |
| GET | `models` | GetModels |
| GET | `models/{modelId}` | GetModelById |
| PUT | `models/{modelId}` | UpdateModel |
| POST | `models/{modelId}/set-default` | SetDefaultModel |

Same shape as Prompts, already grouped under `Application/Admin/Models/`.
**Second.**

### Model Lab → `Application/ModelLab/` *(new)*
| Verb | Route | Method |
|---|---|---|
| GET | `model-lab/matrix` | GetModelLabMatrix |

One endpoint, but a distinct product surface (the Model Lab page) that reads
preview captures across models. Open question below on whether it folds into
Models or Previews instead of standing alone.

### Matchup previews → `Application/Previews/` *(exists)*
| Verb | Route | Method |
|---|---|---|
| GET | `matchup/preview/{contestId}` | GetAiPreview |
| POST | `matchup/preview/{contestId}` | UpsertContestPreview |
| POST | `matchup/preview/{contestId}/reset` | ResetContestPreview |
| POST | `matchup/preview/{contestId}/capture` | CaptureContestPreviewPrompt |
| GET | `matchup/preview/{contestId}/captures` | GetContestPreviewPromptCaptures |
| POST | `matchup/preview/{contestId}/experiment` | RunContestPreviewExperiment |
| POST | `matchup/preview/{contestId}/experiment/panel` | RunContestPreviewPanel |
| POST | `ai/game-recap` | GenerateGameRecap |
| POST | `ai-refresh` | RefreshAiExistence |
| POST | `ai-audit` | AiPreviewsAudit |

`Application/Previews/` already exists with `PreviewController.cs`. The admin
surface lands beside it as a second controller, not merged into it.

### Contests → `Application/Contests/` *(exists)*
| Verb | Route | Method |
|---|---|---|
| POST | `contest/{contestId}/reenrich` | ReenrichContest |
| POST | `contests/refresh` | RefreshContestsBySeasonYear |
| GET | `football/contests/{id:guid}/matchup` | GetFootballMatchupForContest |
| POST | `football/contests/{id:guid}/replay` | ReplayFootballContest |
| GET | `baseball/contests/{id:guid}/matchup` | GetBaseballMatchupForContest |
| POST | `baseball/contests/{id:guid}/replay` | ReplayBaseballContest |

Note the sport prefixes are **route** structure, not folder structure — one
admin contests controller serves both, as today.

### Scoring → `Application/Scoring/` *(exists)*
| Verb | Route | Method |
|---|---|---|
| POST | `contest/{contestId}/score` | ScoreContest |
| POST | `backfill-league-scores/{seasonYear}` | BackfillLeagueScores |

### Pick'em groups → `Application/PickemGroups/` *(exists)*
| Verb | Route | Method |
|---|---|---|
| POST | `leagues/{leagueId:guid}/weeks/{week:int}/replay` | ReplayLeagueWeekContests |
| POST | `ai-predictions/{syntheticId}` | PostBulkPicks |

`PostBulkPicks` is the synthetic-picks surface; handlers are already under
`Application/Admin/SyntheticPicks/`.

### MetricBot → `Application/MetricBot/` *(new)*
| Verb | Route | Method |
|---|---|---|
| POST | `metricbot/run-week` | RunMetricBotWeek |
| POST | `metricbot/backtest` | BacktestMetricBotWeek |
| GET | `metricbot/health` | GetMetricBotHealth |

Thin proxies over the MetricBot client. Self-contained. Good early candidate.

### Notifications → `Application/Notifications/` *(new)*
| Verb | Route | Method |
|---|---|---|
| POST | `notifications/test-push` | SendTestPushNotification |
| POST | `notifications/matchups/backfill` | BackfillNotificationMatchups |

### Franchises → `Application/Franchises/` *(exists)*
| Verb | Route | Method |
|---|---|---|
| POST | `sourcing/franchise-seasons/{sport}/{seasonYear:int}` | RequestFranchiseSeasonSourcing |

### Data-integrity queries → `Application/Diagnostics/` *(new)*
| Verb | Route | Method |
|---|---|---|
| GET | `errors/competitions-without-competitors` | GetCompetitionsWithoutCompetitors |
| GET | `errors/competitions-without-plays` | GetCompetitionsWithoutPlays |
| GET | `errors/competitions-without-drives` | GetCompetitionsWithoutDrives |
| GET | `errors/competitions-without-metrics` | GetCompetitionsWithoutMetrics |

Four variations of one question: which competitions are missing a child
dataset. Strong candidate to collapse into a single parameterised endpoint —
but **not during the move**. See "Rules" below.

### Developer tools → `Application/Diagnostics/` *(new)*
| Verb | Route | Method |
|---|---|---|
| POST | `generate-url-identity` | GenerateUrlIdentity |
| POST | `ai-test` | TestAiCommunications |
| POST | `keda/load-test` | GenerateLoadTest |
| POST | `signalr-debug/contest-status` | BroadcastDebugContestStatus |
| POST | `signalr-debug/football-play` | BroadcastDebugFootballPlay |
| POST | `signalr-debug/baseball-play` | BroadcastDebugBaseballPlay |

The three `signalr-debug` endpoints carry the most inline logic in the whole
controller (~50 lines each, hand-built payloads). They already have a
`Application/Admin/SignalRDebug/` folder.

---

## Rules for every move PR

1. **No behavior change.** Move code; do not improve it. Renames, signature
   changes, collapsing near-duplicates, and `Result<T>` clean-up are all
   follow-ups with their own PRs. This is the rule that keeps each PR
   reviewable and each rollback trivial.
2. **The route snapshot diff is the review.** It must show exactly the
   endpoints this PR intends to move, each with its `[AdminApiToken]` flag
   still set. Any unexpected row — especially a lost auth flag — stops the PR.
3. **One domain per PR.** No "while I was in there".
4. **`[AdminApiToken]` on the new controller class**, never inherited by
   accident. The snapshot test asserts it.
5. **DI registrations move with the slice** and are verified by booting the
   API locally — unit tests do not catch lifetime mistakes.

---

## Suggested order

Ordered by isolation, so the riskiest work happens after the pattern is proven:

0. **Settle the auth model** — retire or demote `X-Admin-Token`, move to
   `[Authorize(Roles = "Admin")]` / named policies, decide the Bruno story
1. **Settle the namespace question** (root / `api/` / neutral prefix)
2. Route-table snapshot test (asserting the *new* auth policy per endpoint)
3. Full old→new route table, reviewed in one sitting
4. **Prompts** — establishes the pattern end to end
5. **Models** + **Model Lab**
6. **MetricBot**, **Notifications**, **Franchises** — small and self-contained
7. **Diagnostics** (integrity queries + dev tools + SignalR debug)
8. **Previews** — larger, and shares a folder with live preview code
9. **Contests**, **Scoring**, **PickemGroups** — most entangled with existing
   non-admin slices
10. Delete the empty `Application/Admin/`; relocate whatever remains of the
    auth primitive

Steps 4–9 are independent of each other and can be interleaved with feature
work.

### Client strategy

`adminApi.js` holds 41 of the call sites in one file, so updating it slice by
slice as each PR lands is nearly free and keeps the admin pages working
continuously. Recommended over letting them break and repairing at the end —
but it is a preference, not a blocker. Bruno collections (~42) can lag; they
are operator tooling, not runtime.

---

## Open questions — these need your call

**Q1. Naming for the admin controller inside a domain folder.**
`Application/Contests/` already has `ContestsController.cs`, and there is a
third contest-ish surface at `Application/UI/Contest/ContestController.cs`.
Proposal: `ContestsAdminController.cs` beside the existing one. Alternative:
an `Admin/` subfolder *within* each domain slice — which reads well but
re-introduces the word you are trying to get rid of.

**Q2. `Application/Contests/` vs `Application/UI/Contest/`.**
Two contest folders already exist and I do not know the intended split. This
refactor adds a third caller to that ambiguity. Worth settling the rule before
step 7, even if the answer is "leave it".

**Q3. Should `AdminApiTokenAttribute` leave `Application/` entirely?**
It is infrastructure, and the standing VSA rule is no infrastructure inside
slices. `Infrastructure/Auth/` seems right. Small change, but it is what lets
`Application/Admin/` be deleted outright.

**Q4. Do the four `errors/competitions-without-*` endpoints survive as four?**
They are one question with a parameter. Collapsing them is a route change, so
it is out of scope here — but worth deciding now whether the move should
anticipate it.

**Q5. Is `Model Lab` its own slice?**
It is one endpoint today, but it backs a whole admin page and will likely grow
(per-model accuracy, prompt-scoped comparisons). Standing it up now is cheap;
folding it into `Models/` and splitting later is also fine.

---

## What success looks like

- `Application/Admin/` no longer exists.
- The string `admin/` appears in no route template in the service.
- No file in `src/SportsData.Api` is over ~300 lines because of admin
  endpoints.
- The route snapshot shows every formerly-admin endpoint under its new name,
  each still carrying `[AdminApiToken]` — zero endpoints lost their auth flag
  across the whole refactor.
- `adminApi.js` and the admin pages work at the end, and ideally at every
  step in between.
