SELECT c."Id"
FROM public."Contest" c
INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = c."AwayTeamFranchiseSeasonId"
INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = c."HomeTeamFranchiseSeasonId"
WHERE c."SeasonWeekId" = @SeasonWeekId
  AND c."FinalizedUtc" IS NOT NULL
  AND (fsAway."GroupSeasonMap" LIKE '%fbs%' OR fsHome."GroupSeasonMap" LIKE '%fbs%')
ORDER BY c."Name";
