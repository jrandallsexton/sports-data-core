# Multi-Sport League Creation Plan

Plan and implementation checklist for expanding the league creation flow
(`/app/league/create`) to support NCAA Football, NFL, and MLB. MLB is gated
behind admin accounts for now — used to exercise the live-season pipeline,
not exposed to end users.

## Current State (pre-change)

- **Frontend:** `LeagueCreatePage.jsx` is NCAA-only. Layout cleanup pass is
  done (wider container, `.form-row` grid for short selects, rankings is a
  dropdown, inline Other options).
- **Backend:**
  - `CreateLeagueRequest` DTO has **no** `Sport` field.
  - `CreateLeagueCommand.cs` exists with `Sport` on it but is **unused** —
    the handler takes `CreateLeagueRequest` directly. Prune during the split.
  - `CreateLeagueCommandHandler` hardcodes `Sport.FootballNcaa` /
    `League.NCAAF` (lines 107, 111) and resolves conferences via
    `_franchiseClientFactory.Resolve(Sport.FootballNcaa)` (line 85). No real
    multi-sport plumbing.
  - `PickemGroup` entity already has `Sport` + `League` columns — data layer
    is sport-aware.
  - `PickemGroup` has a `Conferences` collection (`PickemGroupConference`);
    for NFL/MLB those rows would semantically represent divisions.

## Target Architecture

### Command split (per sport)

Three sibling command folders under
`src/SportsData.Api/Application/UI/Leagues/Commands/`:

- `CreateFootballNcaaLeague/` — conferences + ranking filter + FBS toggle
- `CreateFootballNflLeague/` — divisions only
- `CreateBaseballMlbLeague/` — divisions only

Each contains its own `Request`, `Command`, `Handler`, `ICommandHandler`.

### Shared base DTO

`CreateLeagueRequestBase` (abstract) holds the fields common to all three:

- `Name`
- `Description`
- `PickType`
- `UseConfidencePoints`
- `TiebreakerType`
- `TiebreakerTiePolicy`
- `IsPublic`
- `DropLowWeeksCount`
- `SeasonYear`
- `StartsOn` (nullable DateTime) — see League Window below
- `EndsOn` (nullable DateTime) — see League Window below

Each sport-specific request inherits and adds its own fields.

### League Window (partial-season support)

Leagues may optionally run over a subset of the season. Two drivers:

