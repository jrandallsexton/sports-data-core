-- Companion to _remediation_wk3_matchups_api_2026_09_14.sql. Run AFTER it.
--
-- DATABASE: sdNotification.All
--
-- Notification keeps its own replica of league matchups (fed by
-- PickemGroupMatchupCreated) for kickoff waves and pick-deadline reminders.
-- The week-3 NCAAFB rows there describe matchups that no longer exist; the
-- regeneration republishes them, so clear the stale copies first.
--
-- NotificationMatchupsReady is the "your week-3 matchups are ready" dedup log.
-- Deleting those rows means members get that push AGAIN when the week
-- regenerates. Left as an operator choice below (commented out).
--
-- Ends in ROLLBACK. Change to COMMIT once the preview looks right.

BEGIN;

-- 1) Preview.
SELECT 'PickemGroupMatchups wk3 (NCAAFB)' AS what, count(*)
FROM public."PickemGroupMatchups" m
JOIN public."PickemGroups" g ON g."Id" = m."PickemGroupId"
WHERE m."SeasonYear" = 2026 AND m."SeasonWeek" = 5 AND g."Sport" = 2
UNION ALL
SELECT 'NotificationMatchupsReady wk3 (NCAAFB) - ' || r."Result", count(*)
FROM public."NotificationMatchupsReady" r
JOIN public."PickemGroups" g ON g."Id" = r."LeagueId"
WHERE r."SeasonYear" = 2026 AND r."SeasonWeek" = 5 AND g."Sport" = 2
GROUP BY r."Result";

-- 2) Stale matchup replica rows.
DELETE FROM public."PickemGroupMatchups" m
USING public."PickemGroups" g
WHERE g."Id" = m."PickemGroupId"
  AND m."SeasonYear" = 2026 AND m."SeasonWeek" = 5 AND g."Sport" = 2;

-- 3) OPTIONAL - re-arm the "matchups ready" push for week 5.
--    Uncomment only if members should be notified again after regeneration.
-- DELETE FROM public."NotificationMatchupsReady" r
-- USING public."PickemGroups" g
-- WHERE g."Id" = r."LeagueId"
--   AND r."SeasonYear" = 2026 AND r."SeasonWeek" = 5 AND g."Sport" = 2;

-- 4) Verify: expect 0.
SELECT count(*) AS "StaleMatchupsLeft"
FROM public."PickemGroupMatchups" m
JOIN public."PickemGroups" g ON g."Id" = m."PickemGroupId"
WHERE m."SeasonYear" = 2026 AND m."SeasonWeek" = 5 AND g."Sport" = 2;

COMMIT; --ROLLBACK; --;   -- change to COMMIT
