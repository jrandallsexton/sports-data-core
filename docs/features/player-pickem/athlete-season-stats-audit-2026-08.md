# Athlete Season Stats Audit — 2026-08-19

Findings from the first athlete-stat audit gating Player Pick'em go-live
(see `docs/features/player-pickem/player-pickem.md`). Scope: NCAAFB
(`sdProducer.FootballNcaa`), motivated by the week-1 lineup picker: "list all
active FBS QBs and show their stats for this season and last."

## TL;DR

1. The 2026 roster/QB list is healthy. The stats attached to it are not:
   every stat doc on a 2026 `AthleteSeason` row actually contains **2025
   season numbers**, because ESPN's 2026 athlete document links to the
   prior season's statistics endpoint and we attach whatever comes back to
   the spawning roster row.
2. Stats attached to **2025** rows are nearly empty (~1,942 athletes vs
   ~16k for every other year since 2004), and 2025 **per-game** athlete
   stats were never sourced at all.
3. ~162k AthleteSeason rows carry duplicate "Season" stat docs.
4. Net effect: the only broad copy of 2025 season totals in the system is
   mislabeled under 2026 — and it will be silently joined (and eventually
   confused) by real 2026 docs once games start on Aug 28.

## Evidence

### Roster coverage (fine)

Active FBS QBs by season (`AthleteSeason` joined to `FranchiseSeason` with
`GroupSeasonMap like '%fbs%'`, position `QB`, `IsActive`):

| Season | Active FBS QBs |
|-------:|---------------:|
| 2024   | 518 |
| 2025   | 472 |
| 2026   | **852** (852 distinct athletes — no dupes; ~6.3/team = full rosters) |

2025's low count means *last* season's roster sourcing was partial, not
that 2026 is inflated.

### Season-stat coverage by roster year (broken)

Distinct athletes with `AthleteSeasonStatistic` rows, by the roster row's
`SeasonYear`: every year 2004–2024 lands ~8k–16k. Then:

| Roster year | Athletes with stats | Doc `CreatedUtc` |
|------------:|--------------------:|------------------|
| 2025 | **1,942** | 69 on 2025-12-21, 38 on 2026-07-24, 1,835 on 2026-08-16 |
| 2026 | **15,263** | 9,240 on 2026-04-14, 6,015 on 2026-08-17 |

15k athletes have "2026 stats" before a single 2026 game has been played.

### Case study: Sam Leavitt (`AthleteId 4afaf7e4-f027-c0ea-a5a1-358f3730f057`)

| Roster year | Team | Stat docs | Contents |
|------------:|------|----------:|----------|
| 2023 | Spartans | 2 (dupes) | — |
| 2024 | Sun Devils | 2 (dupes) | 13 GP, 2,885 yds, 24 TD |
| 2025 | Sun Devils | **0** | (played 7 games, then injured) |
| 2026 | Tigers | 1 | **7 GP, 1,628 yds, 10 TD ← his 2025 partial season** |

His actual 2025 production is filed under 2026; his 2025 row is empty.

### Per-game stats (missing for 2025)

`AthleteCompetition` coverage: 2023 (21,801 athletes / 1,784 games) and
2024 (22,335 / 1,827). **Nothing for 2025.** Performance scoring and
backtesting need game logs; last season has none.

### Duplicate docs

162,608 AthleteSeasons have two "Season" (`SplitId=0`) stat docs; 19 have
three. Observed pairs differ only in `SplitType` (`''` vs `'season'`) and
creation date (e.g., 2026-02-21 vs 2026-02-25) — re-sourcing passes that
produced different doc identities for the same logical resource. Exact
identity drift (URL shape change vs DTO parse change) should be confirmed
against the Provider document store before dedup.

## Root cause (confirmed against ESPN + code)

ESPN's season-scoped athlete document for 2026:

```
GET .../college-football/seasons/2026/athletes/5078810
→ statistics.$ref = .../seasons/2025/types/3/athletes/5078810/statistics
```

ESPN itself hands out the **previous season's** statistics ref until the
new season has data. Our pipeline:

1. `AthleteSeasonDocumentProcessor.ProcessStatistics` spawns the child
   request straight from `dto.Statistics` — no season check.
2. `AthleteSeasonStatisticsDocumentProcessor.ProcessInternal` resolves the
   target row via `TryGetOrDeriveParentId` — i.e., **the spawning
   AthleteSeason (2026)** — and never compares it to the season year
   embedded in the doc's own `$ref` (2025). The URI-derivation fallback
   (`EspnUriMapper.AthleteSeasonStatisticsRefToAthleteSeasonRef`) would
   have parsed the *correct* season from the ref; the explicit ParentId
   short-circuits it.
