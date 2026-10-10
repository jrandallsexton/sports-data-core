# 08. Persist and audit

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 1 (proof of concept) · **Status:** done 2026-10-10 (uncommitted); two checks open, see Outcome

**Depends on:** [02 Enablement and schema](02-enablement-and-schema.md), [07 Select and phrase](07-select-and-phrase.md)

**Feature doc:** [Stage 6: persist](instant-analysis.md#stage-6-persist) · [Logging and audit](instant-analysis.md#logging-and-audit)

## Goal

Every fact is stored against its triggering play, and every decision is logged.

## Scope

- Writing `PlayAnalysis` rows (one per fact, `PlayId` linked, one set per play per run).
- Seq structured events with the play's `CorrelationId`: play analysed, fact generated, fact dropped (with reason), and per-stage timings, all carrying `PlayId`.
- The post-game audit query from the feature doc, saved as a script.

**Out of scope:**

- Delivery to clients (task 10).
- The lab UI (task 09).

## Deliverables

- The persistence step.
- The logging.
- The audit script for walking a game play by play, under `docs/features/instant-analysis/sql/`.

## Acceptance criteria

- [x] After an opted-in game, the audit query lists every play in order with its facts, including plays with none.
- [ ] Dropped facts appear in Seq with their reason and `PlayId`. (Logged at Information with `PlayId` in scope and covered by unit tests; not yet seen in Seq, since the local Producer's Information logs aren't visible.)
- [x] Re-processing the same play doesn't create duplicate rows. (Unit test plus the unique index tested in task 02; the end-to-end second replay was blocked locally, see Outcome.)

## Outcome (2026-10-10)

The analysis job now persists each play's selected facts as `PlayAnalysis` rows in one save, and logs every
decision.

- **Rows:** one per selected fact, `Ordinal` = display order, `Priority`, `TemplateVersion`, `Text`, `CreatedUtc`
  from `IDateTimeProvider`, `CreatedBy` = the play's `CorrelationId`. `Facts` (jsonb) holds `kind`,
  `athleteSeasonId`, `franchiseSeasonId`, `value` and `provenance` (as built in tasks 05 and 06).
- **One set per play per run:** the job skips a play that already has rows for its run (`AnalysisRunId` null =
  live). A racing duplicate job hits the unique index on (`PlayId`, `AnalysisRunId`, `Ordinal`); its whole save
  fails, the first set stays, and it's logged as success.
- **Lab entry point ready:** the analysis takes a run id and a season-document rule (newest, or before kickoff), so
  task 09 only adds the lab trigger.
- **Logs** (Information, scoped with `CorrelationId`, `CausationId`, `ContestId`, `CompetitionId`, `PlayId`,
  `AnalysisRunId`): "Play analysed" (sequence, type id, kinds, actors, victims, roles), "Fact generated" (type,
  kind, scope, value, priority, text, ids, provenance as JSON), "Fact dropped" (type, kind, athlete, reason), and
  "Play analysis timing" (load, classify, game facts, season facts, select, persist, total ms; counts).
- **Audit script:** [`08a`](sql/08a-post-game-audit.sql).

### Local end-to-end (prod restore, the example game, flag on)

- Replay: all 200 plays analysed, 200 jobs succeeded, 0 failed. **22 plays produced 55 facts**: `GameCount` 20,
  `SeasonCount` 16, `GameLongest` 8, `GameCountAgainst` 5, `TeamGameCount` 5, `SeasonCountAgainst` 1. No duplicate
  sets. Sample: "T. Cooley's first interception of the game.", "LSU's first takeaway of the game.",
  "S. Leavitt's ninth touchdown pass of the season.", "J. Strong has been sacked once this game."
- **Every season fact double-counts on this data**, as task 06 predicted: today's documents were rewritten after
  the game and include it (`writtenAfterKickoff` = true on all 17). E.g. "T. Cooley's third interception of the
  season" (correct: second). Live, the document is normally from before the game; `writtenAfterKickoff` lets the
  audit find the cases where it isn't.
- **Second replay blocked:** after a restart, Hangfire re-runs about 50 `ICompetitionBroadcastingJob`s left in
  `Processing` (created 2026-10-07 to 10-10, restored from prod), which take every worker, so the second replay job
  stayed `Enqueued`. Duplicate protection is covered by the unit test and the task 02 index test instead.
