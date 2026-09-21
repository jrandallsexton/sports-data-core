-- Remediation: move AP poll sourcing from 6 PM Eastern to just after the
-- 2 PM release, and express it in Eastern rather than UTC.
--
-- DATABASE: sdProvider.FootballNcaa
--
-- REQUIRES the CronTimeZoneId column (migration 21SepV1_ResourceIndexCronTimeZone)
-- and the SourcingJobOrchestrator change that reads it. Run this AFTER Provider
-- is deployed: on an older build the column does not exist and this fails;
-- worse, setting the cron alone would move sourcing to 14:10 UTC, which is
-- 10:10 AM Eastern — four hours BEFORE the poll is published.
--
-- Why it moves:
--   AP releases the poll at 14:00 Eastern on Sunday. Sourcing ran at 22:00 UTC
--   (18:00 Eastern), four hours later. The API's per-sport matchup scheduler now
--   runs Sunday 14:30 Eastern expecting the poll to be in the database, so
--   without this it would build every ranked league's slate from LAST week's
--   rankings — and that is sticky, because the refresh path adds newly-eligible
--   matchups but never removes ones that fell out (picks against them survive).
--
-- Why Eastern and not UTC:
--   The release is a broadcast clock. 14:00 Eastern is 18:00 UTC in summer and
--   19:00 UTC in winter, and the football season crosses the November DST
--   boundary — a fixed UTC cron would drift an hour mid-season.
--
-- Why 14:05 and not 14:25:
--   Sourcing is not instant. The ResourceIndex run enqueues documents that
--   Producer then processes into SeasonPollWeek + ranking rows, and the matchup
--   scheduler needs those ROWS, not just the fetch. 25 minutes is a judgement
--   call, not a measured SLA. If it proves tight, widen the gap here rather
--   than moving the matchup job later.
--
-- A late poll is not catastrophic either way: SeasonPollWeekCreated re-triggers
-- a refresh for ranked leagues that already have a week shell.
--
-- Ends in ROLLBACK. Change the last line to COMMIT once the preview looks right.

BEGIN;

-- 1) Before.
SELECT "Id", "Name", "CronExpression", "CronTimeZoneId", "IsEnabled", "IsRecurring"
FROM public."ResourceIndex"
WHERE "Id" = 'ab980339-9958-4238-8db1-7459c556b6c7';

-- 2) Guard: the row must be the rankings resource, recurring and enabled.
--    A wrong id would silently update nothing, which reads like success.
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM public."ResourceIndex"
    WHERE "Id" = 'ab980339-9958-4238-8db1-7459c556b6c7'
      AND "Name" LIKE '%seasons.rankings'
      AND "IsRecurring"
      AND "IsEnabled")
  THEN RAISE EXCEPTION 'Rankings ResourceIndex not found, or not enabled/recurring; aborting.';
  END IF;
END $$;

UPDATE public."ResourceIndex"
SET "CronExpression" = '5 14 * * 0',
    "CronTimeZoneId" = 'America/New_York',
    "ModifiedUtc" = now()
WHERE "Id" = 'ab980339-9958-4238-8db1-7459c556b6c7';

-- 3) After. Expect 5 14 * * 0 / America/New_York.
SELECT "Id", "Name", "CronExpression", "CronTimeZoneId", "ModifiedUtc"
FROM public."ResourceIndex"
WHERE "Id" = 'ab980339-9958-4238-8db1-7459c556b6c7';

-- 4) Everything else stays UTC. Only the rankings row should carry a zone.
SELECT "Name", "CronExpression", COALESCE("CronTimeZoneId", '(UTC)') AS zone
FROM public."ResourceIndex"
WHERE "IsRecurring" AND "IsEnabled"
ORDER BY "Ordinal";

ROLLBACK; --COMMIT;   -- change to COMMIT once the preview looks right

-- AFTER COMMITTING: restart the Provider pod. SourcingJobOrchestrator registers
-- these with Hangfire at startup, so the old 22:00 UTC schedule keeps running
-- until it re-registers.
