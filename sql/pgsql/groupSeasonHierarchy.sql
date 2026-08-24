
--select * from public."GroupSeason" where "SeasonYear" = 2026 order by "Name"
--update public."GroupSeason" set "Abbreviation" = 'fbs' where "Id" = '1e10586b-5d26-c8bf-5698-c3330336d61d';
--update public."GroupSeason" set "Abbreviation" = 'ind' where "Id" = '9ea09f5b-bd90-6751-ea62-926a45fb3e76';
--update public."GroupSeason" set "Abbreviation" = 'fbs' where "Name" = 'FBS' and "Abbreviation" = 'UNK';
update public."GroupSeason" set "Abbreviation" = 'fcs' where "Name" = 'FCS' and "Abbreviation" = 'UNK';

--select * from public."FranchiseSeason" where "Id" = 'c13b7c74-6892-3efa-2492-36ebf5220464' -- ND 2d6997c7-4e93-beb1-5418-9b84fb4c06ce
select * from public."FranchiseSeason" where "SeasonYear" = 2024 and "GroupSeasonMap" is null;
select * from public."FranchiseSeason" where "SeasonYear" = 2026 and "GroupSeasonMap" like '%fcs%' order by "Slug"

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
--WHERE f."Id" = 'd2ca25ce-337e-1913-b405-69a16329efe7' -- LSU Tigers
--ORDER BY fs."SeasonYear" DESC;--  f."Slug", fs."Id";
ORDER BY f."Slug", fs."Id";

