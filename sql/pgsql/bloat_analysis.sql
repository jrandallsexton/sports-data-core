-- Table bloat analysis and MassTransit outbox cleanup.
-- Run against: sdProducer.FootballNcaa, sdProvider.FootballNcaa, sdApi.All
--              (any non-Hangfire database — do NOT run against Hangfire databases)
--
-- For Hangfire purge and vacuum, see: purge_succeeded_jobs.sql

-- =============================================================================
-- STEP 1: Table bloat analysis
-- =============================================================================
-- Shows largest tables with bytes-per-row to identify bloat.
-- If bytes_per_row is >10KB for tables without large text/JSON columns, bloat is likely.
-- VACUUM FULL on those tables will reclaim space (requires exclusive lock).

SELECT
    schemaname || '.' || relname AS table_name,
    pg_size_pretty(pg_total_relation_size(relid)) AS total_size,
    n_live_tup AS live_rows,
    n_dead_tup AS dead_rows,
    CASE WHEN n_live_tup > 0
         THEN pg_total_relation_size(relid) / n_live_tup
         ELSE 0
    END AS bytes_per_row
FROM pg_stat_user_tables
ORDER BY pg_total_relation_size(relid) DESC
LIMIT 20;

-- Results from sdProducer.FootballNcaa (2026-03-24):
-- Data tables are legitimate (~200-500 bytes/row). No significant bloat.
-- OutboxMessage (2091 MB, 0 live rows) and OutboxState (962 MB, 0 live rows) are pure bloat.

-- =============================================================================
-- STEP 2: Reclaim MassTransit outbox bloat
-- =============================================================================
-- Run against: sdProducer.FootballNcaa (and any other database using MassTransit outbox)
-- OutboxMessage and OutboxState accumulate dead space as messages are processed and deleted.
-- WARNING: VACUUM FULL takes an exclusive lock — no reads or writes until done.

VACUUM FULL public."OutboxMessage";
VACUUM FULL public."OutboxState";
