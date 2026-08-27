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

BEGIN;

-- 1. Move matchups from each phantom preseason-keyed week to its
--    regular-season twin (same group, same week number).
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

-- 3. Drop the phantoms: NCAA preseason- and offseason-keyed week rows.
--    (Any matchups they held were moved in step 1; offseason rows never
--    held any.)
DELETE FROM public."PickemGroupWeek" ph
USING  public."PickemGroup" g
WHERE  g."Id" = ph."GroupId"
  AND  g."Sport" = 2
  AND  ph."SeasonPhaseTypeCode" IN (1, 4);

COMMIT;

-- Verify: expect zero rows.
SELECT g."Name", w."SeasonWeek", w."SeasonPhaseTypeCode"
FROM   public."PickemGroupWeek" w
JOIN   public."PickemGroup" g ON g."Id" = w."GroupId"
WHERE  g."Sport" = 2 AND w."SeasonPhaseTypeCode" IN (1, 4);