-- FranchiseSeasonId	Franchise	GroupSeasonId	SeasonYear	GroupHierarchyPath
-- fe246458-7d82-0137-12ec-00346206577a	lsu-tigers	b64acf49-b1ce-818b-858b-721d5f2b62e2	2026	NCAAF|NCAA|UNK|sec
-- c13b7c74-6892-3efa-2492-36ebf5220464	lsu-tigers	845eb718-b58a-bf9e-fa90-5de861c60325	2025	NCAAF|NCAA|fbs|sec
-- 8f90c51b-d906-2c02-e8e8-2ac0ac6340ae	lsu-tigers	e6256713-df9f-55c6-c67f-febf109844d6	2024	NCAAF|NCAA|UNK|sec
-- 0dcce1e1-5c3e-f85a-926a-066297c1d26d	lsu-tigers	74e0ed54-b820-e129-7f27-74abab858883	2023	NCAAF|NCAA|UNK|sec|secw
-- 8be8ef96-d766-0516-e90f-d42bf9b1d52d	lsu-tigers	9a62caab-9b25-0272-5967-a7f5506b361e	2022	NCAAF|NCAA|UNK|sec|secw
-- 46a9b7c0-6805-dd9c-6bb8-506cb7a92733	lsu-tigers	5ee70e11-a818-eca5-3394-359befdbeba4	2021	NCAAF|NCAA|UNK|sec|secw
-- faa614a4-71d1-4e47-5362-e99917c394c0	lsu-tigers	202ae119-f438-a64d-ef80-9e2b7c0a74ee	2020	NCAAF|NCAA|UNK|sec|secw
-- 46cc1006-f69d-30db-5dc5-e00804a7f58a	lsu-tigers	bbd8e1c6-8571-bbd0-2101-8f5a0ab33e9a	2019	NCAAF|NCAA|UNK|sec|secw
-- aae62fb7-9915-116c-5bca-540a49ca8fbe	lsu-tigers	2cb72ab8-5ac2-65ca-389d-93422252eeb9	2018	NCAAF|NCAA|UNK|sec|secw
-- 4ebb00a0-4b04-76fa-8bfd-0785e1133b01	lsu-tigers	3ec83be8-9217-a7ae-b0e7-64f64537849a	2017	NCAAF|NCAA|UNK|sec|secw
-- df71c318-6a84-2bf8-0685-a723b23db228	lsu-tigers	d9c3a90c-52ee-995c-1137-42a14633dbad	2016	NCAAF|NCAA|UNK|sec|secw
-- 7d7cc6c0-69ea-a516-fb01-b84ad9c1a5c2	lsu-tigers	00bbb48c-1d2d-1a24-ffab-732f034ccf19	2015	NCAAF|NCAA|UNK|sec|secw
-- cb50aabd-a512-a598-d9f6-f5b28ad53e3b	lsu-tigers	1146147c-851d-2599-1cb9-6b8e244436d7	2014	NCAAF|NCAA|UNK|sec|secw
-- 1c070f73-aa97-b478-2c32-4bf5dc4d345b	lsu-tigers	d87d3825-4192-335e-1629-e454fb066395	2013	NCAAF|NCAA|UNK|sec|secw
-- 0d775aa4-bf8b-8155-62ed-d3babff3b2ca	lsu-tigers	5272dbe3-1620-7d5b-76fa-659a6c57d663	2012	NCAAF|NCAA|UNK|sec|secw
-- 14ba6904-bb36-240f-0348-77f002ee7e14	lsu-tigers	de295b01-f368-0866-2d20-9ffffc1f21e7	2011	NCAAF|NCAA|UNK|sec|secw
-- eda41cd2-cb6f-278d-7294-c7194dcc94a7	lsu-tigers	31ccd23a-a158-e23e-6277-828d059c60fd	2010	NCAAF|NCAA|UNK|sec|secw
-- eb08cd51-dee2-b457-6df0-9b4c36cdef85	lsu-tigers	4198d560-a35e-0d46-8a23-72a3e57230f4	2009	NCAAF|NCAA|UNK|sec|secw
-- 4bfe0f1e-a862-423b-c368-3a68265705ee	lsu-tigers	ccc4572c-41bc-bcf5-4acc-26a59ed4c017	2008	NCAAF|NCAA|UNK|sec|secw
-- 868b05f9-d085-9bc5-c142-f98d7809cd0e	lsu-tigers	7e9e6d5e-c8c0-aaed-45f9-1c79c9f9cede	2007	NCAAF|NCAA|UNK|sec|secw
-- 02bc91d2-4c31-5088-a6a0-c2daf87bc94e	lsu-tigers	24c5e108-9fef-0acb-2abb-471fffeec81a	2006	NCAAF|NCAA|UNK|sec|secw
-- 1312170e-b99f-f3f0-6c90-68d546a8c96d	lsu-tigers	abbe083b-72e3-07d4-83b6-e680ea2c37a3	2005	NCAAF|NCAA|UNK|sec|secw
-- 0c519b36-a0a8-749d-2d57-b72527f05693	lsu-tigers	b756901d-f24e-fdca-78ed-9ea73b0c0bd4	2004	NCAAF|NCAA|UNK|sec|secw
-- 8fd17838-1095-a86d-25e2-14ebe108d4d6	lsu-tigers	b80ac206-0e91-3f0c-3c80-67c93f56e45e	2003	NCAAF|NCAA|UNK|sec|secw
-- 78174d37-0e16-cf1c-be3a-a912be6f8551	lsu-tigers	2a5a5022-b25f-2068-3fdd-dfaf73fd153c	2002	NCAAF|NCAA|UNK|sec|secw
-- 0072e950-0afe-0f08-9c18-801375d7e3e9	lsu-tigers	c2bb73af-87da-68de-59d7-729f87c9caea	2001	NCAAF|NCAA|UNK|sec|secw
-- b84399c0-b1a3-bb2f-ce2b-e109af8db9c1	lsu-tigers	4795fae5-b473-4c7a-8c50-644fb9a971d5	2000	NCAAF|NCAA|UNK|sec
-- f569766e-a68e-beb8-07b9-793361a4a33e	lsu-tigers	786bd1a7-0816-e728-2e10-2164d49309f6	1999	NCAAF|NCAA|UNK|sec
-- 36217e5a-aaa7-d09c-a5be-3119f111e6c4	lsu-tigers	e7c8f159-efc9-3948-ce1b-978a0bcead0b	1998	NCAAF|NCAA|UNK|sec
-- d89c6381-f168-ee44-a2be-eae6ae8f25a8	lsu-tigers	b51ad573-eaba-a8aa-da12-bf1760097f36	1997	NCAAF|NCAA|UNK|sec
-- 7c8132c5-9e4e-90be-d7be-b01e06fdc592	lsu-tigers	4223627d-d9cb-bf48-7b18-51d93274c359	1996	NCAAF|NCAA|UNK|sec
-- 067d6143-0406-82a3-7454-26ca41144fd2	lsu-tigers	b018ad21-b3e0-0c7b-ff1a-49d4288b9ad1	1995	NCAAF|NCAA|UNK|sec
-- 58ff156f-2434-1039-bc5d-06c351bd6b03	lsu-tigers	74c524e5-b9c4-06e7-28ea-cd63acdfdb95	1994	NCAAF|NCAA|UNK|sec
-- 77d6d6d8-a6be-fe44-52d0-d284146d19ad	lsu-tigers	ad67fccf-7ea1-85ca-74e2-b4d257bc780e	1993	NCAAF|NCAA|UNK|sec
-- f8430ccf-2147-74e4-4988-4f4d0c7892a1	lsu-tigers	a4316616-b4aa-3a39-80f5-0032d132c399	1992	NCAAF|NCAA|UNK|sec
-- 7b9cbab3-153c-883c-21e0-97445daded76	lsu-tigers	84e63435-2c8b-b785-e4f2-6dc5f4506c2d	1991	NCAAF|NCAA|UNK|sec
-- 1f85a1c9-1338-8bca-8cbf-f70cab6b30d6	lsu-tigers	8b668a38-e7c7-8a3a-0cb1-c30607cf4f01	1990	NCAAF|NCAA|UNK|sec

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

