SELECT
  c."SeasonWeekId" as "SeasonWeekId",
  c."Id" AS "ContestId",
  c."StartDateUtc" as "StartDateUtc",
  replace(cs."StatusDescription", ' ', '') AS "Status",  

  v."Name" as "Venue",
  v."City" as "VenueCity",
  v."State" as "VenueState",
  
  fAway."DisplayName" as "Away",
  fAway."Abbreviation" as "AwayShort",
  fsAway."Id" as "AwayFranchiseSeasonId",
  flAway."Uri" as "AwayLogoUri",
  fAway."Slug" as "AwaySlug",
  fsrdAway."Current" as "AwayRank",
  gsAway."Slug" as "AwayConferenceSlug",
  fsAway."Wins" as "AwayWins",
  fsAway."Losses" as "AwayLosses",
  fsAway."ConferenceWins" as "AwayConferenceWins",
  fsAway."ConferenceLosses" as "AwayConferenceLosses",
  
  fHome."DisplayName" as "Home",
  fHome."Abbreviation" as "HomeShort",
  fsHome."Id" as "HomeFranchiseSeasonId",
  flHome."Uri" as "HomeLogoUri",  
  fHome."Slug" as "HomeSlug",
  fsrdHome."Current" as "HomeRank",
  gsHome."Slug" as "HomeConferenceSlug",
  fsHome."Wins" as "HomeWins",
  fsHome."Losses" as "HomeLosses",
  fsHome."ConferenceWins" as "HomeConferenceWins",
  fsHome."ConferenceLosses" as "HomeConferenceLosses",
  
  co."Details" as "SpreadCurrentDetails",
  co."Spread" as "SpreadCurrent",
  cto."SpreadPointsOpen" as "SpreadOpen",
  co."OverUnder" as "OverUnderCurrent",
  co."TotalPointsOpen" as "OverUnderOpen",
  co."OverOdds" as "OverOdds",
  co."UnderOdds" as "UnderOdds",

  c."AwayScore",
  c."HomeScore",
  c."WinnerFranchiseId" as "WinnerFranchiseSeasonId",
  c."SpreadWinnerFranchiseId" as "SpreadWinnerFranchiseSeasonId",
  c."OverUnder" as "OverUnderResult",
  c."EndDateUtc" as "CompletedUtc"

FROM public."Contest" c
INNER JOIN public."Venue" v on v."Id" = c."VenueId"
INNER JOIN public."Competition" comp on comp."ContestId" = c."Id"
LEFT  JOIN public."CompetitionStatus" cs on cs."CompetitionId" = comp."Id"
LEFT  JOIN public."CompetitionOdds" co on co."CompetitionId" = comp."Id" AND co."ProviderId" = '58'
LEFT  JOIN public."CompetitionTeamOdds" cto on cto."CompetitionOddsId" = co."Id" and cto."Side" = 'Home'

INNER JOIN public."FranchiseSeason" fsAway on fsAway."Id" = c."AwayTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fAway on fAway."Id" = fsAway."FranchiseId"

LEFT JOIN LATERAL (
  SELECT fl.*
  FROM public."FranchiseLogo" fl
  WHERE fl."FranchiseId" = fAway."Id"
  ORDER BY fl."CreatedUtc" ASC -- or ORDER BY fl."Id" ASC
  LIMIT 1
) flAway ON TRUE

INNER JOIN public."GroupSeason" gsAway on gsAway."Id" = fsAway."GroupSeasonId"
LEFT  join public."FranchiseSeasonRanking" fsrAway on fsrAway."FranchiseSeasonId" = fsAway."Id" and fsrAway."Type" = 'ap' and fsrAway."SeasonWeekId" = c."SeasonWeekId"
LEFT  join public."FranchiseSeasonRankingDetail" fsrdAway on fsrdAway."FranchiseSeasonRankingId" = fsrAway."Id"

INNER JOIN public."FranchiseSeason" fsHome on fsHome."Id" = c."HomeTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fHome on fHome."Id" = fsHome."FranchiseId"

LEFT JOIN LATERAL (
  SELECT fl.*
  FROM public."FranchiseLogo" fl
  WHERE fl."FranchiseId" = fHome."Id"
  ORDER BY fl."CreatedUtc" ASC -- or ORDER BY fl."Id" ASC
  LIMIT 1
) flHome ON TRUE

INNER JOIN public."GroupSeason" gsHome on gsHome."Id" = fsHome."GroupSeasonId"
LEFT  join public."FranchiseSeasonRanking" fsrHome on fsrHome."FranchiseSeasonId" = fsHome."Id" and fsrHome."Type" = 'ap' and fsrHome."SeasonWeekId" = c."SeasonWeekId"
LEFT  join public."FranchiseSeasonRankingDetail" fsrdHome on fsrdHome."FranchiseSeasonRankingId" = fsrHome."Id"

WHERE c."Id" = ANY(@ContestIds)
ORDER BY c."StartDateUtc", fHome."Slug";
