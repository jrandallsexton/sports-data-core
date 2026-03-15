-- ============================================================================
-- REMEDIATION: FranchiseSeasonRanking.SeasonWeekId IS NULL
-- Date: 2026-03-14
--
-- Issue: 1,326 FranchiseSeasonRanking rows have SeasonWeekId = NULL.
--   All have /weeks/ in their SourceUrl, so none are legitimately null.
--
-- Root cause: The TeamSeasonRankDocumentProcessor sets SeasonWeekId via
--   externalRefIdentityGenerator.Generate(dto.Season.Type.Week.Ref).CanonicalId
--   but if the SeasonWeek entity didn't exist yet at processing time, the
--   generated ID wouldn't match any row and EF would set it to NULL.
--
-- Strategy: Extract the week URL from each ranking's SourceUrl by stripping
--   /rankings/{id}, then join to SeasonWeekExternalId to resolve the SeasonWeek.
--
-- URL mapping:
--   Ranking: .../seasons/{year}/types/{typeId}/weeks/{weekId}/rankings/{rankingId}
--   Week:    .../seasons/{year}/types/{typeId}/weeks/{weekId}
-- ============================================================================

-- Step 0a: Count of FranchiseSeasonRanking with null SeasonWeekId
SELECT COUNT(*) AS "NullSeasonWeekCount"
FROM public."FranchiseSeasonRanking"
WHERE "SeasonWeekId" IS NULL;

-- Step 0b: Of those, how many have /weeks/ in their SourceUrl (should NOT be null)?
SELECT COUNT(*) AS "HasWeekInUrlButNullSeasonWeekId"
FROM public."FranchiseSeasonRanking" fsr
INNER JOIN public."FranchiseSeasonRankingExternalId" fsre ON fsre."RankingId" = fsr."Id"
WHERE fsr."SeasonWeekId" IS NULL
  AND fsre."SourceUrl" LIKE '%/weeks/%';

-- Step 1: Preview — verify the URL derivation and join resolve correctly
SELECT
    fsr."Id" AS "RankingId",
    fsr."SeasonWeekId" AS "CurrentSeasonWeekId",
    fsre."SourceUrl" AS "RankingSourceUrl",
    SPLIT_PART(fsre."SourceUrl", '/rankings/', 1) AS "DerivedWeekUrl",
    swe."SourceUrl" AS "MatchedWeekSourceUrl",
    sw."Id" AS "ResolvedSeasonWeekId"
FROM public."FranchiseSeasonRanking" fsr
INNER JOIN public."FranchiseSeasonRankingExternalId" fsre ON fsre."RankingId" = fsr."Id"
INNER JOIN public."SeasonWeekExternalId" swe
    ON swe."SourceUrl" = SPLIT_PART(fsre."SourceUrl", '/rankings/', 1)
INNER JOIN public."SeasonWeek" sw ON sw."Id" = swe."SeasonWeekId"
WHERE fsr."SeasonWeekId" IS NULL
  AND fsre."SourceUrl" LIKE '%/weeks/%'
LIMIT 20;

-- Step 1b: Count how many will be fixed (should match 0b)
SELECT COUNT(*) AS "WillBeFixed"
FROM public."FranchiseSeasonRanking" fsr
INNER JOIN public."FranchiseSeasonRankingExternalId" fsre ON fsre."RankingId" = fsr."Id"
INNER JOIN public."SeasonWeekExternalId" swe
    ON swe."SourceUrl" = SPLIT_PART(fsre."SourceUrl", '/rankings/', 1)
INNER JOIN public."SeasonWeek" sw ON sw."Id" = swe."SeasonWeekId"
WHERE fsr."SeasonWeekId" IS NULL
  AND fsre."SourceUrl" LIKE '%/weeks/%';

-- Step 2: Execute the update (uncomment after verifying Step 1)
-- BEGIN;
--
-- UPDATE public."FranchiseSeasonRanking" fsr
-- SET "SeasonWeekId" = sw."Id"
-- FROM public."FranchiseSeasonRankingExternalId" fsre
-- INNER JOIN public."SeasonWeekExternalId" swe
--     ON swe."SourceUrl" = SPLIT_PART(fsre."SourceUrl", '/rankings/', 1)
-- INNER JOIN public."SeasonWeek" sw ON sw."Id" = swe."SeasonWeekId"
-- WHERE fsre."RankingId" = fsr."Id"
--   AND fsr."SeasonWeekId" IS NULL
--   AND fsre."SourceUrl" LIKE '%/weeks/%';
--
-- -- Verify: should match Step 1b count
-- SELECT COUNT(*) AS "RemainingOrphans"
-- FROM public."FranchiseSeasonRanking"
-- WHERE "SeasonWeekId" IS NULL;
--
-- -- If counts look correct: COMMIT;
-- -- Otherwise: ROLLBACK;
