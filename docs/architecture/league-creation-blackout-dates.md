# League creation: blackout (no-game) dates

**Status:** Approved. **Phase 1 (Producer `game-dates` query + endpoint, API
proxy, create-time zero-game guard) implemented.** Phase 2 (picker on-select
validation, mobile then web) pending.
**Owner:** Randall.
**Scope:** `SportsData.Producer` (canonical query), `SportsData.Api` (UI endpoint +
create-time guard), `src/UI/sd-mobile` + `src/UI/sd-ui` (date picker).

## Problem

Creating a **single-day MLB league on the All-Star break** (Home Run Derby day —
zero games) succeeds but produces a league with **zero picks**. The user picked a
date with no games; nothing told them, and nothing stopped them.

### Root cause (traced)

1. `CreateLeagueCommandHandlerBase.ExecuteAsync` accepts `StartsOn`/`EndsOn`,
   stores them on `PickemGroup`, and publishes `PickemGroupCreated`. **No check
   that the window contains any games.**
2. `PickemGroupCreatedHandler` → `BootstrapLeagueMatchupsProcessor`
   resolves the *weeks* overlapping the window via
   `SeasonClient.GetSeasonWeeksOverlapping(from, to)`. The All-Star **week**
   exists (weeks are calendar structure, independent of whether games fall on a
   given day), so it enqueues a per-week job.
3. `MatchupScheduleProcessor` fetches all matchups for the week, then applies the
   window filter (`MatchupScheduleProcessor.cs`):
   ```csharp
   allMatchups = allMatchups.Where(m =>
       (!group.StartsOn.HasValue || m.StartDateUtc >= group.StartsOn.Value) &&
       (!group.EndsOn.HasValue   || m.StartDateUtc <= group.EndsOn.Value)).ToList();
   ```
   On the Derby day this yields **zero** matchups. `AreMatchupsGenerated` stays
   false, and the daily `MatchupScheduler` keeps retrying a window that will
   never contain games. Empty league, forever.

The bug is **input validation + UX**: nothing prevents selecting a no-game date,
and nothing rejects a window with no games.

## Goals

1. **Prevent** selecting no-game dates in the create-league date picker
   ("blackout dates"), for every sport/league.
2. **Backstop** with a server-side guard that rejects (or refuses to create) a
   windowed league whose date range contains zero games — so a stale client or a
   lagging blackout source can never produce an empty league.

Both were explicitly requested. The guard is the correctness floor; the picker is
the UX.

## Non-goals (deferred)

- A precomputed/materialized blackout table and its invalidation. **See "Data
  source" — deriving on-demand dissolves the "how do we remove blackout dates
  when postseason games get added" problem entirely, so we don't build it.**
- Blacking out dates beyond the loaded schedule horizon (future/unpublished
  season) — see Open decision 3.

## Data source — derive on-demand, do NOT precompute

The authoritative signal already exists: **canonical `Contest.StartDateUtc`** in
the per-sport Producer DB. "Which dates have games for sport S in [from, to]" is a
cheap indexed `SELECT DISTINCT`:

```sql
SELECT DISTINCT (c."StartDateUtc" AT TIME ZONE 'America/New_York')::date AS "GameDate"
FROM   public."Contest" c
WHERE  c."StartDateUtc" >= @fromUtc AND c."StartDateUtc" < @toUtc
ORDER  BY "GameDate";
```

(Sport is implicit — Producer is per-sport, resolved via the client factory.)
These are US sports; instants bucket to a US calendar date via a **fixed**
`America/New_York` — user/device timezones do not apply and are not a
parameter (decided).

**Recommendation: compute this on-demand, no precompute table.** Rationale
(brutal-candor version): a precomputed `(Sport, Date, HasGames)` table buys us
nothing here and *introduces* the exact maintenance burden already flagged — when
postseason games are added, a cached "blackout" would be stale until a rebuild
job caught up. The on-demand query is always correct, always fresh, and reads the
same live `Contest` data the pipeline writes. Date-picker opens and league
creates are low-frequency; the query is a bounded, indexed distinct over one
season's contests (MLB ≈ 2,430 rows/season). If profiling ever shows it hot, add
a short-TTL cache **then** — but it won't. This matches the instinct that it's
"trivial," while sidestepping the invalidation problem.

> If `Contest.StartDateUtc` isn't already indexed, add a btree index — the range
> scan wants it. (Verify during implementation.)

### One primitive, two consumers

Both features are the same question:

| Consumer | Question | Query shape |
|----------|----------|-------------|
| Date picker (UX) | which dates in the visible range **have** games? | list of `GameDate` |
| Create guard (correctness) | does the chosen window have **≥1** game? | `COUNT(*) > 0` (or non-empty list) |

So we build **one** capability — "distinct game dates in a UTC range for this
sport" — and both consume it. The guard is just "was the list non-empty."

## Surface

### Producer (canonical read)

New query + endpoint on the per-sport Producer:

```
GET /api/contests/game-dates?from={utcIso}&to={utcIso}
→ 200 { "gameDates": ["2026-07-11", "2026-07-12", "2026-07-15", ...] }   // dates WITH games
```

