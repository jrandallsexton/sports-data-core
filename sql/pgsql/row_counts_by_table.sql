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

   select * from public."Contest"
   select * from public."ContestExternalId" where "ContestId" = '8775fdbd-802a-1d25-735e-bbf702ac7e2d'
   select * from public."Competition"
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
   where "CompetitionId" = '3766bbff-bbdb-8529-e506-4945507c11ca'
   order by "SequenceNumber"
   
   select * from public."Competitor"
   select * from public."lkPlayType"
   
   select con."Name" as "Contest", V."Name", V."City", V."State", pt."Description", p.* from public."Play" p
   inner join public."lkPlayType" pt on pt."Id" = p."Type"
   inner join public."Competition" c on c."Id" = p."CompetitionId"
   inner join public."Contest" con on con."Id" = c."ContestId"
   inner join public."Venue" V on V."Id" = con."VenueId"
   order by p."SequenceNumber"
