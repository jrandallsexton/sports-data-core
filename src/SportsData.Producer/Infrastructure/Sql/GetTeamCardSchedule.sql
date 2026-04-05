SELECT
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
    cs."StatusDescription" as "Status",
    c."FinalizedUtc" as "FinalizedUtc",
    c."AwayScore" as "AwayScore",
    c."HomeScore" as "HomeScore",
    CASE
        WHEN fAway."Slug" = @Slug AND c."WinnerFranchiseId" = fsAway."Id" THEN true
        WHEN fHome."Slug" = @Slug AND c."WinnerFranchiseId" = fsHome."Id" THEN true
        ELSE null
    END AS "WasWinner"
FROM public."Contest" C
INNER JOIN public."Competition" COMP on COMP."ContestId" = C."Id"
INNER JOIN public."CompetitionStatus" CS on CS."CompetitionId" = COMP."Id"
INNER JOIN public."SeasonWeek" SW on SW."Id" = C."SeasonWeekId"
INNER JOIN public."FranchiseSeason" fsAway on fsAway."Id" = c."AwayTeamFranchiseSeasonId"
INNER JOIN public."FranchiseSeason" fsHome on fsHome."Id" = c."HomeTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fAway on fAway."Id" = fsAway."FranchiseId"
INNER JOIN public."Franchise" fHome on fHome."Id" = fsHome."FranchiseId"
LEFT JOIN public."Venue" v on v."Id" = c."VenueId"
WHERE (fAway."Slug" = @Slug OR fHome."Slug" = @Slug) AND C."SeasonYear" = @SeasonYear
ORDER BY C."StartDateUtc"
