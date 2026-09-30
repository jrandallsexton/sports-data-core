-- Remediation: NCAAFB 2026 week 3 matchups were generated before the Week 3
-- AP poll landed (poll-week filing bug, PR #758), so their ranks - and the
-- previews written from them - reflect the Week 2 poll. Wipe week 3 and let
-- MatchupScheduler regenerate against the corrected rankings.
--
-- DATABASE: sdApi.All
-- Companion: _remediation_wk3_matchups_notification_2026_09_14.sql (run after).
--
-- Scope key: SeasonWeek 7348ffda = NCAAFB 2026 regular-season week 3. SeasonWeek
-- ids are unique per sport, so this alone restricts every statement to NCAAFB.
--
-- The PickemGroupWeek rows are KEPT with AreMatchupsGenerated reset to false:
-- that is the condition MatchupScheduler polls for, so the next run regenerates
-- and re-fires PickemGroupWeekMatchupsGenerated (which re-enqueues previews).
--
-- Ends in ROLLBACK. Change to COMMIT once the preview counts look right.

BEGIN;

DROP TABLE IF EXISTS _wk5;
CREATE TEMP TABLE _wk5 AS
SELECT DISTINCT m."ContestId", m."GroupId"
FROM public."PickemGroupMatchup" m
WHERE m."SeasonWeekId" = 'fe57b4a9-4bb8-21b8-16de-269f1e032de1';

-- 1) Preview: what is about to go.
SELECT 'PickemGroupWeek (reset, not deleted)' AS what, count(*) FROM public."PickemGroupWeek" WHERE "SeasonWeekId" = 'fe57b4a9-4bb8-21b8-16de-269f1e032de1'
UNION ALL SELECT 'PickemGroupMatchup',            count(*) FROM public."PickemGroupMatchup" WHERE "SeasonWeekId" = 'fe57b4a9-4bb8-21b8-16de-269f1e032de1'
UNION ALL SELECT 'distinct contests',             count(DISTINCT "ContestId") FROM _wk5
UNION ALL SELECT 'UserPick',                      count(*) FROM public."UserPick" p WHERE p."Week" = 5 AND (p."PickemGroupId", p."ContestId") IN (SELECT "GroupId", "ContestId" FROM _wk5)
UNION ALL SELECT 'PickResult',                    count(*) FROM public."PickResult" r WHERE r."UserPickId" IN (SELECT p."Id" FROM public."UserPick" p WHERE p."Week" = 5 AND (p."PickemGroupId", p."ContestId") IN (SELECT "GroupId", "ContestId" FROM _wk5))
UNION ALL SELECT 'MatchupPreviewPrompt',          count(*) FROM public."MatchupPreviewPrompt" WHERE "ContestId" IN (SELECT "ContestId" FROM _wk5)
UNION ALL SELECT 'MatchupPreview',                count(*) FROM public."MatchupPreview" WHERE "ContestId" IN (SELECT "ContestId" FROM _wk5)
UNION ALL SELECT 'PickemGroupWeekResult wk5',     count(*) FROM public."PickemGroupWeekResult" r WHERE r."SeasonYear" = 2026 AND r."SeasonWeek" = 5 AND r."PickemGroupId" IN (SELECT DISTINCT "GroupId" FROM _wk5);

-- 2) Previews and their prompt audit rows (prompt rows point at the preview
--    with SetNull, so order does not matter; delete both for a clean regen).
DELETE FROM public."MatchupPreviewPrompt" WHERE "ContestId" IN (SELECT "ContestId" FROM _wk5);
DELETE FROM public."MatchupPreview"       WHERE "ContestId" IN (SELECT "ContestId" FROM _wk5);

-- 3) Picks made against the wiped matchups (week 5, these groups, these contests).
DELETE FROM public."PickResult"
WHERE "UserPickId" IN (
    SELECT p."Id" FROM public."UserPick" p
    WHERE p."Week" = 5 AND (p."PickemGroupId", p."ContestId") IN (SELECT "GroupId", "ContestId" FROM _wk5));
DELETE FROM public."UserPick" p
WHERE p."Week" = 5 AND (p."PickemGroupId", p."ContestId") IN (SELECT "GroupId", "ContestId" FROM _wk5);

-- 4) Week-5 standings rows (recomputed by LeagueWeekScoringJob once games score).
DELETE FROM public."PickemGroupWeekResult" r
WHERE r."SeasonYear" = 2026 AND r."SeasonWeek" = 5 AND r."PickemGroupId" IN (SELECT DISTINCT "GroupId" FROM _wk5);

-- 5) The matchups themselves.
DELETE FROM public."PickemGroupMatchup" WHERE "SeasonWeekId" = 'fe57b4a9-4bb8-21b8-16de-269f1e032de1';

-- 6) Un-latch the week so MatchupScheduler regenerates it.
UPDATE public."PickemGroupWeek"
SET "AreMatchupsGenerated" = false,
    "ModifiedUtc" = now(),
    "ModifiedBy"  = '00000000-0000-0000-0000-000000000000'
WHERE "SeasonWeekId" = 'fe57b4a9-4bb8-21b8-16de-269f1e032de1';

-- 7) Verify: expect 0 matchups, 0 previews for those contests, weeks un-latched.
SELECT 'matchups left' AS what, count(*) FROM public."PickemGroupMatchup" WHERE "SeasonWeekId" = 'fe57b4a9-4bb8-21b8-16de-269f1e032de1'
UNION ALL SELECT 'previews left',   count(*) FROM public."MatchupPreview" WHERE "ContestId" IN (SELECT "ContestId" FROM _wk5    )
UNION ALL SELECT 'weeks still latched', count(*) FROM public."PickemGroupWeek" WHERE "SeasonWeekId" = 'fe57b4a9-4bb8-21b8-16de-269f1e032de1' AND "AreMatchupsGenerated";

COMMIT; --ROLLBACK; --;   -- change to COMMIT
