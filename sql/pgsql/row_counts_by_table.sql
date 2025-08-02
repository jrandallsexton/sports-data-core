SELECT *
FROM public."Athlete"
WHERE "DoB" > '2000-01-01'
  AND "WeightLb" > 350
  AND "LastName" ~ '^[A-Za-z]+$'
  AND "FirstName" ~ '^[A-Za-z]+$';

 select * from public."AthletePosition" order by "Name"
 select * from public."AthletePositionExternalIds" where "AthletePositionId" = 'fadd0991-919d-4ab2-95a2-f0f1f205a25d'
  select * from public."AthletePositionExternalIds" where "SourceUrlHash" = '68bec6ae410c0b37bf0e4008de777012401b41cadf393b250c922fbdbed55313'
   select * from public."Franchise" order by "Name"
   select * from public."Franchise" where "Abbreviation" is null
   select * from public."Franchise" where "Slug" = 'ohio-dominican-panthers'
   select * from public."FranchiseSeason" where "FranchiseId" = '7520a598-6399-05ae-df21-386929c53e55'
   select * from public."FranchiseSeasonExternalId" where "FranchiseSeasonId" = '5a7ccba4-a844-ffd8-264b-5f5ba639983c'
   select * from public."FranchiseExternalId" where "FranchiseId" = 'ba491b1b-606d-5272-fdf4-461cf0cb1be8'
   select * from public."SeasonPhase" order by "Year"
   select * from public."SeasonYear" order by "Year"
   select * from public."Venue"
   
   select V."Name", V."City", V."State", C.* from public."Contest" C
   inner join public."Venue" V on V."Id" = C."VenueId"
   WHERE C."ShortName" LIKE '%LSU%' ORDER BY C."StartDateUtc"

   select * from public."Contest" WHERE "ShortName" like '%LSU%'order by "StartDateUtc"
   select * from public."ContestExternalId" where "ContestId" = '8775fdbd-802a-1d25-735e-bbf702ac7e2d'
   select * from public."Competition" where "ContestId" = '38e65cdb-1d03-899c-4c43-e30049379f7f'
   select * from public."PowerIndex"
   select * from public."Play"
   
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

   select S."Id", S."Year", S."Name", S."StartDate", S."EndDate", SP."Name" AS "CurrentPhase", SP."EndDate" AS "PhaseEnd"
   from public."Season" S
   inner join public."SeasonPhase" SP on SP."Id" = S."ActivePhaseId"
   order by S."Year" DESC
