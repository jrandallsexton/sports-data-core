/**************************************************************************************************
 * DATA REMEDIATION SCRIPT: Fix SeasonYear Corruption
 * 
 * PROBLEM:
 * Bug in application caused 24 NCAA Football GroupSeason records (years 1949-2026) to be created  
 * with wrong SeasonYear (all showing 2023). This cascaded to:
 *   - 1,266 GroupSeason records (including descendants)
 *   - 1,335 FranchiseSeason records
 *   - 6,161 Contest records
 *   - 260 FranchiseSeasonRanking records
 *   - 3,973 FranchiseSeasonRecord records
 * 
 * ROOT CAUSE:
 * GroupSeason.SeasonYear was set incorrectly during creation, despite having correct Season FK.
 * SeasonYear exists on many tables to reduce joins, but was populated with wrong value.
 * 
 * FIX STRATEGY:
 * Update SeasonYear from authoritative sources:
 *   1. GroupSeason: Use Season.Year (joined by SeasonId)
 *   2. FranchiseSeason: Use corrected GroupSeason.SeasonYear
 *   3. Contest: Use FranchiseSeason.SeasonYear (from home/away team)
 *   4. Other tables: Use FranchiseSeason.SeasonYear
 * 
 * SAFETY:
 * - Uses CTEs to identify exact records to update
 * - Each step shows affected count before updating
 * - Can be run in transaction and rolled back if needed
 * 
 * DATABASE: sdProducer.FootballNcaa (Production Backup - Local)
 * CREATED: 2026-03-02
 **************************************************************************************************/

-- ============================================================================
-- VERIFICATION QUERIES (Run these first to see current state)
-- ============================================================================

-- Show the 24 bad root records
SELECT 
    gs."Id",
    gs."SeasonYear" as wrong_season_year,
    s."Year" as correct_season_year,
    gs."Slug",
    gs."Name",
    gs."CreatedUtc"
FROM public."GroupSeason" gs
INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
WHERE gs."Slug" = 'ncaa-football' 
  AND gs."SeasonYear" != s."Year"
ORDER BY s."Year";

-- Count all affected records
WITH RECURSIVE all_bad_group_seasons AS (
    SELECT gs."Id", s."Year" as correct_year
    FROM public."GroupSeason" gs
    INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
    WHERE gs."Slug" = 'ncaa-football' AND gs."SeasonYear" != s."Year"
    
    UNION ALL
    
    SELECT gs."Id", abgs.correct_year
    FROM public."GroupSeason" gs
    INNER JOIN all_bad_group_seasons abgs ON gs."ParentId" = abgs."Id"
)
SELECT 
    COUNT(DISTINCT abgs."Id") as bad_group_season_count,
    COUNT(DISTINCT fs."Id") as franchise_season_count,
    COUNT(DISTINCT con."Id") as contest_count,
    COUNT(DISTINCT fsr."Id") as franchise_season_ranking_count,
    COUNT(DISTINCT fsrec."Id") as franchise_season_record_count
FROM all_bad_group_seasons abgs
LEFT JOIN public."FranchiseSeason" fs ON fs."GroupSeasonId" = abgs."Id"
LEFT JOIN public."Contest" con ON (con."HomeTeamFranchiseSeasonId" = fs."Id" OR con."AwayTeamFranchiseSeasonId" = fs."Id")
LEFT JOIN public."FranchiseSeasonRanking" fsr ON fsr."FranchiseSeasonId" = fs."Id"
LEFT JOIN public."FranchiseSeasonRecord" fsrec ON fsrec."FranchiseSeasonId" = fs."Id";

-- ============================================================================
-- REMEDIATION STEPS
-- ============================================================================

-- Uncomment BEGIN to run as transaction (recommended for testing)
-- BEGIN;

-- ----------------------------------------------------------------------------
-- STEP 1: Fix Root GroupSeason Records (24 records)
-- ----------------------------------------------------------------------------
-- These are the NCAA Football records with wrong SeasonYear

