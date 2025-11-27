WITH next_week AS (
  SELECT sw."Id" AS "SeasonWeekId",
         sw."Number" AS "WeekNumber",
         s."Id" AS "SeasonId",
         s."Year" AS "SeasonYear"
  FROM public."Season" s
  JOIN public."SeasonWeek" sw ON sw."SeasonId" = s."Id"
  JOIN public."SeasonPhase" sp ON sp."Id" = sw."SeasonPhaseId"
  WHERE sp."Name" = 'Regular Season'
    AND sw."StartDate" <= NOW() and sw."EndDate" > NOW()
  ORDER BY sw."StartDate"
  LIMIT 1
)

SELECT
  nw."SeasonWeekId",
  c."Id" AS "ContestId",
  c."StartDateUtc" AS "StartDateUtc",
  cs."StatusTypeName" as "Status",

  v."Name"                  as "VenueName",
  v."City"                  as "VenueCity",
  v."State"                 as "VenueState",
  v."Latitude"              as "VenueLatitude",
  v."Longitude"             as "VenueLongitude",

  fAway."Slug"              as "AwaySlug",
  fAway."ColorCodeHex"      as "AwayColor",
  fAway."Abbreviation"      as "AwayAbbreviation",
  fsrdAway."Current"        as "AwayRank",
  fsAway."Wins"             as "AwayWins",
  fsAway."Losses"           as "AwayLosses",
  fsAway."ConferenceWins"   as "AwayConferenceWins",
  fsAway."ConferenceLosses" as "AwayConferenceLosses",
  gsAway."Slug"             as "AwayConferenceSlug",  

  fHome."Slug"              as "HomeSlug",
  fHome."ColorCodeHex"      as "HomeColor",
  fHome."Abbreviation"      as "HomeAbbreviation",
  fsrdHome."Current"        as "HomeRank",
  fsHome."Wins"             as "HomeWins",
  fsHome."Losses"           as "HomeLosses",
  fsHome."ConferenceWins"   as "HomeConferenceWins",
  fsHome."ConferenceLosses" as "HomeConferenceLosses",
  gsHome."Slug"             as "HomeConferenceSlug",

  co."Details"        as "Spread",
  (co."Spread" * -1)  as "AwaySpread",
  co."Spread"         as "HomeSpread",
  co."OverUnder"      as "OverUnder",
  co."OverOdds"       as "OverOdds",
  co."UnderOdds"      as "UnderOdds"
FROM next_week nw
inner join public."Contest" c ON c."SeasonWeekId" = nw."SeasonWeekId"
inner join public."Competition" comp on comp."ContestId" = c."Id"
left  join public."CompetitionOdds" co on co."CompetitionId" = comp."Id" AND co."ProviderId" = '58'
left  join public."CompetitionStatus" cs on cs."CompetitionId" = comp."Id"
inner join public."Venue" v on v."Id" = c."VenueId"
inner join public."FranchiseSeason" fsAway on fsAway."Id" = c."AwayTeamFranchiseSeasonId"
inner join public."Franchise" fAway on fAway."Id" = fsAway."FranchiseId"
inner join public."GroupSeason" gsAway on gsAway."Id" = fsAway."GroupSeasonId"
inner join public."FranchiseSeason" fsHome on fsHome."Id" = c."HomeTeamFranchiseSeasonId"
inner join public."Franchise" fHome on fHome."Id" = fsHome."FranchiseId"
inner join public."GroupSeason" gsHome on gsHome."Id" = fsHome."GroupSeasonId"
left  join public."FranchiseSeasonRanking" fsrAway on fsrAway."FranchiseSeasonId" = fsAway."Id" and
      fsrAway."DefaultRanking" = true and fsrAway."Type" in ('ap', 'cfp') and
      fsrAway."SeasonWeekId" = nw."SeasonWeekId"
left  join public."FranchiseSeasonRankingDetail" fsrdAway on fsrdAway."FranchiseSeasonRankingId" = fsrAway."Id"
left  join public."FranchiseSeasonRanking" fsrHome on fsrHome."FranchiseSeasonId" = fsHome."Id" and
      fsrHome."DefaultRanking" = true and fsrHome."Type" in ('ap', 'cfp') and
      fsrHome."SeasonWeekId" = nw."SeasonWeekId"
left  join public."FranchiseSeasonRankingDetail" fsrdHome on fsrdHome."FranchiseSeasonRankingId" = fsrHome."Id"
--WHERE c."StartDateUtc" >= NOW() --and fHome."Slug" = 'miami-hurricanes'
ORDER BY "StartDateUtc", fHome."Slug"