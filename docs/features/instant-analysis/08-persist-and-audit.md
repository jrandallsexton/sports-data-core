# 08. Persist and audit

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 1 (proof of concept) · **Status:** not started

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

- [ ] After an opted-in game, the audit query lists every play in order with its facts, including plays with none.
- [ ] Dropped facts appear in Seq with their reason and `PlayId`.
- [ ] Re-processing the same play doesn't create duplicate rows.