-- Preview what will be updated
SELECT 
    gs."Id",
    gs."SeasonYear" as old_season_year,
    s."Year" as new_season_year,
    gs."Slug"
FROM public."GroupSeason" gs
INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
WHERE gs."Slug" = 'ncaa-football' 
  AND gs."SeasonYear" != s."Year"
ORDER BY s."Year";

-- Execute update
UPDATE public."GroupSeason" gs
SET 
    "SeasonYear" = s."Year",
    "ModifiedUtc" = NOW(),
    "ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee' -- Manual fix user ID
FROM public."Season" s
WHERE gs."SeasonId" = s."Id"
  AND gs."Slug" = 'ncaa-football'
  AND gs."SeasonYear" != s."Year";

SELECT '✓ STEP 1 COMPLETE: Fixed root GroupSeason records' as status;


-- ----------------------------------------------------------------------------
-- STEP 2: Fix Child/Descendant GroupSeason Records (~1,242 records)
-- ----------------------------------------------------------------------------
-- These inherit SeasonYear from their parent via ParentId chain

-- Preview count
WITH RECURSIVE bad_descendants AS (
    -- Start with corrected roots
    SELECT gs."Id", gs."SeasonYear" as correct_year
    FROM public."GroupSeason" gs
    INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
    WHERE gs."Slug" = 'ncaa-football' AND s."Year" != 2023
    
    UNION ALL
    
    -- Find children with wrong SeasonYear
    SELECT gs."Id", bd.correct_year
    FROM public."GroupSeason" gs
    INNER JOIN bad_descendants bd ON gs."ParentId" = bd."Id"
    WHERE gs."SeasonYear" != bd.correct_year
)
SELECT COUNT(*) as records_to_fix FROM bad_descendants;

-- Execute update - Loop through parent hierarchy 
-- (Run this multiple times until it returns 0 affected rows to handle deep hierarchies)
DO $$
DECLARE
    rows_affected INTEGER;
BEGIN
    -- Update children where parent has correct SeasonYear but child does not
    UPDATE public."GroupSeason" child
    SET 
        "SeasonYear" = parent."SeasonYear",
        "ModifiedUtc" = NOW(),
        "ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'
    FROM public."GroupSeason" parent
    WHERE child."ParentId" = parent."Id"
      AND child."SeasonYear" != parent."SeasonYear";
    
    GET DIAGNOSTICS rows_affected = ROW_COUNT;
    RAISE NOTICE 'Updated % child GroupSeason records', rows_affected;
END $$;

-- Verify no more mismatches (should return 0 rows)
WITH RECURSIVE hierarchy AS (
    SELECT "Id", "ParentId", "SeasonYear", "Slug", 0 as level
    FROM public."GroupSeason"
    WHERE "ParentId" IS NULL
    
    UNION ALL
    
    SELECT gs."Id", gs."ParentId", gs."SeasonYear", gs."Slug", h.level + 1
    FROM public."GroupSeason" gs
    INNER JOIN hierarchy h ON gs."ParentId" = h."Id"
)
SELECT h.*, p."SeasonYear" as parent_season_year
FROM hierarchy h
LEFT JOIN public."GroupSeason" p ON p."Id" = h."ParentId"
WHERE h."ParentId" IS NOT NULL 
  AND h."SeasonYear" != p."SeasonYear";

SELECT '✓ STEP 2 COMPLETE: Fixed descendant GroupSeason records' as status;


-- ----------------------------------------------------------------------------
-- STEP 3: Fix FranchiseSeason Records (~1,335 records)
-- ----------------------------------------------------------------------------

-- Preview what will be updated
SELECT COUNT(*) as records_to_fix
FROM public."FranchiseSeason" fs
INNER JOIN public."GroupSeason" gs ON gs."Id" = fs."GroupSeasonId"
WHERE fs."SeasonYear" != gs."SeasonYear";

-- Execute update
UPDATE public."FranchiseSeason" fs
SET 
    "SeasonYear" = gs."SeasonYear",
    "ModifiedUtc" = NOW(),
    "ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'
