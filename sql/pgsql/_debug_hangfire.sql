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





