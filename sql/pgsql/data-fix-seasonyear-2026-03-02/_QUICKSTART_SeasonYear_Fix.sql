-- ============================================================================
-- QUICK START: SeasonYear Corruption Fix
-- ============================================================================
-- Execute this in your PostgreSQL client connected to: sdProducer.FootballNcaa
-- 
-- RECOMMENDATION: Run each section separately, review results before proceeding
-- ============================================================================

-- ────────────────────────────────────────────────────────────────────────────
-- STEP 0: VERIFY THE PROBLEM EXISTS
-- ────────────────────────────────────────────────────────────────────────────

-- Should show 24 rows with wrong SeasonYear (all showing 2023)
SELECT 
    gs."Id",
    gs."SeasonYear" as wrong_year,
    s."Year" as correct_year,
    gs."Slug",
    gse."SourceUrl"
FROM public."GroupSeason" gs
INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
INNER JOIN public."GroupSeasonExternalId" gse ON gse."GroupSeasonId" = gs."Id"
WHERE gs."Slug" = 'ncaa-football' AND gs."SeasonYear" != s."Year"
ORDER BY s."Year";


-- ────────────────────────────────────────────────────────────────────────────
-- STEP 1: BEGIN TRANSACTION (Safety First!)
-- ────────────────────────────────────────────────────────────────────────────

BEGIN; -- Start transaction - all changes can be rolled back


-- ────────────────────────────────────────────────────────────────────────────
-- STEP 2: FIX ROOT GROUPSEASON RECORDS
-- ────────────────────────────────────────────────────────────────────────────

-- Execute the fix
UPDATE public."GroupSeason" gs
SET 
    "SeasonYear" = s."Year",
    "ModifiedUtc" = NOW(),
    "ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'
FROM public."Season" s
WHERE gs."SeasonId" = s."Id"
  AND gs."Slug" = 'ncaa-football'
  AND gs."SeasonYear" != s."Year";
-- Expected: UPDATE 24


-- ────────────────────────────────────────────────────────────────────────────
-- STEP 3: FIX CHILD GROUPSEASON RECORDS (Run 4-5 times until 0 rows affected)
-- ────────────────────────────────────────────────────────────────────────────

-- Run this query multiple times until it shows "UPDATE 0"
UPDATE public."GroupSeason" child
SET 
    "SeasonYear" = parent."SeasonYear",
    "ModifiedUtc" = NOW(),
    "ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'
FROM public."GroupSeason" parent
WHERE child."ParentId" = parent."Id"
  AND child."SeasonYear" != parent."SeasonYear";
-- Run until: UPDATE 0


-- ────────────────────────────────────────────────────────────────────────────
-- STEP 4: FIX FRANCHISESEASON RECORDS
-- ────────────────────────────────────────────────────────────────────────────

UPDATE public."FranchiseSeason" fs
SET 
    "SeasonYear" = gs."SeasonYear",
    "ModifiedUtc" = NOW(),
    "ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'
FROM public."GroupSeason" gs
WHERE fs."GroupSeasonId" = gs."Id"
  AND fs."SeasonYear" != gs."SeasonYear";
-- Expected: UPDATE ~1335


-- ────────────────────────────────────────────────────────────────────────────
-- STEP 5: FIX CONTEST RECORDS
-- ────────────────────────────────────────────────────────────────────────────

UPDATE public."Contest" c
SET 
    "SeasonYear" = fs."SeasonYear",
    "ModifiedUtc" = NOW(),
    "ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'
FROM public."FranchiseSeason" fs
WHERE fs."Id" = c."HomeTeamFranchiseSeasonId"
  AND c."SeasonYear" != fs."SeasonYear";
-- Expected: UPDATE ~6161

-- Also fix away-side mismatches (only when home and away years agree to avoid cross-season conflicts)
UPDATE public."Contest" c
SET 
    "SeasonYear" = afs."SeasonYear",
    "ModifiedUtc" = NOW(),
    "ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'
FROM public."FranchiseSeason" afs,
     public."FranchiseSeason" hfs
WHERE afs."Id" = c."AwayTeamFranchiseSeasonId"
  AND hfs."Id" = c."HomeTeamFranchiseSeasonId"
  AND c."SeasonYear" != afs."SeasonYear"
  AND hfs."SeasonYear" = afs."SeasonYear"; -- guard: only update if both sides agree
-- Expected: UPDATE ~small number (most already handled by home-side pass above)


-- ────────────────────────────────────────────────────────────────────────────
-- STEP 6: FIX FRANCHISESEASONRANKING RECORDS
-- ────────────────────────────────────────────────────────────────────────────

