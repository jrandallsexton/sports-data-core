SELECT
  c."SeasonWeekId" as "SeasonWeekId",
  c."Id" AS "ContestId",
  c."StartDateUtc" as "StartDateUtc",
  replace(cs."StatusDescription", ' ', '') AS "Status",

  STRING_AGG(cb."MediaName", ' | ') AS "Broadcasts",

  v."Name" as "Venue",
  v."City" as "VenueCity",
  v."State" as "VenueState",
  
  fAway."DisplayName" as "Away",
  fAway."Abbreviation" as "AwayShort",
  fsAway."Id" as "AwayFranchiseSeasonId",
  flAway."Uri" as "AwayLogoUri",
  fAway."Slug" as "AwaySlug",
  fAway."ColorCodeHex" as "AwayColor",
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
  fHome."ColorCodeHex" as "HomeColor",
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
LEFT JOIN public."CompetitionBroadcast" cb on cb."CompetitionId" = comp."Id"
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
  left  join public."FranchiseSeasonRanking" fsrAway on fsrAway."FranchiseSeasonId" = fsAway."Id" and
        fsrAway."DefaultRanking" = true and fsrAway."Type" in ('ap', 'cfp') and
        fsrAway."SeasonWeekId" = c."SeasonWeekId"
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
  left  join public."FranchiseSeasonRanking" fsrHome on fsrHome."FranchiseSeasonId" = fsHome."Id" and
        fsrHome."DefaultRanking" = true and fsrHome."Type" in ('ap', 'cfp') and
        fsrHome."SeasonWeekId" = c."SeasonWeekId"
LEFT  join public."FranchiseSeasonRankingDetail" fsrdHome on fsrdHome."FranchiseSeasonRankingId" = fsrHome."Id"

--WHERE c."Id" = 'ee11ed43-9a77-9e87-73c4-5ce6ca312ae5'
WHERE c."Id" IN (
  '016861cc-9cab-1296-c156-c6be3c46cadb',
'24477be2-e202-7ce2-ef3b-4b71a9bc3b58',
'39087234-df27-817d-7798-80190853a704',
'4a41c8a6-bcc5-473a-f88b-d151a5c9aeea',
'6fb9aa0a-720b-751a-a36f-d0e54fcaf1bf',
'71bae14f-71b7-9554-65ee-028dc0912ad9',
'7ba93dac-a490-f35b-e0d2-61c747391a31',
'7d6edbc7-c777-abf1-b2c6-46a501996492',
'ae646c2a-c263-3b8c-b8ad-16f24b38a8fc',
'd9c160f7-bd11-28f9-f19d-2e14d6c3d8dd',
'e9724378-b059-f6cb-4cd6-74d5416cbf3e')

GROUP BY
  c."SeasonWeekId",
  c."Id",
  c."StartDateUtc",
  cs."StatusDescription",
  v."Name", v."City", v."State",

  fAway."DisplayName", fAway."DisplayNameShort", fsAway."Id", flAway."Uri", fAway."Slug",
  fsrdAway."Current", gsAway."Slug",
  fsAway."Wins", fsAway."Losses", fsAway."ConferenceWins", fsAway."ConferenceLosses",

    fAway."Abbreviation", fAway."ColorCodeHex",
  fHome."Abbreviation", fHome."ColorCodeHex",


  fHome."DisplayName", fHome."DisplayNameShort", fsHome."Id", flHome."Uri", fHome."Slug",
  fsrdHome."Current", gsHome."Slug",
  fsHome."Wins", fsHome."Losses", fsHome."ConferenceWins", fsHome."ConferenceLosses",

  co."Details", co."Spread", co."OverUnder", co."OverOdds", co."UnderOdds",
  cto."SpreadPointsOpen", co."TotalPointsOpen",
  c."AwayScore", c."HomeScore", c."WinnerFranchiseId", c."SpreadWinnerFranchiseId",
  c."OverUnder", c."EndDateUtc"


ORDER BY c."StartDateUtc", fHome."Slug";


-- SELECT * from "Competition" where "ContestId" = '295474a7-a45c-85ae-b95d-9c7902b0744e'
--select * from public."CompetitionBroadcast" where "CompetitionId" = '0c911932-8ca0-c341-83a4-f84c269a463d'
--select * from public."CompetitionBroadcast" where "Station" = '6936478' or "Station" = 'SEC Network'
-- SELECT * from "Competition" where "Id" = '95cf4eb4-08e5-814b-e20b-e19cceccef84'
--select * from public."CompetitionOdds" where "CompetitionId" = '95cf4eb4-08e5-814b-e20b-e19cceccef84'
--select * from public."CompetitionTeamOdds" WHERE "CompetitionOddsId" = 'b77a7510-4a7a-6f2b-1755-96924c34495a'