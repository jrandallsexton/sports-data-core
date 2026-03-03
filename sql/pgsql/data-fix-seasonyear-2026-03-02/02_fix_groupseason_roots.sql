-- ============================================================================
-- STEP 02: FIX ROOT GROUPSEASON RECORDS
-- ============================================================================
-- DATABASE: sdProducer.FootballNcaa
-- PURPOSE: Fix the 24 NCAA Football root GroupSeason records
-- RECORDS AFFECTED: 24 GroupSeason records
-- SAFE TO RUN: Yes - updates onlydenormalized SeasonYear field
-- ============================================================================

-- ----------------------------------------------------------------------------
-- PREVIEW: What will be updated
-- ----------------------------------------------------------------------------

SELECT 
    gs."Id",
    gs."SeasonYear" as old_season_year,
    s."Year" as new_season_year,
    gs."Slug",
    gs."Name"
FROM public."GroupSeason" gs
INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
WHERE gs."Slug" = 'ncaa-football' 
  AND gs."SeasonYear" != s."Year"
ORDER BY s."Year";

-- Expected: 24 rows


-- ----------------------------------------------------------------------------
-- EXECUTE: Update root GroupSeason records
-- ----------------------------------------------------------------------------

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


-- ----------------------------------------------------------------------------
-- VERIFY: Confirm fix was successful
-- ----------------------------------------------------------------------------

-- Should return 0 rows (no more mismatches)
SELECT 
    gs."Id",
    gs."SeasonYear",
    s."Year" as season_year,
    gs."Slug"
FROM public."GroupSeason" gs
INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
WHERE gs."Slug" = 'ncaa-football' 
  AND gs."SeasonYear" != s."Year";

-- Expected: 0 rows


-- Sample some corrected records
SELECT 
    gs."Id",
    gs."SeasonYear",
    s."Year" as season_year,
    gs."Slug",
    gse."SourceUrl"
FROM public."GroupSeason" gs
INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
INNER JOIN public."GroupSeasonExternalId" gse ON gse."GroupSeasonId" = gs."Id"
WHERE gs."Slug" = 'ncaa-football'
  AND s."Year" IN (2016, 2020, 2024)
ORDER BY s."Year";

-- Expected: SeasonYear should match the year in SourceUrl

-- ============================================================================
-- ✓ STEP 02 COMPLETE: Root GroupSeason records fixed
-- NEXT: Run 03_fix_groupseason_children.sql
-- ============================================================================