- Dapper/EF read, `.AsNoTracking()`, projected to a DTO (no entities).
- Instants bucket to a US calendar date via a fixed `America/New_York` (no `tz`
  parameter).
- Returns the **positive** set — dates that have games (decided, #4). The UI
  derives blackout = (all dates in range) − gameDates; the guard checks
  non-empty. Payloads are bounded (a season is a few hundred date strings at
  most), so positive-vs-negative size is a non-issue.

### API (UI-facing proxy + guard)

**Endpoint** for the picker — resolves the sport client and proxies:

```
GET /ui/leagues/game-dates?sport={sport}&league={league}&from={date}&to={date}
→ 200 { "gameDates": [...] }
```
Uses `IContestClientFactory.Resolve(sport)` → a new client method
`GetGameDates(from, to)`.

**Create-time guard** in `CreateLeagueCommandHandlerBase.ExecuteAsync`, added
after the existing validation, before building the `PickemGroup`:

- Only when the request is **windowed** (`StartsOn` or `EndsOn` set). Full-season
  leagues (both null) get the whole season and skip the check.
- Resolve the contest client for `SportMode`, call `GetGameDates(StartsOn,
  EffectiveEndsOn)`. If the result is empty:
  ```csharp
  return new Failure<Guid>(default!, ResultStatus.Validation,
      [new ValidationFailure(nameof(request.StartsOn),
          $"No {SportMode} games are scheduled in the selected date range. " +
          "Choose a range that includes at least one game day.")]);
  ```
  This mirrors the existing unresolved-slug failure shape, so the FE already
  knows how to surface it.

This backstop ships **independently of the UI work** and, on its own, fixes the
reported bug (the empty league can no longer be created).

### UI (the picker) — feasibility flag

**The current widgets cannot disable interior dates.** Mobile's
`@react-native-community/datetimepicker` (`create-league.tsx` `DateField`) and
web's `<input type="date">` (`LeagueCreatePage.jsx`) both support only
`minimumDate`/`maximumDate` bounds — not arbitrary disabled cells. Truly
"greying out" the Derby day requires a **calendar component** that supports
per-date disabling:

- **Mobile:** `react-native-calendars` (`markedDates` / `disabledByDefault` +
  enabled game days), or an equivalent. New dependency.
- **Web:** a date-picker lib with `disabledDates` (e.g. `react-day-picker`) or a
  small custom month grid. New dependency or component.

**Decided: ship the lightweight path.** This is a rare edge case (unlikely to
surface in NCAAFB/NFL), so a two-platform calendar-component swap isn't worth it.

- **On-select validation (chosen).** Keep the current pickers; when the user
  picks a start/end date, check it against the fetched game-dates and, if it's
  not a game day (or the resulting range has no game days), show an inline error
  ("No games on Jul 14 — pick another date"). Not a true "disable," but combined
  with the server guard it fully prevents the empty league.
- **Calendar-component swap (deferred).** True disable-in-picker
  (`react-native-calendars` / `react-day-picker`) is a future nicety if the edge
  case ever proves common; not built now.

The picker fetches game-dates for the relevant season once when sport is chosen
(bounded set — cache client-side) rather than per-tap, and bounds its selectable
max to the known season end (decided, #3) so unsourced future dates don't read as
blackout.

## Rollout / phasing

1. **Phase 1 — backend (fixes the bug on its own):** Producer `game-dates` query
   + endpoint; API client method + `/ui/leagues/game-dates`; **create-time
   zero-game guard**. No empty league can be created after this.
2. **Phase 2 — picker UX:** consume `game-dates` in the create-league date
   picker via **on-select validation** (decided), mobile then web.

Phase 1 is the priority; Phase 2 is polish on top of a now-correct backend.

## Testing

- **Producer query:** returns distinct dates with games; excludes no-game days;
  respects the range bounds and `tz` bucketing (a late-night UTC game lands on
  the right local date).
- **API guard (`CreateLeagueCommandHandlerBase`):** windowed range with zero
  games → `Failure(Validation)` and **no** `PickemGroupCreated` published;
  windowed range with games → success; full-season (both null) → guard skipped
  (no client call). Use a mocked contest client.
- **API endpoint:** proxies the resolved sport client; shape matches.
- **UI:** blackout dates disabled/rejected; selecting a game day succeeds.

## Decisions (resolved)

1. **Timezone bucketing — decided.** US sports only; user/device timezones do not
   apply and are not a parameter. Bucket `StartDateUtc` to a US calendar date via
   a fixed `America/New_York` in the query.
2. **Picker approach — decided: on-select validation.** Rare edge case (unlikely
   in NCAAFB/NFL), so the two-platform calendar-component swap isn't worth it.
   Server guard (Phase 1) is the real fix; on-select validation is the Phase-2
   UX. Calendar swap deferred as a future nicety.
3. **Future / unpublished schedule — decided.** Bound the picker's selectable max
   to the known season end so unsourced future dates don't read as blackout.
4. **Endpoint returns — decided: game-dates (positive set).** Payloads are bounded
   either way; positive is the direct query output and makes the guard a trivial
   non-empty check.
