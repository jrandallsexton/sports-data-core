-- ============================================================================
-- STEP 04: FIX FRANCHISESEASON RECORDS
-- ============================================================================
-- DATABASE: sdProducer.FootballNcaa
-- PURPOSE: Fix FranchiseSeason records to match their GroupSeason.SeasonYear
-- RECORDS AFFECTED: ~1,335 FranchiseSeason records
-- SAFE TO RUN: Yes - updates only denormalized SeasonYear field
-- ============================================================================

-- ----------------------------------------------------------------------------
-- PREVIEW: Count and sample records to be updated
-- ----------------------------------------------------------------------------

SELECT COUNT(*) as records_to_fix
FROM public."FranchiseSeason" fs
INNER JOIN public."GroupSeason" gs ON gs."Id" = fs."GroupSeasonId"
WHERE fs."SeasonYear" != gs."SeasonYear";

-- Expected: ~1,335 (Actual: 727) (Actual #2: 757)


-- Sample records showing the mismatch
SELECT 
    fs."Id",
    fs."SeasonYear" as old_year,
    gs."SeasonYear" as new_year,
    fs."Slug" as team,
    gs."Slug" as group_slug
FROM public."FranchiseSeason" fs
INNER JOIN public."GroupSeason" gs ON gs."Id" = fs."GroupSeasonId"
WHERE fs."SeasonYear" != gs."SeasonYear"
ORDER BY fs."Slug"
LIMIT 20;


-- ----------------------------------------------------------------------------
-- EXECUTE: Update FranchiseSeason records
-- ----------------------------------------------------------------------------

UPDATE public."FranchiseSeason" fs
SET 
    "SeasonYear" = gs."SeasonYear",
    "ModifiedUtc" = NOW(),
    "ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'
FROM public."GroupSeason" gs
WHERE fs."GroupSeasonId" = gs."Id"
  AND fs."SeasonYear" != gs."SeasonYear";

-- Expected: UPDATE ~1,335 (Actual: 727) (Actual #2: 757)


-- ----------------------------------------------------------------------------
-- VERIFY: Confirm fix was successful
-- ----------------------------------------------------------------------------

-- Should return 0 rows
SELECT COUNT(*) as remaining_mismatches
FROM public."FranchiseSeason" fs
INNER JOIN public."GroupSeason" gs ON gs."Id" = fs."GroupSeasonId"
WHERE fs."SeasonYear" != gs."SeasonYear";

-- Expected: 0 (Actual: 0)


-- Sample some corrected records from 2016 season
SELECT 
    fs."Id",
    fs."SeasonYear",
    fs."Slug" as team,
    gs."SeasonYear" as group_season_year,
    gs."Slug" as group_slug,
    s."Year" as season_year
FROM public."FranchiseSeason" fs
INNER JOIN public."GroupSeason" gs ON gs."Id" = fs."GroupSeasonId"
INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
WHERE s."Year" = 2016
ORDER BY fs."Slug"
LIMIT 20;

-- Expected: All three year columns should match (2016) (Actual: All three year columns match 2016)


-- Count FranchiseSeasons by corrected year
SELECT 
    fs."SeasonYear",
    COUNT(*) as team_count
FROM public."FranchiseSeason" fs
INNER JOIN public."GroupSeason" gs ON gs."Id" = fs."GroupSeasonId"
INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
WHERE s."Year" IN (1949, 1954, 1968, 1971, 1975, 1977, 1979, 1980, 1981, 1988, 
                    1994, 2001, 2013, 2014, 2015, 2016, 2017, 2018, 2019, 2020, 
                    2021, 2022, 2024, 2025, 2026)
GROUP BY fs."SeasonYear"
ORDER BY fs."SeasonYear";

-- Expected: Distribution of teams across the corrected years

-- ============================================================================
-- ✓ STEP 04 COMPLETE: FranchiseSeason records fixed
-- NEXT: Run 05_fix_contest.sql
-- ============================================================================
