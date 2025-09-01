WITH next_week AS (
  SELECT sw."Id" AS "SeasonWeekId",
         sw."Number" AS "WeekNumber",
         s."Id" AS "SeasonId",
         s."Year" AS "SeasonYear"
  FROM public."Season" s
  JOIN public."SeasonWeek" sw ON sw."SeasonId" = s."Id"
  JOIN public."SeasonPhase" sp ON sp."Id" = sw."SeasonPhaseId"
  WHERE sp."Name" = 'Regular Season'
    AND sw."StartDate" <= CURRENT_DATE and sw."EndDate" > CURRENT_DATE
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
  fsrdAway."Current" as "AwayRank",
  fsHome."Id" as "HomeFranchiseSeasonId",
  fHome."Slug" as "HomeSlug",
  fHome."DisplayName" as "Home",
  fsrdHome."Current" as "HomeRank",
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
left  join public."FranchiseSeasonRanking" fsrAway on fsrAway."FranchiseSeasonId" = fsAway."Id" and fsrAway."Type" = 'ap'
left  join public."FranchiseSeasonRankingDetail" fsrdAway on fsrdAway."FranchiseSeasonRankingId" = fsrAway."Id"
left  join public."FranchiseSeasonRanking" fsrHome on fsrHome."FranchiseSeasonId" = fsHome."Id" and fsrHome."Type" = 'ap'
left  join public."FranchiseSeasonRankingDetail" fsrdHome on fsrdHome."FranchiseSeasonRankingId" = fsrHome."Id"
WHERE c."Sport" = 2 --and c."Id" = '212ba3a8-352f-5cb0-125b-36acf79c81b2'
ORDER BY "StartDateUtc", "Home"

-- TODO: 18 Aug: Make this work by passing in a list of ContestIds
