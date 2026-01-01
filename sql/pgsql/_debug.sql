
select * from public."Athlete" where "Id" = 'fd200ac9-01e6-c385-3ba6-ba5f74a941bb'

select * from public."AthleteImage"
select count(*) from public."AthleteImage"

select f."Slug", ath.*  from public."AthleteImage" ai
inner join public."Athlete" ath on ath."Id" = ai."AthleteId"
inner join public."AthleteSeason" ats on ats."AthleteId" = ath."Id"
inner join public."FranchiseSeason" fs on fs."Id" = ats."FranchiseSeasonId"
inner JOIN public."Franchise" f on f."Id" = fs."FranchiseId"
where ath."IsActive" = true
order by f."Slug", ath."LastName", ath."FirstName"

select * from public."AthleteExternalId" where "AthleteId" = '839ab5ca-a490-92d2-2e22-31478ff032b0'
select * from public."AthleteSeason"
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

select * from public."FranchiseSeason" where "Id" = '68ca12c9-e48c-69a3-f788-6616e643cfb9'
select * from public."FranchiseSeason" where "Id" = '077e005c-74ff-4de5-e6b7-a1702e5ac0fe'

select * from public."FranchiseSeasonStatisticCategory"
where "FranchiseSeasonId" = 'c13b7c74-6892-3efa-2492-36ebf5220464'
order by "Name"

select * from public."FranchiseSeasonLogo" where "FranchiseSeasonId" = '06f4ca69-91d8-bbeb-cf41-8fc5440de97c'

select * from public."GroupSeason" where "Id" = '845eb718-b58a-bf9e-fa90-5de861c60325' -- SEC
select * from public."GroupSeason" where "Id" = 'bc72c270-1636-6248-17a1-9443be531c07' -- FBS (I-A)
select * from public."GroupSeason" where "Id" = '3437b85c-ba19-181e-af1e-c8b30a28dff6' -- NCAA Division I
select * from public."GroupSeason" where "Id" = 'acb492db-a8c1-71ed-3ffb-f9f1d2398195' -- NCAA Football

--update public."GroupSeason" set "Abbreviation" = 'fbs' where "Id" = 'bc72c270-1636-6248-17a1-9443be531c07'
update public."GroupSeason" set "Abbreviation" = 'fcs' where "Slug" = 'fcs-i-aa' and "Abbreviation" = 'UNK'
select * from public."GroupSeason" where "Abbreviation" = 'UNK'
select * from public."GroupSeason" where "SeasonYear" = 2024 order by "Name"
select * from public."GroupSeason" where "Slug" = 'fbs-i-a' and "SeasonYear" = 2025

select * from public."GroupSeason" where "ParentId" is null
select * from public."FranchiseSeason" where "SeasonYear" = 2025 order by "Slug"
select * from public."FranchiseSeasonRanking" order by "Date", "Name"
select * from public."FranchiseSeasonRanking" where "SeasonWeekId" = '5b8eb135-4b85-aa16-0d8d-49760c6b617b' order by "Date"
select * from public."FranchiseSeasonRanking" where "Type" = 'cfp' order by "Date"
select * from public."FranchiseSeasonRanking" where "ShortHeadline" = '2025 CFP Seedings: Week 16' order by "Date"

--update public."FranchiseSeasonRanking" set "DefaultRanking" = false where "SeasonWeekId" = '749b10f2-7d08-98fe-4bcb-58b9d9138e7f' and "Type" = 'ap' 

--update public."FranchiseSeasonRanking" set "SeasonWeekId" = '99105d46-d7d3-cd2d-380a-0e9302395a3c' where "ShortHeadline" = '2025 CFP Rankings: Week 16'
--update public."FranchiseSeasonRanking" set "SeasonWeekId" = '99105d46-d7d3-cd2d-380a-0e9302395a3c' where "ShortHeadline" = '2025 AP Poll: Week 16'
--update public."FranchiseSeasonRanking" set "SeasonWeekId" = '99105d46-d7d3-cd2d-380a-0e9302395a3c' where "ShortHeadline" = '2025 AFCA Coaches Poll: Week 16'
select * from public."FranchiseSeasonRankingDetail" where "FranchiseSeasonRankingId" = 'dbb70f8d-41ff-ab2b-3eac-a70fa97f9cbb'



