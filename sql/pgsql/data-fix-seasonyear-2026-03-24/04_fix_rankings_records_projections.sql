-- ============================================================================
-- STEP 04: FIX FranchiseSeasonRanking, FranchiseSeasonRecord,
--          FranchiseSeasonProjection SeasonYear
-- ============================================================================
-- DATABASE: sdProducer.FootballNcaa
-- SOURCE OF TRUTH: FranchiseSeason.SeasonYear (corrected in Step 02)
-- ============================================================================

BEGIN;

-- ---- FranchiseSeasonRanking ------------------------------------------------

-- Preview
SELECT COUNT(*) AS rankings_to_fix
FROM public."FranchiseSeasonRanking" fsr
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsr."FranchiseSeasonId"
WHERE fsr."SeasonYear" != fs."SeasonYear";

-- Fix
UPDATE public."FranchiseSeasonRanking" fsr
SET "SeasonYear" = fs."SeasonYear"
FROM public."FranchiseSeason" fs
WHERE fs."Id" = fsr."FranchiseSeasonId"
  AND fsr."SeasonYear" != fs."SeasonYear";

-- ---- FranchiseSeasonRecord -------------------------------------------------

-- Preview
SELECT COUNT(*) AS records_to_fix
FROM public."FranchiseSeasonRecord" fsrec
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsrec."FranchiseSeasonId"
WHERE fsrec."SeasonYear" != fs."SeasonYear";

-- Fix
UPDATE public."FranchiseSeasonRecord" fsrec
SET "SeasonYear" = fs."SeasonYear"
FROM public."FranchiseSeason" fs
WHERE fs."Id" = fsrec."FranchiseSeasonId"
  AND fsrec."SeasonYear" != fs."SeasonYear";

-- ---- FranchiseSeasonProjection ---------------------------------------------

-- Preview
SELECT COUNT(*) AS projections_to_fix
FROM public."FranchiseSeasonProjection" fsp
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsp."FranchiseSeasonId"
WHERE fsp."SeasonYear" != fs."SeasonYear";

-- Fix
UPDATE public."FranchiseSeasonProjection" fsp
SET "SeasonYear" = fs."SeasonYear"
FROM public."FranchiseSeason" fs
WHERE fs."Id" = fsp."FranchiseSeasonId"
  AND fsp."SeasonYear" != fs."SeasonYear";

COMMIT;
