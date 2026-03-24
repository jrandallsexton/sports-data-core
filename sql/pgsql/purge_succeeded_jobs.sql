-- Purge succeeded Hangfire jobs older than 7 days.
-- Run against: sdProvider.FootballNcaa.Hangfire AND sdProducer.FootballNcaa.Hangfire
--
-- This is the local equivalent of the K8s CronJob (hangfire-purge-succeeded).
-- Use this to validate the purge logic against a local backup before deploying.
--
-- Related tables (state, jobparameter) have FK cascades, so deleting from
-- hangfire.job automatically cleans up child rows.
--
-- After running, consider VACUUM ANALYZE to reclaim disk space and refresh stats.

-- =============================================================================
-- STEP 1: Preview — understand what's in the database before touching anything
-- =============================================================================

-- Job counts by state
SELECT statename, COUNT(*) AS cnt
FROM hangfire.job
GROUP BY statename
ORDER BY cnt DESC;

-- Succeeded jobs: how many succeeded more than 7 days ago vs. recently?
-- Uses hangfire.state.createdat (when the job entered 'Succeeded' state),
-- NOT hangfire.job.createdat (when the job was first enqueued).
SELECT
    COUNT(*) FILTER (WHERE s.createdat < NOW() - INTERVAL '7 days') AS to_purge,
    COUNT(*) FILTER (WHERE s.createdat >= NOW() - INTERVAL '7 days') AS to_keep
FROM hangfire.job j
INNER JOIN hangfire.state s ON s.jobid = j.id
WHERE j.statename = 'Succeeded'
  AND s.name = 'Succeeded';

-- Table sizes before purge
SELECT
    relname AS table_name,
    pg_size_pretty(pg_total_relation_size(relid)) AS total_size,
    n_live_tup AS live_rows,
    n_dead_tup AS dead_rows
FROM pg_stat_user_tables
WHERE schemaname = 'hangfire'
ORDER BY pg_total_relation_size(relid) DESC;

-- =============================================================================
-- STEP 2: Batch delete — removes 50,000 rows per iteration
-- Adjust the interval or batch size as needed.
-- =============================================================================

DO $$
DECLARE
    _deleted INT := 1;
    _total   BIGINT := 0;
BEGIN
    WHILE _deleted > 0 LOOP
        WITH to_delete AS (
            SELECT j.id FROM hangfire.job j
            INNER JOIN hangfire.state s ON s.jobid = j.id
            WHERE j.statename = 'Succeeded'
              AND s.name = 'Succeeded'
              AND s.createdat < NOW() - INTERVAL '7 days'
            LIMIT 50000
        ),
        removed AS (
            DELETE FROM hangfire.job
            WHERE id IN (SELECT id FROM to_delete)
            RETURNING id
        )
        SELECT COUNT(*) INTO _deleted FROM removed;

        _total := _total + _deleted;
        RAISE NOTICE 'Deleted % rows (% total)', _deleted, _total;

        -- Brief pause to let other transactions breathe
        PERFORM pg_sleep(0.5);
    END LOOP;

    RAISE NOTICE 'Done. Total succeeded jobs purged: %', _total;
END $$;

-- =============================================================================
-- STEP 3: Post-purge — verify results and refresh planner stats
-- =============================================================================

ANALYZE hangfire.job;
ANALYZE hangfire.state;
ANALYZE hangfire.jobparameter;

-- Job counts after purge
SELECT statename, COUNT(*) AS cnt
FROM hangfire.job
GROUP BY statename
ORDER BY cnt DESC;

-- Table sizes after purge (space won't shrink until VACUUM FULL, but live_rows will drop)
SELECT
    relname AS table_name,
    pg_size_pretty(pg_total_relation_size(relid)) AS total_size,
    n_live_tup AS live_rows,
    n_dead_tup AS dead_rows
FROM pg_stat_user_tables
WHERE schemaname = 'hangfire'
ORDER BY pg_total_relation_size(relid) DESC;

-- =============================================================================
-- STEP 4: VACUUM FULL — reclaim disk space by rewriting tables
-- =============================================================================
-- WARNING: Takes an exclusive lock per table — no reads or writes until done.
-- Safe on a local backup. In prod, stop Hangfire workers first.
-- Run against: sdProvider.FootballNcaa.Hangfire AND sdProducer.FootballNcaa.Hangfire

VACUUM FULL hangfire.job;
VACUUM FULL hangfire.state;
VACUUM FULL hangfire.jobparameter;
VACUUM FULL hangfire.counter;
VACUUM FULL hangfire.lock;
VACUUM FULL hangfire.jobqueue;

-- Verify space reclaimed
SELECT
    relname AS table_name,
    pg_size_pretty(pg_total_relation_size(relid)) AS total_size,
    n_live_tup AS live_rows
FROM pg_stat_user_tables
WHERE schemaname = 'hangfire'
ORDER BY pg_total_relation_size(relid) DESC;

-- See also: bloat_analysis.sql for non-Hangfire table bloat analysis and outbox cleanup.
