
select * from public."FranchiseLogo" where "FranchiseId" = 'd2ca25ce-337e-1913-b405-69a16329efe7'
select * from public."FranchiseSeason" where "Slug" = 'texas-tech-red-raiders'
select * from public."Franchise"
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
select * from public."Franchise"
select * from public."FranchiseExternalId"
select * from public."FranchiseSeason" where "Slug" = 'lsu-tigers'

select * from public."FranchiseSeasonStatisticCategory"
where "FranchiseSeasonId" = 'c13b7c74-6892-3efa-2492-36ebf5220464'
order by "Name"

select * from public."FranchiseSeasonLogo" where "FranchiseSeasonId" = '06f4ca69-91d8-bbeb-cf41-8fc5440de97c'
select * from public."GroupSeason" where "Id" = '7ff6fb28-dd1a-dd42-28d1-45a4a4bda516'
select * from public."FranchiseSeasonRanking" order by "Date"
select * from public."FranchiseSeasonRanking" where "Type" = 'ap' order by "Date"
--update public."FranchiseSeasonRanking" set "SeasonWeekId" = '11c6a599-b698-3d3d-8283-74e0c604555c' where "ShortHeadline" = '2025 AP Poll: Week 7'
select * from public."FranchiseSeasonRankingDetail" where "FranchiseSeasonRankingId" = '654be351-4408-ebae-3b1b-c59cd4b6b39b'

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
where c."Id" = '8a64dddf-0094-9a3a-2618-55c276296ef8'
-- https://api-dev.sportdeets.com/ui/matchup/8a64dddf-0094-9a3a-2618-55c276296ef8/preview

select * from public."Contest" where "Id" = '8a64dddf-0094-9a3a-2618-55c276296ef8'
select * from public."Contest" where "Id" = '8a64dddf-0094-9a3a-2618-55c276296ef8'
--update public."Contest" set "FinalizedUtc" = null, "SpreadWinnerFranchiseId" = null, "WinnerFranchiseId" = null, "OverUnder" = 0 where "Id" = '8a64dddf-0094-9a3a-2618-55c276296ef8'

-- FIX DEV - 07 OCT 2025
-- update public."Contest" set "FinalizedUtc" = null, "SpreadWinnerFranchiseId" = null, "WinnerFranchiseId" = null, "OverUnder" = 0 where "SeasonWeekId" = 'cda55a87-951b-0e56-f114-f0733280efda'

select * from public."Contest" where "HomeTeamFranchiseSeasonId" = 'c13b7c74-6892-3efa-2492-36ebf5220464' order by "StartDateUtc"
select * from public."Competition" where "ContestId" = '8a64dddf-0094-9a3a-2618-55c276296ef8'
select * from public."CompetitionProbability" where "CompetitionId" = '65c4132d-4ee5-8418-470e-cb96b63a7b8e' order by "SequenceNumber"

select * from public."CompetitionCompetitorStatistics"
where "FranchiseSeasonId" = 'c13b7c74-6892-3efa-2492-36ebf5220464'

select * from public."CompetitionCompetitorStatisticCategories"
where "CompetitionCompetitorStatisticId" = '78ea9a07-9bfb-0397-caeb-be0c26d1ee50'
order by "Name"

    SELECT fssc."Name" AS "Category",
        fss."Name" AS "StatisticKey",
        fss."Name" AS "StatisticValue",
        fss."DisplayValue",
        fss."PerGameValue",
        fss."PerGameDisplayValue",
        fss."Rank"
    from public."FranchiseSeasonStatisticCategory" fssc
    inner join public."FranchiseSeasonStatistic" fss on fss."FranchiseSeasonStatisticCategoryId" = fssc."Id"
    where fssc."FranchiseSeasonId" = '2bb15a4f-652a-1a5f-3060-dcc3355bac93'
    order by "Category", "StatisticKey"

select * from public."CompetitionCompetitorStatisticStats" where "CompetitionCompetitorStatisticCategoryId" = '0c5d0ca5-802c-4087-9bcf-75b85e90383a' order by "Name"

select * from public."CompetitionOdds" where "CompetitionId" = '65c4132d-4ee5-8418-470e-cb96b63a7b8e'
select * from public."CompetitionTeamOdds" where "CompetitionOddsId" = '3d893e38-5b96-f677-833e-fc1bd5f23e64'
select * from public."CompetitionLeader"
select * from public."lkLeaderCategory" order by "Name"
select * from public."CompetitionLeaderStat"
select * from public."CompetitionStatus" where "CompetitionId" = 'd0c23fa1-ad50-c95b-5110-fcff717fde78'
select * from public."CompetitionStatus" where "StatusDescription" = 'In Progress'
select * from public."Contest" where "SeasonWeekId" = 'd8d8db49-2692-56dc-ded8-f7606f5fc041' and "StartDateUtc" < '2025-09-07 01:13:00+00' order by "StartDateUtc"
select * from public."Contest" where "AwayTeamFranchiseSeasonId" = '8a37fae5-3901-b39e-5dc3-f69f0488d5fb' or "HomeTeamFranchiseSeasonId" = '8a37fae5-3901-b39e-5dc3-f69f0488d5fb' order by "StartDateUtc"
select * from public."Venue"

select *
from public."GroupSeason" gs
inner join public."Season" s on s."Id" = gs."SeasonId"
where gs."Slug" = 'sec' and gs."SeasonYear" = 2025

select * from public."CompetitionPrediction"
select * from public."CompetitionPredictionValue"
select * from public."PredictionMetric" order by "Name"
--delete from public."PredictionMetric"