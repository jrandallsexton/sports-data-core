# Logo license audit — ensuring only generated marks render

**Context (2026-07-18):** licensed team logos are still appearing in the UI (e.g.
the `team-header` on `/app/sport/football/ncaa/team/washington-huskies/2024` and
`/2026`). Goal for app-store submission: the UI must render only
sportDeets-generated marks (rows whose `Rel` array contains `sportdeets-mark`),
never ESPN-sourced licensed logos.

## How selection works today

`TeamCard.jsx` → TeamCard API → Producer `GetTeamCardQueryHandler` →
`LogoSelectionService.SelectWithFallback(franchiseSeason.Logos, franchise.Logos)`.

Two problems, possibly both in play:

1. **Logic bug — fail-open shadowing** (`LogoSelectionService.cs`, `SelectWithFallback`):
   `selector(seasonLogos) ?? selector(franchiseLogos)`. `selector` returns
   non-null whenever the list is non-empty (its last resort just returns the
   first logo). So if a FranchiseSeason has *any* logos but no mark, its ESPN
   logo is returned and the **franchise-level logos are never consulted** — a
   franchise-level mark can't rescue a mark-less season. Correct precedence
   should be: season-mark → franchise-mark → season-ESPN → franchise-ESPN.

2. **Data gap** — marks were inserted per-FranchiseSeason (`FranchiseSeasonLogo`);
   `src/marks/batch/insert.js` notes franchise-level rows were deferred to "a
   later batch pass." Seasons the batch didn't cover fall straight through to
   ESPN.

3. **Fail-open policy** — even after fixing (1) and backfilling (2), a team with
   no mark *anywhere* still returns an ESPN logo. A hard guarantee requires
   failing **closed**: return null / a neutral placeholder when no
   `sportdeets-mark` exists, never an ESPN row.

`Rel` is a Postgres `text[]`, so membership is tested with
`'sportdeets-mark' = ANY("Rel")`.

---

## Diagnostic queries

> Run against the **football / NCAA canonical (Producer) database**. Adjust the
> DB per sport if auditing others.

### 1. Washington Huskies — season-level marks (the two linked seasons)

```sql
SELECT fs."SeasonYear",
       fsl."Rel",
       fsl."Uri",
       fsl."IsForDarkBg"
FROM "Franchise" f
JOIN "FranchiseSeason" fs ON fs."FranchiseId" = f."Id"
LEFT JOIN "FranchiseSeasonLogo" fsl ON fsl."FranchiseSeasonId" = fs."Id"
WHERE f."Slug" = 'washington-huskies'
  AND fs."SeasonYear" IN (2024, 2026)
ORDER BY fs."SeasonYear";
```
SeasonYear	Rel	Uri	IsForDarkBg
2024	NULL	NULL	NULL
2026	NULL	NULL	NULL

### 2. Washington Huskies — franchise-level marks (the "later pass")

```sql
SELECT fl."Rel",
       fl."Uri",
       fl."IsForDarkBg"
FROM "Franchise" f
JOIN "FranchiseLogo" fl ON fl."FranchiseId" = f."Id"
WHERE f."Slug" = 'washington-huskies';
```
Rel	Uri	IsForDarkBg
{}	https://sportdeetssa.blob.core.windows.net/franchise-logo-football-ncaa/6c4cfbb006cb82aa4f1e14a203c4d510f88ff9f93a498a246ffc9c0da315cd79.png	NULL
{}	https://sportdeetssa.blob.core.windows.net/franchise-logo-football-ncaa/ecca50fb72d549b9ae57fdc674609a3db2bfc2f13a8b2bc30aa063c62d67f0be.png	NULL

**What to look for:** does any returned row's `Rel` contain `sportdeets-mark`,
and at which level (season vs franchise)?
- No mark at either level → **data gap** (backfill the mark).
- Mark at franchise but not season 2024/2026 → **logic bug** is what's hiding it
  (the fail-open shadow), fix the selector.
- Mark present at the season level → selection isn't picking it; dig into the
  `Rel` direction tags (`roundel` / `shield` / `hex`).

---

## Fleet-wide audit (the app-store sweep)

