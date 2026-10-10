# 01. Data spike

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 0 · **Status:** done 2026-10-10. See [findings](01-data-spike-findings.md)

**Depends on:** none

**Feature doc:** [Worked example](instant-analysis.md#worked-example-the-interception-in-_debug_instant_analysissql) · [Stage 2: classify](instant-analysis.md#stage-2-classify-what-happened-who-did-it) · [Stage 3: dig](instant-analysis.md#stage-3-dig-the-data) · ["As of this play"](instant-analysis.md#as-of-this-play-how-a-season-count-is-computed) · [Known data gaps](instant-analysis.md#known-data-gaps-from-the-2026-10-10-survey)

## Goal

Replace the plan's remaining assumptions with measured facts before any code is written. Everything later depends on knowing which participant roles, and which stats, can be trusted mid-game.

## Scope

- Participant roles on live data for fumbles (lost, recovered, returned), sacks and safeties. Interceptions are already confirmed: the defender is `passDefender` (and `returner`); there is no `interceptor` role.
- How current `AthleteSeasonStatistic` is during and after a game: does it ever already include part of the current game, and how long after a final does it reflect it? Use the refresh paths listed in the feature doc.
- `AthleteCompetitionStatistic` coverage for 2026 FBS (games and athletes with rows).
- A prototype "as of this play" season-count query on the example interception (`sql/pgsql/_debug_instant_analysis.sql`), computed from both sources, with the two compared.

**Out of scope:**

- Any production code. Output is findings plus SQL.

## Deliverables

- A findings section added to this task doc (or a `01-data-spike-findings.md` next to it).
- The role mapping per play kind, ready for task 04.
- A recommendation for task 06: which source backs the season-to-date term, with evidence.
- The prototype SQL, saved under `docs/features/instant-analysis/sql/` (this feature's SQL lives with its docs, not in `sql/pgsql/`).

## Acceptance criteria

- [ ] Every play kind in the feature doc's classify table has a verified actor role (or is dropped from Phase 1 with a reason).
- [ ] The double-counting risk for `AthleteSeasonStatistic` is answered with data from at least two live games.
- [ ] The prototype returns the correct count for the example interception (his second of the season).

## Notes

- Participant rows only exist for plays ingested since 2026-07-22; pick 2026 games.
