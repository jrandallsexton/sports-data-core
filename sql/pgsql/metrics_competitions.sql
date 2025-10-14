--select * from public."SeasonWeek"

select *
from public."Contest" co
inner join public."Competition" c on c."ContestId" = co."Id"
where co."SeasonWeekId" = '5edb7b2b-d153-abc9-a965-c4c56a9bac04' 

select frs."Slug", gs."Slug"
from public."FranchiseSeason" frs
left join public."GroupSeason" gs on gs."Id" = frs."GroupSeasonId"
left join public."GroupSeason" gsParent on gsParent."Id" = gs."ParentId"
where gsParent."Slug" = 'fbs-i-a' or gsParent."Slug" = 'fbs-i-aa' or gsParent."Slug" = 'division-i'
order by frs."Slug"

select frs.*
from public."FranchiseSeason" frs
where frs."Slug" like '%app%'

select * from public."FranchiseSeason" fs where fs."GroupSeasonId" is null

select * from public."GroupSeason" where "Id" = '9777b61f-df13-4cf6-5bf7-e2eb69365526'
select * from public."GroupSeason" where "Id" = 'e15e5118-bde4-926e-4410-961cdef721bd'

select gsParent."Slug", gs.*
from public."GroupSeason" gs
inner join public."GroupSeason" gsParent on gsParent."Id" = gs."ParentId"

select distinct "Slug" from public."GroupSeason" order by "Slug"

-- :seasonYear is a parameter (optional if you don't version GroupSeason by year)
WITH RECURSIVE gs_tree AS (
  -- anchor: the FBS root for the season
  SELECT gs."Id", gs."ParentId", gs."Slug", gs."SeasonYear"
  FROM public."GroupSeason" gs
  WHERE gs."Slug" = 'fbs-i-a'
    AND gs."SeasonYear" = 2025

  UNION ALL

  -- recurse: include all descendants (conferences, divisions, sub-divisions)
  SELECT child."Id", child."ParentId", child."Slug", child."SeasonYear"
  FROM public."GroupSeason" child
  JOIN gs_tree parent ON child."ParentId" = parent."Id"
)
SELECT fs.*
FROM public."FranchiseSeason" fs
JOIN gs_tree g ON fs."GroupSeasonId" = g."Id"
WHERE fs."SeasonYear" = 2025;

-- want all competitions for Week 1 that involve teams in FBS
-- for each competition, i want to know if it is missing data in CompetitionPlay


-- :seasonYear := 2025
-- :weekNumber := 1

WITH RECURSIVE gs_tree AS (
  -- Anchor at FBS root for the season
  SELECT gs."Id", gs."ParentId", gs."Slug", gs."SeasonYear"
  FROM public."GroupSeason" gs
  WHERE gs."Slug" = 'fbs-i-a'
    AND gs."SeasonYear" = 2025

  UNION ALL

  -- Descend through conferences/divisions
  SELECT child."Id", child."ParentId", child."Slug", child."SeasonYear"
  FROM public."GroupSeason" child
  JOIN gs_tree parent ON child."ParentId" = parent."Id"
),
fbs_fs AS (
  -- FranchiseSeasons that roll up to FBS for the season
  SELECT fs."Id" AS "FranchiseSeasonId"
  FROM public."FranchiseSeason" fs
  JOIN gs_tree g ON fs."GroupSeasonId" = g."Id"
  WHERE fs."SeasonYear" = 2025
),
week_contests AS (
  -- Contests for the target week, joined to their competitions that include an FBS team
  SELECT DISTINCT
         c."Id"           AS "CompetitionId",
         ct."Id"          AS "ContestId",
         ct."SeasonYear",
         sw."Number"      AS "WeekNumber",
         ct."FinalizedUtc"      AS "FinalizedUtc"
  FROM public."Contest" ct
  JOIN public."SeasonWeek" sw       ON sw."Id" = ct."SeasonWeekId"
  JOIN public."Competition" c       ON c."ContestId" = ct."Id"
  JOIN public."CompetitionCompetitor" cc ON cc."CompetitionId" = c."Id"
  JOIN fbs_fs f                     ON f."FranchiseSeasonId" = cc."FranchiseSeasonId"
  WHERE ct."SeasonYear" = 2025
    AND sw."Number" = 1
    -- optional: if you only care about games that should have plays by now:
    -- AND ct."Status" IN ('post','final','completed','end')
)
SELECT wc."CompetitionId",
       wc."ContestId",
       wc."SeasonYear",
       wc."WeekNumber",
       wc."FinalizedUtc",
       COALESCE(p.play_count, 0) AS "PlayCount",
       (COALESCE(p.play_count, 0) = 0) AS "MissingPlayLog"
FROM week_contests wc
LEFT JOIN (
  SELECT "CompetitionId", COUNT(*) AS play_count
  FROM public."CompetitionPlay"
  GROUP BY "CompetitionId"
) p ON p."CompetitionId" = wc."CompetitionId"
ORDER BY wc."CompetitionId";

