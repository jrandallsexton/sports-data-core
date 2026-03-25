-- ============================================================================
-- STEP 02: FIX FranchiseSeason.SeasonYear
-- ============================================================================
-- DATABASE: sdProducer.FootballNcaa
-- SOURCE OF TRUTH: GroupSeason.SeasonYear (already correct after March 2 fix)
-- ============================================================================

BEGIN;

-- Preview: what will be updated
SELECT
    fs."Id",
    fs."Slug" AS team,
    fs."SeasonYear" AS current_wrong,
    gs."SeasonYear" AS will_become
FROM public."FranchiseSeason" fs
INNER JOIN public."GroupSeason" gs ON gs."Id" = fs."GroupSeasonId"
WHERE fs."SeasonYear" != gs."SeasonYear"
ORDER BY gs."SeasonYear", fs."Slug";

-- Fix
UPDATE public."FranchiseSeason" fs
SET "SeasonYear" = gs."SeasonYear"
FROM public."GroupSeason" gs
WHERE gs."Id" = fs."GroupSeasonId"
  AND fs."SeasonYear" != gs."SeasonYear";

-- Verify: should return 0
SELECT COUNT(*) AS remaining_mismatches
FROM public."FranchiseSeason" fs
INNER JOIN public."GroupSeason" gs ON gs."Id" = fs."GroupSeasonId"
WHERE fs."SeasonYear" != gs."SeasonYear";

COMMIT;
