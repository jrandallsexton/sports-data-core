SELECT
  relname AS table_name,
  pg_size_pretty(pg_total_relation_size(relid)) AS total_size,
  n_live_tup AS live_rows
FROM
  pg_stat_user_tables
ORDER BY
  pg_total_relation_size(relid) DESC;

  select count(*) from hangfire."job"
  select count(*) from hangfire."state"
  SELECT * FROM hangfire."lock";

  DELETE FROM hangfire."lock" WHERE "resource" = 'hangfire:lock:recurring-job:SourcingJobOrchestrator';





