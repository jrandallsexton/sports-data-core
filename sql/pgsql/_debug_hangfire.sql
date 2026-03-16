SELECT
  relname AS table_name,
  pg_size_pretty(pg_total_relation_size(relid)) AS total_size,
  n_live_tup AS live_rows
FROM
  pg_stat_user_tables
ORDER BY
  pg_total_relation_size(relid) DESC;

  SELECT DATE(createdat) as date, COUNT(*) 
FROM hangfire.job 
GROUP BY DATE(createdat) 
ORDER BY date DESC 
LIMIT 10;

SELECT queue, COUNT(*) FROM hangfire.jobqueue GROUP BY queue;

  select count(*) from hangfire."job" where "statename" = 'Enqueued';
  select count(*) from hangfire."state"
  SELECT * FROM hangfire."lock";

  SELECT COUNT(*)::int FROM hangfire.jobqueue WHERE fetchedat IS NULL

SELECT * FROM hangfire.jobqueue

select * from hangfire."job" limit 10;
  SELECT COUNT(*) as count 
FROM hangfire.job 
ORDER BY count DESC;

  DELETE FROM hangfire."lock" WHERE "resource" = 'hangfire:lock:recurring-job:SourcingJobOrchestrator';

-- 2026-03-16: Provider hangfire.job table has ~4.2M rows.
-- The metrics exporter CronJob hangs on COUNT(*) GROUP BY statename due to full sequential scan.
-- This index allows the GROUP BY to use an index scan instead.
-- Use CONCURRENTLY to avoid locking the table during creation.
-- Run against: sdProvider.FootballNcaa.Hangfire
CREATE INDEX CONCURRENTLY idx_job_statename ON hangfire.job (statename);

-- Purge old succeeded/deleted jobs if table continues to grow.
-- Hangfire expires succeeded jobs after 24h by default, but the expiration
-- process may fall behind under heavy load. Safe to remove manually:
-- DELETE FROM hangfire.job WHERE statename = 'Succeeded' AND createdat < NOW() - INTERVAL '48 hours';
-- DELETE FROM hangfire.job WHERE statename = 'Deleted' AND createdat < NOW() - INTERVAL '48 hours';