### 3. FranchiseSeasons that have logos but NO mark (the fail-open risk set)

These are the season rows that will render an ESPN logo today.

```sql
SELECT f."Slug",
       fs."SeasonYear",
       COUNT(fsl."Id") AS total_logos,
       COUNT(*) FILTER (WHERE 'sportdeets-mark' = ANY(fsl."Rel")) AS mark_logos
FROM "Franchise" f
JOIN "FranchiseSeason" fs ON fs."FranchiseId" = f."Id"
LEFT JOIN "FranchiseSeasonLogo" fsl ON fsl."FranchiseSeasonId" = fs."Id"
GROUP BY f."Slug", fs."SeasonYear"
HAVING COUNT(fsl."Id") > 0
   AND COUNT(*) FILTER (WHERE 'sportdeets-mark' = ANY(fsl."Rel")) = 0
ORDER BY f."Slug", fs."SeasonYear";
```

### 4. Franchise-level mark coverage (did the "later pass" ever run?)

```sql
SELECT f."Slug",
       COUNT(fl."Id") AS total_logos,
       COUNT(*) FILTER (WHERE 'sportdeets-mark' = ANY(fl."Rel")) AS mark_logos
FROM "Franchise" f
LEFT JOIN "FranchiseLogo" fl ON fl."FranchiseId" = f."Id"
GROUP BY f."Slug"
ORDER BY mark_logos ASC, f."Slug";
```
Result: Not a sinlge one of the 893 teams have a value > 0 in mark_logos column.

Rows with `mark_logos = 0` are franchises whose only logos are ESPN — the
franchise-level fallback would serve a licensed logo for them.

### 5. Count of at-risk season rows (one-number severity)

```sql
SELECT COUNT(*) AS at_risk_franchise_seasons
FROM (
  SELECT fs."Id"
  FROM "FranchiseSeason" fs
  JOIN "FranchiseSeasonLogo" fsl ON fsl."FranchiseSeasonId" = fs."Id"
  GROUP BY fs."Id"
  HAVING COUNT(*) FILTER (WHERE 'sportdeets-mark' = ANY(fsl."Rel")) = 0
) x;
```
Result: 791 rows
---

## Findings (2026-07-18, from queries 1–2)

**Washington Huskies = pure data gap. No generated mark exists at any level.**

- Season (Q1): zero `FranchiseSeasonLogo` rows for 2024/2026 (all NULL).
- Franchise (Q2): two rows, both `Rel = {}` (empty — NOT `{sportdeets-mark,...}`),
  on `sportdeetssa.blob.../franchise-logo-football-ncaa/...`.

**The blob URL is a red herring.** Those franchise rows are the **original ESPN
logos re-hosted to our own blob** (Producer mirrors ESPN art to avoid hotlinking).
Re-hosting doesn't change licensing — still the copyrighted logo. Generated marks
are the rows tagged `Rel = {sportdeets-mark, <direction>}`; empty `Rel` ⇒ NOT a
mark ⇒ ESPN mirror. So selection correctly finds no mark, falls through the ESPN
cascade (Priority 5 returns the first untagged franchise logo), and **renders the
licensed mirror — fail-open**. The logic bug (season shadows franchise) is real
but is NOT the cause here (season was empty, so franchise *was* consulted).

## Severity (from Q4/Q5, 2026-07-18)

- **Q4: zero of 893 franchises have a franchise-level mark.** The franchise-level
  fallback can therefore ONLY ever serve a licensed mirror — the "later pass" for
  franchise marks never ran.
- **Q5: 791 franchise-seasons** have season logos but no season-level mark.

This is broad, not isolated. Consequence: with a fail-closed selector, a
significant number of teams will show the **placeholder** until backfill — so the
placeholder is a high-visibility surface (must look good), and the backfill is a
prioritized track, not an afterthought. Fail-closed remains mandatory: a
placeholder is compliant, a licensed logo is not.

## Fix plan

1. **Fail-closed selector** — `LogoSelectionService` returns a `sportdeets-mark`
   or `null`; never an untagged / ESPN-mirror row. This is the only *hard*
   guarantee no licensed logo can render, and it fixes the fail-open shadowing
   bug for free (no ESPN cascade to shadow anything).
