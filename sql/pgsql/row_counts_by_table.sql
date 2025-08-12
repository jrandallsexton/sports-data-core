SELECT *
FROM public."Athlete"
WHERE "DoB" > '2000-01-01'
  AND "WeightLb" > 350
  AND "LastName" ~ '^[A-Za-z]+$'
  AND "FirstName" ~ '^[A-Za-z]+$';

 select * from public."AthletePosition" order by "Name"
 select * from public."AthletePositionExternalIds" where "AthletePositionId" = 'fadd0991-919d-4ab2-95a2-f0f1f205a25d'
  select * from public."AthletePositionExternalIds" where "SourceUrlHash" = '68bec6ae410c0b37bf0e4008de777012401b41cadf393b250c922fbdbed55313'
   select * from public."Franchise" order by "Slug"
   select * from public."Group" order by "Slug"
   select * from public."FranchiseLogo"
   select * from public."Franchise" where "Abbreviation" is null
   select * from public."Franchise" where "Slug" = 'lsu-tigers'
   select * from public."FranchiseSeason" where "FranchiseId" = '7520a598-6399-05ae-df21-386929c53e55'
   select * from public."FranchiseSeasonExternalId" where "FranchiseSeasonId" = '5a7ccba4-a844-ffd8-264b-5f5ba639983c'
   
   select * from public."FranchiseSeasonRanking"
   select * from public."FranchiseSeasonRankingDetail"

   select *
   from public."Franchise" f
   inner join public."FranchiseSeason" fson on fson."FranchiseId" = f."Id"
   inner join public."FranchiseSeasonRanking" fsr on fsr."FranchiseSeasonId" = fson."Id"
   inner join public."FranchiseSeasonRankingDetail" fsrd on fsrd."FranchiseSeasonRankingId" = fsr."Id"
   where fsr."Type" = 'ap'
   order by fsrd."Current"
   
   select * from public."FranchiseExternalId" where "FranchiseId" = 'ba491b1b-606d-5272-fdf4-461cf0cb1be8'
   select * from public."SeasonPhase" order by "Year"
   
   select * from public."SeasonWeek" sw
   inner join public."Season" s on s."Id" = sw."SeasonId"
   where s."Year" = 2025
   order by sw."StartDate" DESC
   
   select * from public."SeasonRanking"
   select * from public."SeasonRankingEntry"

   
   select sre.* from public."Season" s
   inner join public."SeasonPhase" sp on sp."SeasonId" = s."Id"
   inner join public."SeasonWeek" sw on sw."SeasonId" = s."Id" and sw."SeasonPhaseId" = sp."Id"
   inner join public."SeasonRanking" sr on sr."SeasonWeekId" = sw."Id"
   inner join public."SeasonRankingEntry" sre on sre."SeasonRankingId" = sr."Id"
   where s."Year" = 2024 and sp."Abbreviation" = 'reg' and sw."Number" = 2 and sr."ProviderPollId" = '1' and sre."Current" > 0
   order by sw."Number", sre."Current"
   
   select * from public."Venue" where "Name" = 'Tiger Stadium (LA)'
   --update public."Franchise" set "VenueId" = '8121cd60-3244-363b-623c-41cbbbec5972' where "Id" = 'd2ca25ce-337e-1913-b405-69a16329efe7'
   
   select V."Name", V."City", V."State", C.* from public."Contest" C
   inner join public."Venue" V on V."Id" = C."VenueId"
   WHERE C."ShortName" LIKE '%LSU%' ORDER BY C."StartDateUtc"

   select * from public."Contest" WHERE "ShortName" like '%LSU%'order by "StartDateUtc"
   select * from public."ContestExternalId" where "ContestId" = '8775fdbd-802a-1d25-735e-bbf702ac7e2d'
   select * from public."Competition" where "ContestId" = '8fac22f3-a8a4-773c-672b-d1c293f5d4a2'