select * from public."Season"
select * from public."SeasonPhase" order by "Year", "Slug"
select * from public."SeasonWeek" order by "StartDate"
--update public."SeasonWeek" set "EndDate" = '2025-12-14 07:59:00+00' where "Id" = '99105d46-d7d3-cd2d-380a-0e9302395a3c'
select sw.* from public."SeasonWeek" sw
inner join public."Season" s on s."Id" = sw."SeasonId"
where s."Year" = 2024 and sw."EndDate" < now() 
order by sw."StartDate"

--update public."SeasonWeek" set "IsNonStandardWeek" = false
-- update public."SeasonWeek" set "IsNonStandardWeek" = true
-- where "Number" > 14 or "SeasonPhaseId" != '467c9ac2-97ab-f5e3-6fa0-41b2eeca638b'

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

    select * from public."Contest" where "Id" = '4368b706-e7fe-7dc1-786a-c54f8eda67cd'

select * from public."CompetitionStream"
select * from public."Contest" where "Id" = '9c1dd681-8a67-91bb-1492-95742699410e'
select count(*) from public."Contest" where "SeasonYear" = 2024
select * from public."Contest" where "SeasonWeekId" = '947db3ad-0c7b-044b-2355-cabfffc6c1a7' order by "StartDateUtc"
select * from public."ContestExternalId" where "ContestId" = '59960665-7a2d-5c6e-d260-563132d4005b'
select * from public."Competition" where "ContestId" = 'c94e1efc-4809-bf27-d1e1-fb6d97cb16b7'
select * from public."CompetitionExternalId" where "CompetitionId" = '5e83718e-e1e4-2c73-804f-0c4a1f19f850'
select * from public."CompetitionCompetitor" where "CompetitionId" = 'ef5ffae3-2399-ef06-f8a0-c8f1f95e9f54'
select * from public."CompetitionNote" where "CompetitionId" = '7690d4e5-b401-4c6d-749d-0233f902994a'
select * from public."CompetitionStatus" where "CompetitionId" = 'cd68bd61-707f-90ad-26ae-f5f2ecd7d0cc'
select * from public."CompetitionCompetitorLineScore" where "CompetitionCompetitorId" = '8b8efe9a-1631-9e76-dc79-c4e560fa2f56' order by "Period"
--delete from public."CompetitionCompetitorLineScore" where "CompetitionCompetitorId" = '8b8efe9a-1631-9e76-dc79-c4e560fa2f56' and "SourceDescription" = 'Feed'
select * from public."CompetitionProbability" where "CompetitionId" = '6d6c0ebd-5912-271d-b478-3eb22fcc3a50' order by "SequenceNumber"
select * from public."CompetitionPlay" where "CompetitionId" = 'e48739ff-6394-193e-acff-46c5c178ae6a' order by "SequenceNumber"::int

select play."PeriodNumber", play."ClockDisplayValue", prob.* from public."CompetitionProbability" prob
left join public."CompetitionPlay" play on play."Id" = prob."PlayId"
where prob."CompetitionId" = 'e48739ff-6394-193e-acff-46c5c178ae6a' order by prob."SequenceNumber"::int

--delete from public."CompetitionProbability" where "CompetitionId" = '6d6c0ebd-5912-271d-b478-3eb22fcc3a50'
select * from public."CompetitionMedia"

select count(*) from public."CompetitionProbability"
select count(*) from public."CompetitionSituation"
select count(*) from public."CompetitionPlay"
select count(*) from public."CompetitionDrive"

