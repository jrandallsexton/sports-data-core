# League Creation Hardening (2026-05-28)

Tighten the league-creation eager-bootstrap path so leagues with a
non-trivial `[StartsOn, EndsOn]` window don't silently mis-populate
their first `PickemGroupWeek`. Surfaced while testing MLB league
creation: a league with `StartsOn = today + 10 days` produces an
orphan empty `PickemGroupWeek` row pointing at the *current* week
(which has nothing to do with the league).

**Motivating bug.** Single-day MLB leagues (`StartsOn = EndsOn = some
future date`) end up with **two** `PickemGroupWeek` rows in prod —
the orphan empty one created at league-creation time, and the real
one created days later by `MatchupScheduler` once the current
`SeasonWeek` window reaches `StartsOn`. `/user/me` projects
`seasonWeeks` straight off `PickemGroupWeek.SeasonWeek` values, so
both show up in the league's ascending week list. The PicksPage UI
defaults to the first week → zero matchups → reads as broken.

**Status:** drafted 2026-05-28. Partially executed — three of four
PRs:

- **PR-A** (factory extraction) — **merged** in #372.
- **PR-C** (UI date-picker guards on web + mobile) — **merged** in
  #373. Note: originally listed third in the plan; reordered to
  ship in parallel with PR-A since it's independent of all
  backend work.
- **PR-B** (server validator + bootstrap-mode dispatch) — **in
  flight** as the PR that introduces this doc.
- **PR-D** (`MatchupScheduler` window gate + handler base
  extraction) — **not started**. The motivating bug (single-day
  leagues producing two `PickemGroupWeek` rows in `/user/me`) is
  only fully closed once PR-D lands; PR-B alone removes the
  eager-bootstrap orphan but leaves the daily-scheduler orphan
  intact.

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
| Season-long, created mid-season | ✅ Works as designed. First week populates immediately. |
| Future-start (`StartsOn = today + 10`) | ⚠️ Orphan empty `PickemGroupWeek` row at *today's* week. No matchups (processor's window filter drops them). No event published. Daily scheduler creates a fresh orphan empty week every day until `StartsOn` lands. Eventually self-corrects when current-week ≥ `StartsOn`. |
| Past-only backfill (window entirely in past) | ❌ Should never have been accepted in the first place. No validator rule prevents it today. PR-B adds one. |
| Non-current `SeasonYear` | ❌ Same — no validator rule today. PR-B adds one. Handler currently ignores `SeasonYear` and uses real-time current week regardless. |
| Created during postseason | ⚠️ Handler doesn't adjust `SeasonWeek` ordinal for postseason or set `IsNonStandardWeek`. Daily scheduler does both (`MatchupScheduler:101-117`). Two creation sites have drifted. |

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

After the validator rejects past / wrong-year inputs, the
handler's mode dispatch shrinks to two:

| Mode | When | Action |
| --- | --- | --- |
| `Immediate` | `(StartsOn ?? -∞) <= now`. | Bootstrap *current week* (today's behavior, narrowed). |
| `Future` | `StartsOn > now`. | **No-op.** Don't create any `PickemGroupWeek`. Daily scheduler will pick it up once `now >= StartsOn`. |

`Immediate` fires for:
- Full-season leagues (`StartsOn` null).
- Today-or-earlier-start leagues whose window still extends into
  the future (the validator allows these; the half-played
  single-day case fits here).
- Leagues whose `StartsOn` was just-barely-future at validation
  time but the event-handler clock has since rolled past.

`Future` fires for leagues whose `StartsOn` is still strictly
in the future at consume time. The daily scheduler picks them
up when the calendar reaches `StartsOn`.

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

4. **PR-D: `MatchupScheduler` window gate + handler base
   extraction.** Add the `[StartsOn, EndsOn]` and `DeactivatedUtc`
   gates in the scheduler. Collapse the three sport-specific
   create-league handlers into a shared base. Wire `MaxUsers` /
   `NonStandardWeekGroupSeasonMapFilter` through.

Dependency graph: PR-A blocks PR-B and PR-D (both use the
factory). PR-B and PR-C are independent. PR-D depends only on
PR-A. Sensible order: A → C in parallel with B → D.

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
  holes. PR-C's base extraction fixes all three at once. Don't
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

For PR-B in particular, the bootstrap-mode dispatch is the
behavioral change that has to be exercised. Test plan:

- Unit tests on `PickemGroupCreatedHandler` covering each mode
  (`Immediate` / `Future` / `Past` / `WrongYear`), asserting
  whether `PickemGroupWeek` was created and whether
  `ScheduleGroupWeekMatchupsCommand` was enqueued.
- Local E2E: create an MLB league with `StartsOn = today + 10`,
  confirm no `PickemGroupWeek` rows materialize. Re-run the
  daily scheduler at `StartsOn + 1`, confirm a row appears.
- Local E2E: create an MLB league with `StartsOn = today`,
  confirm the current-week row appears and matchups populate
  (current happy-path regression check).

For PR-C, the scheduler window gate: spin up two leagues — one
future-start, one current — let the scheduler run, confirm only
the current-start league gets a new `PickemGroupWeek` row.
