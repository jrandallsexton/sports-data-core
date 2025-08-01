DO $$
DECLARE
  lsuFranchiseId uuid;
  lsuFranchiseSeasonId uuid;
  contestRecord RECORD;
BEGIN
  -- Get FranchiseId by Slug
  SELECT "Id" INTO lsuFranchiseId
  FROM public."Franchise"
  WHERE "Slug" = 'lsu-tigers';

  RAISE NOTICE 'LSU Franchise Id: %', lsuFranchiseId;

  -- Get FranchiseSeasonId for 2024
  SELECT "Id" INTO lsuFranchiseSeasonId
  FROM public."FranchiseSeason"
  WHERE "FranchiseId" = lsuFranchiseId AND "SeasonYear" = 2024;

  RAISE NOTICE 'LSU FranchiseSeasonId: %', lsuFranchiseSeasonId;

  -- Loop through filtered Contest rows with explicit columns
  FOR contestRecord IN (
    SELECT
      "Name",
      "ShortName",
      "StartDateUtc",
      "Status",
      "Clock",
      "DisplayClock",
      "Period",
      "Sport",
      "SeasonYear",
      "SeasonType",
      "Week",
      "VenueId"
    FROM public."Contest"
    WHERE "HomeTeamFranchiseSeasonId" = lsuFranchiseSeasonId
       OR "AwayTeamFranchiseSeasonId" = lsuFranchiseSeasonId
    ORDER BY "StartDateUtc"
  )
  LOOP
    RAISE NOTICE 'Contest: %, %, %, %, %, %, %, %, %, %, %, %',
      contestRecord."Name",
      contestRecord."ShortName",
      contestRecord."StartDateUtc",
      contestRecord."Status",
      contestRecord."Clock",
      contestRecord."DisplayClock",
      contestRecord."Period",
      contestRecord."Sport",
      contestRecord."SeasonYear",
      contestRecord."SeasonType",
      contestRecord."Week",
      contestRecord."VenueId";
  END LOOP;

END $$;



   
   select * from public."FranchiseSeason" where "FranchiseId" = 'd2ca25ce-337e-1913-b405-69a16329efe7'
   select * from public."FranchiseSeasonExternalId" where "FranchiseSeasonId" = 'd2ca25ce-337e-1913-b405-69a16329efe7'
   select * from public."FranchiseSeasonExternalId" where "SourceUrlHash" = 'bc2c92ab7abfa9f3ec15b451fe92b34f9c1963bcd047ee6786a51dfed1b97ed8'

   select * from public."Contest" where "EventNote" != null
   
   select * from public."FranchiseExternalId" where "FranchiseId" = 'ba491b1b-606d-5272-fdf4-461cf0cb1be8'
   select * from public."SeasonPhase" order by "Year"
   select * from public."SeasonYear" order by "Year"
   select * from public."Venue"
   
   select V."Name", V."City", V."State", C.* from public."Contest" C
   inner join public."Venue" V on V."Id" = C."VenueId"
   WHERE C."ShortName" LIKE '%LSU%' ORDER BY C."StartDateUtc"

   select * from public."Broadcast"
   select * from public."Competition"
   select * from public."lkPlayType"

   select * from public."Drive" D where D."Id" = 'a84b1a04-cd8c-92c2-db86-7f98fe8942f4'
   select * from public."DriveExternalId"
   
   select * from public."Drive" D
   inner join public."DriveExternalId" DE on DE."DriveId" = D."Id"
   where D."Id" = 'a84b1a04-cd8c-92c2-db86-7f98fe8942f4'
   
   select con."Name" as "Contest", V."Name", V."City", V."State", pt."Description", p.*
   from public."Play" p
   inner join public."lkPlayType" pt on pt."Id" = p."Type"
   inner join public."Competition" c on c."Id" = p."CompetitionId"
   inner join public."Contest" con on con."Id" = c."ContestId"
   inner join public."Venue" V on V."Id" = con."VenueId"
   order by p."SequenceNumber"
