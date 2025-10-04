select ccls."Period", ccls."Value", cc."HomeAway", c."AwayTeamFranchiseSeasonId", c."HomeTeamFranchiseSeasonId"
from public."CompetitionCompetitorLineScore" ccls
inner join public."CompetitionCompetitor" cc on cc."Id" = ccls."CompetitionCompetitorId"
inner join public."Competition" co on co."Id" = cc."CompetitionId"
inner join public."Contest" c on c."Id" = co."ContestId"
where co."ContestId" = '8fac22f3-a8a4-773c-672b-d1c293f5d4a2'
order by ccls."Period", cc."HomeAway"