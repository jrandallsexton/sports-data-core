select
	fs."Id" as "FranchiseSeasonId",
	fsl."Uri" as "FranchiseLogoUrl",
	fs."Slug" as "FranchiseSlug",
	fs."DisplayNameShort" as "FranchiseName",
	fs."Wins",
	fs."Losses",
	fsrd."Current" as "Rank",
	fsrd."Previous" as "PreviousRank",
	fsrd."Points",
	fsrd."FirstPlaceVotes",
	fsrd."Trend",
	fsrd."Date" as "PollDateUtc"
from public."FranchiseSeasonRankingDetail" fsrd
inner join public."FranchiseSeasonRanking" fsr on fsr."Id" = fsrd."FranchiseSeasonRankingId"
inner join public."FranchiseSeason" fs on fs."Id" = fsr."FranchiseSeasonId"
left join lateral (
  select fsl."Uri"
  from public."FranchiseSeasonLogo" fsl
  where fsl."FranchiseSeasonId" = fs."Id"
  order by fsl."Uri"
  limit 1
) as fsl on true
inner join public."SeasonWeek" sw on sw."Id" = fsr."SeasonWeekId"
WHERE fsr."Type" = 'ap' and sw."Id" = '5edb7b2b-d153-abc9-a965-c4c56a9bac04' -- Week1
order by fsrd."Current" asc