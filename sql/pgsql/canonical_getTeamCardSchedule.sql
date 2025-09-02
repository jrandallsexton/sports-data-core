select
	C."Week" AS "Week",
	C."StartDateUtc" AS "Date",	
	CASE
        WHEN fAway."Slug" = 'south-carolina-gamecocks' THEN fHome."DisplayName"
        ELSE fAway."DisplayName"
    END AS "Opponent",
    CASE
        WHEN fAway."Slug" = 'south-carolina-gamecocks' THEN fHome."Slug"
        ELSE fAway."Slug"
    END AS "OpponentSlug",
    CASE
        WHEN fAway."Slug" = 'south-carolina-gamecocks' THEN 'Away'
        ELSE 'Home'
    END AS "LocationType",
	c."FinalizedUtc" as "FinalizedUtc",
	c."AwayScore" as "AwayScore",
	c."HomeScore" as "AwayScore",
	CASE
        WHEN fAway."Slug" = 'south-carolina-gamecocks' AND c."WinnerFranchiseId" = fsAway."Id" THEN true
		WHEN fHome."Slug" = 'south-carolina-gamecocks' AND c."WinnerFranchiseId" = fsHome."Id" THEN true
        ELSE null
    END AS "WasWinner",
	V."Name" || ' [' || V."City" || ', ' || V."State" || ']' as "Location"
from public."Contest" C
inner join public."FranchiseSeason" fsAway on fsAway."Id" = c."AwayTeamFranchiseSeasonId"
inner join public."FranchiseSeason" fsHome on fsHome."Id" = c."HomeTeamFranchiseSeasonId"	
inner join public."Franchise" fAway on fAway."Id" = fsAway."FranchiseId"
inner join public."Franchise" fHome on fHome."Id" = fsHome."FranchiseId"
left join public."Venue" v on v."Id" = c."VenueId"
where (fAway."Slug" = 'south-carolina-gamecocks' OR fHome."Slug" = 'south-carolina-gamecocks') and C."SeasonYear" = 2025
ORDER BY C."StartDateUtc"