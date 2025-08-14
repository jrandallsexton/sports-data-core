WITH next_week AS (
  SELECT sw."Id" AS "SeasonWeekId",
         sw."Number" AS "WeekNumber",
         s."Id" AS "SeasonId",
         s."Year" AS "SeasonYear"
  FROM public."Season" s
  JOIN public."SeasonWeek" sw ON sw."SeasonId" = s."Id"
  JOIN public."SeasonPhase" sp ON sp."Id" = sw."SeasonPhaseId"
  WHERE sp."Name" = 'Regular Season'
    AND sw."StartDate" >= CURRENT_DATE
  ORDER BY sw."StartDate"
  LIMIT 1
)

SELECT 
  nw."SeasonYear",
  nw."WeekNumber",
  c."Id" AS "ContestId",
  c."Name" AS "Matchup",
  c."StartDateUtc",
  fsAway."Id" as "AwayFranchiseSeasonId",
  fAway."Slug" as "AwaySlug",
  fAway."DisplayName" as "Away",
  null as "AwayRank",
  fsHome."Id" as "HomeFranchiseSeasonId",
  fHome."Slug" as "HomeSlug",
  fHome."DisplayName" as "Home",
  null as "HomeRank",
  0 as "AwayWins",
  0 as "AwayLosses",
  0 as "AwayConferenceWins",
  0 as "AwayConferenceLosses",
  0 as "HomeWins",
  0 as "HomeLosses",
  0 as "HomeConferenceWins",
  0 as "HomeConferenceLosses",
  0 as "AwaySpread",
  0 as "HomeSpread",
  0 as "OU",
  v."Name" as "Venue",
  v."City" as "VenueCity",
  v."State" as "VenueState"
FROM next_week nw
INNER JOIN public."Contest" c ON c."SeasonWeekId" = nw."SeasonWeekId"
inner join public."Venue" v on v."Id" = c."VenueId"
inner join public."FranchiseSeason" fsAway on fsAway."Id" = c."AwayTeamFranchiseSeasonId"
inner join public."Franchise" fAway on fAway."Id" = fsAway."FranchiseId"
inner join public."FranchiseSeason" fsHome on fsHome."Id" = c."HomeTeamFranchiseSeasonId"
inner join public."Franchise" fHome on fHome."Id" = fsHome."FranchiseId"
WHERE c."Sport" = 2
ORDER BY "StartDateUtc"

--select * from public."Venue"
