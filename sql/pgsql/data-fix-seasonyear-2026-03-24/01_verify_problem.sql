-- ============================================================================
-- STEP 01: VERIFY THE PROBLEM
-- ============================================================================
-- DATABASE: sdProducer.FootballNcaa
-- PURPOSE: Read-only queries to find FranchiseSeason records where SeasonYear
--          doesn't match the parent GroupSeason's SeasonYear.
--          The March 2 fix corrected GroupSeason roots, but FranchiseSeason
--          and its dependents still carry the wrong value.
-- SAFE TO RUN: Yes - no data modifications
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. FranchiseSeason records where SeasonYear != GroupSeason.SeasonYear
-- ----------------------------------------------------------------------------
SELECT
    fs."Id" AS franchise_season_id,
    fs."Slug" AS team,
    fs."SeasonYear" AS wrong_year,
    gs."SeasonYear" AS correct_year,
    gs."Slug" AS group_slug,
    gs."Name" AS group_name
FROM public."FranchiseSeason" fs
INNER JOIN public."GroupSeason" gs ON gs."Id" = fs."GroupSeasonId"
WHERE fs."SeasonYear" != gs."SeasonYear"
ORDER BY gs."SeasonYear", fs."Slug";

-- ----------------------------------------------------------------------------
-- 2. Count of affected FranchiseSeason records
-- ----------------------------------------------------------------------------
SELECT COUNT(*) AS mismatched_franchise_seasons
FROM public."FranchiseSeason" fs
INNER JOIN public."GroupSeason" gs ON gs."Id" = fs."GroupSeasonId"
WHERE fs."SeasonYear" != gs."SeasonYear";

-- ----------------------------------------------------------------------------
-- 3. Contest records where SeasonYear doesn't match Season.Year
--    (derived via SeasonWeek -> Season, same path used by fix script 03)
-- ----------------------------------------------------------------------------
SELECT COUNT(*) AS mismatched_contests
FROM public."Contest" c
INNER JOIN public."SeasonWeek" sw ON sw."Id" = c."SeasonWeekId"
INNER JOIN public."Season" s ON s."Id" = sw."SeasonId"
WHERE c."SeasonYear" != s."Year";

-- ----------------------------------------------------------------------------
-- 4. FranchiseSeasonRanking records with wrong SeasonYear
-- ----------------------------------------------------------------------------
SELECT COUNT(*) AS mismatched_rankings
FROM public."FranchiseSeasonRanking" fsr
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsr."FranchiseSeasonId"
INNER JOIN public."GroupSeason" gs ON gs."Id" = fs."GroupSeasonId"
WHERE fsr."SeasonYear" != gs."SeasonYear";

-- ----------------------------------------------------------------------------
-- 5. FranchiseSeasonRecord records with wrong SeasonYear
-- ----------------------------------------------------------------------------
SELECT COUNT(*) AS mismatched_records
FROM public."FranchiseSeasonRecord" fsrec
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsrec."FranchiseSeasonId"
INNER JOIN public."GroupSeason" gs ON gs."Id" = fs."GroupSeasonId"
WHERE fsrec."SeasonYear" != gs."SeasonYear";

-- ----------------------------------------------------------------------------
-- 6. FranchiseSeasonProjection records with wrong SeasonYear
-- ----------------------------------------------------------------------------
SELECT COUNT(*) AS mismatched_projections
FROM public."FranchiseSeasonProjection" fsp
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsp."FranchiseSeasonId"
INNER JOIN public."GroupSeason" gs ON gs."Id" = fs."GroupSeasonId"
WHERE fsp."SeasonYear" != gs."SeasonYear";

-- ----------------------------------------------------------------------------
-- 7. Summary: which wrong SeasonYear values exist and how many per year?
-- ----------------------------------------------------------------------------
SELECT
    fs."SeasonYear" AS wrong_year,
    gs."SeasonYear" AS correct_year,
    COUNT(*) AS franchise_season_count
FROM public."FranchiseSeason" fs
INNER JOIN public."GroupSeason" gs ON gs."Id" = fs."GroupSeasonId"
WHERE fs."SeasonYear" != gs."SeasonYear"
GROUP BY fs."SeasonYear", gs."SeasonYear"
ORDER BY gs."SeasonYear";
