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
  nw."SeasonWeekId" as "SeasonWeekId",
  nw."WeekNumber",
  c."Id" AS "ContestId",
  c."StartDateUtc" as "StartDateUtc",

  v."Name" as "Venue",
  v."City" as "VenueCity",
  v."State" as "VenueState",

  fAway."DisplayName" as "Away",
  flAway."Uri" as "AwayLogoUri",
  fAway."Slug" as "AwaySlug",
  fsrdAway."Current" as "AwayRank",
  gsAway."Slug" as "AwayConferenceSlug",
  0 as "AwayWins",
  0 as "AwayLosses",
  0 as "AwayConferenceWins",
  0 as "AwayConferenceLosses",

  fHome."DisplayName" as "Home",
  flHome."Uri" as "HomeLogoUri",  
  fHome."Slug" as "HomeSlug",
  fsrdHome."Current" as "HomeRank",
  gsHome."Slug" as "HomeConferenceSlug",
  0 as "HomeWins",
  0 as "HomeLosses",
  0 as "HomeConferenceWins",
  0 as "HomeConferenceLosses",
  
  co."Details" as "Spread",
  (co."Spread" * -1) as "AwaySpread",
  co."Spread" as "HomeSpread",
  co."OverUnder" as "OverUnder",
  co."OverOdds" as "OverOdds",
  co."UnderOdds" as "UnderOdds"
FROM next_week nw
inner join public."Contest" c ON c."SeasonWeekId" = nw."SeasonWeekId"
inner join public."Venue" v on v."Id" = c."VenueId"
inner join public."Competition" comp on comp."ContestId" = c."Id"
left  join public."CompetitionOdds" co on co."CompetitionId" = comp."Id"
inner join public."FranchiseSeason" fsAway on fsAway."Id" = c."AwayTeamFranchiseSeasonId"
inner join public."Franchise" fAway on fAway."Id" = fsAway."FranchiseId"

LEFT JOIN LATERAL (
  SELECT fl.*
  FROM public."FranchiseLogo" fl
  WHERE fl."FranchiseId" = fAway."Id"
  ORDER BY fl."CreatedUtc" ASC -- or ORDER BY fl."Id" ASC
  LIMIT 1
) flAway ON TRUE

inner join public."GroupSeason" gsAway on gsAway."Id" = fsAway."GroupSeasonId"
inner join public."FranchiseSeason" fsHome on fsHome."Id" = c."HomeTeamFranchiseSeasonId"
inner join public."Franchise" fHome on fHome."Id" = fsHome."FranchiseId"

LEFT JOIN LATERAL (
  SELECT fl.*
  FROM public."FranchiseLogo" fl
  WHERE fl."FranchiseId" = fHome."Id"
  ORDER BY fl."CreatedUtc" ASC -- or ORDER BY fl."Id" ASC
  LIMIT 1
) flHome ON TRUE

inner join public."GroupSeason" gsHome on gsHome."Id" = fsHome."GroupSeasonId"
left  join public."FranchiseSeasonRanking" fsrAway on fsrAway."FranchiseSeasonId" = fsAway."Id" and fsrAway."Type" = 'ap'
left  join public."FranchiseSeasonRankingDetail" fsrdAway on fsrdAway."FranchiseSeasonRankingId" = fsrAway."Id"
left  join public."FranchiseSeasonRanking" fsrHome on fsrHome."FranchiseSeasonId" = fsHome."Id" and fsrHome."Type" = 'ap'
left  join public."FranchiseSeasonRankingDetail" fsrdHome on fsrdHome."FranchiseSeasonRankingId" = fsrHome."Id"
where fHome."Slug" = 'miami-hurricanes'
ORDER BY "StartDateUtc", fHome."Slug"