SELECT "SeasonYear", "Name", "Abbreviation", COUNT(*)
FROM public."GroupSeason"
WHERE "ParentId" IN (SELECT "Id" FROM public."GroupSeason" WHERE "Abbreviation" = 'NCAA')
GROUP BY "SeasonYear", "Name", "Abbreviation"
ORDER BY "SeasonYear" DESC, "Name";

-- Results
-- SeasonYear	Name	Abbreviation	count
-- 2026	FBS	UNK	1
-- 2026	FCS	UNK	1
-- 2025	FBS (I-A)	fbs	4
-- 2025	FCS (I-AA)	fcs	4
-- 2024	FBS	UNK	1
-- 2024	FCS	UNK	1
-- 2023	FBS	UNK	1
-- 2023	FCS	UNK	1
-- 2022	FBS	UNK	1
-- 2022	FCS	UNK	1
-- 2021	FBS	UNK	1
-- 2021	FCS	UNK	1
-- 2020	FBS	UNK	1
-- 2020	FCS	UNK	1
-- 2019	FBS	UNK	1
-- 2019	FCS	UNK	1
-- 2018	FBS	UNK	1
-- 2018	FCS	UNK	1
-- 2017	FBS	UNK	1
-- 2017	FCS	UNK	1
-- 2016	FBS	UNK	1
-- 2016	FCS	UNK	1
-- 2015	FBS	UNK	1
-- 2015	FCS	UNK	1
-- 2014	FBS	UNK	1
-- 2014	FCS	UNK	1
-- 2013	FBS	UNK	1
-- 2013	FCS	UNK	1
-- 2012	FBS	UNK	4
-- 2012	FCS	UNK	4
-- 2011	FBS	UNK	4
-- 2011	FCS	UNK	4
-- 2010	FBS	UNK	4
-- 2010	FCS	UNK	4
-- 2009	FBS	UNK	4
-- 2009	FCS	UNK	4
-- 2008	FBS	UNK	4
-- 2008	FCS	UNK	4
-- 2007	FBS	UNK	4
-- 2007	FCS	UNK	4
-- 2006	FBS	UNK	4
-- 2006	FCS	UNK	4
-- 2005	FBS	UNK	4
-- 2005	FCS	UNK	4
-- 2004	FBS	UNK	4
-- 2004	FCS	UNK	4
-- 2003	FBS	UNK	4
-- 2003	FCS	UNK	4
-- 2002	FBS	UNK	2
-- 2002	FCS	UNK	2
-- 2001	FBS	UNK	2
-- 2001	FCS	UNK	2
-- 2000	FBS	UNK	1
-- 2000	FCS	UNK	1
-- 1999	FBS	UNK	1
-- 1999	FCS	UNK	1
-- 1998	FBS	UNK	1
-- 1997	FBS	UNK	1
-- 1996	FBS	UNK	1
-- 1995	FBS	UNK	1
-- 1994	FBS	UNK	1
-- 1993	FBS	UNK	1
-- 1992	FBS	UNK	1
-- 1991	FBS	UNK	1
-- 1990	FBS	UNK	1
-- 1988	FBS	UNK	1
-- 1981	FBS	UNK	1
-- 1980	FBS	UNK	1
-- 1979	FBS	UNK	1
-- 1977	FBS	UNK	1
-- 1975	FBS	UNK	1
-- 1971	FBS	UNK	1
-- 1968	FBS	UNK	1
-- 1954	FBS	UNK	1
-- 1949	FBS	UNK	1

