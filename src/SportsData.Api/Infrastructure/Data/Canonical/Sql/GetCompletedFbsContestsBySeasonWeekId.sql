select c."Id"
from public."Contest" c
inner join public."FranchiseSeason" fsAway on fsAway."Id" = c."AwayTeamFranchiseSeasonId"
inner join public."FranchiseSeason" fsHome on fsHome."Id" = c."HomeTeamFranchiseSeasonId"
where
    c."SeasonWeekId" = @SeasonWeekId and
    (fsAway."GroupSeasonMap" like '%fbs%' or fsHome."GroupSeasonMap" like '%fbs%') AND
    c."FinalizedUtc" is not null
order by c."Name"