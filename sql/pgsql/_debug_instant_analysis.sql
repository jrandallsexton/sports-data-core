select * from public."Contest" where "Id" = '98035135-931b-34a2-1f9e-be42e1647453';

select * from public."Competition" where "ContestId" = '98035135-931b-34a2-1f9e-be42e1647453';

select * from public."CompetitionStream" where "CompetitionId" = '6f3ebe93-0d6e-144e-6aec-8e6bf42180d4';

select * from public."CompetitionStream" where "ScheduledTimeUtc" > now();

select * from public."CompetitionPlay" where "CompetitionId" = '6f3ebe93-0d6e-144e-6aec-8e6bf42180d4' order by "SequenceNumber"::bigint;

select * from public."CompetitionPlay" where "CompetitionId" = '6f3ebe93-0d6e-144e-6aec-8e6bf42180d4' and "TypeId" = '26';

select * from public."PlayAnalysis" order by "CreatedUtc";
select * from public."PlayAnalysisRating" order by "CreatedUtc";
select * from public."PlayAnalysisRun" order by "CreatedUtc" desc;

-- delete from public."PlayAnalysis";
-- delete from public."PlayAnalysisRating";
-- delete from public."PlayAnalysisRun";


select * from public."CompetitionPlayParticipant" where "CompetitionPlayId" = 'bb0fb521-bf2f-08f5-afb0-97693bed4a07'; -- passer, passDefender, returner, tackler (interception play)

select * from public."AthleteSeason" where "Id" = 'b73973f2-60d4-9cdc-2801-71d8e81a6b4e'; -- Tamarcus Cooley (S) this was his second INT of the season

select * from public."AthleteSeasonStatistic" where "AthleteSeasonId" = 'b73973f2-60d4-9cdc-2801-71d8e81a6b4e';

select * from public."AthleteSeasonStatisticCategory" where "AthleteSeasonStatisticId" = '8e2dbab4-a411-3530-0957-b25b3582e0e9';

select * from public."AthleteSeasonStatisticStat" where "AthleteSeasonStatisticCategoryId" = '4cb185d6-a1b2-49f8-81c6-9c870b23d8e9';

-- Play arrives. Analyze. Sack? Int? Run? Pass? Key play? Run > 10 yards? Pass > 10 yards?
-- Who did it? Have they done this previously? If so, how many times?
-- Are they approaching a season or career milestone?