# AthleteSeason fabrication — findings and remediation design

**Status**: findings verified in prod 2026-08-15; remediation NOT yet
executed. This document is the authorization gate for the campaign.
**Blocks**: player pick'em go-live (data-correctness gate, per the
feature's athlete-stat audit prerequisite). Does NOT block current-season
surfaces — see impact triage.

## Finding

`AthleteSeason` is dominated by fabricated rows binding modern athletes
to historical franchise-seasons they never played in.

Exhibit: DJ Pickett (LSU CB, true freshman class of 2025) carries an
LSU `AthleteSeason` row for **every season 1990–2026** — 35 rows, one
per historical LSU `FranchiseSeason`, created near-daily through the
March 2026 historical backfill (`sql/pgsql/_debug_athleteSeasons.sql`
preserves the discovery trail).

Scale (distinct season-years per athlete-franchise pair):

| DB | 1–6 seasons (plausible) | 7–19 | 20+ (fabricated class) |
|---|---|---|---|
| FootballNcaa | 73,037 | 265* | 99,572 |
| FootballNfl | 11,714 | 3,081 | 16,930 |

\* NCAA 7–19 partially legitimate (long careers are not); NFL 7–19
contains real long careers AND fabrication tails — classification is
per-row (below), not per-bucket.

NCAAFB row count: **3,617,609** `AthleteSeason` rows for 153,005
athletes; the fabricated class accounts for roughly 3.4M.

## Root cause

An ESPN API quirk multiplied by our historical backfill:

1. **ESPN's league-level season athletes index is not season-scoped.**
   `GET /v2/.../seasons/1994/athletes` returns count=104,244 — the
   entire athlete database re-rendered under every season path. The
   per-athlete docs under those paths (`/seasons/1994/athletes/5141632`)
   resolve and render the athlete's **current** team as `Team.$ref`
   under the requested season (`/seasons/1994/teams/99`).
2. **The per-team roster index IS season-scoped and honest.**
   `/seasons/{year}/teams/{id}/athletes` returns real rosters from
   roughly 2004 onward (LSU probe: 2000=0, 2002=0, 2004=62, 2006=58,
   2010=58, 2014+=137–170) and empty before.
3. **The March 2026 historical backfill sourced the league-level index
   per season.** `AthleteSeasonDocumentProcessor.TryResolveFranchiseSeasonIdAsync`
   trusts the doc's `Team.$ref` at face value, minting an
   `AthleteSeason` that binds the modern athlete to the historical
   franchise-season.

**Why the purge predicate cannot be "created by that campaign"**: for an
athlete who genuinely played in season X, the same flow produced a
CORRECT row (ESPN renders their real season-X team under the season-X
path). Correct and fabricated rows are interleaved within the same
sourcing campaign, same timestamps, same creator identity.

## Design principle (mirrors ESPN's own product)

ESPN's site never shows historical rosters. A team page has a roster
for the CURRENT season only; a former player's page shows
season-by-season STATS — participation evidence, not roster claims.

Adopt the same contract:

- **`AthleteSeason` = roster membership**, trustworthy only where a
  roster document exists (roster era, ~2004+) or where game
  participation proves it.
- **Historical career views derive from participation** (statistics,
  competitions, leader rows), not from `AthleteSeason` coverage.
- Pre-roster-era seasons get NO dependent-less `AthleteSeason` rows.
  An athlete with 2001 game stats keeps their row via evidence; a
  roster claim with no evidence does not exist.

## Remediation phases

### Phase 0 — stop the bleeding (code, PR) — IMPLEMENTED

- **Provider**: the historical sourcing saga's AthleteSeason tier
  (Tier 4) is removed — saga finalizes after TeamSeason; the
  `HistoricalSourcingUriBuilder` mapping for the league-level index is
  gone (any legacy tier-4 job that slips through now throws instead of
  building the poisoned URL); legacy tier-4 `ResourceIndexJobs` rows
  are excluded from force-reschedule.
- **Producer guard — corroborated binding** (required, not belt: ~3.4M
  poisoned docs remain CACHED IN MONGO, and any replay would otherwise
  recreate rows post-purge): `AthleteSeasonDocumentProcessor` binds
  `FranchiseSeasonId` ONLY when the command's `ParentId` equals the
  resolved franchise-season's canonical id — the provenance only the
  TeamSeason roster cascade provides (Provider propagates ParentId
  through index fan-out). Uncorroborated docs still create/update the
  row **unbound** (dependency consumers — injuries, leaders, play
  participants — FK to the row and retry until it exists, so skipping
  creation would loop them; the binding is the poison, not the row),
  and an uncorroborated update never strips a roster-established
  binding. The roster cascade binds on its next pass.

### Phase 1 — evidence inventory (read-only SQL, both DBs)

Classify every `AthleteSeason` row:

- **KEEP-dependent**: referenced by any of `AthleteCompetition`,
  `AthleteCompetitionStatistic`, `AthleteSeasonStatistic`,
  `AthleteSeasonInjury`, `AthleteSeasonNotes`, `CompetitionLeaderStat`,
  `FranchiseSeasonLeaderStat`, `SeasonFutureBook` — participation or
  business evidence.
