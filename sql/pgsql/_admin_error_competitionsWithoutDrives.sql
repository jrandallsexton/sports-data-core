WITH RECURSIVE gs_tree AS (
  -- Anchor: root FBS group for 2025
  SELECT gs."Id", gs."ParentId", gs."Slug", gs."SeasonYear"
  FROM public."GroupSeason" gs
  WHERE gs."Slug" = 'fbs-i-a'
    AND gs."SeasonYear" = 2025

  UNION ALL

  -- Recurse into subgroups
  SELECT child."Id", child."ParentId", child."Slug", child."SeasonYear"
  FROM public."GroupSeason" child
  JOIN gs_tree parent ON child."ParentId" = parent."Id"
),
fbs_fs AS (
  -- FBS FranchiseSeasons for 2025
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
competitions_with_only_orphaned_plays AS (
  SELECT 
      con."Id" AS "ContestId",
      con."Name" AS "ContestName",
      con."StartDateUtc",
      comp."Id" AS "CompetitionId",
      COUNT(cp."Id") AS "PlayCount",
      COUNT(cp."DriveId") AS "PlaysWithDriveId",
      MAX(cp."Text") AS "LastPlayText"
  FROM public."Competition" comp
  JOIN public."Contest" con ON con."Id" = comp."ContestId"
  JOIN public."CompetitionPlay" cp ON cp."CompetitionId" = comp."Id"
  WHERE con."StartDateUtc" < now()
  GROUP BY con."Id", con."Name", con."StartDateUtc", comp."Id"
  HAVING COUNT(cp."Id") > 0 AND COUNT(cp."DriveId") = 0
)
-- Final filter: Only FBS-involved
SELECT c.*
FROM competitions_with_only_orphaned_plays c
JOIN fbs_competitions f ON c."CompetitionId" = f."CompetitionId"
ORDER BY c."StartDateUtc";
