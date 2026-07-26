
--select * from public."GroupSeason" where "SeasonYear" = 2025 order by "Name"
--update public."GroupSeason" set "Abbreviation" = 'fbs' where "Id" = '1e10586b-5d26-c8bf-5698-c3330336d61d';
--update public."GroupSeason" set "Abbreviation" = 'ind' where "Id" = '9ea09f5b-bd90-6751-ea62-926a45fb3e76';
--update public."GroupSeason" set "Abbreviation" = 'fbs' where "Name" = 'FBS' and "Abbreviation" = 'UNK';

--select * from public."FranchiseSeason" where "Id" = 'c13b7c74-6892-3efa-2492-36ebf5220464' -- ND 2d6997c7-4e93-beb1-5418-9b84fb4c06ce

--select * from public."GroupSeason" where "Name" = 'FBS' order by "SeasonYear" desc;

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
    fs."SeasonYear",
    gp.path AS "GroupHierarchyPath"
FROM public."FranchiseSeason" fs
JOIN public."Franchise" f ON fs."FranchiseId" = f."Id"
JOIN public."GroupSeason" gs ON fs."GroupSeasonId" = gs."Id"
JOIN group_path gp ON gp."Id" = gs."Id"
WHERE f."Id" = 'd2ca25ce-337e-1913-b405-69a16329efe7'
ORDER BY fs."SeasonYear" DESC;--  f."Slug", fs."Id";

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

