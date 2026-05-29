# League Creation Possibility Matrix (2026-05-29)

Pre-implementation review doc for the PR-D redesign — the
conversation reveal that the "always look up current SeasonWeek and
filter" model is wrong by construction for windowed leagues, then
further refined by recognizing that NFL/NCAAFB/MLB schedules are
published *months* in advance, so the question isn't "is the date far
future" but "do we have the inputs the processor needs."

Working alongside `docs/league-creation-hardening.md` (the parent
plan). This doc is the design-space enumeration that drives the
PR-D code.

---

## Dimensions

- **Window shape**: `(StartsOn, EndsOn)` — neither / either / both.
- **Relative to "now"**: past, straddles, near-future (within current
  SeasonWeek), far-future (next SeasonWeek+), very-far-future
  (no SeasonWeek defined yet, e.g., next-year leagues).
- **Span**: zero-day, single-day, multi-day same week, multi-day
  cross-week, multi-week.
- **External inputs** (see appendix): SeasonWeek availability,
  schedule availability, AP poll availability.

**Already gated by PR-B validator** (merged):

- `EffectiveEndsOn <= now` → rejected. No past-only windows.
- `StartsOn >= EffectiveEndsOn` → rejected. No zero-width windows.

Marked ⛔ in the table.

---

## Matrix

Two outputs per row now — the **shell** (`PickemGroupWeek` row) and
the **matchups** (`PickemGroupMatchup` rows). They have different
gates: shells need only the SeasonWeek; matchups additionally need
schedule data and, for NCAAFB+RankingFilter, an AP poll.

| # | Description | Lookup | Shell at create | Matchups at create | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | Full-season, in-season | `GetCurrentSeasonWeek` | ✅ current week | ✅ (or 🟡 if NCAAFB+TOP25 and current week's poll not yet published — unlikely once in-season) | The original "happy path" the legacy code was built for. Scheduler advances weekly. |
| 2 | Full-season, between weeks (sport idle hours) | `GetCurrentSeasonWeek` | ✅ if a week is current | ✅ | Transient `null` from the endpoint → throw + retry (PR-B behavior). |
| 3 | Full-season, off-season | `GetCurrentSeasonWeek` | ❌ defer | ❌ defer | `null` is permanent here; DLQ via retry policy. Commissioner re-creates when season starts. |
| 4 | Partial window, `StartsOn=null`, `EndsOn=future` | date-range or current | ✅ all weeks `[now, EndsOn]` | ✅ where inputs available; 🟡 elsewhere | Bootstrap current + future shells through EndsOn. Scheduler advances weekly within window. |
| 5 | Partial window, `StartsOn=past`, `EndsOn=null` | date-range or current | ✅ current week | ✅ where inputs available | Already in-window; behaves like row 1 after creation. |
| 6 | Partial window, `StartsOn=future`, `EndsOn=null` | date-range | ✅ all weeks `[StartsOn, cap]` | ✅ where inputs available; 🟡 elsewhere | Caps at `StartsOn + 1 year` (DP-3). |
| 7 | Windowed, "now" inside `[StartsOn, EndsOn]` | date-range | ✅ all overlapping weeks | ✅ where inputs available | Mid-window leagues. |
| 8 | Windowed single-day, tomorrow | date-range | ✅ 1 week (current SeasonWeek) | ✅ schedule known; 🟡 if NCAAFB+TOP25 and poll for current week somehow not yet up | **Motivating bug**. Tomorrow is inside the current SeasonWeek. |
| 9 | Windowed single-day, next Tuesday | date-range | ✅ 1 week (next SeasonWeek) | ✅ schedule known; 🟡 if NCAAFB+TOP25 and poll for that week not yet published | Current code would have created the wrong week. |
| 10 | Windowed single-day, next month | date-range | ✅ 1 week (the future SeasonWeek) | ✅ schedule known; 🟡 if NCAAFB+TOP25 and poll for that week not yet published | Same shape, further out. |
| 11 | Windowed single-day, far enough out that the SeasonWeek isn't in the DB yet (next-year league) | date-range | ❌ defer (empty result) | ❌ defer | Only true "defer" case. Scheduler will pick up once the SeasonWeek is sourced. |
| 12 | Windowed multi-day, spans 2 SeasonWeeks | date-range | ✅ 2 shells | ✅ where inputs available per week; 🟡 elsewhere | One enqueued `ScheduleGroupWeekMatchupsCommand` per week. |
| 13 | Windowed multi-week, spans N SeasonWeeks | date-range | ✅ N shells | ✅ where inputs available per week; 🟡 elsewhere | Scales linearly. Realistic multi-week NCAAFB+TOP25 windows would have early weeks waiting on per-week polls. |
| 14 | Windowed half-played (today morning → today EoD) | date-range or current | ✅ 1 week | ✅ schedule known | Carve-out from PR-B docs; existing pick-lock handles played games. |
| 15 | Windowed past-end (EndsOn < now) | (n/a) | ⛔ | ⛔ | Validator blocks. |
| 16 | Windowed zero-width (StartsOn = EndsOn instant) | (n/a) | ⛔ | ⛔ | Validator blocks. |
| 17 | Week-range mode | (n/a) | ⛔ | ⛔ | Dead UI; `finalizeLeagueCreation` short-circuits. Not part of this PR. |

