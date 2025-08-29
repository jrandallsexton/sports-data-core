select
  "Id",
  "AwayScore",
  "HomeScore",
  "WinnerFranchiseId" as "WinnerFranchiseSeasonId",
  "SpreadWinnerFranchiseId" as "SpreadWinnerFranchiseSeasonId",
  "FinalizedUtc"
from public."Contest" where "Id" = @ContestId