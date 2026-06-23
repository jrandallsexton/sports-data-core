# League Creation Hardening (2026-05-28)

Tighten the league-creation eager-bootstrap path so leagues with a
non-trivial `[StartsOn, EndsOn]` window don't silently mis-populate
their first `PickemGroupWeek`. Surfaced while testing MLB league
creation: a league with `StartsOn = today + 10 days` produces an
orphan empty `PickemGroupWeek` row pointing at the *current* week
(which has nothing to do with the league). Combines `league-creation-hardening.md`
(the hardening plan and PR sequence) and `league-creation-matrix.md`
(the possibility-matrix companion that drove the PR-D redesign),
previously separate root-level docs. The hardening plan is the
spine; the matrix lives near the end as a "Possibility matrix"
reference section that the PR-D narrative refers back to.

**Motivating bug.** Single-day MLB leagues (`StartsOn = EndsOn = some
future date`) end up with **two** `PickemGroupWeek` rows in prod —
the orphan empty one created at league-creation time, and the real
one created days later by `MatchupScheduler` once the current
`SeasonWeek` window reaches `StartsOn`. `/user/me` projects
`seasonWeeks` straight off `PickemGroupWeek.SeasonWeek` values, so
both show up in the league's ascending week list. The PicksPage UI
defaults to the first week → zero matchups → reads as broken.

**Status:** drafted 2026-05-28. **Scope and shape of PR-D
expanded materially on 2026-05-29** — local testing surfaced
that PR-B's `Future`-mode dispatch was wrong for near-future
leagues (tomorrow-only), and reflection on the original
"current-week-only" model showed it was wrong by construction
for *any* windowed league. PR-D was held open and rebuilt as a
proper redesign rather than the originally-scoped scheduler-gate
fix.

- **PR-A** (factory extraction) — **merged** in #372.
- **PR-C** (UI date-picker guards on web + mobile) — **merged**
  in #373. Originally listed third in the plan; reordered to
  ship in parallel with PR-A since it's independent of all
  backend work.
- **PR-B** (server validator + Immediate/Future dispatch) —
  **merged** in #375. The `Future`-mode no-op shipped here is
  superseded by PR-D below; the validator + permanent-vs-transient
  exception split stay.
- **PR-D** (creation-time bootstrap redesign) — **in flight** as
  the PR you're reading the doc against. Replaces the original
  "scheduler window gate only" scope with:
  1. New Producer endpoint that returns SeasonWeeks overlapping
     a date range (drives windowed-league resolution).
  2. New `SeasonClient.GetSeasonWeeksOverlapping` consuming it.
  3. New `BootstrapLeagueMatchupsProcessor` — Hangfire
     orchestrator that decides full-season vs windowed, resolves
     target SeasonWeek(s), and fans out one
     `ScheduleGroupWeekMatchupsCommand` per week to the existing
     per-week worker.
  4. `PickemGroupCreatedHandler` slimmed to a thin MassTransit-side
     fan-out point (existence check + single enqueue). All week
     resolution and shell creation moved to the new processor.
  5. `MatchupScheduleProcessor.AreMatchupsGenerated` rule fix:
     only set `true` when matchups were actually written; empty
     results leave the flag `false` so the daily scheduler
     retries (required for the eager-bootstrap path to work for
     NCAAFB+RankingFilter shells created pre-poll).
  6. Factory rename `CreateForCurrentWeek` → `Create` to match
     its post-redesign use (any SeasonWeek, not just current).
  7. Original `MatchupScheduler` window/active gate stays — it's
     still useful for the daily refresh path even though
     creation-time orphans are now prevented upstream.

  Design space the redesign covers is enumerated in the
  [Possibility matrix](#possibility-matrix) section below.

- **PR-E** (deferred) — handler base extraction collapsing the
  three sport-specific create-league handlers (NCAA / NFL / MLB)
  into a shared base; wire-through of `MaxUsers` and
  `NonStandardWeekGroupSeasonMapFilter` request fields. No
  behavior change to in-flight bug, so safe to land whenever the
  refactor cost feels worth paying.

Backfill is explicitly deferred (see *Out of scope*).

---

## What "current week" is trying to do

The intent of `PickemGroupCreatedHandler` is **eager bootstrap**:
populate the league's first `PickemGroupWeek` + schedule matchups
synchronously off the `PickemGroupCreated` event so the
commissioner can immediately go make picks after creating the
league. The implicit assumption is *"league created → user wants
to pick this week's games right now."*

That assumption holds for one case — a season-long league
created during the season. It mismatches every other case:

- League starts in the future (most common during preseason
  signup window).
- League window is entirely in the past (backfill / replay).
- League explicitly targets a non-current `SeasonYear`.
- League created during postseason where the "current" week is
  non-standard (CFP, MLB playoffs, NFL postseason).

---

## Current state

### Files involved

- `src/SportsData.Api/Application/UI/Leagues/Commands/CreateBaseballMlbLeague/CreateBaseballMlbLeagueCommandHandler.cs`
- `src/SportsData.Api/Application/UI/Leagues/Commands/CreateFootballNflLeague/CreateFootballNflLeagueCommandHandler.cs`
- `src/SportsData.Api/Application/UI/Leagues/Commands/CreateFootballNcaaLeague/CreateFootballNcaaLeagueCommandHandler.cs`
- `src/SportsData.Api/Application/PickemGroups/PickemGroupCreatedHandler.cs`
- `src/SportsData.Api/Application/Jobs/MatchupScheduler.cs`
- `src/SportsData.Api/Application/Processors/MatchupScheduleProcessor.cs`
- `src/SportsData.Api/Application/UI/Leagues/Commands/CreateLeagueRequestBase.cs`
- `src/SportsData.Api/Infrastructure/Data/Entities/PickemGroup.cs`

### Flow today

1. **Command handler** (`CreateBaseballMlbLeagueCommandHandler:50-158`):
   creates the `PickemGroup` with `StartsOn`/`EndsOn` from the
   request, adds conferences + commissioner + synthetic member,
   publishes `PickemGroupCreated`, saves. Sport-agnostic except
   for the divisions step. Three near-identical copies exist
   (NCAA / NFL / MLB).

2. **Event handler** (`PickemGroupCreatedHandler.CreateCurrentWeekInternal:46-101`):
   unconditionally calls
   `ISeasonClientFactory.Resolve(sport).GetCurrentSeasonWeek()`,
   creates a `PickemGroupWeek` for that week, enqueues
   `ScheduleGroupWeekMatchupsCommand`. Ignores `StartsOn`,
   `EndsOn`, and the `SeasonYear` on the event itself.

3. **Matchup processor** (`MatchupScheduleProcessor:37-180`):
   fetches matchups for `(sport, year, week)`, applies the
   `[StartsOn, EndsOn]` window filter (lines 101-112), filters
   by ranking/conference, writes `PickemGroupMatchup` rows,
   publishes `PickemGroupWeekMatchupsGenerated` *only if at least
   one matchup survived* (lines 191-199). Empty-week early-return
   is deliberate — see comment at 188-190.

4. **Daily recurring job** (`MatchupScheduler.ExecuteAsync:48-69`):
   for every sport with at least one league, gets current week,
   for every league of that sport creates a `PickemGroupWeek` for
   the current week if missing, enqueues
   `ScheduleGroupWeekMatchupsCommand`. Also ignores
   `[StartsOn, EndsOn]`.

### What this produces today

Concrete scenarios with the current code:

| Scenario | What happens |
| --- | --- |
| Season-long, created mid-season | Works as designed. First week populates immediately. |
| Future-start (`StartsOn = today + 10`) | Orphan empty `PickemGroupWeek` row at *today's* week. No matchups (processor's window filter drops them). No event published. Daily scheduler creates a fresh orphan empty week every day until `StartsOn` lands. Eventually self-corrects when current-week ≥ `StartsOn`. |
| Past-only backfill (window entirely in past) | Should never have been accepted in the first place. No validator rule prevents it today. PR-B adds one. |
| Non-current `SeasonYear` | Same — no validator rule today. PR-B adds one. Handler currently ignores `SeasonYear` and uses real-time current week regardless. |
| Created during postseason | Handler doesn't adjust `SeasonWeek` ordinal for postseason or set `IsNonStandardWeek`. Daily scheduler does both (`MatchupScheduler:101-117`). Two creation sites have drifted. |

