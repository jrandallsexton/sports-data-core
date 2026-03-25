-- ============================================================================
-- STEP 01: VERIFY THE PROBLEM
-- ============================================================================
-- DATABASE: sdProducer.FootballNcaa
-- PURPOSE: Read-only queries to understand scope of SeasonYear corruption
-- SAFE TO RUN: Yes - no data modifications
-- ============================================================================

-- ----------------------------------------------------------------------------
-- Show the 24 bad root NCAA Football GroupSeason records
-- ----------------------------------------------------------------------------
-- Expected: 24 rows showing SeasonYear = 2023 but actual year varies (1949-2026)

select * from public."Contest" where "Id" = '06747d6c-31c6-8651-bd96-97b0f00a2f78'; -- Incorrectly shows 2023 for SeasonYear column
select * from public."SeasonWeek" where "Id" = '14950462-f17c-4f36-b1dd-bcb865ffe945'; -- Week 1 for the Contest record above; correctly shows 2017 in StartDate and EndDate
select * from public."Season" where "Id" = '43d29d2b-6f33-7314-799a-dfe396a0ddc7'; -- Season record for the above; correctly shows 2017 in Year column

select * from public."FranchiseSeason" where "Id" = 'fd955d84-be8a-e93c-ae10-990d8a49f440'; -- BYU; AwayFranchiseSeasonId for the Contest record above; shows wrong SeasonYear = 2023
select * from public."GroupSeason" where "Id" = '9530f610-6537-e801-167e-7bfb9d9177f2'; -- BYU groupSeason; shows 2017 for SeasonYear column
select * from public."FranchiseSeason" where "Id" = '4ebb00a0-4b04-76fa-8bfd-0785e1133b01'; -- LSU; HomeFranchiseSeasonId for the Contest record above; shows wrong SeasonYear = 2023
select * from public."GroupSeason" where "Id" = '3ec83be8-9217-a7ae-b0e7-64f64537849a'; -- LSU groupSeason; shows 2017 for SeasonYear column

SELECT 
    gs."Id",
    gs."SeasonYear" as wrong_season_year,
    s."Year" as correct_season_year,
    (s."Year" - gs."SeasonYear") as year_difference,
    gs."Slug",
    gs."Name",
    gse."SourceUrl",
    gs."CreatedUtc"
FROM public."GroupSeason" gs
INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
INNER JOIN public."GroupSeasonExternalId" gse ON gse."GroupSeasonId" = gs."Id"
WHERE gs."Slug" = 'ncaa-football' 
  AND gs."SeasonYear" != s."Year"
ORDER BY s."Year";


-- ----------------------------------------------------------------------------
-- Count all affected records across the entire hierarchy
-- ----------------------------------------------------------------------------
-- This shows the cascade effect of the bad root records

WITH RECURSIVE all_bad_group_seasons AS (
    -- Start with bad root records
    SELECT gs."Id", s."Year" as correct_year
    FROM public."GroupSeason" gs
    INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
    WHERE gs."Slug" = 'ncaa-football' AND gs."SeasonYear" != s."Year"
    
    UNION ALL
    
    -- Get all descendants recursively
    SELECT gs."Id", abgs.correct_year
    FROM public."GroupSeason" gs
    INNER JOIN all_bad_group_seasons abgs ON gs."ParentId" = abgs."Id"
)
SELECT 
    COUNT(DISTINCT abgs."Id") as bad_group_season_count,
    COUNT(DISTINCT fs."Id") as franchise_season_count,
    COUNT(DISTINCT con."Id") as contest_count,
    COUNT(DISTINCT fsr."Id") as franchise_season_ranking_count,
    COUNT(DISTINCT fsrec."Id") as franchise_season_record_count,
    COUNT(DISTINCT fsp."Id") as franchise_season_projection_count
FROM all_bad_group_seasons abgs
LEFT JOIN public."FranchiseSeason" fs ON fs."GroupSeasonId" = abgs."Id"
LEFT JOIN public."Contest" con ON (con."HomeTeamFranchiseSeasonId" = fs."Id" OR con."AwayTeamFranchiseSeasonId" = fs."Id")
LEFT JOIN public."FranchiseSeasonRanking" fsr ON fsr."FranchiseSeasonId" = fs."Id"
LEFT JOIN public."FranchiseSeasonRecord" fsrec ON fsrec."FranchiseSeasonId" = fs."Id"
LEFT JOIN public."FranchiseSeasonProjection" fsp ON fsp."FranchiseSeasonId" = fs."Id";


-- ----------------------------------------------------------------------------
-- Sample some affected FranchiseSeason records
-- ----------------------------------------------------------------------------
-- Shows teams affected by descendants of the 2016 bad root record

WITH RECURSIVE bad_2016_hierarchy AS (
    -- Root: 2016 NCAA Football record
    SELECT gs."Id"
    FROM public."GroupSeason" gs
    INNER JOIN public."GroupSeasonExternalId" gse ON gse."GroupSeasonId" = gs."Id"
    WHERE gse."SourceUrl" LIKE '%/seasons/2016/%'
      AND gs."Slug" = 'ncaa-football'
    
    UNION ALL
    
    -- All descendants
    SELECT gs."Id"
    FROM public."GroupSeason" gs
    INNER JOIN bad_2016_hierarchy h ON gs."ParentId" = h."Id"
)
SELECT 
    fs."Id",
    fs."SeasonYear" as wrong_year,
    fs."Slug" as team,
    gs."Slug" as group_slug,
    gs."Name" as group_name,
    s."Year" as correct_year
FROM public."FranchiseSeason" fs
INNER JOIN public."GroupSeason" gs ON gs."Id" = fs."GroupSeasonId"
INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
WHERE gs."Id" IN (SELECT "Id" FROM bad_2016_hierarchy)
ORDER BY fs."Slug"
LIMIT 20;


-- ----------------------------------------------------------------------------
-- Sample hierarchy for one affected year (2016)
-- ----------------------------------------------------------------------------
-- Shows the cascade through GroupSeason hierarchy

WITH RECURSIVE hierarchy AS (
    -- Root: 2016 NCAA Football record
    SELECT 
        gs."Id",
        gs."ParentId",
        gs."SeasonYear",
        gs."Slug",
        gs."Name",
        0 as level,
        gs."Slug"::TEXT as path
    FROM public."GroupSeason" gs
    INNER JOIN public."GroupSeasonExternalId" gse ON gse."GroupSeasonId" = gs."Id"
    WHERE gse."SourceUrl" LIKE '%/seasons/2016/%'
      AND gs."Slug" = 'ncaa-football'
    
    UNION ALL
    
    -- Children
    SELECT 
        gs."Id",
        gs."ParentId",
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
    "SeasonYear" as current_wrong_year,
    "Slug",
    "Name",
    path
FROM hierarchy
ORDER BY level, "Slug"
LIMIT 50;


-- ----------------------------------------------------------------------------
-- EXPECTED RESULTS SUMMARY
-- ----------------------------------------------------------------------------
-- Query 1: 24 rows (bad root records from years 1949-2026)
-- Query 2: ~1,266 GroupSeason, ~1,335 FranchiseSeason, ~6,161 Contest, etc.
-- Query 3: Sample teams from 2016 showing wrong SeasonYear
-- Query 4: Hierarchy showing cascade from 2016 root through conferences
-- ============================================================================
