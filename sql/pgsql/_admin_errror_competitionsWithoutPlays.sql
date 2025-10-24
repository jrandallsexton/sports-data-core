-- SELECT 
--     con."Id" AS "ContestId",
--     con."Name" AS "ContestName",
--     con."StartDateUtc",
--     comp."Id" AS "CompetitionId",
--     COUNT(cp."Id") AS "PlayCount",
--     MAX(cp."Text") AS "LastPlayText"
-- FROM public."Competition" comp
-- JOIN public."Contest" con ON con."Id" = comp."ContestId"
-- LEFT JOIN public."CompetitionPlay" cp ON cp."CompetitionId" = comp."Id"
-- WHERE con."StartDateUtc" < now()  -- ⏰ Only games that should have started
-- GROUP BY con."Id", con."Name", con."StartDateUtc", comp."Id"
-- HAVING COUNT(cp."Id") <= 10
-- ORDER BY con."StartDateUtc";



WITH RECURSIVE gs_tree AS (
  -- Anchor at FBS root group for 2025
  SELECT gs."Id", gs."ParentId", gs."Slug", gs."SeasonYear"
  FROM public."GroupSeason" gs
  WHERE gs."Slug" = 'fbs-i-a'
    AND gs."SeasonYear" = 2025

  UNION ALL

  -- Traverse into subgroups (ACC-East, etc.)
  SELECT child."Id", child."ParentId", child."Slug", child."SeasonYear"
  FROM public."GroupSeason" child
  JOIN gs_tree parent ON child."ParentId" = parent."Id"
),
fbs_fs AS (
  -- FBS FranchiseSeason IDs for 2025
  SELECT fs."Id" AS "FranchiseSeasonId"
  FROM public."FranchiseSeason" fs
  JOIN gs_tree g ON fs."GroupSeasonId" = g."Id"
  WHERE fs."SeasonYear" = 2025
),
fbs_competitions AS (
  -- Competitions with at least one FBS team
  SELECT DISTINCT cc."CompetitionId"
  FROM public."CompetitionCompetitor" cc
  JOIN fbs_fs fbs ON cc."FranchiseSeasonId" = fbs."FranchiseSeasonId"
),
suspicious_playlogs AS (
  -- Competitions that should have play data but have ≤ 10 plays
  SELECT 
      con."Id" AS "ContestId",
      con."Name" AS "ContestName",
      con."StartDateUtc",
      comp."Id" AS "CompetitionId",
      COUNT(cp."Id") AS "PlayCount",
      MAX(cp."Text") AS "LastPlayText"
  FROM public."Competition" comp
  JOIN public."Contest" con ON con."Id" = comp."ContestId"
  LEFT JOIN public."CompetitionPlay" cp ON cp."CompetitionId" = comp."Id"
  WHERE con."StartDateUtc" < now()
  GROUP BY con."Id", con."Name", con."StartDateUtc", comp."Id"
  HAVING COUNT(cp."Id") <= 10
)
-- Final filtered list: suspicious + contains at least one FBS team
SELECT s.*
FROM suspicious_playlogs s
JOIN fbs_competitions f ON s."CompetitionId" = f."CompetitionId"
ORDER BY s."StartDateUtc";
