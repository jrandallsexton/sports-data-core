-- ============================================================================
-- STEP 05: FINAL VERIFICATION
-- ============================================================================
-- DATABASE: sdProducer.FootballNcaa
-- PURPOSE: Confirm zero SeasonYear mismatches remain across all tables.
--          Every query below should return 0.
-- SAFE TO RUN: Yes - no data modifications
-- ============================================================================

-- GroupSeason vs Season (should already be 0 from March 2 fix)
SELECT 'GroupSeason' AS entity, COUNT(*) AS mismatches
FROM public."GroupSeason" gs
INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
WHERE gs."SeasonYear" != s."Year"

UNION ALL

-- FranchiseSeason vs GroupSeason
SELECT 'FranchiseSeason', COUNT(*)
FROM public."FranchiseSeason" fs
INNER JOIN public."GroupSeason" gs ON gs."Id" = fs."GroupSeasonId"
WHERE fs."SeasonYear" != gs."SeasonYear"

UNION ALL

-- Contest vs Season (via SeasonWeek)
SELECT 'Contest', COUNT(*)
FROM public."Contest" c
INNER JOIN public."SeasonWeek" sw ON sw."Id" = c."SeasonWeekId"
INNER JOIN public."Season" s ON s."Id" = sw."SeasonId"
WHERE c."SeasonYear" != s."Year"

UNION ALL

-- FranchiseSeasonRanking vs FranchiseSeason
SELECT 'FranchiseSeasonRanking', COUNT(*)
FROM public."FranchiseSeasonRanking" fsr
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsr."FranchiseSeasonId"
WHERE fsr."SeasonYear" != fs."SeasonYear"

UNION ALL

-- FranchiseSeasonRecord vs FranchiseSeason
SELECT 'FranchiseSeasonRecord', COUNT(*)
FROM public."FranchiseSeasonRecord" fsrec
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsrec."FranchiseSeasonId"
WHERE fsrec."SeasonYear" != fs."SeasonYear"

UNION ALL

-- FranchiseSeasonProjection vs FranchiseSeason
SELECT 'FranchiseSeasonProjection', COUNT(*)
FROM public."FranchiseSeasonProjection" fsp
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsp."FranchiseSeasonId"
WHERE fsp."SeasonYear" != fs."SeasonYear"

ORDER BY entity;