- Commissioners want short leagues (e.g., "Weeks 1–4", "September games
  only").
- Enables end-of-league scoring logic to be tested mid-season without
  waiting for real end-of-season.

**Storage shape:** single date window on `PickemGroup`:

- `StartsOn` — nullable DateTime (null = start of season)
- `EndsOn` — nullable DateTime (null = end of season)

**UI entry modes** (both produce the same stored shape):

- **By week range** — user picks start week / end week; UI computes
  `StartsOn = start-of-week-N`, `EndsOn = end-of-week-M`. Works uniformly
  for NCAA, NFL, and MLB — ESPN's `SeasonType.week`/`weeks` refs define
  week boundaries natively for all three.
- **By date range** — user picks dates directly.

**Filtering:** at scoring / matchup-generation time, filter contests by
`contest.StartTime BETWEEN StartsOn AND EndsOn`. Game-level granularity —
"all September games" correctly excludes the Oct 1/2 games that happen to
fall in Week 4.

**Tradeoff:** storage loses the explicit "weeks-based league" semantic.
If a view needs to render "Week 1 of 4", it recomputes from dates at
read-time. Cheap and acceptable.

### Sport-specific fields

| Field              | NCAA | NFL | MLB |
|--------------------|------|-----|-----|
| ConferenceSlugs    | ✓    | —   | —   |
| DivisionSlugs      | —    | ✓   | ✓   |
| RankingFilter      | ✓    | —   | —   |
| FbsOnly (UI-only)  | ✓    | —   | —   |

### Endpoints

Three endpoints on `LeagueController`:

- `POST /ui/leagues/football/ncaa`
- `POST /ui/leagues/football/nfl`
- `POST /ui/leagues/baseball/mlb`

Existing `POST /ui/leagues` stays temporarily as an alias → NCAA handler.
Remove after FE cut-over.

### Entity naming (deferred decision)

`PickemGroupConference` semantically needs to cover divisions for NFL/MLB.
Two options:

1. Rename to `PickemGroupGrouping` (or similar sport-neutral name) +
   migration. Cleaner long-term.
2. Reuse as-is for divisions. No migration; semantically fuzzy.

**Decision:** ship with option 2 first so MLB/NFL unblock; rename later as
its own focused change.

## Frontend Plan

### Sport segmented control

- State: `sport` (default `FootballNcaa`).
- Control at top of form. Options: NCAA, NFL, MLB.
- MLB option rendered only if `userDto?.isAdmin` (pattern matches
  `AdminRoute`).

### Conditional sections

- Rankings dropdown → NCAA only.
- FBS Only toggle → NCAA only.
- Team filter source per sport:
  - NCAA → conferences (existing endpoint).
  - NFL → divisions (new endpoint).
  - MLB → divisions (new endpoint).

### Copy tweaks

- Tiebreaker "Closest to Total Points" → "Total Runs" for MLB.
- Placeholder text (`"e.g., Saturday Showdown"`, `"A fun league for SEC
  fans."`) → per-sport variants.

### Request routing

- `buildCreateLeagueRequest` split into three builders (or one with sport
  discriminator that picks endpoint). Endpoint dispatch driven by `sport`
  state.

## Implementation Checklist

### Phase 1 — Backend split (COMPLETE — pending migration validation)

- [x] Delete unused `CreateLeagueCommand.cs`
- [x] Add `StartsOn` / `EndsOn` nullable columns to `PickemGroup` entity
      (`src/SportsData.Api/Infrastructure/Data/Entities/PickemGroup.cs`)
- [x] EF migration generated:
      `Migrations/20260417103635_17AprV1_PickemGroupWindow.cs` — **NOT
      applied to any DB yet; user to validate locally per guardrail**
- [x] `PickemGroupCreated` event unchanged — window is not needed at event
      time (resolved at matchup-generation time)
- [x] Add `CreateLeagueRequestBase` abstract class with shared fields
      including `StartsOn` / `EndsOn`
- [x] Create `CreateFootballNcaaLeague/` folder with request + handler +
      interface. NCAA-specific logic (conferences, ranking filter)
- [x] Register NCAA handler in `ServiceRegistration.cs`
- [x] Add `POST /ui/leagues/football/ncaa` endpoint to `LeagueController`
- [x] Keep existing `POST /ui/leagues` endpoint as `[Obsolete]` alias →
      NCAA handler (same JSON shape, FE unaffected until cutover)
- [x] Port existing tests to `CreateFootballNcaaLeagueCommandHandlerTests`
- [x] Create `CreateFootballNflLeague/` folder (divisions only, no ranking
      filter) + handler + interface + registration + `POST
      /ui/leagues/football/nfl` route + tests
- [x] Create `CreateBaseballMlbLeague/` folder + handler + interface +
      registration + `POST /ui/leagues/baseball/mlb` route + tests
- [x] `dotnet build` clean (SportsData.Api — 0 warnings, 0 errors)
- [x] `dotnet test` green (SportsData.Api.Tests.Unit — 322 passed, 11 new)

### Phase 2 — Divisions endpoint (NFL + MLB)

- [ ] Decide endpoint shape: one sport-aware endpoint or two sport-specific
      endpoints under Franchise service
- [ ] Implement divisions query (`GetDivisionsBySport` or similar)
- [ ] Wire through `IFranchiseClientFactory` for NFL + MLB modes
- [ ] Bruno collection entries for new endpoints

### Phase 3 — Frontend scaffolding (COMPLETE)

- [x] Add `sport` state to `LeagueCreatePage` (default `FootballNcaa`)
- [x] Add segmented control UI component
- [x] Gate MLB option on `userDto?.isAdmin`
- [x] Conditional render Rankings / FBS-only on NCAA
- [x] Swap team-filter data source by sport (hardcoded NFL/MLB divisions)
- [x] Per-sport copy adjustments (tiebreaker label, placeholders)
- [x] League window UI: entry-mode toggle (Full Season / Week Range /
      Date Range); numeric week dropdowns bounded by sport's maxWeeks;
      date pickers are plain `<input type="date">`
- [x] Split `buildCreateLeagueRequest` into three sport-specific builders
      in `api/leagues/requests/createLeagueRequests.js`; dispatch to the
      correct endpoint per sport. Week Range mode blocked with a message
      pending the season-calendar endpoint.

### Phase 3.5 — Scoring / matchup filter changes

- [ ] Matchup generation respects league window (skip contests outside
      `[StartsOn, EndsOn]`)
- [ ] Score aggregation respects league window (don't count out-of-window
      picks)
- [ ] Week-level views (leaderboard, overview) clamp to window
- [ ] Tests for partial-season leagues (e.g., weeks 1–4 league, date-range
      league)

### Phase 4 — Cleanup

- [ ] Remove deprecated `POST /ui/leagues` endpoint once FE is confirmed
      routing to sport-specific endpoints
- [ ] (Deferred) Rename `PickemGroupConference` → sport-neutral name with
      migration

## Open Questions

- Divisions endpoint shape: sport-aware single endpoint vs. per-sport
  routes? (Parity with how conferences endpoint looks today would favor
  per-sport.)
- NFL pick-type semantics: ATS works naturally; any edge cases?
- MLB tiebreaker: "Closest to Total Runs" enough, or do we need a
  run-line-specific field later?

## Notes

- MLB admin gating is UI-only — the BE endpoint doesn't need auth-role
  checks beyond the existing `[Authorize]` attribute unless we want to
  harden it. TBD once the feature matures past testing.
- `PickemGroup.Sport` + `League` are already populated correctly on write;
  only the handler's hardcoded values need replacement.
