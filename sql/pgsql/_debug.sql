
select * from public."FranchiseLogo" where "FranchiseId" = 'd2ca25ce-337e-1913-b405-69a16329efe7'
select * from public."FranchiseSeason" where "Slug" = 'texas-tech-red-raiders'
select * from public."AthletePosition"
/* Season Roster */
select
	ats."LastName", ats."FirstName", ats."DisplayName", ats."ShortName",
	ats."Slug", ap."Name" as "Position",
	ats."WeightLb", ats."WeightDisplay",
	ats."HeightIn", ats."HeightDisplay",
	ats."Jersey"
from public."AthleteSeason" ats
inner join public."AthletePosition" ap on ap."Id" = ats."PositionId"
where ats."FranchiseSeasonId" = 'c13b7c74-6892-3efa-2492-36ebf5220464' and
      ats."FirstName" != '-' and
	  ats."IsActive" = true
order by ap."Name", ats."LastName", ats."FirstName"

select * from public."FranchiseSeasonRecord"
select * from public."FranchiseSeason" where "FranchiseId" = 'd2ca25ce-337e-1913-b405-69a16329efe7'
select * from public."FranchiseSeasonLogo"
select * from public."GroupSeason" where "Id" = '7ff6fb28-dd1a-dd42-28d1-45a4a4bda516'
select * from public."FranchiseSeasonRanking" where "Type" = 'ap' order by "Date"
--update public."FranchiseSeasonRanking" set "SeasonWeekId" = 'e74fa119-0208-337e-0f96-0c64224d7d20' where "ShortHeadline" = '2025 AP Poll: Week 3'
select * from public."FranchiseSeasonRankingDetail" where "FranchiseSeasonRankingId" = '654be351-4408-ebae-3b1b-c59cd4b6b39b'

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
WHERE fsr."Type" = 'ap' and sw."Number" = 3 and s."Year" = 2025
order by fsrd."Current" asc

select * from public."Season"
select * from public."SeasonPhase"
select * from public."SeasonWeek" order by "StartDate"

select
  c."Id" as "ContestId",
  c."AwayTeamFranchiseSeasonId",
  c."HomeTeamFranchiseSeasonId",
  c."SeasonWeekId",
  c."AwayScore",
  c."HomeScore",
  c."WinnerFranchiseId" as "WinnerFranchiseSeasonId",
  c."SpreadWinnerFranchiseId" as "SpreadWinnerFranchiseSeasonId",
  c."FinalizedUtc"
from public."Contest" c
where c."Id" = 'f1444005-a1eb-6476-8a4a-dd6650be654e'

select * from public."Contest" where "Id" = 'f1444005-a1eb-6476-8a4a-dd6650be654e'
select * from public."Competition" where "ContestId" = 'f1444005-a1eb-6476-8a4a-dd6650be654e'
select * from public."CompetitionOdds" where "CompetitionId" = 'a9ea7891-6306-6fa4-f217-5a7e6f2162fc'

select * from public."Contest" where "SeasonWeekId" = 'd8d8db49-2692-56dc-ded8-f7606f5fc041' and "StartDateUtc" < '2025-09-07 01:13:00+00' order by "StartDateUtc"
select * from public."Contest" where "AwayTeamFranchiseSeasonId" = '8a37fae5-3901-b39e-5dc3-f69f0488d5fb' or "HomeTeamFranchiseSeasonId" = '8a37fae5-3901-b39e-5dc3-f69f0488d5fb' order by "StartDateUtc"
select * from public."Venue"

select *
from public."GroupSeason" gs
inner join public."Season" s on s."Id" = gs."SeasonId"
where gs."Slug" = 'sec' and gs."SeasonYear" = 2025