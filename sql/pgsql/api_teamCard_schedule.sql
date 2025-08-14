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
  AND C."SeasonYear" = 2023
ORDER BY C."StartDateUtc";

select fs."SeasonYear"
from public."FranchiseSeason" fs
inner join public."Franchise" f on f."Id" = fs."FranchiseId"
where f."Slug" = 'lsu-tigers'
order by fs."SeasonYear" DESC

select cs.*
from public."Contest" c
inner join public."Competition" comp on comp."ContestId" = c."Id"
inner join public."CompetitionStatus" cs on cs."CompetitionId" = comp."Id"
where c."Id" = '38e65cdb-1d03-899c-4c43-e30049379f7f'

WITH target_franchise AS (
  SELECT f."Id" AS "FranchiseId"
  FROM public."Franchise" f
  WHERE f."Slug" = 'lsu-tigers'
),
available AS (
  SELECT array_agg(DISTINCT fs."SeasonYear" ORDER BY fs."SeasonYear" DESC) AS "AvailableSeasons"
  FROM public."FranchiseSeason" fs
  JOIN target_franchise tf ON tf."FranchiseId" = fs."FranchiseId"
)
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
    V."Name" || ' [' || V."City" || ', ' || V."State" || ']' AS "Location",
    av."AvailableSeasons"
FROM public."Contest" C
INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = C."AwayTeamFranchiseSeasonId"
INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = C."HomeTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fAway ON fAway."Id" = fsAway."FranchiseId"
INNER JOIN public."Franchise" fHome ON fHome."Id" = fsHome."FranchiseId"
LEFT JOIN public."Venue" V ON V."Id" = C."VenueId"
CROSS JOIN available av
WHERE (fAway."Slug" = 'lsu-tigers' OR fHome."Slug" = 'lsu-tigers')
  AND C."SeasonYear" = 2023
ORDER BY C."StartDateUtc";

