select
	C."Week" AS "Week",
	C."StartDateUtc" AS "Date",	
	CASE
        WHEN fAway."Slug" = @Slug THEN fHome."DisplayName"
        ELSE fAway."DisplayName"
    END AS "Opponent",
    CASE
        WHEN fAway."Slug" = @Slug THEN fHome."Slug"
        ELSE fAway."Slug"
    END AS "OpponentSlug",
    CASE
        WHEN fAway."Slug" = @Slug THEN 'Away'
        ELSE 'Home'
    END AS "LocationType",
	'NotSourced' as "Result",
	CASE
        WHEN fAway."Slug" = @Slug THEN false
        ELSE true
    END AS "WasWinner",
	V."Name" || ' [' || V."City" || ', ' || V."State" || ']' as "Location"
from public."Contest" C
inner join public."FranchiseSeason" fsAway on fsAway."Id" = c."AwayTeamFranchiseSeasonId"
inner join public."FranchiseSeason" fsHome on fsHome."Id" = c."HomeTeamFranchiseSeasonId"	
inner join public."Franchise" fAway on fAway."Id" = fsAway."FranchiseId"
inner join public."Franchise" fHome on fHome."Id" = fsHome."FranchiseId"
left join public."Venue" v on v."Id" = c."VenueId"
where (fAway."Slug" = @Slug OR fHome."Slug" = @Slug) and C."SeasonYear" = @SeasonYear
ORDER BY C."StartDateUtc"