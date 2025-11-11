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
WHERE fsr."Type" = 'ap' and sw."Number" = 12 and s."Year" = 2025
order by fsrd."Current" asc

--select * from public."FranchiseSeasonRanking" where "Type" = 'ap' order by "Date"
--update public."FranchiseSeasonRanking" set "SeasonWeekId" = '532897ff-daf6-f901-8c59-ddd5269d8d80' where "ShortHeadline" = '2025 AP Poll: Week 4'
--select * from public."FranchiseSeasonRankingDetail"
--select * from public."SeasonWeek" order by "StartDate" -- e74fa119-0208-337e-0f96-0c64224d7d20
-- '532897ff-daf6-f901-8c59-ddd5269d8d80' -- Week 4