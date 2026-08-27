# Phase-Aware League Weeks

Status: IMPLEMENTED 2026-08-26 (authorized same day; see Deviations)
Date: 2026-08-26

## The defect chain

Week numbers repeat across `SeasonPhase`s within one season year (NFL
2026: Preseason 1-4, Regular 1-18, Postseason 1-5). Three layers
currently erase the phase:

1. **Fetch**: `MatchupScheduleProcessor` calls
   `GetMatchupsForSeasonWeek(year, weekNumber)` — even though its
   command ALREADY carries the phase-precise `SeasonWeekId`. The
   number-based call is where ambiguity enters. (Now regular-scoped by
   default, which also means the NFL preseason test league can never
   sync a preseason slate again — an unintended regression of intent.)
2. **Storage/display model**: `PickemGroupWeek`/`PickemGroupMatchup`
   store `SeasonWeekId` but not phase, and the league DTO exposes
   `seasonWeeks` as a bare int list. A league spanning preseason +
   regular season has TWO "week 4"s the UI cannot distinguish.
3. **URL**: `/picks/weeks/4` (and every earlier variant) cannot say
   which week 4. This bites NCAAFB and NFL both at postseason.

The fix is not a URL scheme; it is threading phase through all three.

## Design

### Producer

- `GetMatchupsForSeasonWeek.sql`: SELECT adds
  `sp."TypeCode" AS "SeasonPhaseTypeCode"` (join already exists).
- NEW query variant: matchups **by SeasonWeekId**
  (`WHERE c."SeasonWeekId" = @SeasonWeekId`) — endpoint
  `contests/matchups/by-season-week-id/{seasonWeekId}` + client method.
  Precise identity, no phase/number ambiguity, works for ANY phase.
- `Matchup` DTO gains `SeasonPhaseTypeCode` (int) — additive.
- The number-based endpoint keeps its TypeCode filter (default 2) for
  callers that genuinely mean "regular-season week N" (map page,
  player-lineup anchoring).

### API

- `MatchupScheduleProcessor` fetches by `command.SeasonWeekId` instead
  of (year, number). This alone restores preseason syncs for the test
  league and makes every future postseason sync correct by construction.
- `PickemGroupWeek` + `PickemGroupMatchup` gain `SeasonPhaseTypeCode`
  (int, migration) stamped from the Matchup DTO on sync.
- Backfill: one-time UPDATE joining existing rows to canonical
  SeasonWeek data (ops step, local + prod).
- League DTOs (GetMe membership + league summary): ADDITIVE
  `seasonWeekDetails: [{ seasonWeekId, week, phase }]` alongside the
  existing `seasonWeeks` int list (mobile keeps working untouched;
  mobile adopts later). `phase` is a slug: `preseason` | `regular` |
  `postseason` (TypeCode 1/2/3).

### Web

- Canonical URL (always, both games):
  `/app/league/{leagueId}/picks/phase/{phase}/weeks/{week}` — the
  user-approved shape. `leaguePicksPath(leagueId, week, phase)` emits it;
  phase-less forms redirect to canonical once the week's phase resolves.
- PicksPage week model becomes phase-aware: the week list renders from
  `seasonWeekDetails` (falling back to `seasonWeeks` as regular during
  rollout), the route (phase, week) pair selects the entry, and
  navigations emit both segments. `LeagueWeekSelector` labels
  non-regular weeks (e.g. "Pre W4", "Post W1").
- Roster builder stays pinned (regular, week 1) → canonical
  `/picks/phase/regular/weeks/1` until its week selector lands.

## Sequencing

1. Producer vertical (SQL, by-id query + endpoint + client, DTO field).
2. API vertical (processor by-id fetch, migration + stamp, DTO
   additive, backfill script).
3. Web vertical (paths + PicksPage week model + selector labels).

Each step is independently shippable; step 1+2 fix the preseason-sync
regression even before the web work lands.

## Deviations from the proposal (as implemented)

- Phase is stamped on `PickemGroupWeek` only, NOT on
  `PickemGroupMatchup` — the matchup's week context reaches every
  consumer through its GroupWeek, and a second copy had no reader.
- `CurrentSeasonWeek` selection (GetMe) now orders phase-then-week —
  chronological by construction — instead of bare week number, which
  mis-picked regular Week 1 over a pending preseason Week 4.
  `CurrentSeasonWeekId` disambiguates it fully.
- The backfill surfaced 5 OFF-SEASON league weeks (TypeCode 4); the
  slug set gained "offseason" to label them honestly.
- Data fetches (picks, matchups) remain NUMBER-based `(league, week)`.
  A league holding BOTH phases of the same week number would fetch
  ambiguously — acceptable today (no such league exists; the test
  league is preseason-only), and the tracked prerequisite for real
  postseason pick'em is moving those endpoints to SeasonWeekId.
- Phase-less `/weeks/N` URLs are accepted and redirect to the canonical
  phase-qualified URL by adopting the first matching entry's phase.

## Out of scope

- Mobile adoption of `seasonWeekDetails` (additive; batch with next EAS
  round).
- Player Pick'em week selector (separate tracked alpha-blocker; will
  consume the same phase-aware model).
- Postseason league mechanics (CFP bracket pass, playoff pick'em) —
  this design only guarantees their week identity won't collide.
