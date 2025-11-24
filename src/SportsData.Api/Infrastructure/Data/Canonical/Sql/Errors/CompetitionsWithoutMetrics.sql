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
competitions_without_metrics AS (
  -- Competitions that should have metrics but don't
  SELECT 
      con."Id" AS "ContestId",
      con."Name" AS "ContestName",
      con."StartDateUtc",
      comp."Id" AS "CompetitionId",
      COUNT(cm."CompetitionId") AS "MetricCount"
  FROM public."Competition" comp
  JOIN public."Contest" con ON con."Id" = comp."ContestId"
  LEFT JOIN public."CompetitionMetric" cm ON cm."CompetitionId" = comp."Id"
  WHERE con."StartDateUtc" < now()
  GROUP BY con."Id", con."Name", con."StartDateUtc", comp."Id"
  HAVING COUNT(cm."CompetitionId") = 0
)
-- Final filtered list: missing metrics + contains at least one FBS team
SELECT c.*
FROM competitions_without_metrics c
JOIN fbs_competitions f ON c."CompetitionId" = f."CompetitionId"
ORDER BY c."StartDateUtc";
