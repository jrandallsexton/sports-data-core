
select * from public."Athlete" where "Id" = 'fd200ac9-01e6-c385-3ba6-ba5f74a941bb'
select * from public."AthleteExternalId" where "AthleteId" = '839ab5ca-a490-92d2-2e22-31478ff032b0'

select * from public."FranchiseLogo" where "FranchiseId" = 'd2ca25ce-337e-1913-b405-69a16329efe7'
select * from public."FranchiseSeason" where "GroupSeasonId" = '59e30a3c-b1cd-098e-cf76-9c4ac2441427'
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

select * from public."GroupSeason" where "Id" = '845eb718-b58a-bf9e-fa90-5de861c60325' -- SEC
select * from public."GroupSeason" where "Id" = 'bc72c270-1636-6248-17a1-9443be531c07' -- FBS (I-A)
select * from public."GroupSeason" where "Id" = '3437b85c-ba19-181e-af1e-c8b30a28dff6' -- NCAA Division I
select * from public."GroupSeason" where "Id" = 'acb492db-a8c1-71ed-3ffb-f9f1d2398195' -- NCAA Football

select * from public."GroupSeason" where "Slug" = 'fbs-i-a' and "SeasonYear" = 2025

select * from public."GroupSeason" where "ParentId" is null
select * from public."FranchiseSeason" where "GroupSeasonId" is null
select * from public."FranchiseSeasonRanking" order by "Date"
select * from public."FranchiseSeasonRanking" where "Type" = 'ap' order by "Date"
--update public."FranchiseSeasonRanking" set "SeasonWeekId" = '66277eb1-12cd-37cc-eb5d-950f10468f6d' where "ShortHeadline" = '2025 AP Poll: Week 10'
select * from public."FranchiseSeasonRankingDetail" where "FranchiseSeasonRankingId" = '654be351-4408-ebae-3b1b-c59cd4b6b39b'

select * from public."Season"
select * from public."SeasonPhase"
select * from public."SeasonWeek" order by "StartDate"

select con."Id", con."Name" from public."Contest" con
inner join public."Competition" comp on comp."ContestId" = con."Id"
where con."StartDateUtc" < now() and comp."Id" not in (select distinct "CompetitionId" from public."CompetitionPlay") and con."Name" like '%LSU%'
order by con."StartDateUtc"

select con."Id", con."Name", comp."Id" as "CompId" from public."Contest" con
inner join public."Competition" comp on comp."ContestId" = con."Id"
where con."Id" = '93401ef8-139a-00aa-76bb-320c1918aac9'
select * from public."CompetitionPlay" cp where cp."CompetitionId" = 'b710d758-8425-8997-219f-f819a8708925'

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

select count(*) from public."CompetitionProbability"
select count(*) from public."CompetitionSituation"
select count(*) from public."CompetitionPlay"
select count(*) from public."CompetitionDrive"

-- delete from public."CompetitionProbability"
-- delete from public."CompetitionSituation"
-- delete from public."CompetitionPlay"
-- delete from public."CompetitionDrive"

--update public."Contest" set "FinalizedUtc" = null, "SpreadWinnerFranchiseId" = null, "WinnerFranchiseId" = null, "OverUnder" = 0 where "Id" = '8a64dddf-0094-9a3a-2618-55c276296ef8'

-- FIX DEV - 07 OCT 2025
-- update public."Contest" set "FinalizedUtc" = null, "SpreadWinnerFranchiseId" = null, "WinnerFranchiseId" = null, "OverUnder" = 0 where "SeasonWeekId" = 'cda55a87-951b-0e56-f114-f0733280efda'

select * from public."Contest" where "Id" = '6ac9be3d-a870-6398-22bc-0c8262eb3b1c'
select * from public."Competition" where "ContestId" = '6ac9be3d-a870-6398-22bc-0c8262eb3b1c'
select * from public."CompetitionProbability" where "CompetitionId" = 'be17ed21-7431-fbd0-d88e-7827425a77ca' order by "SequenceNumber"::int

select * from public."CompetitionDrive" where "CompetitionId" = '8645e547-d083-6370-7836-bb328f70c346' order by "SequenceNumber"::int
select * from public."CompetitionPlay" where "CompetitionId" = '8645e547-d083-6370-7836-bb328f70c346' and "Type" = 8 order by "SequenceNumber"::int
select * from public."CompetitionMetric" where "CompetitionId" = '8645e547-d083-6370-7836-bb328f70c346'
select count(*) from public."CompetitionMetric"
--delete from public."CompetitionMetric"

SELECT json_agg(row_to_json(cm))
FROM (
    select * from public."CompetitionDrive" where "CompetitionId" = '8645e547-d083-6370-7836-bb328f70c346' order by "SequenceNumber"::int
) cm;

SELECT json_agg(row_to_json(cm))
FROM (
    select * from public."CompetitionPlay" where "CompetitionId" = '8645e547-d083-6370-7836-bb328f70c346' order by "SequenceNumber"::int
) cm;

SELECT json_agg(row_to_json(cm))
FROM (
    SELECT * FROM public."CompetitionMetric"
) cm;


select * from public."CompetitionBroadcast"
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
    where fssc."FranchiseSeasonId" = 'c13b7c74-6892-3efa-2492-36ebf5220464'
    order by "Category", "StatisticKey"

select * from public."CompetitionCompetitorScores" where "CompetitionCompetitorId" = 'ffebeea8-fa17-ec5a-4798-2836b05cf54f'
select * from public."CompetitionCompetitorScores" order by "CompetitionCompetitorId"
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

select f."Slug", fsm.* from public."FranchiseSeasonMetric" fsm
inner join public."FranchiseSeason" fs on fs."Id" = fsm."FranchiseSeasonId"
inner join public."Franchise" f on f."Id" = fs."FranchiseId"
where fsm."OppPointsPerDrive" > fsm."PointsPerDrive"
order by "PointsPerDrive"