UPDATE public."FranchiseSeasonRanking" fsr
SET 
    "SeasonYear" = fs."SeasonYear",
    "ModifiedUtc" = NOW(),
    "ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'
FROM public."FranchiseSeason" fs
WHERE fsr."FranchiseSeasonId" = fs."Id"
  AND fsr."SeasonYear" != fs."SeasonYear";
-- Expected: UPDATE ~260


-- ────────────────────────────────────────────────────────────────────────────
-- STEP 7: FIX FRANCHISESEASONRECORD RECORDS
-- ────────────────────────────────────────────────────────────────────────────

UPDATE public."FranchiseSeasonRecord" fsrec
SET 
    "SeasonYear" = fs."SeasonYear",
    "ModifiedUtc" = NOW(),
    "ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'
FROM public."FranchiseSeason" fs
WHERE fsrec."FranchiseSeasonId" = fs."Id"
  AND fsrec."SeasonYear" != fs."SeasonYear";
-- Expected: UPDATE ~3973


-- ────────────────────────────────────────────────────────────────────────────
-- STEP 8: FIX FRANCHISESEASONPROJECTION RECORDS (if any)
-- ────────────────────────────────────────────────────────────────────────────

UPDATE public."FranchiseSeasonProjection" fsp
SET 
    "SeasonYear" = fs."SeasonYear",
    "ModifiedUtc" = NOW(),
    "ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'
FROM public."FranchiseSeason" fs
WHERE fsp."FranchiseSeasonId" = fs."Id"
  AND fsp."SeasonYear" != fs."SeasonYear";
-- Expected: UPDATE 0 (likely none)


-- ────────────────────────────────────────────────────────────────────────────
-- STEP 9: VERIFY ALL FIXES (Should return 0 for all rows)
-- ────────────────────────────────────────────────────────────────────────────

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
INNER JOIN public."FranchiseSeason" hfs ON hfs."Id" = c."HomeTeamFranchiseSeasonId"
INNER JOIN public."FranchiseSeason" afs ON afs."Id" = c."AwayTeamFranchiseSeasonId"
WHERE c."SeasonYear" != hfs."SeasonYear"
   OR c."SeasonYear" != afs."SeasonYear"

UNION ALL

SELECT 'FranchiseSeasonRanking vs FranchiseSeason mismatch', COUNT(*)
FROM public."FranchiseSeasonRanking" fsr
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsr."FranchiseSeasonId"
WHERE fsr."SeasonYear" != fs."SeasonYear"

UNION ALL

SELECT 'FranchiseSeasonRecord vs FranchiseSeason mismatch', COUNT(*)
FROM public."FranchiseSeasonRecord" fsrec
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsrec."FranchiseSeasonId"
WHERE fsrec."SeasonYear" != fs."SeasonYear"

UNION ALL

SELECT 'FranchiseSeasonProjection vs FranchiseSeason mismatch', COUNT(*)
FROM public."FranchiseSeasonProjection" fsp
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsp."FranchiseSeasonId"
WHERE fsp."SeasonYear" != fs."SeasonYear";

-- ✅ All counts should be 0!


-- ────────────────────────────────────────────────────────────────────────────
-- STEP 10: COMMIT OR ROLLBACK
-- ────────────────────────────────────────────────────────────────────────────

-- If verification passed (all counts = 0), commit the changes:
COMMIT;

-- If something went wrong, undo everything:
-- ROLLBACK;


-- ────────────────────────────────────────────────────────────────────────────
-- POST-FIX: Sample some corrected data
-- ────────────────────────────────────────────────────────────────────────────

-- Check the 2016 record we originally found
SELECT 
    gs."Id",
    gs."SeasonYear",
    gs."Slug",
    s."Year" as season_year,
    gse."SourceUrl"
FROM public."GroupSeason" gs
INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
INNER JOIN public."GroupSeasonExternalId" gse ON gse."GroupSeasonId" = gs."Id"
WHERE gs."Id" = '7586ee28-2e8d-f8e3-cabf-042bc6449da1';
-- Should show SeasonYear = 2016 now!

-- Sample a few FranchiseSeasons to verify they inherited correct year
SELECT 
    fs."Id",
    fs."SeasonYear",
    fs."Slug",
    gs."SeasonYear" as group_season_year,
    s."Year" as season_year
FROM public."FranchiseSeason" fs
INNER JOIN public."GroupSeason" gs ON gs."Id" = fs."GroupSeasonId"
INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
WHERE gs."Slug" = 'ncaa-football' 
  AND s."Year" IN (2016, 2020, 2024)
LIMIT 20;
-- SeasonYear should match Season.Year for all records

