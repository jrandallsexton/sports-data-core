SELECT
	C."Id" AS "ContestId",
    C."Week" AS "Week",
    C."StartDateUtc" AS "Date",    
    CASE
        WHEN fAway."Slug" = 'lsu-tigers' THEN fHome."DisplayName"
        ELSE fAway."DisplayName"
    END AS "Opponent",
    CASE
        WHEN fAway."Slug" = 'lsu-tigers' THEN fHome."Slug"
        ELSE fAway."Slug"
    END AS "OpponentSlug",
    CASE
        WHEN fAway."Slug" = 'lsu-tigers' THEN 'Away'
        ELSE 'Home'
    END AS "LocationType",
    'NotSourced' AS "Result",
    CASE
        WHEN fAway."Slug" = 'lsu-tigers' THEN false
        ELSE true
    END AS "WasWinner",
    V."Name" || ' [' || V."City" || ', ' || V."State" || ']' AS "Location"
FROM public."Contest" C
INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = C."AwayTeamFranchiseSeasonId"
INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = C."HomeTeamFranchiseSeasonId"    
INNER JOIN public."Franchise" fAway ON fAway."Id" = fsAway."FranchiseId"
INNER JOIN public."Franchise" fHome ON fHome."Id" = fsHome."FranchiseId"
LEFT JOIN public."Venue" V ON V."Id" = C."VenueId"
WHERE (fAway."Slug" = 'lsu-tigers' OR fHome."Slug" = 'lsu-tigers')
  AND C."SeasonYear" = 2024
ORDER BY C."StartDateUtc";

select fs."SeasonYear"
from public."FranchiseSeason" fs
inner join public."Franchise" f on f."Id" = fs."FranchiseId"
where f."Slug" = 'lsu-tigers'
order by fs."SeasonYear" DESC

select *
from public."Contest" c
inner join public."Competition" comp on comp."ContestId" = c."Id"
--inner join public."CompetitionStatus" cs on cs."CompetitionId" = comp."Id"
where c."Id" = '38e65cdb-1d03-899c-4c43-e30049379f7f'

select cs.*
from public."Contest" c
inner join public."Competition" comp on comp."ContestId" = c."Id"
inner join public."CompetitionStatus" cs on cs."CompetitionId" = comp."Id"
where c."Id" = '38e65cdb-1d03-899c-4c43-e30049379f7f'

