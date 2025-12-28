-- Cleanup orphaned retries in Hangfire
-- Run this against the Hangfire database (e.g., sdProvider.FootballNcaa.Hangfire, sdProducer.FootballNcaa.Hangfire)

BEGIN;

-- 1. Cleanup orphaned retries in hangfire.set
-- These are jobs that appear in the "Retries" tab but have no corresponding job record.
DELETE FROM hangfire.set s
WHERE s.key = 'retries'
AND NOT EXISTS (
    SELECT 1 FROM hangfire.job j WHERE j.id::text = s.value
);

-- 2. Cleanup orphaned jobqueue entries
-- These are jobs in the queue but the job record is gone.
DELETE FROM hangfire.jobqueue q
WHERE NOT EXISTS (
    SELECT 1 FROM hangfire.job j WHERE j.id = q.jobid
);

-- 3. Cleanup stuck 'Enqueued' jobs
-- These are jobs marked as 'Enqueued' but are not actually in the queue.
DELETE FROM hangfire.job j
WHERE j.statename = 'Enqueued'
AND NOT EXISTS (
    SELECT 1 FROM hangfire.jobqueue q WHERE q.jobid = j.id
);

COMMIT;
