# Team mark direction preference — design doc

Background: `docs/team-mark-design-brief.md` (why generated marks), and
`src/marks/batch-generation-plan.md` (how marks land in storage).

This doc covers wiring the back-end to **serve those marks** with a
selectable direction (Roundel / Shield / Hex). Initial scope is the
matchup card path used by the web app's league-week view:

`GetLeagueWeekMatchupsQueryHandler` (API)
→ `IContestClient.GetMatchupsByContestIds` (HTTP)
→ `GetMatchupsByContestIdsQueryHandler` (Producer)
→ `GetMatchupsByContestIds.sql` (Postgres lateral joins).

## Scope

In:
- New `MarkDirection` enum, threaded API → Producer → SQL
- SQL change to prefer `sportdeets-mark` rows tagged with the
  requested direction, with a transparent fallback to existing
  ESPN-sourced rows
- API handler defaults the direction to `Roundel` (hardcoded —
  see "Why no user column yet" below)

Out (deferred):
- Per-user persisted preference (no UI yet)
- The other ~10 logo-selecting SQL queries (see Cascade)
- `LogoSelectionService` updates (no callers in this path)
- Light/dark mark variants (single render handles both today)

## The enum

```csharp
// SportsData.Core/Common/MarkDirection.cs
public enum MarkDirection
{
    Roundel = 0,
    Shield  = 1,
    Hex     = 2
}
```

Lives in Core so API client + Producer query + Producer SQL handler
all reference the same type. Roundel = 0 makes it the default value
for any uninitialized field, matching the system default.

## Why no `User.PreferredMark` column yet

Considered: add `User.PreferredMark MarkDirection?` (nullable) now
with default Roundel.

Rejected because:
- **No UI surface exists yet.** Per the top-down-UI-first project
  guardrail, until the profile-toggle UX is designed we don't know
  what the persisted shape needs (real enum vs. string for
  extensibility, companion fields for analytics, per-sport overrides,
  etc.).
- **Speculative schema.** A column whose value is uniformly Roundel
  until the toggle UI ships is exactly what the "design for hypothetical
  future requirements" guardrail warns against.
- **No time saved.** The migration cost is the same whether shipped
  now or with the UI PR. Swapping a hardcoded constant for a DB lookup
  is a five-line change.

When the toggle UI happens, that PR ships:
- EF migration adding `User.PreferredMark`
- `PATCH /user/preferences` (or extension to the existing endpoint)
- The 5-line swap in `GetLeagueWeekMatchupsQueryHandler`
- sd-ui profile-page toggle component

All as one coherent change.

## Wire contract change

### Producer endpoint

`POST /contests/matchups/by-ids` currently accepts a bare `Guid[]`
body. Wrap it in a request DTO so we can include the direction
without breaking the body shape on every parameter we add later:

```csharp
public record GetMatchupsByContestIdsRequest(
    Guid[] ContestIds,
    MarkDirection Direction);
```

Lives in `SportsData.Core.Dtos.Canonical` next to `LeagueMatchupDto`.

### Producer query

```csharp
public record GetMatchupsByContestIdsQuery(
    Guid[] ContestIds,
    MarkDirection Direction);
```

### Client interface

```csharp
Task<Result<List<LeagueMatchupDto>>> GetMatchupsByContestIds(
    List<Guid> contestIds,
    MarkDirection direction,
    CancellationToken ct = default);
```

Three call sites need updating:
1. `GetLeagueWeekMatchupsQueryHandler` (the target — passes the
   resolved direction)
2. `GetMatchupForContestQueryHandler` (admin debug single matchup —
   defaults to Roundel; admin has no user-pref context)
3. `AdminController.cs` line ~371 (admin endpoint — defaults to Roundel)

## SQL change

`src/SportsData.Producer/Infrastructure/Sql/GetMatchupsByContestIds.sql`

Today, each `FranchiseLogo` / `FranchiseSeasonLogo` lateral picks the
oldest row by `CreatedUtc ASC`. That means even with our 90 new
sportdeets-mark rows from the batch script, the ESPN-sourced rows
still win (older).

Replace `ORDER BY` in each of the eight lateral subqueries with a
preference-ordered `CASE`:

```sql
LEFT JOIN LATERAL (
  SELECT fsl.* FROM public."FranchiseSeasonLogo" fsl
  WHERE fsl."FranchiseSeasonId" = fsAway."Id"
  ORDER BY
    CASE
      WHEN fsl."Rel" @> ARRAY['sportdeets-mark', @Direction]::text[] THEN 0
      WHEN 'sportdeets-mark' = ANY(fsl."Rel")                        THEN 1
      ELSE                                                                2
    END,
    fsl."CreatedUtc" ASC
  LIMIT 1
) fslAway ON TRUE
```

