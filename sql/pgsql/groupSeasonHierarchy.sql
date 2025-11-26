WITH RECURSIVE group_path AS (
    -- Anchor: top-level GroupSeasons (no parent)
    SELECT 
        gs."Id",
        gs."Abbreviation",
        gs."Abbreviation"::TEXT AS path
    FROM public."GroupSeason" gs
    WHERE gs."ParentId" IS NULL

    UNION ALL

    -- Recursively build hierarchy from parent to child
    SELECT 
        child."Id",
        child."Abbreviation",
        gp.path || '|' || child."Abbreviation" AS path
    FROM public."GroupSeason" child
    JOIN group_path gp ON child."ParentId" = gp."Id"
)

SELECT 
    fs."Id" AS "FranchiseSeasonId",
    f."Slug" AS "Franchise",
    fs."GroupSeasonId",
    gp.path AS "GroupHierarchyPath"
FROM public."FranchiseSeason" fs
JOIN public."Franchise" f ON fs."FranchiseId" = f."Id"
JOIN public."GroupSeason" gs ON fs."GroupSeasonId" = gs."Id"
JOIN group_path gp ON gp."Id" = gs."Id"
ORDER BY f."Slug", fs."Id";

-- WITH RECURSIVE group_path AS (
--     -- Anchor: top-level GroupSeasons (no parent)
--     SELECT 
--         gs."Id",
--         gs."Abbreviation",
--         gs."Abbreviation"::TEXT AS path
--     FROM public."GroupSeason" gs
--     WHERE gs."ParentId" IS NULL

--     UNION ALL

--     -- Recursively build hierarchy from parent to child
--     SELECT 
--         child."Id",
--         child."Abbreviation",
--         gp.path || '|' || child."Abbreviation" AS path
--     FROM public."GroupSeason" child
--     JOIN group_path gp ON child."ParentId" = gp."Id"
-- )

-- UPDATE public."FranchiseSeason" fs
-- SET "GroupSeasonMap" = gp.path
-- FROM public."GroupSeason" gs
-- JOIN group_path gp ON gp."Id" = gs."Id"
-- WHERE fs."GroupSeasonId" = gs."Id";

