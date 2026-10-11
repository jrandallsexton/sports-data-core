# 02. Enablement and schema

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 1 (proof of concept) · **Status:** done 2026-10-10 (uncommitted)

**Depends on:** none

**Feature doc:** [Ship dark and the lab](instant-analysis.md#ship-dark-and-the-lab) · [Stage 6: persist](instant-analysis.md#stage-6-persist) · [Decisions](instant-analysis.md#decisions-2026-10-10)

## Goal

Everything the pipeline gates on and writes to exists, so later tasks are pure logic.

## Scope

- `CompetitionStream.LiveAnalysisEnabled` (bool, default `false`) on `src/SportsData.Producer/Infrastructure/Data/CompetitionStream.cs`, with a Producer migration.
- The kill switch `SportsData.Producer:InstantAnalysis:Enabled`, off unless `true`, set per sport via the Producer pod's sport label. No manifest entry is needed while it's off; add one (`Prod.FootballNcaa`) when going live.
- The `PlayAnalysis` entity and migration, columns as in the feature doc: `Id`, `ContestId`, `CompetitionId`, `PlayId` (**required FK to `CompetitionPlay`**), `AnalysisRunId` (null for live), `FactType`, `Facts` (jsonb, numbers plus provenance), `Text`, `Priority`, `TemplateVersion`, `CreatedUtc`.
- Indexes: (`CompetitionId`, `PlayId`) for audits; unique (`PlayId`, `AnalysisRunId`, `Ordinal`) NULLS NOT DISTINCT, which enforces one set per play per run.

**Out of scope:**

- Populating anything; no consumer or job yet.
- An admin toggle for the flag (SQL is enough for now).

## Deliverables

- Entity, configuration and migration for `PlayAnalysis`.
- The `LiveAnalysisEnabled` column and migration.
- Config binding for the kill switch.

## Acceptance criteria

- [ ] Migrations tested locally against the local stack (test-migrations-locally rule).
- [ ] `PlayAnalysis.PlayId` rejects a play id that doesn't exist.
- [ ] Toggling `LiveAnalysisEnabled` by SQL on one stream row is enough to opt one game in.

## Notes

- Table name singular (`PlayAnalysis`), per the singular-table-names convention.

## Outcome (2026-10-10)

- `PlayAnalysis` entity: `src/SportsData.Producer/Infrastructure/Data/Entities/PlayAnalysis.cs`, registered on
  `TeamSportDataContext` (so Baseball gets the table too, ready for Phase 3). Added `Ordinal` (position in the play's
  set) to make the one-set-per-play rule a database constraint.
- `CompetitionStream.LiveAnalysisEnabled` (not null, default false).
- Migrations `10OctV1_InstantAnalysisSchema` for Football and Baseball (identical Up/Down; no other model drift).
- Kill switch: `Application/Competitions/InstantAnalysis/InstantAnalysisConfig.cs`, a singleton read once at
  startup (same as `DocumentProcessingConfig`). The key uses the Producer prefix rather than `CommonConfig:` because
  it's Producer-only. Unit tests in `InstantAnalysisConfigTests`.
- Verified on local `sdProducer.FootballNcaa` (prod restore): migration applied; a duplicate live fact for a play is
  rejected, the same ordinal in a lab run is allowed, an unknown `PlayId` is rejected by the FK; all 253 existing
  streams are `LiveAnalysisEnabled = false`. `sdProducer.FootballNfl` and `BaseballMlb` apply on Producer boot.
- Opt a game in: `UPDATE "CompetitionStream" SET "LiveAnalysisEnabled" = true WHERE "CompetitionId" = '<id>';`
