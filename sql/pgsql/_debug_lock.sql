-- Run against sdProducer.FootballNcaa on 192.168.0.250

-- Step 1: Find the distinct blocking PIDs (root cause)
SELECT DISTINCT
    blocking_locks.pid AS blocking_pid,
    act.state AS blocking_state,
    act.query AS blocking_query,
    act.query_start
FROM pg_locks blocked_locks
JOIN pg_locks blocking_locks
    ON blocking_locks.locktype = blocked_locks.locktype
    AND blocking_locks.relation = blocked_locks.relation
    AND blocking_locks.pid != blocked_locks.pid
JOIN pg_stat_activity act
    ON act.pid = blocking_locks.pid
WHERE NOT blocked_locks.granted
ORDER BY act.query_start;

-- Step 2: Kill ALL idle-in-transaction sessions on this database
-- (these are the stuck transactions holding locks)
SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE datname = 'sdProducer.FootballNcaa'
  AND state = 'idle in transaction'
  AND pid != pg_backend_pid();
