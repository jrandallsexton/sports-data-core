select * from public."Contest" Where "Id" = 'b458cf5b-ef56-9e93-48a7-3a7f169a9681'
select * from public."ContestExternalId" Where "ContestId" = 'b458cf5b-ef56-9e93-48a7-3a7f169a9681'

select * from public."Competition" where "ContestId" = 'b458cf5b-ef56-9e93-48a7-3a7f169a9681'
select * from public."CompetitionStatus" where "CompetitionId" = 'a2d36bbb-d8cc-4468-838d-10ed9287c4e8'

select distinct "StatusTypeName" from public."CompetitionStatus"

select * from public."CompetitionStream" where "CompetitionId" = 'fcdc3d62-9947-e9af-1525-d9dd9ccd8a14'

select * from public."AthletePosition"
select * from public."AthletePositionExternalId"

select * from public."Season" where "Year" = 2026
select * from public."SeasonWeek" where "SeasonId" = '4810b35f-8e31-2631-eba4-ac268341fc43' order by "Number"

SELECT
  c."Id" AS "ContestId",
  c."AwayTeamFranchiseSeasonId" AS "AwayFranchiseSeasonId",
  c."HomeTeamFranchiseSeasonId" AS "HomeFranchiseSeasonId",
  c."SeasonWeekId",
  co."Id" AS "CompetitionId",
  coo."Spread" AS "Spread",
  coo."ProviderName",
  c."AwayScore",
  c."HomeScore",
  c."WinnerFranchiseId" AS "WinnerFranchiseSeasonId",
  c."SpreadWinnerFranchiseId" AS "SpreadWinnerFranchiseSeasonId",
  c."FinalizedUtc"
FROM public."Contest" c
INNER JOIN public."Competition" co ON co."ContestId" = c."Id"
LEFT JOIN LATERAL (
  SELECT *
  FROM public."CompetitionOdds"
  WHERE "CompetitionId" = co."Id"
    AND "ProviderId" IN ('58', '100')
  ORDER BY CASE WHEN "ProviderId" = '{PreferredOddsProviderId}' THEN 1 ELSE 2 END
  LIMIT 1
) coo ON TRUE
WHERE c."Id" = 'b458cf5b-ef56-9e93-48a7-3a7f169a9681'

  SELECT * FROM public."CompetitionOdds" WHERE "CompetitionId" = 'f19b6d72-92de-18f7-3db4-4e1eb8c4f74c'
