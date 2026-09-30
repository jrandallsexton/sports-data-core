-- =====================================================================
-- Odds-provider remediation (Kent-SC incident, docs/audit/kent-at-scar.txt)
-- Run against: sdProducer.FootballNcaa (prod) AFTER the fix deploys.
-- Order: 1) snapshot  2) clear  3) trigger finalization sweep  4) diff
-- =====================================================================

-- ---------------------------------------------------------------------
-- 0) MLB DETECTION (run against sdProducer.BaseballMlb FIRST - product
--    decision pending). Counts finalized contests whose stored denorm
--    disagrees with the MERGED policy (#732 final):
--    displayed set = 58/100, provider order (58 first), spread NOT
--    considered; live rows (59, 200) never eligible. Contests with NO
--    displayed-set row (historical era) are excluded here - the C#
--    fallback for them is order-nondeterministic and they are outside
--    the remediation window anyway.
-- ---------------------------------------------------------------------
WITH displayed AS (
  SELECT comp."ContestId", o.*,
         ROW_NUMBER() OVER (
           PARTITION BY comp."ContestId"
           ORDER BY CASE o."ProviderId" WHEN '58' THEN 0 ELSE 1 END
         ) AS rn
  FROM public."Competition" comp
  JOIN public."CompetitionOdds" o ON o."CompetitionId" = comp."Id"
  WHERE o."FinalizedUtc" IS NOT NULL
    AND o."ProviderId" IN ('58', '100')
)
SELECT COUNT(*) AS disagreements
FROM public."Contest" c
JOIN displayed r ON r."ContestId" = c."Id" AND r.rn = 1
WHERE c."FinalizedUtc" IS NOT NULL
  AND (c."SpreadWinnerFranchiseSeasonId" IS DISTINCT FROM r."AtsWinnerFranchiseSeasonId"
       OR c."OverUnder" IS DISTINCT FROM r."OverUnderResult");

-- ---------------------------------------------------------------------
-- 1) BEFORE-SNAPSHOT (sdProducer.FootballNcaa) - the diff baseline.
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public._remediation_snapshot_2026_09_06 AS
SELECT c."Id" AS contest_id, c."Name",
       c."FinalizedUtc", c."AuditedUtc",
       c."WinnerFranchiseSeasonId",
       c."SpreadWinnerFranchiseSeasonId",
       c."OverUnder",
       now() AS snapshot_at
FROM public."Contest" c
WHERE c."FinalizedUtc" >= '2026-09-04T00:00:00Z';

SELECT COUNT(*) AS snapshotted FROM public._remediation_snapshot_2026_09_06;

-- ---------------------------------------------------------------------
-- 2) CLEAR finalization + audit flags. SCORES ARE NOT TOUCHED - this is
--    re-derivation, not re-sourcing. The finalization sweep re-derives
--    winner/ATS/O-U under the new policy and republishes ContestFinalized
--    -> pick re-scoring cascades; nulled AuditedUtc re-arms the 6-hourly
--    audit as an independent re-verification.
-- ---------------------------------------------------------------------
-- UPDATE public."Contest"
-- SET "FinalizedUtc" = NULL,
--     "AuditedUtc" = NULL,
--     "WinnerFranchiseSeasonId" = NULL,
--     "SpreadWinnerFranchiseSeasonId" = NULL,
--     "OverUnder" = 0
-- WHERE "FinalizedUtc" >= '2026-09-04T00:00:00Z';

-- ---------------------------------------------------------------------
-- 4) AFTER-DIFF (post-sweep): exactly which games flipped.
-- ---------------------------------------------------------------------
SELECT s."Name",
       s."SpreadWinnerFranchiseSeasonId" AS ats_before,
       c."SpreadWinnerFranchiseSeasonId" AS ats_after,
       s."OverUnder" AS ou_before, c."OverUnder" AS ou_after
FROM public._remediation_snapshot_2026_09_06 s
JOIN public."Contest" c ON c."Id" = s.contest_id
WHERE c."SpreadWinnerFranchiseSeasonId" IS DISTINCT FROM s."SpreadWinnerFranchiseSeasonId"
   OR c."OverUnder" IS DISTINCT FROM s."OverUnder"
ORDER BY s."Name";
