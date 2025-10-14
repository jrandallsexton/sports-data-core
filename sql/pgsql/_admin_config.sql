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
