# League creation: per-sport availability gate

**Status:** Implemented (backend + web + mobile). Pending: set the NCAA config
value in AppConfig; PR + CodeRabbit.
**Owner:** Randall.
**Scope:** `SportsData.Api` (config + service + guard + endpoint),
`src/UI/sd-ui` + `src/UI/sd-mobile` (CTA + create-form gate). **No Producer / Core
change** — league-creation availability is a Pick'em rule, owned entirely by API.

## Problem

We must not allow league creation for a sport before that sport's data is ready.

- **NCAAFB:** many leagues rank teams by the **AP Poll**, which isn't released until
  **Aug 17, 2026**. A league created before then has no rankings to work from — a
  broken first experience during our launch window.
- **NFL:** could technically be created now, but we may want to hold it too. The
  handling (and the exact date) is **not yet decided** — the gate must apply to NFL
  by changing a single config value, not by writing new code.

This is distinct from [[league-creation-blackout-dates]] (which days *have games*
inside a chosen window). This gate is about *when the sport opens for creation at
all*, and it is **time-based so it auto-unlocks** with no deploy.

## Why API, not Producer

Producer is the keeper of canonical data; API is the keeper of all things Pick'em.
League-creation availability is a Pick'em rule — it has no place on
`CurrentSeasonDto` or forcing Producer to populate it. Keeping it in API also means
the create guard reads the value **locally** instead of round-tripping to Producer.

## Mechanism

A single nullable UTC timestamp per sport, held in API config and enforced at the
create seam, exposed to the FE by one endpoint.

### Semantics of the gate

| Value for a sport | Meaning |
|---|---|
| absent | No gate — creatable now (e.g. MLB, already admin-gated separately). |
| future timestamp | **Locked** until that instant; UI shows "opens {date}". |
| past timestamp | Creatable (the gate expired; harmless to leave configured). |

Because it's a timestamp compared to `now`, NCAAFB opens automatically at Aug 17
with **no redeploy and no OTA push**. NFL is the same map with a different value.

### Source — `ApiConfig.LeagueCreationOpensUtc` (per-sport config)

**`Dictionary<string, string>`** on `ApiConfig` (`SportsData.Api:ApiConfig`), keyed by
**Sport enum name**. API is mode=All, so the map is sport-keyed (the
`feedback_appconfig_sport_keyed` pattern). Set the NCAA gate as a hierarchical key in
the API's AppConfig label:

```ini
SportsData.Api:ApiConfig:LeagueCreationOpensUtc:FootballNcaa = 2026-08-17T00:00:00Z
```

> **Why string-keyed, not `Dictionary<Sport, DateTime>`:** the .NET configuration
> binder does **not** reliably populate *enum-keyed* dictionaries — they bind empty
> (and nothing else in this codebase relies on that; the `Dictionary<Sport, …>` props
> on `CommonConfig` are vestigial, with real sport config read via direct string
> keys). String-keyed dictionaries bind reliably (cf. `LoggingConfig.Overrides`). Do
> **not** collapse this to a single `…:LeagueCreationOpensUtc` key holding a JSON
> dictionary — that only binds if the AppConfig value is also marked
> `content-type: application/json` (an extra, brittle step). The hierarchical per-key
> form above is the idiomatic approach.

`ILeagueCreationAvailability` parses each entry once at construction: the key
`"FootballNcaa"` → `Sport`, the value → a UTC instant (`DateTimeStyles.AssumeUniversal
| AdjustToUniversal`, so both `…Z` and a bare `2026-08-17T00:00:00` resolve to the
same UTC instant, never shifted by the host timezone). An unknown sport name is
logged and skipped; a **known** sport with an unparseable date is logged and kept
**closed** (fail-closed — a misconfigured gate must never silently open the sport).
No migration; change the date or unlock early live in AppConfig.

### One service, two consumers

`ILeagueCreationAvailability` (`Application/UI/Leagues/LeagueCreationAvailability.cs`)
reads the config + `IDateTimeProvider`:

- `GetOpensUtc(Sport)` → the future open instant, or `null` if open (absent/elapsed).
  Used by the **create guard**.
- `GetActiveGates()` → all currently-gated sports (future only), earliest first.
  Used by the **FE endpoint**.

### Enforcement (correctness floor)

Create-time guard in `CreateLeagueCommandHandlerBase.ExecuteAsync`, before the
blackout guard and any downstream work:

