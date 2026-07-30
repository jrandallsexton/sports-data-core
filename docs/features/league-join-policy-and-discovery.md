# League Join Policy + Public League Discovery

Status: **Phases 1-2 (API + web) implemented — awaiting local E2E + migration validation; Phase 3 (mobile) not started**
Date: 2026-07-30
Surfaces: SportsData.Api, sd-ui, sd-mobile
Driver: public leagues must be browsable/joinable to drive home-page content;
single-day MLB test leagues surfaced the "when does a league close?" question.

## Decisions (settled with the operator, 2026-07-30)

1. **The commissioner decides, per league, at creation.** No platform-wide
   default behavior — join policy is an explicit choice on the create form,
   alongside pick type and visibility. Post-creation editing is DEFERRED —
   no update-league command exists (league settings are create-only today);
   see the Phase 1 notes below.
2. **Exactly two options.** Deliberately no custom date in v1:
   - `Open` — joinable while the league is live
   - `CloseAtFirstGame` — closed once the league's first contest starts
3. **Applies to ALL leagues, not just public ones.** Visibility (`IsPublic`)
   controls who can *find* a league; join policy controls until when anyone can
   *enter* it. Both public joins and invite-link joins flow through
   `JoinLeagueCommandHandler`, so one check covers both — and shared invite
   links therefore expire naturally when the league closes. That was the
   operator's instinct ("if the commissioner sends an invite, it should expire
   at a certain time") and it is the zero-extra-work outcome.
4. **No "close now" button.** Once policy editing ships (deferred with the
   settings-edit feature), flipping `Open` → `CloseAtFirstGame` after the
   first game has started closes the league immediately; flipping back
   re-opens it. Two enum values give close-now and re-open as consequences,
   not features.

## Current state (verified 2026-07-30)

- `GetPublicLeaguesQueryHandler` returns every `IsPublic` league the caller
  isn't in — **including deactivated ones**; no sport/season filter; DTO lacks
  `memberCount`, sport, league, seasonYear, window info.
- Web `LeagueDiscoverPage` exists and works (Join → `/app/join/{id}` →
  `AutoJoinRedirect`). Mobile has **no** discovery surface — only the
  invite deep-link preview screen.
- `JoinLeagueCommandHandler` checks: league exists, not already a member.
  **A deactivated league is joinable today — straight bug**, fixed here
  regardless of policy work.
- `PickemGroup` has `StartsOn`/`EndsOn` (contest window), `DeactivatedUtc`,
  `IsPublic`. No joinability concept.