Fallback chain:
1. `sportdeets-mark` row tagged with the requested direction → win
2. Any `sportdeets-mark` row → win (covers a team that doesn't yet
   have the requested direction generated)
3. Anything else → fall back to ESPN behavior (preserves today's
   visuals for unbackfilled teams)

`@Direction` is bound as a Dapper parameter on the existing
`CommandDefinition` call, alongside `@ContestIds`. The
`ProducerSqlQueryProvider` startup placeholder validator (matches
`{Name}` only) won't trip on `@Direction`.

The eight laterals that need the change: `flAway`, `fslAway`,
`flDarkAway`, `fslDarkAway`, `flHome`, `fslHome`, `flDarkHome`,
`fslDarkHome`.

### Dark variants and theme-agnostic marks

The current SQL queries dark logos separately and surfaces them as
`AwayLogoUriDark` / `HomeLogoUriDark`. Our sportdeets-marks are
theme-agnostic — we ship one render that works on both backgrounds.

The dark laterals get the same CASE-based ordering. For a team with
sportdeets-mark rows, the dark lateral returns the same URI as the
light lateral. The UI's existing light/dark URL selection logic still
works; both URLs simply point at the same blob. Until/unless we
generate proper dark variants, this is the correct behavior.

## Default-direction resolution in the API handler

```csharp
// GetLeagueWeekMatchupsQueryHandler.ExecuteAsync

// TODO: read from user.PreferredMark once the profile-toggle UI ships.
// For now, every user sees roundels.
var direction = MarkDirection.Roundel;

var matchupsResult = await _contestClientFactory
    .Resolve(league.Sport)
    .GetMatchupsByContestIds(contestIds, direction, cancellationToken);
```

The TODO is load-bearing — it's the line that gets replaced when the
profile toggle ships.

## The cascade (flagged, not in scope)

`GetMatchupsByContestIds.sql` is one of ~10 SQLs in
`ProducerSqlQueryProvider` that surface logo URIs:

- `GetMatchupForPreview.sql`
- `GetMatchupForPreviewBatch.sql`
- `GetMatchupsForCurrentWeek.sql`
- `GetMatchupsForSeasonWeek.sql`
- `GetMatchupByContestId.sql`
- `GetTeamCard.sql`
- `GetTeamCardSchedule.sql`
- `GetTeamFinalizedGames.sql`
- `GetTeamSeasons.sql`
- (possibly others — to be audited)

Each needs the same lateral-join CASE pattern for consistent marks
across the app. `LogoSelectionService` (used by non-SQL paths via EF)
also needs a `direction`-aware overload.

**Explicitly not in scope for this PR.** Prove the pattern on one
SQL, validate visually in the web app, then propagate. The propagation
pass is mechanical once the shape is locked.

## Idempotency / re-runnability of the batch

The synthetic `OriginalUrlHash` on each sportdeets-mark row encodes
direction (`SHA256("sportdeets-mark:{direction}:{Id}")`), so re-running
the batch script overwrites rather than duplicates. If Claude Design
ships a refined roundel later, we rerun the batch and the existing
rows update in place — no SQL change needed downstream.

## Backwards-compat

For teams that don't yet have sportdeets-mark rows (everything that's
not MLB FranchiseSeason 2026 today), the SQL change is a no-op: the
CASE falls to `ELSE 2`, the `ORDER BY` then breaks the tie on
`CreatedUtc ASC`, which matches today's behavior exactly. No
regression for unbackfilled teams.

## Migration story

None. This PR adds no columns, no indexes, no schema changes. EF
build runs zero migrations.

(The eventual UI PR will add a `User.PreferredMark` migration.)

## Test plan

- Build: `dotnet build` on `SportsData.Core`, `SportsData.Producer`,
  `SportsData.Api` — zero errors.
- Local run: bring up Postgres, API, Producer (BaseballMlb).
- Web app → log in → open a league with MLB matchups for week 1 2026.
- Verify matchup cards show generated roundels for MLB teams (sourced
  from `sportdeetssadev.blob.core.windows.net/sportdeets-marks/...`).
- Verify non-MLB matchups (if any seeded locally) still show ESPN
  logos — fallback path.
- Spot-check admin matchup-debug endpoint — should also work with
  roundels (admin path hardcoded to Roundel).
- DB sanity: `SELECT "Uri" FROM "FranchiseSeasonLogo"
  WHERE 'sportdeets-mark' = ANY("Rel") AND 'roundel' = ANY("Rel")` —
  matches what the SQL is now preferring.

## Open question for follow-up PRs

When the profile-toggle UI lands, the `MarkDirection` enum likely
deserves a `BlobPathSegment()` extension or similar so the marks
batch script and the SQL can share the lowercase direction-string
convention without duplicating it. Out of scope here, but worth a
note so the eventual refactor target is obvious.
