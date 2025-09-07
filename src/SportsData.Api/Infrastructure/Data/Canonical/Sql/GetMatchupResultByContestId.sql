select
  c."Id" as "ContestId",
  c."AwayTeamFranchiseSeasonId" as "AwayFranchiseSeasonId",
  c."HomeTeamFranchiseSeasonId" as "HomeFranchiseSeasonId",
  c."SeasonWeekId",
  c."AwayScore",
  c."HomeScore",
  c."WinnerFranchiseId" as "WinnerFranchiseSeasonId",
  c."SpreadWinnerFranchiseId" as "SpreadWinnerFranchiseSeasonId",
  c."FinalizedUtc"
from public."Contest" c
where c."Id" = @ContestId