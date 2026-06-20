-- Global limits / usage
SHOW max_connections;

SELECT count(*) AS total, 
       sum(CASE WHEN state='active' THEN 1 ELSE 0 END) AS active,
       sum(CASE WHEN state='idle in transaction' THEN 1 ELSE 0 END) AS idle_tx
FROM pg_stat_activity;

-- By database
SELECT datname, count(*) AS conns
FROM pg_stat_activity GROUP BY datname ORDER BY conns DESC;

-- By user
SELECT usename, count(*) AS conns
FROM pg_stat_activity GROUP BY usename ORDER BY conns DESC;

SELECT pg_size_pretty(pg_database_size(datname)) AS size, datname FROM pg_database ORDER BY pg_database_size(datname) DESC;

-- ─── Lock contention diagnostics ──────────────────────────────────────────────
-- Find sessions blocked on a lock + who's blocking them. Sort by oldest
-- blocker transaction first — a zombie "idle in transaction" left over from
-- a failed pod startup or an abandoned psql session shows up at the top.
-- Use case: EF "LOCK TABLE __EFMigrationsHistory IN ACCESS EXCLUSIVE MODE"
-- timing out at 30s on new pod startup means another session is sitting on
-- the table; this query identifies who.
-- Once identified: SELECT pg_terminate_backend(<blocker_pid>);
SELECT
  blocked.pid              AS blocked_pid,
  blocked.application_name AS blocked_app,
  blocked.wait_event,
  blocker.pid              AS blocker_pid,
  blocker.application_name AS blocker_app,
  blocker.state            AS blocker_state,
  blocker.xact_start       AS blocker_xact_start,
  now() - blocker.xact_start AS blocker_xact_age,
  blocker.query            AS blocker_query
FROM pg_stat_activity blocked
JOIN pg_stat_activity blocker ON blocker.pid = ANY(pg_blocking_pids(blocked.pid))
WHERE blocked.wait_event_type = 'Lock'
ORDER BY blocker_xact_age DESC;
