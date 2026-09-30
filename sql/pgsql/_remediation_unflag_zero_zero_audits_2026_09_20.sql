-- Remediation: return audit-flagged contests to the normal path when the only
-- thing wrong with them was 0-0 competitor score rows.
--
-- DATABASE: run once per sport — sdProducer.FootballNcaa, sdProducer.FootballNfl.
--           (Baseball is excluded by the WHERE clause; its 0-0 case is already
--           handled by the MLB tie guard and always was.)
--
-- RUN AFTER #776 IS DEPLOYED. The guard added there is what stops these from
-- immediately re-flagging. Run it before, and three sweeps later you are back
-- where you started.
--
-- Context (2026-09-19): the audit treated 0-0 competitor rows as an
-- authoritative expected score. Against a contest holding a REAL score that
-- reads as a mismatch, so the audit cleared FinalizedUtc and re-queued
-- enrichment — which cannot conjure score rows that were never sourced. Each
-- such contest burned its full three-attempt allowance and was flagged. 14 of
-- the 108 flagged contests were this case; Baltimore at Tennessee held a real
-- 16-10 against an expected 0-0.
--
-- Scope: flagged contests whose competitor rows still read 0-0 on BOTH sides
-- AND whose stored score is non-zero — exactly the class #776 now defers.
-- Contests flagged for a genuine source disagreement (scoring plays vs
-- competitor scores differing on magnitude, or transposed) are NOT touched;
-- those still need a human.
--
-- Clearing the flag and the counter returns them to the audit candidate set.
-- Once the score rows are sourced they audit normally; until then #776 defers
-- them at no cost.
--
-- Ends in ROLLBACK. Change the last line to COMMIT once the preview matches.

BEGIN;

DROP TABLE IF EXISTS _zero_flagged;
CREATE TEMP TABLE _zero_flagged AS
SELECT c."Id",
       c."Name",
       c."SeasonYear",
       c."AwayScore",
       c."HomeScore",
       c."AuditAttemptCount",
       c."AuditFlaggedUtc"
FROM public."Contest" c
WHERE c."AuditFlaggedUtc" IS NOT NULL
  -- A real stored score. The 0-0-vs-0-0 case audits normally and is
  -- deliberately left alone (see the existing processor test).
  AND (COALESCE(c."AwayScore", 0) <> 0 OR COALESCE(c."HomeScore", 0) <> 0)
  -- Every competitor score row for this contest reads 0, on both sides. NOT
  -- EXISTS rather than MAX(...) = 0 so a contest with no rows at all is
  -- excluded: that is the separate "no competitor score rows" deferral, and
  -- its flag (if any) came from somewhere else.
  AND EXISTS (
        SELECT 1
        FROM public."Competition" comp
        JOIN public."CompetitionCompetitor" cc ON cc."CompetitionId" = comp."Id"
        JOIN public."CompetitionCompetitorScores" s ON s."CompetitionCompetitorId" = cc."Id"
        WHERE comp."ContestId" = c."Id")
  AND NOT EXISTS (
        SELECT 1
        FROM public."Competition" comp
        JOIN public."CompetitionCompetitor" cc ON cc."CompetitionId" = comp."Id"
        JOIN public."CompetitionCompetitorScores" s ON s."CompetitionCompetitorId" = cc."Id"
        WHERE comp."ContestId" = c."Id"
          AND s."Value" <> 0);

-- 1) Preview. Read this list before committing: every row here goes back into
--    the audit candidate set.
SELECT "SeasonYear", "Name", "AwayScore" || '-' || "HomeScore" AS stored,
       "AuditAttemptCount", "AuditFlaggedUtc"
FROM _zero_flagged
ORDER BY "SeasonYear" DESC, "Name";

-- 2) Counts, alongside the flagged contests this deliberately does NOT touch.
SELECT 'To unflag (0-0 rows vs a real score)' AS what, count(*) FROM _zero_flagged
UNION ALL
SELECT 'Flagged, left alone (genuine disagreement)', count(*)
  FROM public."Contest"
 WHERE "AuditFlaggedUtc" IS NOT NULL
   AND "Id" NOT IN (SELECT "Id" FROM _zero_flagged);

-- 3) Sanity: none of these should be finalized=null. #772 made flagging keep
--    FinalizedUtc, so a null here means something else unfinalized them and
--    this script is not the right tool.
SELECT 'GUARD not finalized (expect 0)' AS what, count(*)
FROM public."Contest"
WHERE "Id" IN (SELECT "Id" FROM _zero_flagged)
  AND "FinalizedUtc" IS NULL;

DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM public."Contest"
    WHERE "Id" IN (SELECT "Id" FROM _zero_flagged) AND "FinalizedUtc" IS NULL)
  THEN RAISE EXCEPTION 'A matched contest is not finalized; aborting — investigate before unflagging.';
  END IF;
END $$;

-- 4) Return them to the candidate set. AuditedUtc is already null (flagged is
--    not validated) and stays that way — these are unverified, not verified.
UPDATE public."Contest"
SET "AuditFlaggedUtc" = NULL,
    "AuditAttemptCount" = 0
WHERE "Id" IN (SELECT "Id" FROM _zero_flagged);

-- 5) Verify: expect 0 left flagged, and every one back in the candidate set
--    (FinalizedUtc not null AND AuditedUtc null AND AuditFlaggedUtc null).
SELECT 'still flagged (expect 0)' AS what, count(*)
  FROM public."Contest"
 WHERE "Id" IN (SELECT "Id" FROM _zero_flagged) AND "AuditFlaggedUtc" IS NOT NULL
UNION ALL
SELECT 'back in the audit candidate set', count(*)
  FROM public."Contest"
 WHERE "Id" IN (SELECT "Id" FROM _zero_flagged)
   AND "FinalizedUtc" IS NOT NULL
   AND "AuditedUtc" IS NULL
   AND "AuditFlaggedUtc" IS NULL;

COMMIT; --ROLLBACK; --COMMIT;   -- change to COMMIT once the preview looks right
