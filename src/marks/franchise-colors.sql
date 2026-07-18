-- ============================================================================
-- Franchise-LEVEL color dump (go-forward). One row per franchise, colors from
-- its most-recent season. Feeds the franchise-level mark backfill: generate a
-- FranchiseLogo mark per franchise (year-invariant), which the selector uses
-- fail-closed. See docs/logo-license-audit.md.
--
-- No SeasonYear / IsActive filter, so ALL franchises are covered (not just the
-- 2026-active 651) — that's what makes the backfill hole-free.
--
-- Export tab-separated with the header row intact; the header MUST start with
-- "FranchiseId\t" (upload.js / generate.js detect the header by that prefix).
-- Save per sport to franchise-colors-{ncaafb,nfl,mlb}.txt.
-- ============================================================================
SELECT DISTINCT ON (fr."Id")
       fr."Id"            AS "FranchiseId",
       f."Slug",
       f."Abbreviation",
       f."ColorCodeHex",
       f."ColorCodeAltHex"
FROM public."FranchiseSeason" f
INNER JOIN public."Franchise" fr ON fr."Id" = f."FranchiseId"
ORDER BY fr."Id", f."SeasonYear" DESC;

-- ----------------------------------------------------------------------------
-- Prior season-level query (2026-active only) — kept for reference. This is the
-- grain the ORIGINAL franchise-SEASON mark pass used; no longer the go-forward.
-- ----------------------------------------------------------------------------
-- SELECT fr."Id" AS "FranchiseId", f."Id" AS "FranchiseSeasonId", f."Slug",
--        f."Abbreviation", f."ColorCodeHex", f."ColorCodeAltHex"
-- FROM public."FranchiseSeason" f
-- INNER JOIN public."Franchise" fr ON fr."Id" = f."FranchiseId"
-- WHERE f."SeasonYear" = 2026 AND f."IsActive" = true
-- ORDER BY f."Slug";
