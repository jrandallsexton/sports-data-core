-- One-time repair: phantom NCAA league weeks keyed to contest-less
-- SeasonWeeks (2026-08-27, follows backfill_pickemgroupweek_phase.sql).
--
-- Root cause: NCAA's "Preseason Week 1" SeasonWeek spans Feb-Aug (an
-- ESPN calendar artifact with ZERO contests ever), so every full-season
-- NCAA league's date window overlapped it and bootstrap created a
-- PickemGroupWeek keyed to it; the old number-based matchup fetch then
-- stored REGULAR-SEASON week-1 games under that preseason-keyed row.
-- Off-season rows (also contest-less) produced empty duplicates the
-- same way. Fixed forward in Producer (week resolution now requires a
-- week to HAVE contests); this repairs existing rows.
--
-- Scope: NCAA leagues only (Sport = 2). NFL preseason league weeks
-- (Sport = 3, phase 1) are CORRECT — real preseason games — untouched.
-- Safe for picks: PickemGroupUserPick keys on ContestId, not week.
-- Idempotent: re-running matches nothing once repaired.
--
-- Run against sdApi.All:

-- Duplicate-safety note: PickemGroupMatchup carries a UNIQUE
-- (GroupId, ContestId) index, so a contest cannot exist under two weeks
-- of one group — the move in step 1 can never collide with a row
-- already on the twin.

BEGIN;

-- 1. Move matchups from each phantom preseason-keyed week to its
--    regular-season twin (same group, same week number). Phantom rows
--    WITHOUT a twin are deliberately not touched — see step 3.
UPDATE public."PickemGroupMatchup" m
SET    "SeasonWeekId" = reg."SeasonWeekId"
FROM   public."PickemGroupWeek" ph
JOIN   public."PickemGroup" g   ON g."Id" = ph."GroupId" AND g."Sport" = 2
JOIN   public."PickemGroupWeek" reg
       ON  reg."GroupId" = ph."GroupId"
       AND reg."SeasonWeek" = ph."SeasonWeek"
       AND reg."SeasonPhaseTypeCode" = 2
WHERE  ph."SeasonPhaseTypeCode" = 1
  AND  m."GroupId" = ph."GroupId"
  AND  m."SeasonWeekId" = ph."SeasonWeekId";

-- 2. The twin now owns the matchups; carry the generated flag.
UPDATE public."PickemGroupWeek" reg
SET    "AreMatchupsGenerated" = true
FROM   public."PickemGroupWeek" ph
JOIN   public."PickemGroup" g ON g."Id" = ph."GroupId" AND g."Sport" = 2
WHERE  ph."SeasonPhaseTypeCode" = 1
  AND  ph."AreMatchupsGenerated"
  AND  reg."GroupId" = ph."GroupId"
  AND  reg."SeasonWeek" = ph."SeasonWeek"
  AND  reg."SeasonPhaseTypeCode" = 2;

-- 3. Drop phantoms ONLY when they hold no matchups. The FK from
--    PickemGroupMatchup to PickemGroupWeek cascades, so an unguarded
--    delete of a phantom that still holds matchups (no regular twin to
--    receive them in step 1) would destroy those matchups. Such rows
--    survive this script and surface in the verify query below for
--    manual repair.
DELETE FROM public."PickemGroupWeek" ph
USING  public."PickemGroup" g
WHERE  g."Id" = ph."GroupId"
  AND  g."Sport" = 2
  AND  ph."SeasonPhaseTypeCode" IN (1, 4)
  AND  NOT EXISTS (
         SELECT 1 FROM public."PickemGroupMatchup" m
         WHERE  m."GroupId" = ph."GroupId"
           AND  m."SeasonWeekId" = ph."SeasonWeekId");

COMMIT;

-- Verify: expect zero rows. Any survivor is a phantom still holding
-- matchups with no regular-season twin — create the twin (or decide the
-- matchups' fate) manually before re-running.
SELECT g."Name", w."SeasonWeek", w."SeasonPhaseTypeCode", count(m."GroupId") AS matchups
FROM   public."PickemGroupWeek" w
JOIN   public."PickemGroup" g ON g."Id" = w."GroupId"
LEFT JOIN public."PickemGroupMatchup" m
       ON m."GroupId" = w."GroupId" AND m."SeasonWeekId" = w."SeasonWeekId"
WHERE  g."Sport" = 2 AND w."SeasonPhaseTypeCode" IN (1, 4)
GROUP BY 1, 2, 3;
