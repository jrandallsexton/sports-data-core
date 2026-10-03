-- Companion to _remediation_purge_tester_users_api_2026_09_16.sql. Run AFTER it.
--
-- DATABASE: sdNotification.All
--
-- Notification keeps its own replica of users and memberships and schedules
-- reminders from them. The UserDeleted purge (UserDeletedConsumer) never ran
-- for these three - the replica still carries them, plus 200+ pending
-- reminder jobs it keeps re-evaluating for accounts with no device. Same
-- footprint the consumer would remove, done by hand.
--
-- No FKs into Users here; order is for readability only.
-- Ends in ROLLBACK. Change to COMMIT once the preview looks right.

BEGIN;

DROP TABLE IF EXISTS _testers;
CREATE TEMP TABLE _testers ("Id" uuid);
INSERT INTO _testers VALUES
  ('49260c5b-6840-446d-93c1-ddc85004750e'),
  ('76eb3c4b-e7a1-4e85-81b3-8ba6d14520f4'),
  ('783715ce-b7d0-459e-8852-ff902ef78753');

-- Sanity: these ids must be the anonymized testers in THIS database too.
SELECT "Id", "DisplayName", "Email" FROM public."Users" WHERE "Id" IN (SELECT "Id" FROM _testers);

-- 1) Preview.
SELECT 'Users' AS what, count(*) FROM public."Users" WHERE "Id" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'PickemGroupMembers',            count(*) FROM public."PickemGroupMembers"            WHERE "UserId" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'NotificationMemberships',       count(*) FROM public."NotificationMemberships"       WHERE "UserId" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'PendingScheduledJobs',          count(*) FROM public."PendingScheduledJobs"          WHERE "UserId" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'NotificationContestStarts',     count(*) FROM public."NotificationContestStarts"     WHERE "UserId" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'NotificationPickDeadlines',     count(*) FROM public."NotificationPickDeadlines"     WHERE "UserId" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'NotificationPollReleases',      count(*) FROM public."NotificationPollReleases"      WHERE "UserId" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'NotificationMatchupsReady',     count(*) FROM public."NotificationMatchupsReady"     WHERE "UserId" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'NotificationLeagueInvitations', count(*) FROM public."NotificationLeagueInvitations" WHERE "UserId" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'NotificationLog',               count(*) FROM public."NotificationLog"               WHERE "UserId" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'NotificationUserPicks',         count(*) FROM public."NotificationUserPicks"         WHERE "UserId" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'UserPicks',                     count(*) FROM public."UserPicks"                     WHERE "UserId" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'UserDevices',                   count(*) FROM public."UserDevices"                   WHERE "UserId" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'UserNotificationPreferences',   count(*) FROM public."UserNotificationPreferences"   WHERE "UserId" IN (SELECT "Id" FROM _testers);

-- 2) Purge.
DELETE FROM public."PendingScheduledJobs"          WHERE "UserId" IN (SELECT "Id" FROM _testers);
DELETE FROM public."NotificationContestStarts"     WHERE "UserId" IN (SELECT "Id" FROM _testers);
DELETE FROM public."NotificationPickDeadlines"     WHERE "UserId" IN (SELECT "Id" FROM _testers);
DELETE FROM public."NotificationPollReleases"      WHERE "UserId" IN (SELECT "Id" FROM _testers);
DELETE FROM public."NotificationMatchupsReady"     WHERE "UserId" IN (SELECT "Id" FROM _testers);
DELETE FROM public."NotificationLeagueInvitations" WHERE "UserId" IN (SELECT "Id" FROM _testers);
DELETE FROM public."NotificationLog"               WHERE "UserId" IN (SELECT "Id" FROM _testers);
DELETE FROM public."NotificationUserPicks"         WHERE "UserId" IN (SELECT "Id" FROM _testers);
DELETE FROM public."UserPicks"                     WHERE "UserId" IN (SELECT "Id" FROM _testers);
DELETE FROM public."UserDevices"                   WHERE "UserId" IN (SELECT "Id" FROM _testers);
DELETE FROM public."UserNotificationPreferences"   WHERE "UserId" IN (SELECT "Id" FROM _testers);
DELETE FROM public."NotificationMemberships"       WHERE "UserId" IN (SELECT "Id" FROM _testers);
DELETE FROM public."PickemGroupMembers"            WHERE "UserId" IN (SELECT "Id" FROM _testers);
DELETE FROM public."Users"                         WHERE "Id"     IN (SELECT "Id" FROM _testers);

-- 3) Verify: expect 0 / 0.
SELECT 'users left' AS what, count(*) FROM public."Users" WHERE "Id" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'memberships left', count(*) FROM public."PickemGroupMembers" WHERE "UserId" IN (SELECT "Id" FROM _testers);

COMMIT; --ROLLBACK;   -- change to COMMIT
