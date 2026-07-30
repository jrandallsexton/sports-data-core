# League Join Policy + Public League Discovery

Status: **Phases 1-2 (API + web) implemented — awaiting local E2E + migration validation; Phase 3 (mobile) not started**
Date: 2026-07-30
Surfaces: SportsData.Api, sd-ui, sd-mobile
Driver: public leagues must be browsable/joinable to drive home-page content;
single-day MLB test leagues surfaced the "when does a league close?" question.

## Decisions (settled with the operator, 2026-07-30)

1. **The commissioner decides, per league, at creation.** No platform-wide
   default behavior — join policy is an explicit choice on the create form,
   alongside pick type and visibility. Editable in league settings until
   deactivation.
2. **Exactly two options.** Deliberately no custom date in v1:
   - `Open` — joinable while the league is live (implemented name; the
     working name `OpenUntilLastLock` was shortened)
   - `CloseAtFirstGame` — closed once the league's first contest starts
3. **Applies to ALL leagues, not just public ones.** Visibility (`IsPublic`)
   controls who can *find* a league; join policy controls until when anyone can
   *enter* it. Both public joins and invite-link joins flow through
   `JoinLeagueCommandHandler`, so one check covers both — and shared invite
   links therefore expire naturally when the league closes. That was the
   operator's instinct ("if the commissioner sends an invite, it should expire
   at a certain time") and it is the zero-extra-work outcome.
4. **No "close now" button.** Flipping `OpenUntilLastLock` →
   `CloseAtFirstGame` after the first game has started closes the league
   immediately; flipping back re-opens it. Two enum values give close-now and
   re-open as consequences, not features.

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

**`OpenUntilLastLock`** — joinable until `DeactivatedUtc` is set. NOT
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
- **`OpenUntilLastLock` has no storable value.** Weekly slates build
  progressively; the "last game time" is unknowable until season end. The
  column would hold null-meaning-open — the enum again, hidden in a nullable.

The read-side cleanliness is kept anyway: DTOs expose a **computed
`closesAtUtc`** (`CloseAtFirstGame` → `min(matchup.StartDateUtc)` at read
time; `OpenUntilLastLock` → null). Clients render a concrete timestamp;
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

1. `JoinPolicy` enum (string-stored) + `PickemGroup.JoinPolicy`, migration
   backfilling `OpenUntilLastLock` (today's de-facto behavior — existing MLB
   test leagues unchanged).
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
   seasonYear, memberCount, window label). Filter or badge closed leagues —
   badge, so a nearly-started league still advertises itself.
5. `LeagueDetailDto`: add `joinPolicy` + `isJoinable` so the invite preview
   and browse detail can render truthfully.
6. Unit tests per guarded path (policy × deactivation × slate-empty).

### Phase 2 — Web (first client, per operator lean)

1. Create flow: "Who can join, and until when?" — two radios.
2. League settings: same control, commissioner-only, hidden when past.
3. Home page: "Leagues you can join" rail fed by the fattened query — the
   content-gap driver for this feature.
4. `LeagueDiscoverPage`: closed badges, join CTA disabled when unjoinable.
5. Invite affordances hidden when league is closed (mirrors `isPast`
   treatment).

### Phase 3 — Mobile (own PR, ships via EAS)

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
