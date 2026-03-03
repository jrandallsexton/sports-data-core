-- ============================================================================
-- STEP 06: FIX RANKINGS AND RECORDS TABLES
-- ============================================================================
-- DATABASE: sdProducer.FootballNcaa
-- PURPOSE: Fix FranchiseSeasonRanking, FranchiseSeasonRecord, and 
--          FranchiseSeasonProjection to match FranchiseSeason.SeasonYear
-- RECORDS AFFECTED: ~4,233 total (260 rankings + 3,973 records + 0 projections)
-- SAFE TO RUN: Yes - updates only denormalized SeasonYear field
-- ============================================================================

-- ----------------------------------------------------------------------------
-- PART A: FIX FRANCHISESEASONRANKING
-- ----------------------------------------------------------------------------

-- Preview
SELECT COUNT(*) as records_to_fix
FROM public."FranchiseSeasonRanking" fsr
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsr."FranchiseSeasonId"
WHERE fsr."SeasonYear" != fs."SeasonYear";

-- Expected: ~260 (Actual: 108)

-- Sample
SELECT 
    fsr."Id",
    fsr."SeasonYear" as old_year,
    fs."SeasonYear" as new_year,
    fsr."Name",
    fsr."ShortHeadline"
FROM public."FranchiseSeasonRanking" fsr
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsr."FranchiseSeasonId"
WHERE fsr."SeasonYear" != fs."SeasonYear"
ORDER BY fsr."Date"
LIMIT 10;

-- Execute
UPDATE public."FranchiseSeasonRanking" fsr
SET 
    "SeasonYear" = fs."SeasonYear",
    "ModifiedUtc" = NOW(),
    "ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'
FROM public."FranchiseSeason" fs
WHERE fsr."FranchiseSeasonId" = fs."Id"
  AND fsr."SeasonYear" != fs."SeasonYear";

-- Expected: UPDATE ~260 (Actual: 108)

-- Verify
SELECT COUNT(*) as remaining_mismatches
FROM public."FranchiseSeasonRanking" fsr
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsr."FranchiseSeasonId"
WHERE fsr."SeasonYear" != fs."SeasonYear";

-- Expected: 0 (Actual: 0)


-- ----------------------------------------------------------------------------
-- PART B: FIX FRANCHISESEASONRECORD
-- ----------------------------------------------------------------------------

-- Preview
SELECT COUNT(*) as records_to_fix
FROM public."FranchiseSeasonRecord" fsrec
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsrec."FranchiseSeasonId"
WHERE fsrec."SeasonYear" != fs."SeasonYear";

-- Expected: ~3,973 (Actual: 1,572)

-- Sample
SELECT 
    fsrec."Id",
    fsrec."SeasonYear" as old_year,
    fs."SeasonYear" as new_year,
    fs."Slug" as team,
    fsrec."Type"
FROM public."FranchiseSeasonRecord" fsrec
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsrec."FranchiseSeasonId"
WHERE fsrec."SeasonYear" != fs."SeasonYear"
LIMIT 10;

-- Execute
UPDATE public."FranchiseSeasonRecord" fsrec
SET 
    "SeasonYear" = fs."SeasonYear",
    "ModifiedUtc" = NOW(),
    "ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'
FROM public."FranchiseSeason" fs
WHERE fsrec."FranchiseSeasonId" = fs."Id"
  AND fsrec."SeasonYear" != fs."SeasonYear";

-- Expected: UPDATE ~3,973 (Actual: 1,572)

-- Verify
SELECT COUNT(*) as remaining_mismatches
FROM public."FranchiseSeasonRecord" fsrec
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsrec."FranchiseSeasonId"
WHERE fsrec."SeasonYear" != fs."SeasonYear";

-- Expected: 0 (Actual: 0)


-- ----------------------------------------------------------------------------
-- PART C: FIX FRANCHISESEASONPROJECTION (if any exist)
-- ----------------------------------------------------------------------------

-- Preview
SELECT COUNT(*) as records_to_fix
FROM public."FranchiseSeasonProjection" fsp
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsp."FranchiseSeasonId"
WHERE fsp."SeasonYear" != fs."SeasonYear";

-- Expected: 0 (likely no projections for historical data) (Actual: 0)

-- Execute (safe to run even if 0 records)
UPDATE public."FranchiseSeasonProjection" fsp
SET 
    "SeasonYear" = fs."SeasonYear",
    "ModifiedUtc" = NOW(),
    "ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'
FROM public."FranchiseSeason" fs
WHERE fsp."FranchiseSeasonId" = fs."Id"
  AND fsp."SeasonYear" != fs."SeasonYear";

-- Expected: UPDATE 0 (Actual: 0)


-- ----------------------------------------------------------------------------
-- SUMMARY: Verify all three tables
-- ----------------------------------------------------------------------------

SELECT 'FranchiseSeasonRanking' as table_name, COUNT(*) as remaining_mismatches
FROM public."FranchiseSeasonRanking" fsr
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsr."FranchiseSeasonId"
WHERE fsr."SeasonYear" != fs."SeasonYear"

UNION ALL

SELECT 'FranchiseSeasonRecord', COUNT(*)
FROM public."FranchiseSeasonRecord" fsrec
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsrec."FranchiseSeasonId"
WHERE fsrec."SeasonYear" != fs."SeasonYear"

UNION ALL

SELECT 'FranchiseSeasonProjection', COUNT(*)
FROM public."FranchiseSeasonProjection" fsp
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsp."FranchiseSeasonId"
WHERE fsp."SeasonYear" != fs."SeasonYear";

-- Expected: All should show 0 (Actual: All show 0)

-- ============================================================================
-- ✓ STEP 06 COMPLETE: Rankings and Records fixed
-- NEXT: Run 99_final_verification.sql
-- ============================================================================
