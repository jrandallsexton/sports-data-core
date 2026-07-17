# Data-driven off-season countdown

## Problem

`PrimarySlotOffSeasonCountdown` (the Tier-1 home slot shown to logged-in users
when no followed sport is in-season) counts down to each sport's kickoff. Both
platforms currently get the kickoff date wrong:

- **Web** (`sd-ui/.../home/PrimarySlotOffSeasonCountdown.jsx`) hardcodes
  `Date.UTC(2026, 8, 5)` / `Date.UTC(2026, 8, 10)` constants. Stale by
  construction — they were right when written and rot silently each season. A
  manual test edit also left them inconsistent.
- **Mobile** (`sd-mobile/.../home/PrimarySlotOffSeasonCountdown.tsx`) *computes*
  kickoff from rules: NCAAFB = "first Saturday of September", NFL = "Thursday
  after Labor Day". Both rules are wrong for 2026:

  | Sport  | Rule computes | Actual (sourced) | Error |
  |--------|---------------|------------------|-------|
  | NCAAFB | Sep 5         | **Aug 29**       | 7 days late |
  | NFL    | Sep 10        | **Sep 9** (Wed)  | 1 day late |

  NCAAFB has no fixed rule at all; NFL's is a convention the league doesn't
  actually honor. A derived date is a guess wearing an algorithm's clothing — it
  produces a plausible-but-wrong date every year with no symptom, which is worse
  than web's loud staleness.

The two platforms also disagree with each other, with no shared source.

## Source of truth

Producer's `SeasonPhase` entity already carries the answer:

```
SeasonPhase { TypeCode, Name, Abbreviation, Slug, Year, StartDate, EndDate, ... }
```

`TypeCode`: 1 = Preseason, 2 = Regular Season, 3 = Postseason, 4 = Off Season.

The countdown target = `StartDate` of the **Regular Season** phase (`TypeCode = 2`)
for the sport's current season. Confirmed present in both sport DBs for 2026 with
correct dates (NCAAFB Aug 29, NFL Sep 9), so the primary path needs no fallback
to ship — see "Absent-data behavior" for graceful degradation.

## Design

Thin pass-through at each layer; no new persistence. Modelled as a **general
per-sport season resource**, not a countdown-shaped endpoint.

**BFF vs. standard-endpoint test:** if the endpoint returned the *countdown*
(e.g. `{ label: "NCAAFB in 43 days" }` — the answer pre-computed for one UI), it
would be a BFF concern and belong under `Application/UI/*`. Because it returns
*raw* season/phase data (`{ TypeCode, StartDate, ... }`) for the client to
interpret, it's a standard endpoint. It therefore lives as a normal vertical
slice under `Application/Seasons/`, on the same `api/{sport}/{league}/{resource}`
convention as the existing Contests / Franchises / Venues slices.

Consequently the API does **not** fan out both sports and knows nothing about
"kickoffs". "Which sports the countdown shows" is a presentation decision that
stays in the countdown component; "kickoff" is that component's interpretation of
"regular-season start" and stays in the UI. The API speaks seasons and phases.

### 1. Producer — current-or-upcoming season with phases

`GET seasons/current` → `CurrentSeasonDto`

```
record SeasonPhaseDto {
  int TypeCode; string Name; DateTime StartDate; DateTime EndDate;
}
record CurrentSeasonDto {
  int SeasonYear; string Name;
  DateTime StartDate; DateTime EndDate;
  List<SeasonPhaseDto> Phases;
}
```

New query handler (`GetCurrentSeason`) resolves the **current-or-upcoming**
`Season` directly from the data — `EndDate >= now`, earliest `StartDate` — with
its `Phases` included, `.AsNoTracking()`, projected to the DTO. This is
data-driven and avoids a calendar heuristic: during a season it returns the
in-progress one (its `EndDate` is still in the future, which also covers the
Jan–Feb playoff window that broke naive `UtcNow().Year`); during the off-season
the prior season's `EndDate` is past, so the next upcoming season wins. No season
row matching (brand-new sport, or a gap before next season is sourced) →
`NotFound`, a legitimate not-yet-sourced case.

Deliberately *not* reusing `GET {year}/overview`: overview drags every week + poll
across the wire and exposes `SeasonPhaseName` (string) rather than `TypeCode`.

### 2. Core — SeasonClient method

`Task<Result<CurrentSeasonDto>> GetCurrentSeason(CancellationToken)`

Added to `IProvideSeasons`. `SeasonClientFactory.Resolve(mode)` already builds
sport-keyed base addresses (`CommonConfig:SeasonClientConfig:{mode}:ApiUrl`,
default fallback), so per-sport routing is free — the resolved client hits that
sport's Producer DB, so "current season" is naturally sport-scoped.

### 3. API — current-season resource (per sport)

`GET api/{sport}/{league}/seasons/current` → `CurrentSeasonDto`

New `Application/Seasons/` slice: controller + `GetCurrentSeason` query handler.
Maps the route's `{sport}/{league}` to a `Sport` mode via `ModeMapper.ResolveMode`
(as Venues/Contests do), resolves `ISeasonClientFactory` for it, and passes the
`CurrentSeasonDto` straight through. One sport per call — the client calls it
once per sport it cares about. General and reusable; no countdown vocabulary and
no season-year guessing (Producer owns "current").

### 4. Web + mobile — consume the resource

Both `PrimarySlotOffSeasonCountdown` components drop their hardcoded constants /
computed rules. Each keeps its own list of sports to surface (a presentation
choice), fetches `seasons/current` per sport, and reads the `TypeCode == 2`
(Regular Season) phase's `StartDate` as the countdown target:

- Web: fetch per surfaced sport (or lift to the home data load).
- Mobile: `useQuery` per sport; delete `ncaafbKickoff` / `nflKickoff` entirely.

`daysUntil` / phrasing logic stays; only the date source changes. A sport whose
current season has no Regular Season phase yet → "kickoff coming soon" / omitted,
never a wrong countdown. Convert any remaining date construction to ISO strings
(no `Date.UTC(y, m, d)`).

## Absent-data behavior

`KickoffUtc = null` for a sport → that sport shows "kickoff coming soon" (or is
omitted) rather than a wrong countdown. Since both 2026 phases are already
sourced this is not the primary path, but it's the correct behavior for a
future season whose phases haven't landed, and it keeps the home page resilient
to a Producer outage.

## Out of scope / follow-ups

- **Preseason pick'em leagues** ([[project_preseason_pickem_leagues]]) — the
  countdown window is where preseason games would live; TypeCode 1 is available
  from the same endpoint if/when that's built.
- Season-year rollover edge cases beyond what's already handled.

## Files touched

- `SportsData.Producer`: new `GetCurrentSeason` query + handler; `SeasonController` action (`seasons/current`); DI registration.
- `SportsData.Core`: `IProvideSeasons` / `SeasonClient.GetCurrentSeason`; `CurrentSeasonDto` + `SeasonPhaseDto` (Core.Dtos.Canonical).
- `SportsData.Api`: new `Application/Seasons/` slice — `SeasonsController` (`api/{sport}/{league}/seasons/current`) + `GetCurrentSeason` query handler + DI registration.
- `sd-ui`: `PrimarySlotOffSeasonCountdown.jsx` + season api client method.
- `sd-mobile`: `PrimarySlotOffSeasonCountdown.tsx` + season api method.
- Tests at each layer.
