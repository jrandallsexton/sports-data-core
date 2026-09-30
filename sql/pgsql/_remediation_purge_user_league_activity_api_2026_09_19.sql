-- Remediation: strip a user's league activity WITHOUT deleting the user.
--
-- DATABASE: sdApi.All
-- Companion: _remediation_purge_user_league_activity_notification_2026_09_19.sql (run after).
--
-- Context (2026-09-19): test accounts joined a REAL, active league during
-- earlier testing. A few picks were made, the vast majority of games were
-- not, and three weeks in they are still sitting in the standings as noise.
-- Unlike the 2026-09-16 tester purge these accounts STAY — only their
-- participation is removed.
--
-- IMPORTANT — why PickemGroupWeekResult is in scope even though you only
-- asked for picks and memberships:
--
--   GetLeagueScoresByWeekQueryHandler reads PickemGroupWeekResults keyed by
--   (PickemGroupId, UserId). It does NOT join PickemGroupMember. So deleting
--   picks and memberships alone leaves the standings page looking exactly the
--   same — the zero-pick rows are what you are seeing. Removing those rows is
--   the part that actually fixes the symptom.
--
--   GetLeaderboardQueryHandler is the other surface; it reads scored UserPicks
--   directly, so it clears once the picks are gone.
--
-- Scope: set _targets below. Leaves the User row, its options, notification
-- preferences and any message content untouched.
--
-- Ends in ROLLBACK. Change the last line to COMMIT once the preview matches.
--select * from public."User" order by "DisplayName";
--select * from public."PickemGroup" where "CommissionerUserId" = '604df3e2-f8be-4b1e-b67b-1113bf895fe2';
--update public."PickemGroup" set "CommissionerUserId" = '5fa4c116-1993-4f2b-9729-c50c62150813' where "Id" = '368c1acd-27ba-4d7d-804f-e7819cb4b273'; -- change commissioner to StatBot

BEGIN;

DROP TABLE IF EXISTS _targets;
CREATE TEMP TABLE _targets ("Id" uuid);
INSERT INTO _targets VALUES
  ('604df3e2-f8be-4b1e-b67b-1113bf895fe2')   -- <-- replace; add rows as needed
;

-- Sanity: confirm these are the accounts you mean BEFORE reading further.
-- A typo'd id simply matches nothing, and every count below would read 0.
SELECT "Id", "Username", "DisplayName", "Email", "DeletedUtc"
FROM public."User"
WHERE "Id" IN (SELECT "Id" FROM _targets);

-- 1) Preview. "Leagues affected" is the blast radius that matters: those are
--    the standings pages that will change.
SELECT 'Users matched (expect > 0)' AS what, count(*) FROM public."User" WHERE "Id" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'Leagues affected',             count(DISTINCT "PickemGroupId") FROM public."PickemGroupMember"     WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'PickemGroupMember',            count(*) FROM public."PickemGroupMember"     WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'UserPick',                     count(*) FROM public."UserPick"              WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT '  ...of which scored',         count(*) FROM public."UserPick"              WHERE "UserId" IN (SELECT "Id" FROM _targets) AND "ScoredAt" IS NOT NULL
UNION ALL SELECT 'PickemGroupWeekResult',        count(*) FROM public."PickemGroupWeekResult" WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT '  ...of which TotalPicks > 0', count(*) FROM public."PickemGroupWeekResult" WHERE "UserId" IN (SELECT "Id" FROM _targets) AND "TotalPicks" > 0
UNION ALL SELECT 'LeagueStandingHistory',        count(*) FROM public."LeagueStandingHistory" WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'PlayerLineup',                 count(*) FROM public."PlayerLineup"          WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'PickemGroupInvitations (either side)', count(*) FROM public."PickemGroupInvitations" WHERE "InviteeUserId" IN (SELECT "Id" FROM _targets) OR "InvitedByUserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'GUARD commissioner (must be 0)',      count(*) FROM public."PickemGroup"    WHERE "CommissionerUserId" IN (SELECT "Id" FROM _targets);

-- Which leagues, by name — worth eyeballing before you commit to touching a
-- real league's standings.
SELECT g."Id" AS "LeagueId", g."Name", count(*) AS "MembershipsToDelete"
FROM public."PickemGroupMember" m
JOIN public."PickemGroup" g ON g."Id" = m."PickemGroupId"
WHERE m."UserId" IN (SELECT "Id" FROM _targets)
GROUP BY g."Id", g."Name"
ORDER BY g."Name";

-- 2) Guard. Removing a commissioner's membership orphans their league, so
--    that is a human decision, not a cleanup script. (RAISE aborts the
--    whole transaction.)
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM public."PickemGroup" WHERE "CommissionerUserId" IN (SELECT "Id" FROM _targets))
    THEN RAISE EXCEPTION 'A target user commissions a league; aborting.';
  END IF;
END $$;

-- 3) Picks and their scoring rows. PickResult first — it references UserPick.
DELETE FROM public."PickResult" WHERE "UserPickId" IN (
  SELECT "Id" FROM public."UserPick" WHERE "UserId" IN (SELECT "Id" FROM _targets));
DELETE FROM public."UserPick" WHERE "UserId" IN (SELECT "Id" FROM _targets);

-- 4) Standings artifacts. THIS is what clears the standings page.
DELETE FROM public."PickemGroupWeekResult" WHERE "UserId" IN (SELECT "Id" FROM _targets);
DELETE FROM public."LeagueStandingHistory" WHERE "UserId" IN (SELECT "Id" FROM _targets);

-- 5) Player Pick'em lineups, if the league carried them.
DELETE FROM public."PlayerLineup" WHERE "UserId" IN (SELECT "Id" FROM _targets);

-- 6) Invitations, so the account cannot be re-added by an outstanding invite.
DELETE FROM public."PickemGroupInvitations"
WHERE "InviteeUserId" IN (SELECT "Id" FROM _targets)
   OR "InvitedByUserId" IN (SELECT "Id" FROM _targets);

-- 7) The memberships themselves, last: everything above is keyed by user +
--    league, and deleting membership first would make the preview and the
--    deletes disagree about which leagues were in scope.
DELETE FROM public."PickemGroupMember" WHERE "UserId" IN (SELECT "Id" FROM _targets);

-- Deliberately NOT touched: User, UserOption, UserNotificationPreferences,
-- MessageThread, MessagePost, MessageReaction. The accounts stay usable and
-- any message content stays attributed.

-- 8) Verify: every count must be 0, and 'user still exists' must be > 0.
SELECT 'user still exists (expect > 0)' AS what, count(*) FROM public."User"      WHERE "Id" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'memberships left',   count(*) FROM public."PickemGroupMember"     WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'picks left',         count(*) FROM public."UserPick"              WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'week results left',  count(*) FROM public."PickemGroupWeekResult" WHERE "UserId" IN (SELECT "Id" FROM _targets)
UNION ALL SELECT 'standing history left', count(*) FROM public."LeagueStandingHistory" WHERE "UserId" IN (SELECT "Id" FROM _targets);

COMMIT; --ROLLBACK; --COMMIT;   -- change to COMMIT once the preview looks right
