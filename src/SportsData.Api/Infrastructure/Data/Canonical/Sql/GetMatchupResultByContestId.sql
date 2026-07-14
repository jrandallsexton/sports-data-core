select
  c."Id" as "ContestId",
  c."AwayTeamFranchiseSeasonId" as "AwayFranchiseSeasonId",
  c."HomeTeamFranchiseSeasonId" as "HomeFranchiseSeasonId",
  c."SeasonWeekId",
  coo."Spread" as "Spread",
  c."AwayScore",
  c."HomeScore",
  c."WinnerFranchiseSeasonId",
  c."SpreadWinnerFranchiseSeasonId",
  c."FinalizedUtc",
  afs."Abbreviation" as "AwayAbbreviation",
  hfs."Abbreviation" as "HomeAbbreviation"
from public."Contest" c
inner join public."Competition" co on co."ContestId" = c."Id"
-- Team abbreviations for the notification copy. Picked-side is resolved in code
-- against Away/HomeFranchiseSeasonId above.
left join public."FranchiseSeason" afs on afs."Id" = c."AwayTeamFranchiseSeasonId"
left join public."FranchiseSeason" hfs on hfs."Id" = c."HomeTeamFranchiseSeasonId"

-- Use LATERAL join to prioritize ESPN (58) over DraftKings (100)
LEFT JOIN LATERAL (
  SELECT *
  FROM public."CompetitionOdds"
  WHERE "CompetitionId" = co."Id" 
    AND "ProviderId" IN ('58', '100')
  ORDER BY CASE WHEN "ProviderId" = '58' THEN 1 ELSE 2 END
  LIMIT 1
) coo ON TRUE

where c."Id" = @ContestId
  -- Scoring callers cannot tolerate pre-enrichment rows. WinnerFranchiseSeasonId,
  -- SpreadWinnerFranchiseSeasonId, and the final HomeScore/AwayScore are all
  -- populated atomically by ContestEnrichmentProcessor alongside
  -- FinalizedUtc. Returning a row before that point produced silent
  -- Guid.Empty/0-0 scoring (PickScoringProcessor / PickScoringService).
  and c."FinalizedUtc" is not null