-- delete from public."CompetitionProbability"
-- delete from public."CompetitionSituation"
-- delete from public."CompetitionPlay"
-- delete from public."CompetitionDrive"

--update public."Contest" set "FinalizedUtc" = null, "SpreadWinnerFranchiseId" = null, "WinnerFranchiseId" = null, "OverUnder" = 0 where "Id" = '24477be2-e202-7ce2-ef3b-4b71a9bc3b58'

-- FIX DEV - 07 OCT 2025
-- update public."Contest" set "FinalizedUtc" = null, "SpreadWinnerFranchiseId" = null, "WinnerFranchiseId" = null, "OverUnder" = 0 where "SeasonWeekId" = 'cda55a87-951b-0e56-f114-f0733280efda'

select * from public."Contest" where "Id" = '6ac9be3d-a870-6398-22bc-0c8262eb3b1c'
select * from public."Contest" where "SeasonWeekId" = '749b10f2-7d08-98fe-4bcb-58b9d9138e7f'
select * from public."Competition" where "ContestId" = '2508b4ac-a021-c3fe-f157-ca0694943621'
select * from public."CompetitionStatus" where "CompetitionId" = 'd9a6c35f-fea4-2dd2-3d6b-34f9cc65ba2d'
select * from public."CompetitionCompetitor" where "CompetitionId" = '94298b10-80c6-b1e3-a899-6534715ba956'
select * from public."CompetitionOdds" where "CompetitionId" = 'd9a6c35f-fea4-2dd2-3d6b-34f9cc65ba2d'

select * from public."CompetitionProbability" where "CompetitionId" = 'af18ebe5-033d-a056-84c5-8358f412685f' order by "SequenceNumber"::int

select cp.* from public."CompetitionProbability" cp
inner join public."CompetitionPlay" cpl on cpl."Id" = cp."PlayId"
where cp."CompetitionId" = 'af18ebe5-033d-a056-84c5-8358f412685f'
order by cpl."ClockValue"::int desc

select * from public."CompetitionDrive" where "CompetitionId" = '8645e547-d083-6370-7836-bb328f70c346' order by "SequenceNumber"::int
select * from public."CompetitionPlay" where "CompetitionId" = '8645e547-d083-6370-7836-bb328f70c346' and "Type" = 8 order by "SequenceNumber"::int
select * from public."CompetitionMetric" where "CompetitionId" = '8645e547-d083-6370-7836-bb328f70c346'
select count(*) from public."CompetitionMetric"
select * from public."FranchiseSeasonMetric"
select * from public."FranchiseSeasonRecord"
select * from public."GroupSeason" order by "Name"
--delete from public."CompetitionMetric"

-- SELECT 
--     column_name,
--     data_type,
--     is_nullable
-- FROM 
--     information_schema.columns
-- WHERE 
--     table_name = 'FranchiseSeasonMetric'
-- ORDER BY 
--     ordinal_position;

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
select count(*) from public."Venue" where "Latitude" = 0;
select * from public."VenueImage"
select * from public."Venue" where "Latitude" > 0 order by "Name"

select *
from public."GroupSeason" gs
inner join public."Season" s on s."Id" = gs."SeasonId"
where gs."Slug" = 'sec' and gs."SeasonYear" = 2025

select * from public."CompetitionPrediction"
select * from public."CompetitionPredictionValue" where "CompetitionPredictionId" = '9e911c06-90b0-4080-a164-925ee89f8531'
select * from public."PredictionMetric" order by "Name"
--delete from public."PredictionMetric"

select f."Slug", fsm.* from public."FranchiseSeasonMetric" fsm
inner join public."FranchiseSeason" fs on fs."Id" = fsm."FranchiseSeasonId"
inner join public."Franchise" f on f."Id" = fs."FranchiseId"
where fsm."OppPointsPerDrive" > fsm."PointsPerDrive"
order by "PointsPerDrive"