2. **Frontend placeholder** — when the logo URL is empty, `TeamCard` renders a
   license-free fallback (team-color circle + abbreviation), not a broken `<img>`.
3. **Data backfill (Option 2 — franchise-level single source) — DECIDED
   (2026-07-18).** Generate a FranchiseLogo mark for EVERY franchise (fresh, all
   893 — uniform, zero holes), select from it, and let go of FranchiseSeasonLogo
   for display. Generated marks are year-invariant, so their home is
   `FranchiseLogo` (one per franchise), NOT per-season. The full generation pipeline still exists in `src/marks/`
   (`marks.js` renderer; `generate.js` → `upload.js` → `insert.js`) and the color
   dumps are present (`franchise-colors-{ncaafb,nfl,mlb}.txt`, which already carry
   `FranchiseId`). Backfill steps:
   - **Broaden the color query** — `franchise-colors.sql` filters
     `SeasonYear = 2026 AND IsActive`, yielding 651 of 893 NCAA franchises. Take
     each franchise's *latest-season* colors instead so all 893 are covered
     (the ~242 inactive/historical ones otherwise stay holes).
   - **Generate + upload** — existing scripts, unchanged.
   - **Add a franchise-level insert path** — `insert.js` only writes
     `FranchiseSeasonLogo` ("Franchise-level rows will land in a later batch
     pass" — never built). Add the sibling that writes `FranchiseLogo` keyed on
     `FranchiseId`, tagged `Rel = {sportdeets-mark, <direction>}`.
   - Going forward: stop writing per-season marks; franchise marks are
     year-invariant and cover every season, so no new holes accrue.
   Once every franchise has a franchise-level mark, (1) fail-closed never renders
   a licensed logo AND the placeholder effectively never fires.

## Corrected fleet sweep

Query 3 above misses the Washington pattern (it requires `total_logos > 0` at the
**season** level; Washington has zero season logos). The franchises actually at
risk are those with **no `sportdeets-mark` at either level** — these render a
licensed mirror today.

### 6. Franchises with NO mark anywhere (season or franchise) — the true risk set

```sql
SELECT f."Slug",
       (SELECT COUNT(*) FROM "FranchiseSeasonLogo" fsl
          JOIN "FranchiseSeason" fs ON fs."Id" = fsl."FranchiseSeasonId"
         WHERE fs."FranchiseId" = f."Id"
           AND 'sportdeets-mark' = ANY(fsl."Rel"))
       + (SELECT COUNT(*) FROM "FranchiseLogo" fl
           WHERE fl."FranchiseId" = f."Id"
             AND 'sportdeets-mark' = ANY(fl."Rel")) AS mark_rows
FROM "Franchise" f
GROUP BY f."Id", f."Slug"
HAVING (SELECT COUNT(*) FROM "FranchiseSeasonLogo" fsl
          JOIN "FranchiseSeason" fs ON fs."Id" = fsl."FranchiseSeasonId"
         WHERE fs."FranchiseId" = f."Id"
           AND 'sportdeets-mark' = ANY(fsl."Rel"))
     + (SELECT COUNT(*) FROM "FranchiseLogo" fl
         WHERE fl."FranchiseId" = f."Id"
           AND 'sportdeets-mark' = ANY(fl."Rel")) = 0
ORDER BY f."Slug";
```

### 7. One-number severity: how many franchises have no mark anywhere

```sql
SELECT COUNT(*) AS franchises_without_any_mark
FROM "Franchise" f
WHERE NOT EXISTS (
        SELECT 1 FROM "FranchiseSeasonLogo" fsl
        JOIN "FranchiseSeason" fs ON fs."Id" = fsl."FranchiseSeasonId"
        WHERE fs."FranchiseId" = f."Id" AND 'sportdeets-mark' = ANY(fsl."Rel"))
  AND NOT EXISTS (
        SELECT 1 FROM "FranchiseLogo" fl
        WHERE fl."FranchiseId" = f."Id" AND 'sportdeets-mark' = ANY(fl."Rel"));
```

## Decisions (resolved 2026-07-18)

- **Fail closed?** — YES. The only hard guarantee no licensed logo can render;
  implemented in A + B.
- **Placeholder design** — team-color circle + abbreviation (client-side, fully
  license-free); implemented in C.
- **Backfill scope** — generate one mark per franchise for ALL franchises
  (Option 2, franchise-level), run in prod for all three sports.

---

## Wiring — the code changes required (the "use it" side)

There are **two separate logo-selection engines**, and both currently fail OPEN
(fall through to the ESPN/untagged row when no mark exists). Backfilling data
alone does not fix either — the selection code must change too.

### A. C# `LogoSelectionService` (4 callers, one file)

Callers, all routed through the service — so one change covers all four:
`GetTeamCardQueryHandler`, `GetContestOverviewQueryHandler`,
`GetContestPlayLogQueryHandler`, `GetCurrentPollsQueryHandler`.

**Change:** make it **fail-closed** — return a `sportdeets-mark` (season, then
franchise) or `null`; never an untagged / ESPN row. This simultaneously:
- guarantees no licensed logo can render, and
- fixes the fail-open shadowing bug in `SelectWithFallback`
  (`selector(season) ?? selector(franchise)` — the ESPN cascade is gone, so a
  season ESPN logo can no longer shadow a franchise mark).

Simplest shape: a `SelectMark(logos, direction)` that returns only a
`sportdeets-mark` Uri or null, then `SelectMark(season) ?? SelectMark(franchise)`.
The whole ESPN priority cascade (curated/dark/white/default/first) is deleted.

### B. Raw SQL `GetMatchupsByContestIds.sql` (picks page / live tiles / matchup cards)

This is the **highest-traffic** logo surface and it does NOT use the C# service —
it selects logos in SQL via LATERAL subqueries and
`COALESCE(fslAway."Uri", flAway."Uri")` (season preferred, franchise fallback).
The **light-background laterals still fail open** (`ORDER BY CASE ... ELSE 2`,
i.e. fall to the ESPN row when no mark exists); the dark laterals already filter
marks-only.

**Change:** make every logo lateral **marks-only** — add
`WHERE fl."Rel" @> ARRAY['sportdeets-mark']` (as the dark laterals already do) and
drop the `ELSE 2` ESPN fallback. Then each lateral returns a mark or nothing, and
`COALESCE(season-mark, franchise-mark)` fail-closes AND stops a season ESPN logo
from shadowing the franchise mark. One SQL file.
(`GetRankingsByPollByWeek.sql` already uses the marks-only pattern — no change.)

### C. Frontend placeholders (web + mobile)

Fail-closed means the logo Uri can be **null** for any team without a mark (rare
after backfill, but must be safe). Every `<img src={logoUri}>` needs a null-guard
that renders a license-free fallback (team-color circle + abbreviation — colors
are already on the DTOs). Surfaces: TeamCard `team-header`, matchup cards
(picks + live tiles, web + mobile), results, contest overview, standings.

### D. Other paths — verified (2026-07-18)

- **Results page** (`GetSeasonResults`) calls the SAME `GetMatchupsByContestIds`
  query as the picks page → **covered by B**, no separate change.
- **`GetRankingsByPollByWeek.sql`** — already marks-only, fail-closed. No change.
- **`GetTeamCard.sql`** selects an unfiltered `FranchiseLogo."Uri"` (fail-open),
  BUT nothing invokes `ProducerSqlQueryProvider.GetTeamCard()` — it's **dead
  code**. The live TeamCard handler uses EF + the C# service (covered by A).
  Cleanup candidate, not a compliance risk.

**Net:** A + B together cover every *live* licensed-logo path found. C (frontend
placeholder) is the remaining safety net for teams with no mark.

### Status (2026-07-18)

- Data backfill: **DONE** — franchise-level marks generated + inserted for all
  three sports in prod (6 script runs, zero errors).
- A (`LogoSelectionService` fail-closed): **DONE** — + 9 unit tests; full Producer
  suite green (502 passed).
- B (`GetMatchupsByContestIds.sql` marks-only): **DONE**.
- C (frontend placeholders): **DONE** — reusable web `TeamLogo` wired into the
  matchup cards + TeamCard team-header; mobile `MatchupCard` placeholder upgraded
  to team-color.

All of A + B + C ship together in one PR (fix/logo-compliance-fail-closed).

### Sequencing (as shipped)

The prod backfill ran first (marks now exist), then A + B + C shipped together in
one PR. With the backfill done, A + B guarantee no licensed logo can render and
every team with a mark shows it; C is the safety net for any team still lacking a
mark. Note the data backfill alone would NOT have been enough — the fail-open
code kept serving licensed logos for any team with a season ESPN row (most of
them), which is exactly what A + B fixes.

---

## Running the franchise-level backfill

Generates one year-invariant mark per franchise and writes it to `FranchiseLogo`
(`Rel = {sportdeets-mark, <direction>}`). Idempotent — re-runnable.

**Prereqs**
- Franchise-grain color dumps in `src/marks/batch/data/`
  (`franchise-colors-{ncaafb,nfl,mlb}.txt`), tab-separated, header row intact.
- `SPORTDEETS_SECRETS_PATH` set (run.ps1 dot-sources it for blob + PG creds).

**The scripts you run:** `run.ps1` only (it invokes `upload.js` then `insert.js`).
`-Grain Franchise` is the default, so it's implicit. Run the two phases as a pair
per sport (insert consumes the newest manifest), then move to the next sport.

Start with **NFL on `dev`** (small + confirmed data) to verify the whole chain,
then repeat for Ncaafb and Mlb, then flip `-Environment prod` when satisfied.

```powershell
cd C:\Projects\sports-data\src\marks\batch

# ── NFL (dev / local first) ─────────────────────────────────────────────
./run.ps1 -Phase upload -Environment dev -Sport Nfl     # render + upload + manifest
./run.ps1 -Phase insert -Environment dev -Sport Nfl     # write FranchiseLogo rows

# ── then NCAAFB, then MLB (same pair each) ──────────────────────────────
./run.ps1 -Phase upload -Environment dev -Sport Ncaafb
./run.ps1 -Phase insert -Environment dev -Sport Ncaafb
./run.ps1 -Phase upload -Environment dev -Sport Mlb
./run.ps1 -Phase insert -Environment dev -Sport Mlb

# ── when verified on dev, repeat with -Environment prod ─────────────────
# ./run.ps1 -Phase upload -Environment prod -Sport Nfl
# ./run.ps1 -Phase insert -Environment prod -Sport Nfl
#   ...Ncaafb, Mlb
```

**Verify after each insert** (against that sport's Producer DB):

```sql
-- franchise-level marks written. Expect (franchises × 3 directions):
--   NFL ~31×3=93, NCAAFB 893×3=2679, MLB ~30×3=90
SELECT COUNT(*) AS franchise_mark_rows
FROM "FranchiseLogo"
WHERE 'sportdeets-mark' = ANY("Rel");

-- franchises still with NO mark anywhere should now be 0 (re-run Q7):
SELECT COUNT(*) AS franchises_without_any_mark
FROM "Franchise" f
WHERE NOT EXISTS (
        SELECT 1 FROM "FranchiseSeasonLogo" fsl
        JOIN "FranchiseSeason" fs ON fs."Id" = fsl."FranchiseSeasonId"
        WHERE fs."FranchiseId" = f."Id" AND 'sportdeets-mark' = ANY(fsl."Rel"))
  AND NOT EXISTS (
        SELECT 1 FROM "FranchiseLogo" fl
        WHERE fl."FranchiseId" = f."Id" AND 'sportdeets-mark' = ANY(fl."Rel"));
```

**Notes**
- `run.ps1 -Environment <dev|prod>` writes blob + DB **together** for that env;
  they must match (a prod DB row can't point at a dev blob URL).
- `-Grain FranchiseSeason` still runs the legacy per-season pass if ever needed.
- The selector change (fail-closed; season mark preferred, then franchise mark,
  else null) comes AFTER this backfill so the app reads these marks instead of
  the licensed mirrors.
