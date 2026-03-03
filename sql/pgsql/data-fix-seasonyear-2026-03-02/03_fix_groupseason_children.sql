-- ============================================================================
-- STEP 03: FIX CHILD GROUPSEASON RECORDS
-- ============================================================================
-- DATABASE: sdProducer.FootballNcaa
-- PURPOSE: Fix all child/descendant GroupSeason records in the hierarchy
-- RECORDS AFFECTED: ~1,242 GroupSeason records (children of the 24 roots)
-- EXECUTION: Run the UPDATE multiple times until it returns "UPDATE 0"
-- SAFE TO RUN: Yes - updates only denormalized SeasonYear field
-- ============================================================================

-- ----------------------------------------------------------------------------
-- PREVIEW: Count of children with wrong SeasonYear
-- ----------------------------------------------------------------------------

-- Simple count of all GroupSeason records with wrong SeasonYear
SELECT COUNT(*) as records_to_fix
FROM public."GroupSeason" gs
INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
WHERE gs."SeasonYear" != s."Year";

-- Expected: ~1,242 initially (Actual: 1,216) (Actual #2: 1,199)


-- ----------------------------------------------------------------------------
-- EXECUTE: Update children to match parent SeasonYear
-- ----------------------------------------------------------------------------
-- NOTE: Run this multiple times until it shows "UPDATE 0"
-- Scoped to the ncaa-football hierarchy to avoid touching unrelated sports/leagues
WITH RECURSIVE target_tree AS (
    SELECT "Id"
    FROM public."GroupSeason"
    WHERE "Slug" = 'ncaa-football'
    UNION ALL
    SELECT gs."Id"
    FROM public."GroupSeason" gs
    INNER JOIN target_tree tt ON gs."ParentId" = tt."Id"
)
UPDATE public."GroupSeason" child
SET 
    "SeasonYear" = parent."SeasonYear",
    "ModifiedUtc" = NOW(),
    "ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'
FROM public."GroupSeason" parent
WHERE child."ParentId" = parent."Id"
  AND child."Id" IN (SELECT "Id" FROM target_tree)
  AND child."SeasonYear" != parent."SeasonYear";

-- Expected: 
-- Run 1: UPDATE ~400-600 (Division I, Division II/III, etc.) (Actual: 36) (Actual #2 runs were all the same as run #1)
-- Run 2: UPDATE ~300-400 (FBS, FCS, etc.) (Actual: 59)
-- Run 3: UPDATE ~200-300 (SEC, Big Ten, etc.) (Actual: 844)
-- Run 4: UPDATE ~100-200 (SEC East, SEC West, etc.) (Actual: 249)
-- Run 5-10: UPDATE remaining records until UPDATE 0 (Actual: Run 5 = 0)

-- RECOMMENDATION: Run the above UPDATE 10 times to ensure all levels are fixed
-- The hierarchy may be deeper than expected (6-8 levels in some cases)


-- ----------------------------------------------------------------------------
-- DIAGNOSTIC: Identify the 11 remaining corrupted records
-- ----------------------------------------------------------------------------

-- Show the 11 remaining records
SELECT 
    gs."Id",
    gs."Slug",
    gs."SeasonYear" as current_year,
    s."Year" as correct_year,
    gs."ParentId",
    parent."SeasonYear" as parent_year
FROM public."GroupSeason" gs
INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
LEFT JOIN public."GroupSeason" parent ON parent."Id" = gs."ParentId"
WHERE gs."SeasonYear" != s."Year"
ORDER BY s."Year", gs."Slug";

-- This shows if the parent is also corrupted (parent_year != correct_year)


-- ----------------------------------------------------------------------------
-- FIX: Direct update based on Season.Year (bypasses parent relationship)
-- ----------------------------------------------------------------------------

-- Scoped to the ncaa-football hierarchy to avoid touching unrelated sports/leagues
WITH RECURSIVE target_tree AS (
    SELECT "Id"
    FROM public."GroupSeason"
    WHERE "Slug" = 'ncaa-football'
    UNION ALL
    SELECT gs."Id"
    FROM public."GroupSeason" gs
    INNER JOIN target_tree tt ON gs."ParentId" = tt."Id"
)
UPDATE public."GroupSeason" gs
SET 
    "SeasonYear" = s."Year",
    "ModifiedUtc" = NOW(),
    "ModifiedBy" = 'e15add7f-557e-4a7e-b6a3-07e320f2a5ee'
FROM public."Season" s
WHERE gs."SeasonId" = s."Id"
  AND gs."Id" IN (SELECT "Id" FROM target_tree)
  AND gs."SeasonYear" != s."Year";

-- Expected: UPDATE 11 (fixes remaining records directly)


-- ----------------------------------------------------------------------------
-- VERIFY: Check for any remaining mismatches
-- ----------------------------------------------------------------------------

-- Simple check: Any GroupSeason with wrong SeasonYear (should return 0)
SELECT COUNT(*) as remaining_corrupted
FROM public."GroupSeason" gs
INNER JOIN public."Season" s ON s."Id" = gs."SeasonId"
WHERE gs."SeasonYear" != s."Year";

-- Expected: 0 (if not 0, run UPDATE again)


-- Parent/Child mismatch check (should return 0 rows)
WITH RECURSIVE hierarchy AS (
    SELECT "Id", "ParentId" as "ParentId", "SeasonYear", "Slug", 0 as level
    FROM public."GroupSeason"
    WHERE "ParentId" IS NULL
    
    UNION ALL
    
    SELECT gs."Id", gs."ParentId", gs."SeasonYear", gs."Slug", h.level + 1
    FROM public."GroupSeason" gs
    INNER JOIN hierarchy h ON gs."ParentId" = h."Id"
)
SELECT 
    h."Id",
    h."SeasonYear" as child_year,
    p."SeasonYear" as parent_year,
    h."Slug",
    h.level
FROM hierarchy h
LEFT JOIN public."GroupSeason" p ON p."Id" = h."ParentId"
WHERE h."ParentId" IS NOT NULL 
  AND h."SeasonYear" != p."SeasonYear"
ORDER BY h.level, h."Slug";

-- Expected: 0 rows (Actual: 0)


-- Sample hierarchy for 2016 to verify cascade
WITH RECURSIVE hierarchy AS (
    SELECT 
        gs."Id",
        gs."SeasonYear",
        gs."Slug",
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
        h.level + 1,
        h.path || ' > ' || gs."Slug"
    FROM public."GroupSeason" gs
    INNER JOIN hierarchy h ON gs."ParentId" = h."Id"
)
SELECT 
    level,
    "SeasonYear",
    "Slug",
    path
FROM hierarchy
ORDER BY level, "Slug"
LIMIT 30;

-- Expected: All records should show SeasonYear = 2016 (Actual: All records show 2016)

-- ============================================================================
-- ✓ STEP 03 COMPLETE: Child GroupSeason records fixed
-- NEXT: Run 04_fix_franchiseseason.sql
-- ============================================================================