3. `AthleteSeasonStatistic` persists no season/provenance column, so once
   attached, the data's true season is invisible to queries. (It IS
   recoverable: entity `Id` is the canonical identity generated from the
   ESPN URL, so regenerating identities for candidate season URLs
   identifies which docs came from which season.)

### The in-season time bomb

Doc replacement keys on the URL-derived identity. When ESPN flips the 2026
athlete doc's statistics ref to `seasons/2026/...`, that is a **new
identity** → a new doc **inserted alongside** the 2025-numbers doc on the
same 2026 row. Nothing overwrites the stale doc; queries that sum or pick
arbitrarily will mix seasons.

## Recommendations

Pre-kickoff (Aug 28) sourcing work:

1. **Season-guard the attach.** In `AthleteSeasonStatisticsDocumentProcessor`,
   parse the season year from the doc's `$ref` and attach to that athlete's
   AthleteSeason **for that year** (resolving by `AthleteId` + season),
   not the spawning row. If no such row exists, log and skip.
2. **Backfill 2025 season stats** by requesting explicitly 2025-scoped
   statistics URLs for athletes with 2025 roster rows (642 of the 852
   active 2026 FBS QBs have one; all positions need it).
3. **Backfill 2025 per-game stats** (`AthleteCompetition*`) — required for
   performance scoring backtests and "recent form" UX regardless.
4. **Relabel or purge the mislabeled 2026-row docs** using identity
   regeneration to prove provenance (docs whose identity matches a
   `seasons/2025/...` URL belong on the 2025 row).
5. **Dedup the 162k doubled docs** after confirming the identity drift
   mechanism in the Provider store; keep newest per (AthleteSeason, split).

Read-model guidance for the lineup picker:

- **Resolve "this season and last" by athlete, not by roster row.** Walk
  the athlete's seasons newest-first and take the two most recent that
  have stats, labeling each with its true season. Week 1 of 2026 then
  shows "2025: 1,628 yds (7 GP)" for a player like Leavitt and "no
  college stats" for a freshman.
- **Default-sort the picker by last-season production** (yards / games
  played). 852 QBs includes every walk-on; production sort is a
  poor-man's depth chart until a real one is sourced.
- `AthleteSeasonInjury` holds 60 rows total — do not build injury badges
  on it yet.

## Follow-up (2026-08-19): the league-season leaders endpoint

```
GET .../college-football/seasons/2025/types/3/leaders?limit=200
```

drives ESPN's own "All Conferences Player Passing Stats 2025" UI (verified:
top passing leader 4,379 yds = Drew Mestemaker, matching the site
row-for-row). Payload: 13 categories (passingYards, rushingYards,
receivingYards, sacks, interceptions, passingTouchdowns,
quarterbackRating, receptions, totalTackles, …), default 25 leaders per
category, **`limit` raises it** (tested 200). Each leader row carries
`value` plus season-scoped `$ref`s for athlete, team, and — critically —
a **correctly-scoped statistics ref**
(`seasons/2025/types/3/athletes/{id}/statistics/0`).

Two implications, both verified:

1. **The per-athlete backfill URL is proven.** Synthesizing
   `seasons/2025/types/3/athletes/{espnId}/statistics/0` for a non-leader
   (Leavitt, 5078810) returns his true 2025 season (7 GP, 1,628 yds,
   10 TD). We hold ESPN athlete ids for every roster row
   (`AthleteSeasonExternalId.SourceUrl`), so recommendation #2 needs no
   new discovery mechanism — generate the URLs directly.
2. **Leaders is the picker's relevance signal.** A leaders sweep
   (13 categories × limit≈200) is ESPN's own answer to "who actually
   plays" — better than home-grown production sorting, and it resolves
   the depth-chart open question for v1. Candidate new `DocumentType`
   (league-season leaders); we already model the per-game and per-team
   variants (`EventCompetitionLeaders`, `TeamSeasonLeaders`), so the DTO
   shape is familiar.

`types/3` semantics partially resolved by the same check: the leaders and
statistics values under `types/3` match ESPN's displayed full-season 2025
totals (i.e., cumulative through postseason), which is exactly what the
picker wants for "last season."

## Open questions

- Whether ESPN's `types/3` statistics ref flips to `types/2` early in a
  season (regular-season-only totals) before postseason exists — affects
  in-season re-source behavior.
- Whether the Provider (Mongo) cached the 2026-row statistics docs under
  hashes that will collide or diverge when ESPN flips the ref — affects
  replay behavior.
