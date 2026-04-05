SELECT
  c."Id" AS "ContestId",
  c."AwayTeamFranchiseSeasonId" AS "AwayFranchiseSeasonId",
  c."HomeTeamFranchiseSeasonId" AS "HomeFranchiseSeasonId",
  c."SeasonWeekId",
  coo."Spread" AS "Spread",
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
    AND "ProviderId" IN ('{PreferredOddsProviderId}', '{FallbackOddsProviderId}')
  ORDER BY CASE WHEN "ProviderId" = '{PreferredOddsProviderId}' THEN 1 ELSE 2 END
  LIMIT 1
) coo ON TRUE
WHERE c."Id" = @ContestId