- **KEEP-roster**: (athlete, franchise-season) appears in that
  season's re-sourced ESPN team roster (roster era only).
- **PURGE**: everything else — which includes ALL dependent-less
  pre-roster-era rows (unverifiable by design) and roster-era rows
  ESPN's roster disowns.

Inventory queries emit counts per class per season-year; the report is
reviewed BEFORE phase 2 runs (halt-on-surprise, same discipline as the
metrics campaign gates).

### Phase 2 — purge (batched deletes, both DBs)

- Delete PURGE rows + their `AthleteSeasonExternalId` children (the
  only dependent they can have, by classification).
- Batched (e.g., 50k/transaction), logged counts per batch,
  resumable; the metricbot-pod orchestrator idiom from the metrics
  recompute campaign applies directly.

### Phase 3 — roster-era rebuild + gates

- Re-source team rosters for every franchise-season in the roster era
  (existing `TeamSeason` linked-document flow — the same filtered
  re-sourcing machinery as #618/#619).
- Gates, per franchise-season: roster count within sanity bounds
  (NCAA 40–150; NFL 40–95); zero athletes with >6 distinct season-years
  per franchise (NCAA) / >23 (NFL, career-length bound); the DJ Pickett
  probe returns exactly his real rows.
- Spot parity: N sampled rosters diffed against ESPN's live roster
  index.

## Impact triage

- **NOT blocked**: current-season (2025/2026) rosters — sourced via the
  legitimate roster flow; the LSU 2026 roster in the discovery file is
  correct. Player pick'em's carry-over-lineup core reads current
  rosters.
- **Poisoned until remediation**: any historical roster view, athlete
  career timelines derived from AthleteSeason, per-season athlete
  counts, and any consumer that trusts (athlete, franchise-season)
  affiliation historically.
- **Avatar batch (marks pipeline)**: unaffected — the athletes dump
  picks each athlete's MOST RECENT affiliation, which is the real one
  for modern athletes (fabrication attaches modern athletes backward,
  never old athletes forward). Optional post-purge regen tightens the
  dump's athlete set.
- **Metrics/MetricBot**: unaffected — competition metrics never read
  AthleteSeason.

## Decisions (Randall, 2026-08-15)

1. **NFL first** — dress rehearsal for the runbook; a mistake on
   NCAAFB would delay player pick'em. NCAAFB runs after NFL review.
2. **Rebuild floor: 2000** — ESPN rosters materialize ~2004; running
   the rebuild back to 2000 is no-harm-no-foul (empty roster index =
   no documents = no rows).
3. **Timing: now** — this is the current focus.
4. **Rebuild mechanism** (verified end-to-end): per seasonYear, POST
   the existing `RequestFranchiseSeasonSourcing` endpoint (#618) —
   **current season (2026) with NO filter** (full cascade: odds, records,
   everything fresh), **historical seasons (2000–2025) with
   `IncludeLinkedDocumentTypes: ["AthleteSeason"]`** — caught by
   `TeamSeasonDocumentProcessor`'s athletes spawn filter, which follows
   the doc's own season-scoped per-team roster ref.
5. **Purge before rebuild**: batched delete of dependent-less
   `AthleteSeason` rows (+ their `AthleteSeasonExternalId`), then the
   roster rebuild recreates roster-era membership under the
   corroborated-binding contract.

## NFL campaign run 1 (2026-08-15) — purge OK, rebuild no-op; fix shipped

The first NFL execution validated the purge and exposed a gap in the
rebuild mechanism:

- **Purge**: 461,302 dependent-less rows deleted (52,224 evidence rows
  kept). Formal gates passed (pairs-over-23-years: 0; orphaned
  dependents: 0).
- **Rebuild**: all 27 season POSTs accepted and fanned out (Seq:
  `Requested=32, Skipped=0, Failed=0` per season), but **zero new bound
  rows were created for any historical season** — only 2025 (+1,769)
  and 2026 (+638) gained rows. Per-season `CreatedUtc` proved it: no
  row for 2000–2024 was created during the run.
- **Root cause**: ESPN renders the `athletes` $ref **only on the
  current season's TeamSeason document**. Historical TeamSeason docs
  (verified live against 2005 and 2020) omit the link entirely — even
  though the roster index itself
  (`/seasons/{y}/teams/{id}/athletes`) exists and serves honest data.
  `TeamSeasonDocumentProcessor` therefore logged
  `SKIP_CHILD_DOCUMENT: parent DTO link is null` for every historical
  team and the cascade died at the first hop. (The historical seasons'
  seemingly-healthy 2004–2018 roster counts in the gate table were
  surviving evidence rows, not rebuilt rows.)
- **Fix**: when the athletes link is absent, the processor synthesizes
  the roster index URL from the TeamSeason document's own ref
  (`{cleanRef}/athletes`). The synthesized URL classifies as a
  resource index in the Provider, and the fan-out propagates the
  franchise season's id as `ParentId` — exactly the provenance the
  corroborated-binding guard requires. Empty pre-2004 indexes remain
  no-harm-no-foul.
- **Re-run**: after the fix deploys, re-POST seasons 2000–2024 (the
  same script; purge pass is idempotent). 2025/2026 are already
  rebuilt.
