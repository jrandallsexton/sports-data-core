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
  order by fsl."Uri" -- or some other prioritization
  limit 1
) as fsl on true
inner join public."SeasonWeek" sw on sw."Id" = fsr."SeasonWeekId"
inner join public."Season" s on s."Id" = sw."SeasonId"
WHERE fsr."Type" = @PollType and sw."Number" = @WeekNumber and s."Year" = @SeasonYear
order by fsrd."Current" asc