select * from public."CompetitionCompetitor"
select * from public."CompetitionCompetitorScores"
select * from public."CompetitionStatus" where "CompetitionId" = 'f5cfd727-3b4a-f464-1ce1-8d2ffbc4e652'
select * from public."CompetitionLink" where "CompetitionId" = 'f5cfd727-3b4a-f464-1ce1-8d2ffbc4e652'
select * from public."CompetitionNote" --where "CompetitionId" = 'f5cfd727-3b4a-f464-1ce1-8d2ffbc4e652'

   select * from public."Competition" comp
   inner join public."CompetitionCompetitor" cc on cc."CompetitionId" = comp."Id"
   where comp."ContestId" = '8fac22f3-a8a4-773c-672b-d1c293f5d4a2'

select * from public."ContestOdds" where "ContestId" = '8fac22f3-a8a4-773c-672b-d1c293f5d4a2'
select * from public."ContestOdds" where "ContestId" = 'f5cfd727-3b4a-f464-1ce1-8d2ffbc4e652'
   
   select * from public."PowerIndex"
   select * from public."Play"
   select * from public."Competitor"
   select "ShortName", "Slug" from public."Group"
   
   select CON."Id" as "ConId", CON."Name" AS "Contest", CON."StartDateUtc", PI."DisplayName" AS "PowerIndex", CPI."Value", CPI."DisplayValue"
   from public."CompetitionPowerIndex" CPI
   inner join public."PowerIndex" PI on PI."Id" = CPI."PowerIndexId"
   inner join public."Competition" C on C."Id" = CPI."CompetitionId"
   inner join public."Contest" CON on CON."Id" = C."ContestId"
   where CPI."CompetitionId" = '3766bbff-bbdb-8529-e506-4945507c11ca'
   ORDER BY PI."DisplayName"

   select * from public."Play"
   where "CompetitionId" = '6fe167b3-01a4-ce7a-4caa-2d8ea922f983'
   order by "SequenceNumber"

	select * from public."CompetitionLeader"
	select * from public."CompetitionProbability"
	select * from public."lkLeaderCategory"
	select * from public."CompetitionLeaderStat"
select * from public."Competition" where "Id" = '268a0393-ee15-4a52-83af-3e52a7c01465'
   select * from public."Drive" where "CompetitionId" = 'b109f713-cddf-df99-529d-289d1b424f8d'
   select * from public."Competitor"
   select * from public."Group"
   select * from public."GroupExternalId"
   select * from public."GroupSeason"
   select * from public."Location" order by "State", "City"
   select * from public."lkPlayType"

   -- CompetitionLeaderStats
   select C."Id" AS "ContestId", C."Name" AS "ContestName", A1."LastName", A1."FirstName", *
   from public."CompetitionLeaderStat" CLS
   inner join public."CompetitionLeader" CL on CL."Id" = CLS."CompetitionLeaderId"
   inner join public."Competition" COMP on COMP."Id" = CL."CompetitionId"
   inner join public."Contest" C on C."Id" = COMP."ContestId"
   inner join public."Athlete" A1 on A1."Id" = CLS."AthleteId"
   WHERE C."Id" = '72a51437-f82b-2597-8842-fa5f6eaa9501'
   ORDER BY A1."LastName", A1."FirstName"
   
   select con."Name" as "Contest", V."Name", V."City", V."State", pt."Description", p.* from public."Play" p
   inner join public."lkPlayType" pt on pt."Id" = p."Type"
   inner join public."Competition" c on c."Id" = p."CompetitionId"
   inner join public."Contest" con on con."Id" = c."ContestId"
   inner join public."Venue" V on V."Id" = con."VenueId"
   order by p."SequenceNumber"

   -- Seasons with active phases
   select S."Id", S."Year", S."Name", S."StartDate", S."EndDate", SP."Name" AS "CurrentPhase", SP."EndDate" AS "PhaseEnd"
   from public."Season" S
   inner join public."SeasonPhase" SP on SP."Id" = S."ActivePhaseId"
   order by S."Year" DESC

   select * from public."CompetitionExternalId" where "SourceUrl" = 'http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334'