FROM public."GroupSeason" gs
WHERE fs."GroupSeasonId" = gs."Id"
  AND fs."SeasonYear" != gs."SeasonYear";

SELECT '✓ STEP 3 COMPLETE: Fixed FranchiseSeason records' as status;


-- ----------------------------------------------------------------------------
-- STEP 4: Fix Contest Records (~6,161 records)
-- ----------------------------------------------------------------------------
-- Contest.SeasonYear should match the FranchiseSeason of home/away teams

-- Preview what will be updated
SELECT COUNT(DISTINCT c."Id") as records_to_fix
FROM public."Contest" c
INNER JOIN public."FranchiseSeason" fs ON (fs."Id" = c."HomeTeamFranchiseSeasonId")
WHERE c."SeasonYear" != fs."SeasonYear";

-- Execute update
UPDATE public."Contest" c
SET 
    "SeasonYear" = fs."SeasonYear",
    "ModifiedUtc" = NOW(),
    "ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'
FROM public."FranchiseSeason" fs
WHERE fs."Id" = c."HomeTeamFranchiseSeasonId"
  AND c."SeasonYear" != fs."SeasonYear";

-- Verify (should return 0 rows)
SELECT COUNT(*) as mismatched_contests
FROM public."Contest" c
INNER JOIN public."FranchiseSeason" hfs ON hfs."Id" = c."HomeTeamFranchiseSeasonId"
INNER JOIN public."FranchiseSeason" afs ON afs."Id" = c."AwayTeamFranchiseSeasonId"
WHERE c."SeasonYear" != hfs."SeasonYear" 
   OR c."SeasonYear" != afs."SeasonYear";

SELECT '✓ STEP 4 COMPLETE: Fixed Contest records' as status;


-- ----------------------------------------------------------------------------
-- STEP 5: Fix FranchiseSeasonRanking Records (~260 records)
-- ----------------------------------------------------------------------------

-- Preview what will be updated
SELECT COUNT(*) as records_to_fix
FROM public."FranchiseSeasonRanking" fsr
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsr."FranchiseSeasonId"
WHERE fsr."SeasonYear" != fs."SeasonYear";

-- Execute update
UPDATE public."FranchiseSeasonRanking" fsr
SET 
    "SeasonYear" = fs."SeasonYear",
    "ModifiedUtc" = NOW(),
    "ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'
FROM public."FranchiseSeason" fs
WHERE fsr."FranchiseSeasonId" = fs."Id"
  AND fsr."SeasonYear" != fs."SeasonYear";

SELECT '✓ STEP 5 COMPLETE: Fixed FranchiseSeasonRanking records' as status;


-- ----------------------------------------------------------------------------
-- STEP 6: Fix FranchiseSeasonRecord Records (~3,973 records)
-- ----------------------------------------------------------------------------

-- Preview what will be updated
SELECT COUNT(*) as records_to_fix
FROM public."FranchiseSeasonRecord" fsrec
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsrec."FranchiseSeasonId"
WHERE fsrec."SeasonYear" != fs."SeasonYear";

-- Execute update
UPDATE public."FranchiseSeasonRecord" fsrec
SET 
    "SeasonYear" = fs."SeasonYear",
    "ModifiedUtc" = NOW(),
    "ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'
FROM public."FranchiseSeason" fs
WHERE fsrec."FranchiseSeasonId" = fs."Id"
  AND fsrec."SeasonYear" != fs."SeasonYear";

SELECT '✓ STEP 6 COMPLETE: Fixed FranchiseSeasonRecord records' as status;


-- ----------------------------------------------------------------------------
-- STEP 7: Fix FranchiseSeasonProjection Records (if any)
-- ----------------------------------------------------------------------------

