select
  c."Id" as "ContestId",
  c."AwayTeamFranchiseSeasonId" as "AwayFranchiseSeasonId",
  c."HomeTeamFranchiseSeasonId" as "HomeFranchiseSeasonId",
  c."SeasonWeekId",
  coo."Spread" as "Spread",
  c."AwayScore",
  c."HomeScore",
  c."WinnerFranchiseId" as "WinnerFranchiseSeasonId",
  c."SpreadWinnerFranchiseId" as "SpreadWinnerFranchiseSeasonId",
  c."FinalizedUtc"
from public."Contest" c
inner join public."Competition" co on co."ContestId" = c."Id"

-- Use LATERAL join to prioritize ESPN (58) over DraftKings (100)
LEFT JOIN LATERAL (
  SELECT *
  FROM public."CompetitionOdds"
  WHERE "CompetitionId" = comp."Id" 
    AND "ProviderId" IN ('58', '100')
  ORDER BY CASE WHEN "ProviderId" = '58' THEN 1 ELSE 2 END
  LIMIT 1
) co ON TRUE

where c."Id" = @ContestId