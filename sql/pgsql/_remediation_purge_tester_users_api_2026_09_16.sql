-- Remediation: hard-delete the Play-testing accounts, 2026-09-16.
--
-- DATABASE: sdApi.All
-- Companion: _remediation_purge_tester_users_notification_2026_09_16.sql (run after).
--
-- The account-deletion feature soft-deletes on purpose (anonymize in place,
-- keep the row so pick/membership/standings FKs stay valid). These three
-- rows are the Google Play review testers: they joined leagues and never
-- picked, and the operator wants them GONE, not anonymized. One-off.
--
-- Scope: User rows with DeletedUtc set AND the anonymization sentinel email
-- ("deleted-<id>@deleted.invalid"). Both conditions, so a live account with
-- a stray DeletedUtc can never match.
--
-- Guards abort the transaction if a tester turns out to be a league
-- commissioner or to have authored articles/messages - none do today, but
-- those are RESTRICT FKs and the right answer there is a human decision,
-- not a cascade.
--
-- Ends in ROLLBACK. Change to COMMIT once the preview matches expectations
-- (locally on 2026-09-16: 3 users, 4 memberships, 4 zero-pick week results,
-- 0 picks, 0 everything else).

BEGIN;

DROP TABLE IF EXISTS _testers;
CREATE TEMP TABLE _testers AS
SELECT "Id"
FROM public."User"
WHERE "DeletedUtc" IS NOT NULL
  AND "Email" LIKE 'deleted-%@deleted.invalid';

-- 1) Preview.
SELECT 'User (to delete)' AS what, count(*) FROM _testers
UNION ALL SELECT 'PickemGroupMember',              count(*) FROM public."PickemGroupMember"        WHERE "UserId" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'UserPick',                       count(*) FROM public."UserPick"                 WHERE "UserId" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'PickResult (via UserPick)',      count(*) FROM public."PickResult"               WHERE "UserPickId" IN (SELECT "Id" FROM public."UserPick" WHERE "UserId" IN (SELECT "Id" FROM _testers))
UNION ALL SELECT 'PickemGroupWeekResult',          count(*) FROM public."PickemGroupWeekResult"    WHERE "UserId" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT '  ...of which TotalPicks > 0',   count(*) FROM public."PickemGroupWeekResult"    WHERE "UserId" IN (SELECT "Id" FROM _testers) AND "TotalPicks" > 0
UNION ALL SELECT 'LeagueStandingHistory',          count(*) FROM public."LeagueStandingHistory"    WHERE "UserId" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'PlayerLineup',                   count(*) FROM public."PlayerLineup"             WHERE "UserId" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'PickemGroupInvitations (either side)', count(*) FROM public."PickemGroupInvitations" WHERE "InviteeUserId" IN (SELECT "Id" FROM _testers) OR "InvitedByUserId" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'MessageReaction',                count(*) FROM public."MessageReaction"          WHERE "UserId" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'UserNotificationPreferences',    count(*) FROM public."UserNotificationPreferences" WHERE "UserId" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'UserOption',                     count(*) FROM public."UserOption"               WHERE "UserId" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'GUARD PickemGroup commissioner (must be 0)', count(*) FROM public."PickemGroup"  WHERE "CommissionerUserId" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'GUARD Article author (must be 0)',          count(*) FROM public."Article"       WHERE "AuthorId" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'GUARD MessageThread (must be 0)',           count(*) FROM public."MessageThread" WHERE "UserId" IN (SELECT "Id" FROM _testers) OR "CreatedBy" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'GUARD MessagePost (must be 0)',             count(*) FROM public."MessagePost"   WHERE "CreatedBy" IN (SELECT "Id" FROM _testers);

-- 2) Guards: a tester who owns a league or authored content is not a
--    tester; stop and look. (RAISE aborts the whole transaction.)
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM public."PickemGroup"   WHERE "CommissionerUserId" IN (SELECT "Id" FROM _testers)) THEN RAISE EXCEPTION 'A matched user is a league commissioner; aborting.'; END IF;
  IF EXISTS (SELECT 1 FROM public."Article"       WHERE "AuthorId"           IN (SELECT "Id" FROM _testers)) THEN RAISE EXCEPTION 'A matched user authored articles; aborting.'; END IF;
  IF EXISTS (SELECT 1 FROM public."MessageThread" WHERE "UserId" IN (SELECT "Id" FROM _testers) OR "CreatedBy" IN (SELECT "Id" FROM _testers)) THEN RAISE EXCEPTION 'A matched user has message threads; aborting.'; END IF;
  IF EXISTS (SELECT 1 FROM public."MessagePost"   WHERE "CreatedBy"          IN (SELECT "Id" FROM _testers)) THEN RAISE EXCEPTION 'A matched user has message posts; aborting.'; END IF;
END $$;

-- 3) Picks and their scoring rows (none expected; explicit rather than
--    relying on the cascade so the counts print).
DELETE FROM public."PickResult" WHERE "UserPickId" IN (SELECT "Id" FROM public."UserPick" WHERE "UserId" IN (SELECT "Id" FROM _testers));
DELETE FROM public."UserPick"   WHERE "UserId" IN (SELECT "Id" FROM _testers);

-- 4) RESTRICT FKs that would block the User delete.
DELETE FROM public."PickemGroupWeekResult"  WHERE "UserId" IN (SELECT "Id" FROM _testers);
DELETE FROM public."PickemGroupInvitations" WHERE "InviteeUserId" IN (SELECT "Id" FROM _testers) OR "InvitedByUserId" IN (SELECT "Id" FROM _testers);

-- 5) Everything else keyed by UserId (cascades would take these; explicit for the counts).
DELETE FROM public."LeagueStandingHistory"       WHERE "UserId" IN (SELECT "Id" FROM _testers);
DELETE FROM public."PlayerLineup"                WHERE "UserId" IN (SELECT "Id" FROM _testers);
DELETE FROM public."MessageReaction"             WHERE "UserId" IN (SELECT "Id" FROM _testers);
DELETE FROM public."UserNotificationPreferences" WHERE "UserId" IN (SELECT "Id" FROM _testers);
DELETE FROM public."UserOption"                  WHERE "UserId" IN (SELECT "Id" FROM _testers);
DELETE FROM public."PickemGroupMember"           WHERE "UserId" IN (SELECT "Id" FROM _testers);

-- 6) The users.
DELETE FROM public."User" WHERE "Id" IN (SELECT "Id" FROM _testers);

-- 7) Verify: expect 0 / 0.
SELECT 'users left' AS what, count(*) FROM public."User" WHERE "Id" IN (SELECT "Id" FROM _testers)
UNION ALL SELECT 'memberships left', count(*) FROM public."PickemGroupMember" WHERE "UserId" IN (SELECT "Id" FROM _testers);

COMMIT; --ROLLBACK;   -- change to COMMIT