```csharp
var opensUtc = _availability.GetOpensUtc(SportMode);
if (opensUtc is not null)
    return new Failure<Guid>(default!, ResultStatus.Validation,
        [new ValidationFailure(nameof(request),
            $"League creation opens {opensUtc:MMMM d, yyyy}. Check back then.")]);
```

Same failure shape the FE already surfaces. Closes the deep-link
(`?sport=FootballNcaa`) and direct-API holes that CTA-hiding alone leaves open.

### FE-facing endpoint

`GET /ui/leagues/creation-availability` → `LeagueCreationAvailabilityDto`:

```json
{ "gates": [ { "sport": "FootballNcaa", "opensUtc": "2026-08-17T00:00:00Z" } ] }
```

Returns only **active** gates; a sport not listed is open. One call covers every
sport — better than per-sport `seasons/current`, since the create page and
off-season countdown both juggle multiple sports at once.

### Frontend gate (both platforms, mirror the `isMlbAvailable` pattern)

1. **Create form** (`LeagueCreatePage.jsx` / `create-league.tsx`) — the real UX
   choke point. Fetch the availability once; a sport in `gates` renders its tab
   **disabled with an "opens {date}" hint** (not silently hidden). Guard
   `initialSport` / the `?sport=` deep-link so a locked sport doesn't land on a dead
   form; fall back to the first open sport / show the locked state.
2. **Off-season countdown** (`PrimarySlotOffSeasonCountdown`) — when a sport is
   locked, swap its "Create {sport} league" CTA for a disabled "{sport} leagues open
   {date}" affordance.
3. **Generic CTAs** (`PrimarySlotNewUser`, `LeagueMembership`, `Leagues` /
   `leagues.tsx`) — route to the create form, which shows the locked state, so these
   need no per-sport logic.

If the availability call fails, the FE fails **open** (sports appear creatable);
the server guard still rejects a locked create with the "opens {date}" message, so
the worst case is a slightly worse UX on a transient outage, never a broken league.

## Rollout

1. NCAAFB: set `...:LeagueCreationOpensUtc:FootballNcaa = 2026-08-17T00:00:00` in the
   API's AppConfig label.
2. NFL: set its key once decided — **no code change**.

## Files touched

- `SportsData.Api`:
  - `Config/ApiConfig.cs` — `LeagueCreationOpensUtc` map.
  - `Application/UI/Leagues/LeagueCreationAvailability.cs` — service (+ interface).
  - `Application/UI/Leagues/Dtos/LeagueCreationAvailabilityDto.cs` — endpoint DTOs.
  - `Application/UI/Leagues/Commands/CreateLeagueCommandHandlerBase.cs` — guard
    (+ ctor param threaded through the 3 sport subclasses).
  - `Application/UI/Leagues/LeagueController.cs` — `GET creation-availability`.
  - `DependencyInjection/ServiceRegistration.cs` — register the service.
- `sd-ui`: `LeagueCreatePage.jsx` (tab gate + deep-link), `PrimarySlotOffSeasonCountdown.jsx` (CTA), availability api client.
- `sd-mobile`: `create-league.tsx` (tab gate + deep-link), `PrimarySlotOffSeasonCountdown.tsx` (CTA), availability api method.
- Tests: `LeagueCreationAvailabilityTests`, gated-sport case on the NCAA create handler.

## Testing

- **Service:** future gate → `GetOpensUtc` returns the instant; absent/past → null;
  Unspecified config instant treated as UTC; a malformed date for a **known** sport →
  fail-closed (stays locked, not silently opened); `GetActiveGates` returns
  future-only, earliest first. (`LeagueCreationAvailabilityTests`, 7 cases.)
- **Guard:** future `OpensUtc` → `Failure(Validation)`, **no** `PickemGroupCreated`
  published. (`CreateFootballNcaaLeagueCommandHandlerTests.ShouldFail_WhenSportCreationNotYetOpen`.)
  Existing create-handler tests auto-mock the service (returns null → open), so they
  stay green with no behavior change.
- **FE:** locked sport tab disabled with hint; locked `?sport=` deep-link doesn't
  dead-end; open sport creates normally.

## Decisions (resolved)

1. **Where the date lives — per-sport API config (`ApiConfig`).** No migration,
   changeable live in AppConfig, API-owned (not Producer/Core). NCAA label gets
   Aug 17, 2026; other sports unset (absent = no gate).
2. **NFL — leave open for now.** Only NCAAFB is gated in this pass. Setting NFL's
   config value later locks it with **no code change**.
