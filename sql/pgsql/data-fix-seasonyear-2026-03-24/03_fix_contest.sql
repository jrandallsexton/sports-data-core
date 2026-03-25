-- ============================================================================
-- STEP 03: FIX Contest.SeasonYear
-- ============================================================================
-- DATABASE: sdProducer.FootballNcaa
-- SOURCE OF TRUTH: Season.Year via SeasonWeek -> Season chain
-- NOTE: We derive from Season.Year (not FranchiseSeason) because Contest
--       has a direct path through SeasonWeekId -> SeasonWeek -> Season.
-- ============================================================================

BEGIN;

-- Preview: what will be updated
SELECT
    c."Id",
    c."Name",
    c."SeasonYear" AS current_wrong,
    s."Year" AS will_become
FROM public."Contest" c
INNER JOIN public."SeasonWeek" sw ON sw."Id" = c."SeasonWeekId"
INNER JOIN public."Season" s ON s."Id" = sw."SeasonId"
WHERE c."SeasonYear" != s."Year"
ORDER BY s."Year", c."Name";

-- Fix
UPDATE public."Contest" c
SET "SeasonYear" = s."Year"
FROM public."SeasonWeek" sw
INNER JOIN public."Season" s ON s."Id" = sw."SeasonId"
WHERE sw."Id" = c."SeasonWeekId"
  AND c."SeasonYear" != s."Year";

-- Verify: should return 0
SELECT COUNT(*) AS remaining_mismatches
FROM public."Contest" c
INNER JOIN public."SeasonWeek" sw ON sw."Id" = c."SeasonWeekId"
INNER JOIN public."Season" s ON s."Id" = sw."SeasonId"
WHERE c."SeasonYear" != s."Year";

COMMIT;