The functional case (#1) covers ~all production usage today
because MLB is the only active sport and the test pattern has
been "create league now, season is live." Holes show up the
moment we start testing realistic signup windows.

---

## Holes

Numbered for cross-reference. Severity = personal call.

1. **`StartsOn` ignored at creation** (high). Causes scenario rows
   2–3 above. The handler should at least *not* create a
   `PickemGroupWeek` for a week that falls outside the league's
   window.

2. **`EndsOn` ignored at creation** (medium). Same shape as #1.
   Less impactful in practice because most leagues either have
   `EndsOn = null` (full season) or `EndsOn` far enough away that
   the current week is in-window.

3. **`SeasonYear` from the event is dropped** (medium).
   `PickemGroupCreated.SeasonYear` exists on the record
   (`src/SportsData.Core/Eventing/Events/PickemGroups/PickemGroupCreated.cs:6-13`),
   gets a value from the handler (`CreateBaseballMlbLeagueCommandHandler:143`),
   and is never read by the consumer. Adding a 2024 backfill flag
   today does nothing.

4. **`PickemGroupWeek` initialization drift between creation sites**
   (medium). `PickemGroupCreatedHandler:75-83` doesn't set
   `IsNonStandardWeek` and doesn't adjust the `SeasonWeek` ordinal
   for postseason. `MatchupScheduler:108-117` does both. A league
   created during postseason via the eager path gets a wrong week
   row; one created the next day via the scheduler gets a right
   one. Two sites must construct the same entity the same way.

5. **`MatchupScheduler` is current-week-only** (medium).
   No mechanism to walk `[StartsOn, EndsOn]` and bootstrap missed
   weeks. Backfill is structurally impossible without code change.

6. **`MatchupScheduler` doesn't gate on the league window** (low).
   Once running daily, it'll happily create empty
   `PickemGroupWeek` rows for a future-start league every day
   until `StartsOn`. Rows accumulate even though they're harmless.

7. **Entity fields not wired through requests** (low):
   - `PickemGroup.MaxUsers` exists on the entity, no request field.
   - `PickemGroup.NonStandardWeekGroupSeasonMapFilter` is read by
     `MatchupScheduleProcessor:116-122` for NCAA postseason
     filtering but isn't on any request — currently only
     populated by direct DB edit.
   - `RankingFilter` is read by the processor (`MatchupScheduleProcessor:83`)
     and is on the NCAA request only. Fine — it's NCAA-specific.

8. **Exception handling treats permanent failures as transient**
   (low). `PickemGroupCreatedHandler:55-57` throws on
   `group is null`. The message would retry / DLQ forever for a
   group that genuinely doesn't exist (race on outbox flush could
   theoretically cause this, but a permanent absence shouldn't
   loop). Same at lines 65-69 for "current week could not be
   found" — for an offseason MLB sport, that's permanent until
   the season starts. Different failure modes deserve different
   responses.

9. **Three near-identical sport-specific command handlers**
   (cosmetic but compounds drift risk). NCAA / NFL / MLB handlers
   share ~90% of their code (only the `DivisionSlugs` resolution
   differs meaningfully). Any fix from this doc has to be applied
   in three places. Worth extracting a shared
   `CreateLeagueCommandHandlerBase<TRequest>` while we're in
   here — bounds the blast radius for the rest of the fixes.

---

## Decisions

### Reject past / wrong-year at the validator

Backfill and replay leagues are **never** valid user input. A
league overlapping already-played games has no pickable surface
(the games are over) and corrupts the time-based assumptions in
the picks pipeline. Reject at the request boundary, not silently
absorb in the handler.

One new rule in `CreateLeagueRequestBaseValidator` (PR-B):

**`EffectiveEndsOn > now`** (using `IDateTimeProvider.UtcNow()`).
Rejects only the unambiguous case: window entirely in the past,
no pickable games possible. **Windows that straddle `now` are
allowed** — e.g., a single-day league created at noon where
morning games are done but afternoon games are still scheduled is
a legitimate use case. The processor's `[StartsOn, EndsOn]`
window filter still grabs the in-window games; the existing
pick-lock logic handles per-game pickability. `EndsOn`-only check
is sufficient because the existing `StartsOn < EffectiveEndsOn`
rule already prevents a degenerate window.

Deferred — **`SeasonYear` == current** rule. Originally listed as
a second PR-B validator rule. Pulled out because neither UI
surfaces a `SeasonYear` picker (both implicitly use the current
year), so the rule only defends a hypothetical admin tool /
direct-API caller. Adding `ISeasonClientFactory` to the
command-handler chain (or the validator) purely for that guard
isn't worth it today. Can land alongside whatever admin tool
eventually exposes the field, or as a follow-up if direct-API
attacks become a concern.

### What does "bootstrap mode" actually mean?