**Legend**: ✅ produce immediately • ❌ skip / defer • 🟡 shell only, matchups deferred until input lands (existing scheduler refresh path picks them up)

---

## Source-of-truth choice

Once we accept "shell whenever the SeasonWeek exists," every row
collapses to one of:

- **Current week only** → rows 1, 2, 14 (and degenerate row 8). Uses
  `GetCurrentSeasonWeek`.
- **Date range** → rows 4, 5, 6, 7, 8, 9, 10, 11, 12, 13. Uses the
  new `GetSeasonWeeksOverlapping(from, to)` endpoint.

Cleanest dispatch: **windowed (either bound set) → date-range;
otherwise (both null) → current week.**

---

## External inputs and their gating

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

This is what makes 🟡 in the matrix: shell-yes, matchups-deferred
because the poll the rank filter depends on hasn't dropped yet.

---

## Processor change: `AreMatchupsGenerated` rule

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

---

## UX: shell-without-matchups placeholder (proposed follow-up)

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

---

## Decision points

Decisions to lock in before code. Each is annotated with my lean
and the tradeoff.

### DP-1 — Always use date-range when any window value is set?

Rows 4/5/8/14 could technically use `GetCurrentSeasonWeek` (cheaper
single call) but unifying around "windowed → date-range; full-season
→ current" is tidier.

- **Lean:** yes, always date-range for windowed.

### DP-2 — Row 11 empty result (no SeasonWeek in DB)

Empty result from the date-range endpoint → log + return; scheduler
catches up when the SeasonWeek lands.

- **Lean:** yes, treat empty as deferred.

### DP-3 — Partial window with `EndsOn = null` upper bound

If `StartsOn = future date` and `EndsOn = null`, we need a `to`
bound. Cap at `now + 365d` on the API side as a defensive bound.

- **Lean:** yes, 365-day cap.

### DP-4 — Multi-week bootstrap fan-out

At creation time, enqueue N matchup-schedule jobs (one per
overlapping SeasonWeek) rather than batching.

- **Lean:** N independent jobs (existing command shape, Hangfire
  handles concurrency).

### DP-5 — Full-season lookahead

Should the full-season handler eagerly bootstrap the next week (or
two) so picks are visible early?

- **Lean:** no. Current only; scheduler advances weekly. Adding
  lookahead is scope creep.

---

## Operational considerations

### Idempotency / outbox replay

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

### Partial-bootstrap failure

When N matchup-schedule jobs are enqueued (rows 12, 13), one can
fail. Hangfire retry handles per-job; the league lands in a
half-populated state during retry. Acceptable.

### Multi-week UX on day one

A 4-week windowed league created today shows 4 entries in
`/user/me`'s `seasonWeeks` immediately. PicksPage / picks tab
default to "latest" week (`seasonWeeks[seasonWeeks.length - 1]`).
For a far-future league the user would land on the *furthest*
week and see the "picks available on X" placeholder. Probably
OK — the default-to-latest matches what they'd want once games
are happening, even if it's a placeholder on day one. **Worth
calling out** in case you'd rather change the default to
"current SeasonWeek if in window, else earliest."

---

## Open question — factory rename

`PickemGroupWeekFactory.CreateForCurrentWeek(group, week)` was
named when the only call site was the eager-bootstrap-of-current
path. After the redesign it creates a `PickemGroupWeek` for *any*
`CanonicalSeasonWeekDto`. Rename to
`PickemGroupWeekFactory.Create(group, week)`? Touches two call
sites + factory tests. Cosmetic but worth doing while in the file.

- **Lean:** yes, rename.

---

## What this changes (implementation outline, post-decisions)

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

**Doc updates:**
- `docs/league-creation-hardening.md` PR-D plan section updated to
  reflect the redesign actually shipping.
- This matrix becomes a permanent reference (not deleted
  post-merge) so future readers see the decision space.
