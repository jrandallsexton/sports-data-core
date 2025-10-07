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
inner join public."CompetitionOdds" coo on coo."CompetitionId" = co."Id" and coo."ProviderId" = '58'
where c."Id" = @ContestId