> **Historical — superseded by PR-D.** This section described
> PR-B's `Immediate` / `Future` dispatch, where the handler did
> the SeasonWeek lookup and picked one of two paths. Live testing
> showed `Future` mode (no-op) was wrong for near-future leagues
> like "tomorrow-only" — the current SeasonWeek usually contains
> tomorrow, so we should be eagerly populating matchups, not
> deferring. The actual PR-D dispatch is documented in the
> [Possibility matrix](#possibility-matrix) section below (rows 1–17).
> In short: the orchestrator branches on *windowed vs full-season*,
> not on *future vs now*, and resolves overlapping SeasonWeeks via
> the new date-range endpoint.

For posterity, the original PR-B dispatch was:

| Mode | When | Action |
| --- | --- | --- |
| `Immediate` | `(StartsOn ?? -∞) <= now`. | Bootstrap *current week*. |
| `Future` | `StartsOn > now`. | **No-op.** Daily scheduler picks it up once `now >= StartsOn`. |

The `Future` no-op is what PR-D removed.

### UI hardening (web + mobile)

The API-side validator is the trust boundary; the UI is the UX
layer. Both required — the validator catches malicious / direct-
API requests, the UI prevents users from constructing invalid
requests in the first place.

**Web** (`src/UI/sd-ui/src/components/leagues/LeagueCreatePage.jsx`):

- Add `min={todayIsoDate}` to both `<input type="date">` (start
  + end). `<input type="date">` respects `min` and grays out
  earlier days in native date pickers — free UX.
- Client-side check before submit: reject if `endsOn < today`.
  Mirrors the server `EffectiveEndsOn > now` rule.
- `DURATION_WEEKS` mode is dead UI today
  (`finalizeLeagueCreation:171-176` short-circuits with an
  alert). Either remove the tab or, when it's wired up, filter
  the week dropdowns to current + future weeks only. Pending —
  tracked here so it doesn't reintroduce the bug when activated.

**Mobile** (`src/UI/sd-mobile/app/create-league.tsx`):

- Add `minimumDate={today}` on the `startsOn` date picker
  (~line 684-692).
- Update the `endsOn` picker's `minimumDate` to
  `max(startsOn ?? today, today)` so the end date can never be
  before today, regardless of whether `startsOn` is set yet.
- Add a `superRefine` rule: `endsOn` (when set) must be `>=`
  today. Same shape as the server rule.

Neither platform's UI work covers the `SeasonYear` mismatch case
because neither UI surfaces a `SeasonYear` picker — both
implicitly use the current year. The server rule is the only
defense, and it's there for a future admin tool / direct-API
caller, not for the standard UI flow.

### `MatchupScheduler` window gate

Add a per-league window check before creating the current-week
`PickemGroupWeek`:

```csharp
if (group.StartsOn.HasValue && currentWeek.EndDate < group.StartsOn.Value) continue;
if (group.EndsOn.HasValue && currentWeek.StartDate > group.EndsOn.Value) continue;
```

(Exact column names depend on the `SeasonWeek` DTO returned by
`GetCurrentSeasonWeek` — verify before writing.) Stops the
daily orphan-row accumulation for future-start leagues.

Also handles `DeactivatedUtc` while we're there — currently
nothing in `MatchupScheduler` filters out inactive leagues.

### `PickemGroupWeek` factory

Extract a single `PickemGroupWeekFactory.CreateForCurrentWeek(
  PickemGroup group,
  CurrentSeasonWeekDto currentWeek)`
that handles:

- `SeasonWeek` ordinal adjustment for postseason (today only in
  `MatchupScheduler:101-106`).
- `IsNonStandardWeek` flag.
- `SeasonWeekId`, `SeasonYear`, `GroupId`, default
  `AreMatchupsGenerated = false`.

Both `PickemGroupCreatedHandler` and `MatchupScheduler` call it.
Fixes hole #4 and stops the two sites from drifting again.

### Command handler base

Extract `CreateLeagueCommandHandlerBase<TRequest>` covering:

- Validation, enum parsing, year defaulting, division resolution
  (via virtual hook), group construction, member adds, synthetic
  user lookup, event publish, save.

Sport-specific handlers shrink to ~10 lines each: a constructor
forwarding deps + a `Task<Dictionary<Guid, string>>
ResolveDivisionsAsync(...)` override (or null if the sport has
no divisions concept). Fixes hole #9.

### Exception handling differentiation

In `PickemGroupCreatedHandler`:

- `group is null` → `LogError + return`. Permanent. Don't DLQ.
  If outbox replays this later, the handler is idempotent (the
  next condition handles "already populated").
- `currentWeek is null` for an active in-window sport → keep
  `throw`. Transient. Worth retry.
- `currentWeek is null` because the sport is offseason → don't
  throw; log and return. This shows up as
  `weekResult.IsSuccess == true with Value == null` vs
  `IsSuccess == false`. Need to confirm the API contract;
  currently both paths collapse into the same null check.

### Wire fields through

- Add `MaxUsers` to `CreateLeagueRequestBase`. Plumb to entity.
- Add `NonStandardWeekGroupSeasonMapFilter` to the NCAA request
  only. (Cosmetic for MLB.)
- `RankingFilter`: already NCAA-only, no change.

---

## Plan

Four PRs:

1. **PR-A: `PickemGroupWeekFactory` extraction.** Pure refactor.
   Both call sites switch over, identical observable behavior,
   tests pin the factory output. Smallest risk; lays the
   foundation for PR-B and PR-D.

2. **PR-B: Server validator + handler dispatch.** Two halves:
   - Validator: add the `EffectiveEndsOn > now` rule in
     `CreateLeagueRequestBaseValidator`. `IDateTimeProvider`
     injected through the base ctor; all three derived
     validators thread the parameter. The
     `SeasonYear == current` rule originally listed here is
     deferred — see *Decisions* above for the reasoning.
   - Handler: compute bootstrap mode (`Immediate` / `Future`),
     route to `BootstrapImmediate` (current path, using the
     factory from PR-A) or a `Future`-mode no-op with a
     `LogInformation` line for Seq trail. Differentiate
     `group is null` (permanent, log + return) from
     `currentWeek is null` (transient, throw).

3. **PR-C: UI hardening (web + mobile).** Tighten the date
   pickers so users can't construct invalid windows in the first
   place:
   - Web `LeagueCreatePage.jsx`: `min={todayIsoDate}` on both
     `<input type="date">` plus a pre-submit guard rejecting
     `endsOn < today`.
   - Mobile `create-league.tsx`: `minimumDate` on both pickers
     plus a `superRefine` Zod rule mirroring the server check.
   - Defense-in-depth — the server validator from PR-B is the
     trust boundary. Ships independently of PR-B (UI is more
     restrictive in what it allows; safe regardless of server
     state).

4. **PR-D: Creation-time bootstrap redesign.** The big one.
   Originally scoped to just the scheduler window/active gate;
   expanded mid-flight after PR-B's `Future`-mode dispatch
   proved wrong for near-future leagues (tomorrow-only) and the
   underlying "current-week-only" model was shown to be wrong
   by construction for any windowed league. Ships:
   - New Producer endpoint
     `GET /api/seasons/weeks/by-date-range?from=…&to=…` plus
     query + handler + tests. Returns the SeasonWeeks whose
     `[StartDate, EndDate]` overlaps the requested range,
     ordered by `StartDate`. Inclusive on both bounds. Empty
     result is a Success (row 11 — "no SeasonWeeks defined
     yet"), `From > To` is a `BadRequest` Failure.
   - New `IProvideSeasons.GetSeasonWeeksOverlapping(from, to)`
     method on `SeasonClient`.
   - New `BootstrapLeagueMatchupsProcessor` (Hangfire,
     `IBootstrapLeagueMatchups`) — the orchestrator. Resolves
     full-season vs windowed, calls the appropriate Producer
     endpoint, fans out one `ScheduleGroupWeekMatchupsCommand`
     per resolved SeasonWeek to the existing
     `MatchupScheduleProcessor`. Caps partial windows
     (`EndsOn = null` or `StartsOn = null`) at `now + 365d` per
     DP-3 in the [Possibility matrix](#possibility-matrix).
   - `PickemGroupCreatedHandler` slimmed to ~50 lines: group
     existence check + single Hangfire enqueue. No more HTTP
     calls, no more `PickemGroupWeek` writes. Stays as a
     MassTransit-side fan-out point for future side-effects
     (invitations, notifications, etc.) per the user's design
     intent.
   - `MatchupScheduleProcessor.AreMatchupsGenerated` rule fix:
     `true` only when matchups were actually written; empty
     filter results leave the flag `false` so the daily
     `MatchupScheduler` re-fires. Required for the eager-bootstrap
     path to work for NCAAFB+`RankingFilter` shells created
     pre-poll (see "External inputs" appendix in the matrix
     section).
   - `PickemGroupWeekFactory.CreateForCurrentWeek` →
     `Create`. Two call sites + tests. Cosmetic but worth doing
     since the factory now creates rows for any SeasonWeek.
   - `MatchupScheduler` window/active gate (the original PR-D
     scope) stays as-is. Still useful as a fast-path filter on
     the daily refresh job — handler-side eager bootstrap means
     creation-time orphans no longer happen, but the gate keeps
     the scheduler from doing pointless work on out-of-window
     leagues.

5. **PR-E (deferred): Handler base extraction + field wiring.**
   Collapse the three sport-specific create-league handlers into
   a shared base. Wire `MaxUsers` / `NonStandardWeekGroupSeasonMapFilter`
   through the request DTOs. Pure hygiene; no behavior change.
   Land whenever the refactor cost feels worth paying.

Dependency graph: PR-A blocks PR-B (use the factory). PR-B and
PR-C are independent. PR-D depends on PR-B (validator stays;
exception split stays) but supersedes PR-B's `Future`-mode
dispatch. PR-E builds on PR-D's slimmed handler shape. Shipped
order: A → C parallel with B → D, E to follow.

---

## Out of scope

- **Backfill / historical replay leagues.** Explicitly rejected
  at the validator after PR-B. If a real product use case for
  historical replay ever surfaces, it's an admin-only operational
  tool — not a public create-league input. That tool would need
  its own design: how to surface progress, idempotency on partial
  completion, behavior when a past `SeasonWeek` has no data,
  whether to auto-`DeactivatedUtc` on completion. None of which
  is being built here.

- **Eager bootstrap of *future* weeks.** Today the system is
  reactive — daily scheduler picks up next week's row when the
  week boundary moves. For a future-start league we considered
  pre-creating the first week at creation time. Decision:
  **don't.** Adds complexity for no user-visible benefit (the
  commissioner can't pick games for a future week anyway), and
  the daily scheduler is the right place for week-rollover
  logic.

- **`MatchupScheduler` → event-driven.** Memory notes call this
  out as "the only scheduling job NOT in the event-driven primary
  path." There's a real argument for switching to a
  `SeasonWeekRolledOver`-style event, but it's a separate
  architectural call and orthogonal to this cleanup.

- **NCAA / NFL handler equivalents.** Same code shape, same
  holes. PR-E's base extraction fixes all three at once. Don't
  fix NCAA/NFL in isolation.

---

## Prod data remediation

The code fix prevents *new* orphan rows. Existing orphans are
already in the prod DB for every single-day / future-start league
created so far. They need to be cleaned up explicitly — a
`PickemGroupWeek` with zero `PickemGroupMatchup` children and
`AreMatchupsGenerated = false` is the diagnostic signature, but
"empty for legitimate reasons" (sourcing in flight, future week
not reached yet) overlaps with that signature.

Safer query for cleanup candidates:

```sql
-- PickemGroupWeek rows that are empty AND fall outside the
-- league's [StartsOn, EndsOn] window. The window check is what
-- distinguishes orphan from "real but not yet populated."
SELECT pgw."Id", pgw."GroupId", pgw."SeasonWeek", pgw."SeasonYear",
       pg."Name", pg."StartsOn", pg."EndsOn"
FROM "PickemGroupWeek" pgw
JOIN "PickemGroup" pg ON pg."Id" = pgw."GroupId"
LEFT JOIN "PickemGroupMatchup" pgm ON pgm."GroupWeekId" = pgw."Id"
LEFT JOIN public."SeasonWeek" sw ON sw."Id" = pgw."SeasonWeekId"
WHERE pgm."Id" IS NULL
  AND pgw."AreMatchupsGenerated" = false
  AND (
    (pg."StartsOn" IS NOT NULL AND sw."EndDate" < pg."StartsOn") OR
    (pg."EndsOn" IS NOT NULL AND sw."StartDate" > pg."EndsOn")
  );
```

Eyeball the result before deleting. Once PR-B ships, this
becomes a one-shot manual cleanup; no recurring need.

---

## Validation

Unit-test coverage shipped on this branch:

- `BootstrapLeagueMatchupsProcessorTests` — 9 cases pinning the
  full-season vs windowed dispatch, the date-range partial-window
  cap, empty result handling, group-not-found permanence, and
  transient endpoint failures. This is the load-bearing
  behavioral test layer for PR-D.
- `PickemGroupCreatedHandlerTests` — 2 cases for the slimmed
  handler: existence-check + enqueue, group-not-found permanence.
- `MatchupScheduleProcessorTests.Process_NoMatchupsAfterFilter_…`
  — pins the `AreMatchupsGenerated` rule that makes the
  eager-bootstrap refresh path work.
- `MatchupSchedulerTests` — existing window/active gate tests
  still pass (the gate stays as a fast-path filter).
- `PickemGroupWeekFactoryTests` — existing 8 cases, renamed
  after the `Create` rename.
- `GetSeasonWeeksByDateRangeQueryHandlerTests` (Producer side) —
  7 cases pinning overlap predicate, ordering, boundary
  inclusion, empty result, and bad-input rejection.

The detailed local E2E matrix below is the pre-merge regression
checklist for **PR-D** specifically — every scenario exercises
some combination of the four shipped PRs to confirm the
motivating bug is fully closed end-to-end.

---

## Local E2E test cases

Pre-merge regression matrix for PR-D. Each scenario exercises
the validator (PR-B), the eager-bootstrap dispatch (PR-B), the
factory (PR-A), the UI guards (PR-C), the scheduler gate (PR-D),
or some combination. Run them with a local stack:

### Setup checklist

- API, Producer, Provider, RabbitMQ, Postgres running locally
  (docker-compose or k8s — whatever your local convention is).
- Logged-in admin user (MLB league creation is admin-gated; see
  `LeagueCreatePage` / `create-league.tsx`).
- MLB current-week data sourced so `GetCurrentSeasonWeek`
  returns a real `CanonicalSeasonWeekDto`. Out-of-season MLB
  collapses several scenarios — confirm `seasons/current-week`
  on Producer returns 200 first.
- A Hangfire dashboard reachable for manually triggering
  `MatchupScheduler.ExecuteAsync` (don't wait for the cron).

### Verification queries

Keep these handy in a SQL session against the local Postgres:

```sql
-- Latest leagues plus their windows
SELECT "Id", "Name", "Sport", "StartsOn", "EndsOn",
       "DeactivatedUtc", "CreatedUtc"
FROM "PickemGroup"
ORDER BY "CreatedUtc" DESC
LIMIT 10;

-- PickemGroupWeek rows for a given league
SELECT pgw."Id", pgw."SeasonYear", pgw."SeasonWeek",
       pgw."SeasonWeekId", pgw."AreMatchupsGenerated",
       pgw."IsNonStandardWeek",
       (SELECT COUNT(*) FROM "PickemGroupMatchup" pgm
        WHERE pgm."GroupId" = pgw."GroupId"
          AND pgm."SeasonWeekId" = pgw."SeasonWeekId") AS matchup_count
FROM "PickemGroupWeek" pgw
WHERE pgw."GroupId" = '<league-id>';

-- /user/me payload for a logged-in user (substitute auth)
curl -s http://localhost:5xxx/api/users/me \
     -H "Authorization: Bearer <token>" | jq '.leagues'
```

### Scenario matrix

The PR-D redesign changed the eager-bootstrap behavior for windowed
leagues. Rather than re-enumerate the design space here, see the
[Possibility matrix](#possibility-matrix) section below for the
full 17-row matrix (shell creation × matchup generation × external
inputs). The local-E2E scenarios below are a representative subset:

| # | Scenario | Validator | Bootstrap (handler → processor) | Daily scheduler |
| --- | --- | --- | --- | --- |
| 1 | Future-start single-day league | accepts | shell + matchups for the future SeasonWeek that overlaps StartsOn | skip (gate) until window opens |
| 2 | Past-end league (admin/direct-API) | reject | (n/a) | (n/a) |
| 3 | Half-played single-day league | accepts | shell + matchups for current SeasonWeek; processor filters to in-window contests | in-window |
| 4 | Full-season league (no window) | accepts | shell + matchups for current SeasonWeek | advances weekly |
| 5 | Closed-window league | (was valid at create) | (was valid at create) | skip (gate) |
| 6 | Deactivated league | (n/a) | (n/a) | skip (gate) |
| 7 | Web UI date picker | rejects past in browser | — | — |
| 8 | Mobile UI date picker | rejects past in app | — | — |
| 9 | Mixed-window batch | varies | per-league orchestrator decides | only in-window processed |
| 10 | NCAAFB + `RankingFilter` shell before AP poll | accepts | shell only; matchups deferred (filter result is empty) | retries daily until poll lands |

### Detailed scenarios

#### Scenario 1 — Future-start single-day league (motivating bug)

**Setup:** create an MLB league via the web UI with `StartsOn =
today + 5 days, EndsOn = today + 5 days`. (Same shape works for
"tomorrow" — the load-bearing case the redesign was driven by.)

**Verify at creation:**
- API responds `201`.
- Seq has handler log line + `BootstrapLeagueMatchupsProcessor`
  log line `"Bootstrap for group {GroupId} resolved 1 SeasonWeek(s)"`.
- `PickemGroupWeek` for this `GroupId` returns **exactly one
  row** — the SeasonWeek whose `[StartDate, EndDate]` contains
  `today + 5 days` (typically same as the current MLB SeasonWeek
  in-season, sometimes the next one if the window straddles a
  Monday boundary).
- `/user/me` for the creator shows the league with that one
  week in `seasonWeeks`.

**Verify shortly after creation:**
- `PickemGroupMatchup` rows for that PickemGroupWeek match the
  MLB schedule for the league window — i.e., the games on
  `today + 5 days`. **Not** today's already-played games (the
  processor's `[StartsOn, EndsOn]` filter drops those).
- `AreMatchupsGenerated` is `true` for that week.

**Trigger daily scheduler** (Hangfire dashboard → run
`MatchupScheduler.ExecuteAsync`).

**Verify after scheduler:**
- No new `PickemGroupWeek` rows (the one created at bootstrap
  is the only valid one until the league window arrives at
  today; the gate skips out-of-window leagues, so the scheduler
  is a no-op here).

**What this proves:** the motivating bug is closed
end-to-end — bootstrap creates the **right** SeasonWeek shell
at creation (PR-D orchestrator), matchups populate
immediately because MLB schedules are sourced well in advance
(PR-D `AreMatchupsGenerated` rule confirms the populated state),
no orphan rows in `/user/me`.

#### Scenario 2 — Past-end league rejected at validator

**Setup:** craft a direct-API POST to
`/api/leagues/baseball-mlb` (admin token) with `StartsOn =
yesterday, EndsOn = yesterday`. The UI won't construct this —
PR-C's `min={today}` blocks it — so use `curl` / Postman to
bypass.

**Verify:**
- API responds `400` with `ResultStatus.Validation`.
- Error payload contains a `EndsOn` violation with message
  `"EndsOn can't be in the past."` (PR-B validator rule.)
- No `PickemGroup` row created.

**What this proves:** the server-side trust boundary holds even
when the UI guards are bypassed.

#### Scenario 3 — Half-played single-day league

**Setup:** create an MLB league via the UI with `StartsOn =
today (some hours ago)` and `EndsOn = today (end of day)`. Use
the date inputs — they should accept today.

**Verify at creation:**
- API responds `201`.
- One `PickemGroupWeek` row for this league at the current
  `SeasonWeekId`. (`BootstrapLeagueMatchupsProcessor` resolves
  the window to one overlapping SeasonWeek and enqueues a
  per-week `ScheduleGroupWeekMatchupsCommand`.)
- `ScheduleGroupWeekMatchupsCommand` enqueued.

**Verify after matchup generation completes:**
- `PickemGroupMatchup` rows exist only for contests with
  `StartDateUtc` between `StartsOn` and `EndsOn` (i.e., today's
  games). Morning games already played should still be present
  (locked picks) — afternoon games still pickable.
- `/user/me` shows the league with `seasonWeeks` containing the
  current week number.

**What this proves:** the validator allows mid-day windows
(the explicit "half-played single-day" carve-out from PR-B),
and the existing matchup processor's window filter handles
per-game pickability correctly.

#### Scenario 4 — Full-season league (no window)

**Setup:** create an MLB league via the UI with **Full Season**
selected (the duration mode that leaves `StartsOn` /
`EndsOn` null).

**Verify:**
- One `PickemGroupWeek` row for the current `SeasonWeekId`.
- Matchups populated.
- Scheduler picks the league up on subsequent runs and creates
  rows for newly-current weeks as the season progresses.

**What this proves:** the gate's `StartsOn == null || EndsOn ==
null` short-circuits do the right thing — full-season leagues
still flow through the existing happy path.

#### Scenario 5 — Closed-window league + scheduler skip

**Setup:** take a league from Scenario 3 or 4 (or create a
fresh one) and `UPDATE "PickemGroup" SET "EndsOn" = now() -
INTERVAL '1 day' WHERE "Id" = '<league-id>'` to simulate the
window closing.

**Trigger scheduler. Verify:**
- No new `PickemGroupWeek` rows added.
- Scheduler did NOT enqueue a `ScheduleGroupWeekMatchupsCommand`
  for this league.
- Already-existing `PickemGroupWeek` rows from the in-window
  period stay intact (the gate prevents *new* rows, doesn't
  delete old ones).

**What this proves:** PR-D's closed-window branch of the gate.

#### Scenario 6 — Deactivated league + scheduler skip

**Setup:** set an in-window league's `DeactivatedUtc` to
`now()`.

**Trigger scheduler. Verify:**
- No new `PickemGroupWeek` rows added.
- No matchup enqueue for this league.

**What this proves:** PR-D's `DeactivatedUtc` filter.

#### Scenario 7 — Web UI date picker guards

**Setup:** open the web app's Create League page in a browser.

**Verify:**
- The Start Date `<input type="date">` rejects yesterday in the
  native picker (`min` attribute).
- The End Date input rejects dates earlier than either today or
  the chosen Start Date, whichever is later.
- In DevTools, override the start input's value to
  `2020-01-01` programmatically and click submit → an alert
  fires *before* the confirmation dialog opens (`handleFormSubmit`
  guard).

**What this proves:** PR-C's web guards.

#### Scenario 8 — Mobile UI date picker guards

**Setup:** open the mobile app's Create League screen.

**Verify:**
- Both date pickers grey out days before today (`minimumDate`).
- If you set Start to a future date and then move it back, the
  End picker's floor follows (`max(startsOn, today)`).
- Attempting to submit with a manipulated state that yields
  `endsOn < today` surfaces the Zod `superRefine` error
  `"End date can't be in the past"`.

**What this proves:** PR-C's mobile guards + the Zod-level
trust boundary mirroring the server validator.

#### Scenario 9 — Mixed-window batch through scheduler

**Setup:** create three MLB leagues:
- A: `StartsOn = today - 7, EndsOn = today + 7` (in-window).
- B: `StartsOn = today + 10` (future-start).
- C: same as A but `UPDATE … SET "DeactivatedUtc" = now()`.

**At creation** (verify after each `POST`):
- A and B both get **one or more** `PickemGroupWeek` rows from
  the creation-time bootstrap (A's current SeasonWeek for A;
  the SeasonWeek containing `today + 10` for B).
- C gets the same as A (deactivation happens after creation).

**Trigger scheduler. Verify:**
- Exactly one `ScheduleGroupWeekMatchupsCommand` enqueued
  (for A, refreshing the current week).
- B's bootstrap-created week stays untouched (gate skips B
  because `StartsOn > now`).
- C's bootstrap-created week stays untouched (gate skips C
  because `DeactivatedUtc IS NOT NULL`).
- Seq log line `Found 1 active sport(s) with in-window
  leagues: BaseballMlb` (the per-sport discovery query also
  applies the window gate).

**What this proves:** creation-time bootstrap (PR-D
orchestrator) handles all three leagues correctly; the daily
scheduler's window/active gate filters per-league for the
*refresh* path.

#### Scenario 10 — NCAAFB + `RankingFilter` shell pre-poll

**Setup:** create an NCAAFB league with `RankingFilter =
AP_TOP_25` and `StartsOn = first Saturday of the 2026 season,
EndsOn = same day`. Pick a date that's before the preseason AP
poll release (typically mid-August).

**Verify at creation:**
- API responds `201`.
- One `PickemGroupWeek` row exists for the SeasonWeek
  containing the StartsOn date.
- `AreMatchupsGenerated` is **`false`** for that row (the
  processor ran, found schedule data for those games, but
  no ranks were set on any matchup → filter result is empty
  → flag stays `false` per the PR-D rule).
- Zero `PickemGroupMatchup` rows.

**Time-shift past the poll publication** (or manually source
poll data via the existing Producer poll-source flow).

**Trigger daily scheduler. Verify:**
- The scheduler finds the shell, sees `AreMatchupsGenerated =
  false`, enqueues `ScheduleGroupWeekMatchupsCommand`.
- The processor re-runs, now with ranks populated → filter
  result is non-empty → `PickemGroupMatchup` rows written →
  `AreMatchupsGenerated` flips to `true`.

**What this proves:** the eager-bootstrap path correctly defers
matchup materialization for NCAAFB+rank-filter shells until the
external input (AP poll) lands. The `AreMatchupsGenerated` rule
is what makes this work — without it the empty pre-poll run
would mark the shell "done" and the scheduler would never come
back.

### Prod-data cleanup checkpoint

After PR-D merges and is deployed, the existing orphan
`PickemGroupWeek` rows already in prod still need the manual
SQL cleanup documented in *Prod data remediation* above. The
gate prevents new orphans but doesn't reach back.

---

## Possibility matrix

Pre-implementation design-space enumeration for the PR-D redesign
(originally captured 2026-05-29). The conversation behind it
revealed that the "always look up current SeasonWeek and filter"
model is wrong by construction for windowed leagues, then further
refined by recognizing that NFL/NCAAFB/MLB schedules are published
*months* in advance, so the question isn't "is the date far
future" but "do we have the inputs the processor needs."

### Dimensions

- **Window shape**: `(StartsOn, EndsOn)` — neither / either / both.
- **Relative to "now"**: past, straddles, near-future (within current
  SeasonWeek), far-future (next SeasonWeek+), very-far-future
  (no SeasonWeek defined yet, e.g., next-year leagues).
- **Span**: zero-day, single-day, multi-day same week, multi-day
  cross-week, multi-week.
- **External inputs** (see appendix below): SeasonWeek availability,
  schedule availability, AP poll availability.

**Already gated by PR-B validator** (merged):

- `EffectiveEndsOn <= now` → rejected. No past-only windows.
- `StartsOn >= EffectiveEndsOn` → rejected. No zero-width windows.

Marked ⛔ in the table.

### Matrix

Two outputs per row — the **shell** (`PickemGroupWeek` row) and
the **matchups** (`PickemGroupMatchup` rows). They have different
gates: shells need only the SeasonWeek; matchups additionally need
schedule data and, for NCAAFB+RankingFilter, an AP poll.

| # | Description | Lookup | Shell at create | Matchups at create | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | Full-season, in-season | `GetCurrentSeasonWeek` | yes — current week | yes (or shell-only if NCAAFB+TOP25 and current week's poll not yet published — unlikely once in-season) | The original "happy path" the legacy code was built for. Scheduler advances weekly. |
| 2 | Full-season, between weeks (sport idle hours) | `GetCurrentSeasonWeek` | yes — if a week is current | yes | Transient `null` from the endpoint → throw + retry (PR-B behavior). |
| 3 | Full-season, off-season | `GetCurrentSeasonWeek` | defer | defer | `null` is permanent here; DLQ via retry policy. Commissioner re-creates when season starts. |
| 4 | Partial window, `StartsOn=null`, `EndsOn=future` | date-range or current | yes — all weeks `[now, EndsOn]` | yes where inputs available; shell-only elsewhere | Bootstrap current + future shells through EndsOn. Scheduler advances weekly within window. |
| 5 | Partial window, `StartsOn=past`, `EndsOn=null` | date-range or current | yes — current week | yes where inputs available | Already in-window; behaves like row 1 after creation. |
| 6 | Partial window, `StartsOn=future`, `EndsOn=null` | date-range | yes — all weeks `[StartsOn, cap]` | yes where inputs available; shell-only elsewhere | Caps at `StartsOn + 1 year` (DP-3). |
| 7 | Windowed, "now" inside `[StartsOn, EndsOn]` | date-range | yes — all overlapping weeks | yes where inputs available | Mid-window leagues. |
| 8 | Windowed single-day, tomorrow | date-range | yes — 1 week (current SeasonWeek) | yes — schedule known; shell-only if NCAAFB+TOP25 and poll for current week somehow not yet up | **Motivating bug**. Tomorrow is inside the current SeasonWeek. |
| 9 | Windowed single-day, next Tuesday | date-range | yes — 1 week (next SeasonWeek) | yes — schedule known; shell-only if NCAAFB+TOP25 and poll for that week not yet published | Current code would have created the wrong week. |
| 10 | Windowed single-day, next month | date-range | yes — 1 week (the future SeasonWeek) | yes — schedule known; shell-only if NCAAFB+TOP25 and poll for that week not yet published | Same shape, further out. |
| 11 | Windowed single-day, far enough out that the SeasonWeek isn't in the DB yet (next-year league) | date-range | defer (empty result) | defer | Only true "defer" case. Scheduler will pick up once the SeasonWeek is sourced. |
| 12 | Windowed multi-day, spans 2 SeasonWeeks | date-range | yes — 2 shells | yes where inputs available per week; shell-only elsewhere | One enqueued `ScheduleGroupWeekMatchupsCommand` per week. |
| 13 | Windowed multi-week, spans N SeasonWeeks | date-range | yes — N shells | yes where inputs available per week; shell-only elsewhere | Scales linearly. Realistic multi-week NCAAFB+TOP25 windows would have early weeks waiting on per-week polls. |
| 14 | Windowed half-played (today morning → today EoD) | date-range or current | yes — 1 week | yes — schedule known | Carve-out from PR-B docs; existing pick-lock handles played games. |
| 15 | Windowed past-end (EndsOn < now) | (n/a) | ⛔ | ⛔ | Validator blocks. |
| 16 | Windowed zero-width (StartsOn = EndsOn instant) | (n/a) | ⛔ | ⛔ | Validator blocks. |
| 17 | Week-range mode | (n/a) | ⛔ | ⛔ | Dead UI; `finalizeLeagueCreation` short-circuits. Not part of this PR. |

### Source-of-truth choice

Once we accept "shell whenever the SeasonWeek exists," every row
collapses to one of:

- **Current week only** → rows 1, 2, 14 (and degenerate row 8). Uses
  `GetCurrentSeasonWeek`.
- **Date range** → rows 4, 5, 6, 7, 8, 9, 10, 11, 12, 13. Uses the
  new `GetSeasonWeeksOverlapping(from, to)` endpoint.

Cleanest dispatch: **windowed (either bound set) → date-range;
otherwise (both null) → current week.**

### External inputs and their gating

A shell needs:

- **The `SeasonWeek` row exists in canonical DB.** True for the
  current season year as soon as ESPN's season calendar is sourced.
  False for next-year leagues (row 11).

Matchups additionally need:

- **Schedule data for the SeasonWeek.** Sourced from ESPN as soon as
  the league publishes its schedule:
  - NFL: schedule released mid-May.
  - NCAAFB: schedule mostly set by Feb–March.
  - MLB: full season released in late September of the prior year.
  - So in practice: **if the SeasonWeek exists, schedule data
    exists too.**
- **AP poll for that SeasonWeek** (only NCAAFB + `RankingFilter`).
  Polls release Sunday afternoons in-season; preseason AP poll
  drops ~mid-August. Outside those windows the filter has nothing
  to apply against.

This is what makes the "shell-only" entries in the matrix:
shell-yes, matchups-deferred because the poll the rank filter
depends on hasn't dropped yet.

### Processor change: `AreMatchupsGenerated` rule

Today the processor unconditionally sets
`groupWeek.AreMatchupsGenerated = true` even when zero matchups
survived the filter. For the legacy Sunday-cron flow that was fine —
the cron only ever ran *after* the AP poll dropped, so empty results
meant "legitimately no qualifying games" and marking them generated
was correct.

For eager bootstrap pre-poll that rule is wrong: a Week-1 shell
created in May would get matchup-gen run against it once
(scheduler's first daily pass), the poll wouldn't be out yet, zero
matchups would result, and the shell would be marked "generated and
done." It would never be reconsidered, even after the August poll
drops.

**New rule:** mark `AreMatchupsGenerated = true` **only when
`groupMatchups.Count > 0`.** Empty result → leave `false` so the
scheduler re-fires matchup generation on the next daily pass.

**Cost:** for a true-empty week (rare bye-week situation in a
full-season league) the scheduler will re-enqueue daily until the
week is no longer current. Each re-enqueue is a contest-client call
+ filter + 0-write SaveChanges. Small. Acceptable.

**Optimization (deferred):** a short-circuit at the top of the
processor that checks "does an AP poll exist for this (sport,
year, week)?" via a Poll table query and bails fast when it
doesn't. Saves the contest-client roundtrip on doomed-pre-poll
runs. Not required for correctness; defer to PR-E or later.

### UX: shell-without-matchups placeholder (proposed follow-up)

> **Status:** not part of PR-D. PR-D ships backend only — no UI
> changes. This section is a sketch of the UX direction we'd want
> *if* and when we wire a placeholder for the shell-without-matchups
> state. Pinned here so the design space is captured alongside the
> backend behavior that creates it.

**The state in question:** a `PickemGroupWeek` row that exists but
has zero `PickemGroupMatchup` rows. Today's UI surfaces this as a
blank body — `MatchupList.jsx` (web) maps `matchups` after loading
and has no empty-state branch of its own; `picks.tsx` (mobile)
renders a `ListEmptyComponent` with title `"No games this week"`.

**Direction (option c from the discussion):** show a placeholder
indicating *when* picks for that week will become available,
rather than the current "no games" framing. The exact copy and
date-source is a UX call that hasn't been made — the discussion
mentioned strings like *"Week N picks available on X"* as a
sketch, not a decision.

**Trigger conditions** (still under-specified):
- NCAAFB + `RankingFilter`, preseason: AP poll release date if
  known.
- NCAAFB + `RankingFilter`, in-season: Sunday afternoon after the
  prior week's games close.
- Other sports / non-rank-filter leagues: don't hit this state
  today and may not need the placeholder at all.

**Implementation sketch** (when it's wired):
- Web: add an empty-state branch in `MatchupList.jsx` keyed on
  `matchups.length === 0` plus a future-week check.
- Mobile: replace the `"No games this week"` title in the picks
  tab's `ListEmptyComponent` with the placeholder copy when the
  same conditions hold.
- API: `/user/me` already exposes the week shell via
  `seasonWeeks` — client decides placeholder based on matchup
  count + sport.

Bullets above are starting points, not committed copy. A real
follow-up PR would resolve the exact wording, the date-source for
"available on X" (poll-publication date isn't in the API contract
today), and whether to extend this to non-NCAAFB use cases.

### Decision points

Decisions locked in before code. Each is annotated with the lean
and the tradeoff.

#### DP-1 — Always use date-range when any window value is set?

Rows 4/5/8/14 could technically use `GetCurrentSeasonWeek` (cheaper
single call) but unifying around "windowed → date-range; full-season
→ current" is tidier.

- **Lean:** yes, always date-range for windowed.

#### DP-2 — Row 11 empty result (no SeasonWeek in DB)

Empty result from the date-range endpoint → log + return; scheduler
catches up when the SeasonWeek lands.

- **Lean:** yes, treat empty as deferred.

#### DP-3 — Partial window with `EndsOn = null` upper bound

If `StartsOn = future date` and `EndsOn = null`, we need a `to`
bound. Cap at `now + 365d` on the API side as a defensive bound.

- **Lean:** yes, 365-day cap.

#### DP-4 — Multi-week bootstrap fan-out

At creation time, enqueue N matchup-schedule jobs (one per
overlapping SeasonWeek) rather than batching.

- **Lean:** N independent jobs (existing command shape, Hangfire
  handles concurrency).

#### DP-5 — Full-season lookahead

Should the full-season handler eagerly bootstrap the next week (or
two) so picks are visible early?

- **Lean:** no. Current only; scheduler advances weekly. Adding
  lookahead is scope creep.

### Operational considerations

#### Idempotency / outbox replay

`PickemGroupCreated` is at-least-once. The handler must be safe
to re-fire. Today's "find existing PickemGroupWeek by SeasonWeekId,
else create" covers single-week. Multi-week version applies the
same per-week check. The composite PK `(GroupId, SeasonWeekId)`
enforces DB-layer dedup.

The matchup-schedule command is gated by
`!groupWeek.AreMatchupsGenerated` — a replay finds the shell, sees
the flag, and skips re-enqueue. With the new "only mark-generated
when populated" rule, a replay that hits an empty week would
re-enqueue, which is fine (processor produces same result).

#### Partial-bootstrap failure

When N matchup-schedule jobs are enqueued (rows 12, 13), one can
fail. Hangfire retry handles per-job; the league lands in a
half-populated state during retry. Acceptable.

#### Multi-week UX on day one

A 4-week windowed league created today shows 4 entries in
`/user/me`'s `seasonWeeks` immediately. PicksPage / picks tab
default to "latest" week (`seasonWeeks[seasonWeeks.length - 1]`).
For a far-future league the user would land on the *furthest*
week and see the "picks available on X" placeholder. Probably
OK — the default-to-latest matches what they'd want once games
are happening, even if it's a placeholder on day one. **Worth
calling out** in case you'd rather change the default to
"current SeasonWeek if in window, else earliest."

### Open question — factory rename

`PickemGroupWeekFactory.CreateForCurrentWeek(group, week)` was
named when the only call site was the eager-bootstrap-of-current
path. After the redesign it creates a `PickemGroupWeek` for *any*
`CanonicalSeasonWeekDto`. Rename to
`PickemGroupWeekFactory.Create(group, week)`? Touches two call
sites + factory tests. Cosmetic but worth doing while in the file.

- **Lean:** yes, rename. (Locked in as part of PR-D.)

### What this changes (implementation outline, post-decisions)

**Producer (new endpoint):**
- `GET /api/seasons/weeks/by-date-range?from={iso}&to={iso}`
- `GetSeasonWeeksByDateRangeQuery` + handler
- SQL overlap predicate: `sw.StartDate <= @to AND sw.EndDate >= @from`,
  ordered by `StartDate`.
- Registered alongside existing season query handlers.

**Core (`SeasonClient`):**
- New `Task<Result<List<CanonicalSeasonWeekDto>>>
       GetSeasonWeeksOverlapping(DateTime from, DateTime to,
                                  CancellationToken ct = default)`
- Routed via the existing sport-resolved factory.

**API (`PickemGroupCreatedHandler`):**
- Slim MassTransit consumer. Performs a single existence check on
  the `PickemGroup` (permanent log + return if missing) and
  enqueues exactly one `BootstrapLeagueMatchupsCommand` via
  Hangfire. No HTTP calls, no SeasonWeek lookup, no
  `PickemGroupWeek` writes. Stays as a fan-out point for future
  creation-time side-effects (invitations, notifications, etc.).
  All dispatch logic moved to the orchestrator below.

**API (`BootstrapLeagueMatchupsProcessor`, new — Hangfire,
`IBootstrapLeagueMatchups`):**
- Owns the windowed-vs-full-season branch: any window bound set →
  date-range path; otherwise → current-week.
- Date-range path: `from = StartsOn ?? now`, `to = EndsOn ??
  from + 365d` (DP-3 cap). Inverted result (`from > to`) → permanent
  error log + return empty (PR-B validator blocks creation-time
  inversion, but post-creation clock drift or admin edits can
  still surface it; retrying won't fix it).
- Calls `SeasonClient.GetSeasonWeeksOverlapping(from, to)` —
  empty result → log + return (row 11); transient endpoint
  failure → throw (Hangfire retries).
- For each resolved SeasonWeek, enqueues a per-week
  `ScheduleGroupWeekMatchupsCommand` carrying the league id +
  week ids + correlation id. Per-week shell creation (find-or-
  create via `PickemGroupWeekFactory.Create`) happens inside the
  existing `MatchupScheduleProcessor` — single owner of per-week
  work, called from both this orchestrator and the daily
  `MatchupScheduler`.
- Idempotent against replay: the per-week processor's
  find-or-create on the composite PK `(GroupId, SeasonWeekId)`
  plus its `!AreMatchupsGenerated` gate handles the dedup.

**API (`MatchupScheduleProcessor`):**
- One-line rule change: `groupWeek.AreMatchupsGenerated = true`
  becomes `groupWeek.AreMatchupsGenerated = groupMatchups.Count > 0;`.
- (Optional / deferred) early-out when AP poll for this week
  doesn't exist yet — query `Poll` table, log + return.

**API (`MatchupScheduler`):** unchanged. Existing "find current
week shell, enqueue if !AreMatchupsGenerated" logic already
implements the refresh path the eager-bootstrap needs.

**Factory:** rename `CreateForCurrentWeek` → `Create`. Two call
sites + tests.

**UI (web + mobile, separate task):** placeholder copy for
shell-without-matchups state. Out of scope for PR-D backend but
listed for tracking.

**Tests:**
- New Producer handler tests for the date-range query (overlap
  predicate, ordering, empty result).
- Rewritten API handler tests covering rows 1 (full-season), 7
  (mid-window), 9 (next-week single-day), 11 (no SeasonWeek), 12
  (multi-week).
- Processor: new test for the `AreMatchupsGenerated` rule (empty
  result → flag stays false).
- Existing scheduler tests still pass.
