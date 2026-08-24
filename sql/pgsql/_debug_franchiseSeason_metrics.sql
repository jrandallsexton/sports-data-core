select * from public."Franchise";

select * from public."FranchiseSeason" where "FranchiseId" = '64a1c51c-41c0-25a8-140c-256e5c307fa9' order by "SeasonYear" desc;

-- 819e11f7-daca-d405-cc29-b4ed67b02543 Miami 2025
-- dc8b24e0-2912-6fdb-93a0-a7524c46176f Tampa 2025

select * from public."CompetitionMetric" cm
where cm."FranchiseSeasonId" = '819e11f7-daca-d405-cc29-b4ed67b02543';

select * from public."FranchiseSeasonMetric"

select * from public."FranchiseSeasonStatisticCategory" fssc
inner join public."FranchiseSeasonStatistic" fss on fss."FranchiseSeasonStatisticCategoryId" = fssc."Id"
where fssc."FranchiseSeasonId" = '819e11f7-daca-d405-cc29-b4ed67b02543';

-- b415e367-c65e-121f-619a-f9314264bbca ContestId: Tampa @ Miami 28 Dec 2025
select * from public."Contest" c where c."Id" = 'b415e367-c65e-121f-619a-f9314264bbca';
select * from public."Competition" where "ContestId" = 'b415e367-c65e-121f-619a-f9314264bbca';

-- e7f96e51-a438-b5d7-ddee-4bc571f64d69 CompetitionId: Tampa @ Miami 28 Dec 2025
select * from public."CompetitionCompetitor" where "CompetitionId" = 'e7f96e51-a438-b5d7-ddee-4bc571f64d69';
-- 4863967e-b0d1-429b-1188-d6180661d66c CompetitionCompetitorId: Tampa
-- 3efa09c1-ebe3-8c42-bed7-e6622d2ad8c7 CompetitionCompetitorId: Miami

select * from public."CompetitionCompetitorStatistics" where "CompetitionId" = 'e7f96e51-a438-b5d7-ddee-4bc571f64d69';

select * from public."CompetitionCompetitorStatisticCategories" ccsc
inner join public."CompetitionCompetitorStatisticStats" ccss on ccss."CompetitionCompetitorStatisticCategoryId" = ccsc."Id"
where ccsc."CompetitionCompetitorStatisticId" = '6da64439-9804-80ef-c451-21146ce5326b'
order by ccss."Abbreviation";

select * from public."CompetitionCompetitorStatisticStats" limit 5;

select * from public."CompetitionCompetitorStatisticCategories"
where "CompetitionCompetitorStatisticId" = '3b757a00-84dd-10e9-10b0-d3f201fcc580';

-- NFL Per-season coverage: how much of the backfill corpus actually exists?
SELECT
    c."SeasonYear",
    COUNT(DISTINCT c."Id")                                            AS contests,
    COUNT(DISTINCT c."Id") FILTER (WHERE c."SeasonWeekId" IS NOT NULL) AS with_week_mapping,
    COUNT(DISTINCT comp."Id")                                         AS competitions,
    COUNT(DISTINCT p."CompetitionId")                                  AS with_plays,
    COUNT(DISTINCT cm."CompetitionId")                                 AS with_metrics
FROM "Contest" c
JOIN "Competition" comp ON comp."ContestId" = c."Id"
LEFT JOIN LATERAL (
    SELECT fp."CompetitionId" FROM "CompetitionPlay" fp
    WHERE fp."CompetitionId" = comp."Id" LIMIT 1
) p ON TRUE
LEFT JOIN LATERAL (
    SELECT m."CompetitionId" FROM "CompetitionMetric" m
    WHERE m."CompetitionId" = comp."Id" LIMIT 1
) cm ON TRUE
GROUP BY c."SeasonYear"
ORDER BY c."SeasonYear";

-- NCAAFB >= 1 FBS School: Per-season coverage: how much of the backfill corpus actually exists?
SELECT
    c."SeasonYear",
    COUNT(DISTINCT c."Id")                                            AS contests,
    COUNT(DISTINCT c."Id") FILTER (WHERE c."SeasonWeekId" IS NOT NULL) AS with_week_mapping,
    COUNT(DISTINCT comp."Id")                                         AS competitions,
    COUNT(DISTINCT p."CompetitionId")                                  AS with_plays,
    COUNT(DISTINCT cm."CompetitionId")                                 AS with_metrics
FROM "Contest" c
JOIN "Competition" comp ON comp."ContestId" = c."Id"
JOIN "FranchiseSeason" fsA ON fsA."Id" = c."AwayTeamFranchiseSeasonId"
JOIN "FranchiseSeason" fsH ON fsH."Id" = c."HomeTeamFranchiseSeasonId"
LEFT JOIN LATERAL (
    SELECT fp."CompetitionId" FROM "CompetitionPlay" fp
    WHERE fp."CompetitionId" = comp."Id" LIMIT 1
) p ON TRUE
LEFT JOIN LATERAL (
    SELECT m."CompetitionId" FROM "CompetitionMetric" m
    WHERE m."CompetitionId" = comp."Id" LIMIT 1
) cm ON TRUE
WHERE fsA."GroupSeasonMap" LIKE '%fbs%' OR fsH."GroupSeasonMap" LIKE '%fbs%'
GROUP BY c."SeasonYear"
ORDER BY c."SeasonYear";

-- SeasonYear	contests	with_week_mapping	competitions	with_plays	with_metrics
-- 1999	697	697	697	0	697
-- 2000	711	711	711	0	711
-- 2001	753	753	753	451	753
-- 2002	762	762	762	31	762
-- 2003	759	759	759	481	759
-- 2004	700	700	700	462	700
-- 2005	688	688	688	590	688
-- 2006	748	748	748	675	748
-- 2007	754	754	754	667	754
-- 2008	765	765	765	736	765
-- 2009	766	766	766	750	766
-- 2010	205	205	205	202	205
-- 2011	780	780	780	771	780
-- 2012	819	819	819	796	819
-- 2013	858	858	858	855	858
-- 2014	873	873	873	854	873
-- 2015	873	873	873	863	873
-- 2016	751	751	751	709	751
-- 2017	856	856	856	821	856
-- 2018	895	895	895	874	895
-- 2019	889	889	889	887	889
-- 2020	691	691	691	564	691
-- 2021	893	893	893	839	893
-- 2022	900	900	900	857	900
-- 2023	910	910	910	902	910
-- 2024	920	920	920	901	920
-- 2025	934	934	934	933	934
-- 2026	783	783	783	0	0