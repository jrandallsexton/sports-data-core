select
    C."Id" AS "ContestId",
	sw."Number" AS "Week",
	C."StartDateUtc" AS "Date",	
	CASE
        WHEN fAway."Slug" = @Slug THEN fHome."DisplayName"
        ELSE fAway."DisplayName"
    END AS "Opponent",
    	CASE
        WHEN fAway."Slug" = @Slug THEN fHome."DisplayNameShort"
        ELSE fAway."DisplayNameShort"
    END AS "OpponentShortName",
    CASE
        WHEN fAway."Slug" = @Slug THEN fHome."Slug"
        ELSE fAway."Slug"
    END AS "OpponentSlug",
    V."Name" || ' [' || V."City" || ', ' || V."State" || ']' as "Location",
    CASE
        WHEN fAway."Slug" = @Slug THEN 'Away'
        ELSE 'Home'
    END AS "LocationType",
	c."FinalizedUtc" as "FinalizedUtc",
	c."AwayScore" as "AwayScore",
	c."HomeScore" as "HomeScore",
	CASE
        WHEN fAway."Slug" = @Slug AND c."WinnerFranchiseId" = fsAway."Id" THEN true
		WHEN fHome."Slug" = @Slug AND c."WinnerFranchiseId" = fsHome."Id" THEN true
        ELSE null
    END AS "WasWinner"

from public."Contest" C
inner join public."SeasonWeek" SW on SW."Id" = C."SeasonWeekId"
inner join public."FranchiseSeason" fsAway on fsAway."Id" = c."AwayTeamFranchiseSeasonId"
inner join public."FranchiseSeason" fsHome on fsHome."Id" = c."HomeTeamFranchiseSeasonId"	
inner join public."Franchise" fAway on fAway."Id" = fsAway."FranchiseId"
inner join public."Franchise" fHome on fHome."Id" = fsHome."FranchiseId"
left join public."Venue" v on v."Id" = c."VenueId"
where (fAway."Slug" = @Slug OR fHome."Slug" = @Slug) and C."SeasonYear" = @SeasonYear
ORDER BY C."StartDateUtc"