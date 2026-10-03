-- Companion to _remediation_purge_user_league_activity_api_2026_09_19.sql.
-- Run AFTER it, with the SAME ids.
--
-- DATABASE: sdNotification.All
--
-- Notification keeps its own replica of users, memberships and picks, and
-- schedules reminders from them. Nothing here reacts to the API-side deletes
-- (the UserDeleted path only fires on account deletion, and these accounts
-- are staying), so without this the service keeps scheduling pick-deadline
-- reminders for leagues the user is no longer in.
--
-- Same stance as the API script: the USER row stays. Only league
-- participation and anything that would generate a notification about it is
-- removed. UserDevices and UserNotificationPreferences are left alone — the
-- accounts are still real and may still be signed in on a device.
--
-- No FKs between these tables; order is for readability.
-- Ends in ROLLBACK. Change the last line to COMMIT once the preview looks right.

BEGIN;

DROP TABLE IF EXISTS _targets;
CREATE TEMP TABLE _targets ("Id" uuid);
INSERT INTO _targets VALUES
  ('604df3e2-f8be-4b1e-b67b-1113bf895fe2')   -- <-- same ids as the API script
;

-- Sanity: the same accounts, in this database.
SELECT "Id", "DisplayName", "Email" FROM public."Users" WHERE "Id" IN (SELECT "Id" FROM _targets);

-- 1) Preview. PendingScheduledJobs is the one that keeps costing you: those
--    are reminders the service re-evaluates on every sweep.
SELECT 'Users matched (expect > 0)' AS what, count(*) FROM public."Users" WHERE "Id" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'PickemGroupMembers',            count(*) FROM public."PickemGroupMembers"            WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'NotificationMemberships',       count(*) FROM public."NotificationMemberships"       WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'PendingScheduledJobs',          count(*) FROM public."PendingScheduledJobs"          WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'UserPicks',                     count(*) FROM public."UserPicks"                     WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'NotificationUserPicks',         count(*) FROM public."NotificationUserPicks"         WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'NotificationContestStarts',     count(*) FROM public."NotificationContestStarts"     WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'NotificationPickDeadlines',     count(*) FROM public."NotificationPickDeadlines"     WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'NotificationPollReleases',      count(*) FROM public."NotificationPollReleases"      WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'NotificationMatchupsReady',     count(*) FROM public."NotificationMatchupsReady"     WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'NotificationLeagueInvitations', count(*) FROM public."NotificationLeagueInvitations" WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'NotificationLog (history)',     count(*) FROM public."NotificationLog"               WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'UserDevices (KEPT)',            count(*) FROM public."UserDevices"                   WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'UserNotificationPreferences (KEPT)', count(*) FROM public."UserNotificationPreferences" WHERE "UserId" IN (SELECT "Id" FROM _targets);

-- 2) Scheduled work first — stop the reminders before removing what they are
--    scheduled from.
DELETE FROM public."PendingScheduledJobs"          WHERE "UserId" IN (SELECT "Id" FROM _targets);
DELETE FROM public."NotificationContestStarts"     WHERE "UserId" IN (SELECT "Id" FROM _targets);
DELETE FROM public."NotificationPickDeadlines"     WHERE "UserId" IN (SELECT "Id" FROM _targets);
DELETE FROM public."NotificationPollReleases"      WHERE "UserId" IN (SELECT "Id" FROM _targets);
DELETE FROM public."NotificationMatchupsReady"     WHERE "UserId" IN (SELECT "Id" FROM _targets);
DELETE FROM public."NotificationLeagueInvitations" WHERE "UserId" IN (SELECT "Id" FROM _targets);

-- 3) The replicated participation data.
DELETE FROM public."NotificationUserPicks"   WHERE "UserId" IN (SELECT "Id" FROM _targets);
DELETE FROM public."UserPicks"               WHERE "UserId" IN (SELECT "Id" FROM _targets);
DELETE FROM public."NotificationMemberships" WHERE "UserId" IN (SELECT "Id" FROM _targets);
DELETE FROM public."PickemGroupMembers"      WHERE "UserId" IN (SELECT "Id" FROM _targets);

-- 4) Delivery history. Uncomment ONLY if you want the audit trail of what was
--    already sent to these accounts gone too; it changes nothing operationally.
-- DELETE FROM public."NotificationLog" WHERE "UserId" IN (SELECT "Id" FROM _targets);

-- Deliberately NOT touched: Users, UserDevices, UserNotificationPreferences.
-- These accounts are staying and may still be signed in.

-- 5) Verify: every count must be 0, and 'user still exists' must be > 0.
SELECT 'user still exists (expect > 0)' AS what, count(*) FROM public."Users" WHERE "Id" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'memberships left',        count(*) FROM public."PickemGroupMembers"      WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'notif memberships left',  count(*) FROM public."NotificationMemberships" WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'picks left',              count(*) FROM public."UserPicks"               WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'pending jobs left',       count(*) FROM public."PendingScheduledJobs"    WHERE "UserId" IN (SELECT "Id" FROM _targets);

COMMIT; --ROLLBACK; --COMMIT;   -- change to COMMIT once the preview looks right