- The IDOR work (#572) already gives non-members a tiered league-detail
  preview (settings + `MemberCount`, roster withheld) — the browse card's
  data needs are mostly served.

## Semantics

**`CloseAtFirstGame`** — closed when `min(StartDateUtc)` over the league's
`PickemGroupMatchup` rows is in the past. Computed **at join time**, never
stored: the slate builds asynchronously after league creation (see
league-slate-async quirk), so a stored close date computed at creation would
be wrong/absent. Empty slate → league is open (nothing has started).

**`Open`** — joinable until `DeactivatedUtc` is set. NOT
implemented as `max(StartDateUtc) < now`: weekly slates build progressively,
so the max over *existing* matchups would wrongly close a football league in
the gap between week N's last kickoff and week N+1's slate build.
Known accepted wrinkle: a single-day league remains technically joinable
between its last first-pitch and its deactivation sweep (an evening,
roughly), during which a joiner has nothing left to pick. Harmless; not worth
machinery in v1.

**Deactivated → never joinable**, under any policy.

### Considered and rejected: storing the close time as a DateTime

The operator raised storing `JoinDeadlineUtc` directly (backfilled by an event
when matchups generate) — cleaner to read, one column to compare. Rejected for
v1 because it is a standing consistency job, not a one-time backfill:

- **Kickoff times move.** `ContestStartTimeUpdated` exists because ESPN
  reshuffles start times routinely; a stored deadline derived from first-game
  time silently drifts unless every reschedule also resyncs it. Same failure
  class as the countdown-anchoring bug: precomputed time values rot, derived
  ones cannot.
- **`Open` has no storable value.** Weekly slates build
  progressively; the "last game time" is unknowable until season end. The
  column would hold null-meaning-open — the enum again, hidden in a nullable.

The read-side cleanliness is kept anyway: DTOs expose a **computed
`closesAtUtc`** (`CloseAtFirstGame` → `min(matchup.StartDateUtc)` at read
time; `Open` → null). Clients render a concrete timestamp;
nothing is stored that can go stale.

If commissioner-set custom close dates ever ship (cut from v1), those are
*authored* values, not derived — a nullable `JoinDeadlineUtc` column arrives
naturally then, alongside the enum, with no rework of this design.

### Do the two options make sense for season-long leagues?

Raised by the operator: `CloseAtFirstGame` reads single-day-shaped — for a
season league, is the cutoff week 1? week 2? Resolution: the option's real
semantic is **"roster locks when play begins"**, which is shape-independent:

- Single-day league → closes at first pitch (everyone picks the same slate)
- Season league → closes at the first game inside the league's window
  (everyone competes over the identical season — the competitive-purity pool)
- Custom-window league → `min()` over its matchups is already window-scoped

The middle ground — "open through week N, then lock" — is the deferred custom
deadline in week clothing. Deliberately not built speculatively; if beta
leagues ask for week-N cutoffs, that is the signal.

**The leading v2 candidate (operator insight, 2026-07-30): derive the window
from `DropLowWeeksCount`.** A league that drops its 3 lowest weeks can absorb
a joiner through week 3 at zero competitive penalty — the weeks they missed
are exactly the weeks the scoring already discards. So a third enum value
(`OpenThroughDroppedWeeks`: closed once week `DropLowWeeksCount` completes)
is derived-not-authored, consistent with the no-stored-DateTime doctrine, and
needs NO new data captured today: `JoinPolicy` stores as an int (house style —
`HasConversion<int>` like the other league enums; a new enum value is a code
change, no migration) and `DropLowWeeksCount` already exists on
`PickemGroup`. v1 proceeds with the two options knowing this slots in clean.

## Implementation plan

### Phase 1 — API

1. `JoinPolicy` enum (int-stored via `HasConversion<int>`, house style) +
   `PickemGroup.JoinPolicy`, migration backfilling `Open` (today's de-facto
   behavior — existing MLB test leagues unchanged).
2. `JoinLeagueCommandHandler`: reject deactivated (bug fix) and
   policy-closed joins with human-readable `Validation` failures ("This
   league closed when its first game started.").
3. Creation commands (all three sports) + validators accept `JoinPolicy` —
   DONE via the shared `CreateLeagueRequestBase`/handler base (optional
   string, absent → `Open`, so pre-existing clients are unaffected).
   **Commissioner edit is deferred**: no update-league command exists at all
   today (league settings are create-only), so policy editing is part of a
   future league-settings-edit feature, not a rider here.
4. `GetPublicLeaguesQueryHandler`: exclude deactivated; compute
   `IsJoinable`/`ClosesAtFirstGame`; fatten `PublicLeagueDto` (sport, league,
   seasonYear, memberCount, window label). ~~Filter or badge closed leagues —
   badge, so a nearly-started league still advertises itself.~~ **Superseded
   in v2**: closed leagues are FILTERED from browse. Badging made sense when
   "closed" meant "kickoff just passed"; once stored expiries made
   ended-but-not-yet-deactivated leagues evaluate as closed (observed
   immediately against restored prod data), badges filled the page with
   unjoinable rows. Browse answers "what can I join?"; the countdown conveys
   urgency for soon-closing leagues.
5. `LeagueDetailDto`: add `joinPolicy` + `isJoinable` so the invite preview
   and browse detail can render truthfully.
6. Unit tests per guarded path (policy × deactivation × slate-empty).

### Phase 2 — Web (first client, per operator lean)

1. Create flow: "Who can join, and until when?" — two radios.
2. League settings: same control, commissioner-only, hidden when past.
3. Home page: "Leagues you can join" rail fed by the fattened query — the
   content-gap driver for this feature.
4. ~~`LeagueDiscoverPage`: closed badges, join CTA disabled when
   unjoinable.~~ Superseded in v2 — browse FILTERS closed leagues (see the
   Phase-1 item-4 note); the closed rendering survives client-side only as a
   defensive path for a league expiring between fetch and render.
5. Invite affordances hidden when league is closed (mirrors `isPast`
   treatment).

### Phase 3 — Mobile (own PR, ships via EAS) — DELIBERATELY HELD

Operator call (2026-07-30): web ships first and gets production soak time
before mobile parity begins. The join/discovery UX is still being shaped by
hands-on use (the v2 revision itself came out of one deploy's soak); starting
mobile now risks paying every subsequent design change twice. Parity starts
when the operator calls the web design settled.


1. Browse screen (new leagues rail on home + full list).
2. Invite preview screen: closed state ("This league is no longer accepting
   members.") instead of a Join button that 400s.
3. League detail: joinability surfaced; invite card hidden when closed.

## Out of scope (v1)

- Custom close dates (third enum value later if asked)
- Invite-record expiry (`PickemGroupInvitation` has no invitee/lifecycle —
  separate feature; see the IDOR design's Option B notes)
- Blocking commissioners from *sending* invites to closed leagues (the join
  gate makes stale links safe; UI hiding covers the common path)
- Late-join scoring adjustments (`DropLowWeeksCount` already softens; revisit
  only if leaderboard complaints materialize)

## v2 revision — stored InvitationsExpireUtc (operator notes, 2026-07-30, post-#576 deploy)

Status: **implemented** — see the approved decisions below; countdown window set at 10 days.

v1 shipped and immediately felt wrong in production. The operator's notes and
subsequent code verification converged on a revised design.

### What v1 got wrong

**`Open` = "joinable until `DeactivatedUtc`" anchored joinability to a
mechanism that is not a lifecycle authority.** Verified against
`LeagueDeactivationJob`: deactivation is a UI-declutter sweep — it fires when
`EndsOn <= now - 7 days`, and **only when `EndsOn != null`**. Therefore:

- A single-day MLB league remains "joinable" for ~8 days after game day
  (next-day-UTC EndsOn + 7-day grace) — the wrongness observed on the browse
  page immediately after deploy.
- A FullSeason league (EndsOn null) is NEVER deactivated, so an Open
  full-season league is joinable forever.

Deactivation stays what it is (declutter). Joinability gets its own authority.

### The revision: store `InvitationsExpireUtc` on `PickemGroup`

The v1 "derived, never stored" doctrine is superseded — with reasons, not
regret:

1. **The countdown requirement changes the calculus.** Browsing users should
   see how long they have to join. v1 could render nothing for Open leagues
   (null = open); a countdown needs a concrete instant for EVERY league, and
   Open leagues had no real value to derive.
2. **The freshness hooks already exist.** The v1 objection (stored deadlines
   rot when kickoffs move) is solved by existing machinery:
   `ContestStartTimeUpdatedHandler` already updates
   `PickemGroupMatchup.StartDateUtc` on reschedules — extending it to refresh
   the stored expiry is a few lines in an existing consumer. Slate rebuilds
   recompute it wholesale.
3. **Most sources are stable anyway** — authored EndsOn, season-calendar week
   boundaries. Only LockedAtKickoff depends on movable kickoff times.

### LeagueWindow is captured explicitly

The operator's notes require the handler to know the LeagueWindow (FullSeason,
WeekRange, DateRange). This is now an explicit int-stored enum on
`PickemGroup`, chosen at creation from the form's duration mode — NOT inferred
from StartsOn/EndsOn null-ness. Inference is exact today only by accident
(WeekRange is unwired, so null/null can only mean FullSeason), and becomes
unreconstructible the moment WeekRange ships as week-to-date translation.
The migration backfills existing windowed rows to DateRange, which is exact
for the same reason. Legacy clients that omit the field get the same
inference at the API boundary, where it is still sound; a validator rejects
requests whose window claim contradicts their dates.

### Computation placement

Four trigger points, all converging on the same idempotent calculator:

1. **The creation event itself** — `PickemGroupCreatedHandler` enqueues a
   recompute alongside the slate bootstrap (the operator's original design).
   Open leagues resolve immediately from EndsOn / the season calendar with no
   slate dependency; drop-week leagues get their calendar-provisional value
   at creation. Also covers the bootstrap's zero-weeks path, which runs no
   per-week job.
2. The END of each per-week slate build that event triggers — the first moment first-game
times and week boundaries are knowable (the async-slate gap closes itself),
and re-slates recompute for free. Reschedule refresh lives in
`ContestStartTimeUpdatedHandler`.

### Per-window logic (operator's initial cut)

| LeagueWindow | Rule |
|---|---|
| FullSeason, has drop weeks | Expiry derived from the dropped-week window — a joiner inside it pays zero competitive penalty (the missed weeks are exactly the discarded ones). This is the DEFAULT for such leagues. |
| FullSeason, no drop weeks | Fall back to the commissioner's Open vs LockedAtKickoff. |
| WeekRange | Fall back to Open vs LockedAtKickoff (simplicity). |
| DateRange | Fall back to Open vs LockedAtKickoff (simplicity). |

Fallback semantics: LockedAtKickoff → first in-window game start (as v1).
Open → the league's LAST pickable moment — last in-window game start (single-
day: last first-pitch; DateRange: bounded by authored EndsOn; FullSeason:
season's final game from the calendar). "Open" no longer means "forever";
it means "while there is anything left to pick."

### Decisions — settled (operator-approved 2026-07-30, implemented)

1. **Drop-week expiry instant**: FIRST KICKOFF of week N+1
   (N = DropLowWeeksCount) — the exact moment joining starts costing points.
2. **Drop-week default vs commissioner choice**: as implemented, the
   FullSeason+drop-weeks rule OVERRIDES the commissioner's
   Open/CloseAtFirstGame selection in the calculator (verified by test:
   a CloseAtFirstGame league with 3 drop weeks expires at week 4's first
   kickoff, not at its first game). The join gate, browse, and detail
   fallbacks mirror the same exclusion so no surface contradicts the
   calculator while a value is uncomputed.
3. **Open + FullSeason last-game source**: season calendar
   (`GetSeasonOverview().EndDate`), never max(matchup start).
4. **Backfill**: the hourly audit-job sweep IS the backfill.
5. **Countdown window**: live countdown inside 10 days (operator-set),
   plain date beyond, `JoinClosesLabel` transitions in place via a
   boundary timer.

### Review positions declined (PR #577, recorded)

- **Optimistic concurrency (`xmin` RowVersion) on `PickemGroup` for expiry
  writes**: declined. Every writer is the same idempotent
  recompute-from-scratch, so "stale overwrite" is a correct recomputation of
  marginally older inputs; divergence is bounded by the next trigger and the
  hourly sweep. A concurrency token on `PickemGroup` would put
  `DbUpdateConcurrencyException` handling obligations on EVERY write path to
  the entity (creation, deactivation, future settings-edit) to protect a
  self-healing column. Revisit only if a non-idempotent writer ever touches
  the row concurrently.
- **WeekRange submitted with null bounds fails BE validation**: intended.
  The create form blocks the mode upstream; if it ever slips through, a loud
  window/dates-mismatch rejection is strictly better than the
  pre-`LeagueWindow` behavior (silently creating a mislabeled FullSeason
  league). Documented at the builder.

### v2 migration note

`JoinPolicy` (the commissioner's choice) STAYS — it is an input to the
computation. `InvitationsExpireUtc` is the computed OUTPUT the gate and the
UI consume. The v1 derived-at-read-time paths (join gate min() query,
closesAtUtc projections) collapse to reading the column once it ships.