-- Preview what will be updated
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_name = 'FranchiseSeasonProjection'
    ) THEN
        RAISE NOTICE 'FranchiseSeasonProjection records to fix: %', (
            SELECT COUNT(*)
            FROM public."FranchiseSeasonProjection" fsp
            INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsp."FranchiseSeasonId"
            WHERE fsp."SeasonYear" != fs."SeasonYear"
        );

        UPDATE public."FranchiseSeasonProjection" fsp
        SET 
            "SeasonYear" = fs."SeasonYear",
            "ModifiedUtc" = NOW(),
            "ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'
        FROM public."FranchiseSeason" fs
        WHERE fsp."FranchiseSeasonId" = fs."Id"
          AND fsp."SeasonYear" != fs."SeasonYear";
    ELSE
        RAISE NOTICE 'FranchiseSeasonProjection table not present - skipping Step 7';
    END IF;
END $$;

SELECT '✓ STEP 7 COMPLETE: Fixed FranchiseSeasonProjection records' as status;


-- ----------------------------------------------------------------------------
-- STEP 8: Fix SeasonPoll Records (if any)
-- ----------------------------------------------------------------------------

-- Check if SeasonPoll has SeasonYear column and needs fixing
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'public' 
          AND table_name = 'SeasonPoll' 
          AND column_name = 'SeasonYear'
    ) THEN
        -- Preview
        RAISE NOTICE 'SeasonPoll records to fix: %', (
            SELECT COUNT(*)
            FROM public."SeasonPoll" sp
            INNER JOIN public."Season" s ON s."Id" = sp."SeasonId"
            WHERE sp."SeasonYear" != s."Year"
        );
        
        -- Execute update
        UPDATE public."SeasonPoll" sp
        SET 
            "SeasonYear" = s."Year",
            "ModifiedUtc" = NOW(),
            "ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'
        FROM public."Season" s
        WHERE sp."SeasonId" = s."Id"
          AND sp."SeasonYear" != s."Year";
          
        RAISE NOTICE '✓ STEP 8 COMPLETE: Fixed SeasonPoll records';
    ELSE
        RAISE NOTICE 'SeasonPoll table does not have SeasonYear column - skipping';
    END IF;
END $$;


-- ============================================================================
-- FINAL VERIFICATION
-- ============================================================================

-- Should return NO rows if fix is successful
SELECT 'GroupSeason vs Season mismatch' as issue, COUNT(*) as count
FROM public."GroupSeason" gs
INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
WHERE gs."SeasonYear" != s."Year"

UNION ALL

SELECT 'GroupSeason child/parent mismatch', COUNT(*)
FROM public."GroupSeason" child
INNER JOIN public."GroupSeason" parent ON parent."Id" = child."ParentId"
WHERE child."SeasonYear" != parent."SeasonYear"

UNION ALL

SELECT 'FranchiseSeason vs GroupSeason mismatch', COUNT(*)
FROM public."FranchiseSeason" fs
INNER JOIN public."GroupSeason" gs ON gs."Id" = fs."GroupSeasonId"
WHERE fs."SeasonYear" != gs."SeasonYear"

UNION ALL

SELECT 'Contest vs FranchiseSeason mismatch', COUNT(*)
FROM public."Contest" c
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = c."HomeTeamFranchiseSeasonId"
WHERE c."SeasonYear" != fs."SeasonYear"

UNION ALL

SELECT 'FranchiseSeasonRanking vs FranchiseSeason mismatch', COUNT(*)
FROM public."FranchiseSeasonRanking" fsr
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsr."FranchiseSeasonId"
WHERE fsr."SeasonYear" != fs."SeasonYear"

UNION ALL

SELECT 'FranchiseSeasonRecord vs FranchiseSeason mismatch', COUNT(*)
FROM public."FranchiseSeasonRecord" fsrec
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsrec."FranchiseSeasonId"
WHERE fsrec."SeasonYear" != fs."SeasonYear";


-- ============================================================================
-- COMPLETION SUMMARY
-- ============================================================================

SELECT 
    '✅ DATA REMEDIATION COMPLETE' as status,
    NOW() as completed_at;

-- If running as transaction, review results above then:
-- COMMIT;   -- To apply changes
-- ROLLBACK; -- To undo changes

