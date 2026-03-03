-- ============================================================================
-- STEP 99: FINAL VERIFICATION
-- ============================================================================
-- DATABASE: sdProducer.FootballNcaa
-- PURPOSE: Comprehensive verification that ALL SeasonYear mismatches are fixed
-- SAFE TO RUN: Yes - read-only queries
-- EXPECTED: All counts should be 0
-- ============================================================================

-- ----------------------------------------------------------------------------
-- COMPREHENSIVE MISMATCH CHECK
-- ----------------------------------------------------------------------------
-- All queries should return count = 0

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

-- ✅ Expected: All counts = 0 (Actual: All counts = 0)


-- ----------------------------------------------------------------------------
-- SPOT CHECK: Verify specific corrected records
-- ----------------------------------------------------------------------------

-- Check the original 2016 NCAA Football record that started this investigation
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

-- ✅ Expected: SeasonYear = 2016, SourceUrl contains "/seasons/2016/"


-- Check a sample of FranchiseSeasons from various corrected years
SELECT 
    fs."Id",
    fs."SeasonYear",
    fs."Slug" as team,
    gs."SeasonYear" as group_season_year,
    s."Year" as season_year
FROM public."FranchiseSeason" fs
INNER JOIN public."GroupSeason" gs ON gs."Id" = fs."GroupSeasonId"
INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
WHERE s."Year" IN (2016, 2020, 2024)
ORDER BY s."Year", fs."Slug"
LIMIT 30;

-- ✅ Expected: All three year columns should match for each row


-- ----------------------------------------------------------------------------
-- DATA DISTRIBUTION: Show corrected records by year
-- ----------------------------------------------------------------------------

-- Count all GroupSeasons, FranchiseSeasons, and Contests for affected years
WITH yearly_counts AS (
    SELECT 
        s."Year" as season_year,
        COUNT(DISTINCT gs."Id") as group_seasons,
        COUNT(DISTINCT fs."Id") as franchise_seasons
    FROM public."Season" s
    LEFT JOIN public."GroupSeason" gs ON gs."SeasonId" = s."Id"
    LEFT JOIN public."FranchiseSeason" fs ON fs."GroupSeasonId" = gs."Id"
    WHERE s."Year" IN (1949, 1954, 1968, 1971, 1975, 1977, 1979, 1980, 1981, 1988, 
                        1994, 2001, 2013, 2014, 2015, 2016, 2017, 2018, 2019, 2020, 
                        2021, 2022, 2023, 2024, 2026)
    GROUP BY s."Year"
),
contest_counts AS (
    SELECT 
        s."Year" as season_year,
        COUNT(DISTINCT c."Id") as contests
    FROM public."Season" s
    LEFT JOIN public."GroupSeason" gs ON gs."SeasonId" = s."Id"
    LEFT JOIN public."FranchiseSeason" fs ON fs."GroupSeasonId" = gs."Id"
    LEFT JOIN public."Contest" c ON c."HomeTeamFranchiseSeasonId" = fs."Id"
    WHERE s."Year" IN (1949, 1954, 1968, 1971, 1975, 1977, 1979, 1980, 1981, 1988, 
                        1994, 2001, 2013, 2014, 2015, 2016, 2017, 2018, 2019, 2020, 
                        2021, 2022, 2023, 2024, 2026)
    GROUP BY s."Year"
)
SELECT 
    yc.season_year,
    yc.group_seasons,
    yc.franchise_seasons,
    cc.contests
FROM yearly_counts yc
LEFT JOIN contest_counts cc ON cc.season_year = yc.season_year
ORDER BY yc.season_year;

-- Shows distribution of corrected data across the affected years


-- ----------------------------------------------------------------------------
-- HIERARCHY VERIFICATION: Check one complete hierarchy (2016)
-- ----------------------------------------------------------------------------

WITH RECURSIVE hierarchy AS (
    SELECT 
        gs."Id",
        gs."SeasonYear",
        gs."Slug",
        gs."Name",
        0 as level,
        gs."Slug"::TEXT as path
    FROM public."GroupSeason" gs
    INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
    WHERE gs."Slug" = 'ncaa-football' AND s."Year" = 2016
    
    UNION ALL
    
    SELECT 
        gs."Id",
        gs."SeasonYear",
        gs."Slug",
        gs."Name",
        h.level + 1,
        h.path || ' > ' || gs."Slug"
    FROM public."GroupSeason" gs
    INNER JOIN hierarchy h ON gs."ParentId" = h."Id"
)
SELECT 
    level,
    "SeasonYear",
    "Slug",
    "Name",
    path
FROM hierarchy
ORDER BY level, "Slug";

-- ✅ Expected: All records should show SeasonYear = 2016 (Actual: All records show 2016)


-- ----------------------------------------------------------------------------
-- RECORD COUNTS: Total records updated
-- ----------------------------------------------------------------------------

SELECT 
    'Total GroupSeasons updated' as metric,
    COUNT(*) as count
FROM public."GroupSeason" gs
INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
WHERE s."Year" IN (1949, 1954, 1968, 1971, 1975, 1977, 1979, 1980, 1981, 1988, 
                    1994, 2001, 2013, 2014, 2015, 2016, 2017, 2018, 2019, 2020, 
                    2021, 2022, 2024, 2026)
  AND gs."ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'

UNION ALL

SELECT 
    'Total FranchiseSeasons updated',
    COUNT(*)
FROM public."FranchiseSeason" fs
WHERE fs."ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'
  AND fs."ModifiedUtc" > (NOW() - INTERVAL '1 hour')

UNION ALL

SELECT 
    'Total Contests updated',
    COUNT(*)
FROM public."Contest" c
WHERE c."ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'
  AND c."ModifiedUtc" > (NOW() - INTERVAL '1 hour');

-- Shows how many records were touched in this fix

-- metric	count
-- Total GroupSeasons updated	1223
-- Total FranchiseSeasons updated	757
-- Total Contests updated	2423

-- ----------------------------------------------------------------------------
-- UI TESTING REFERENCE: Sample IDs for verification
-- ----------------------------------------------------------------------------

-- Sample Contest IDs from corrected years for UI spot-checking
SELECT 
    c."Id" as contest_id,
    c."SeasonYear",
    c."Name" as matchup,
    c."StartDateUtc"
FROM public."Contest" c
INNER JOIN public."FranchiseSeason" hfs ON hfs."Id" = c."HomeTeamFranchiseSeasonId"
INNER JOIN public."GroupSeason" gs ON gs."Id" = hfs."GroupSeasonId"
INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
WHERE s."Year" = 2016
ORDER BY c."StartDateUtc"
LIMIT 10;

-- Use these Contest IDs to verify in the UI that year displays correctly


-- ============================================================================
-- ✅ FINAL VERIFICATION COMPLETE
-- ============================================================================
-- If all verification queries pass:
-- - All mismatches show count = 0
-- - Spot checks show correct years
-- - Hierarchy is consistent
-- - Ready for UI testing and production deployment
-- ============================================================================
