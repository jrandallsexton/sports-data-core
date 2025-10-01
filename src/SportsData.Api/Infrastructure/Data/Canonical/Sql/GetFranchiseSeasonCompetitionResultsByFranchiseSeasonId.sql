SELECT
  c."StartDateUtc",
  c."Id" AS "ContestId",

  fAway."Abbreviation" as "AwayShort",
  fsAway."Id" as "AwayFranchiseSeasonId",
  fAway."Slug" as "AwaySlug",
  fsrdAway."Current" as "AwayRank",

  fHome."Abbreviation" as "HomeShort",
  fsHome."Id" as "HomeFranchiseSeasonId",
  fHome."Slug" as "HomeSlug",
  fsrdHome."Current" as "HomeRank",
  
  co."Details" as "Spread",
  (co."Spread" * -1) as "AwaySpread",
  co."Spread" as "HomeSpread",
  co."OverUnder" as "OverUnder",

  c."FinalizedUtc",
  c."AwayScore",
  c."HomeScore",
  c."WinnerFranchiseId" as "WinnerFranchiseSeasonId",
  c."SpreadWinnerFranchiseId" as "SpreadWinnerFranchiseSeasonId",
  c."OverUnder" as "OverUnderResult",
  c."EndDateUtc" as "CompletedUtc"

FROM public."Contest" c
INNER JOIN public."Venue" v on v."Id" = c."VenueId"
INNER JOIN public."Competition" comp on comp."ContestId" = c."Id"
LEFT  JOIN public."CompetitionOdds" co on co."CompetitionId" = comp."Id" AND co."ProviderId" = '58'

INNER JOIN public."FranchiseSeason" fsAway on fsAway."Id" = c."AwayTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fAway on fAway."Id" = fsAway."FranchiseId"

INNER JOIN public."GroupSeason" gsAway on gsAway."Id" = fsAway."GroupSeasonId"
left  join public."FranchiseSeasonRanking" fsrAway on fsrAway."FranchiseSeasonId" = fsAway."Id" and fsrAway."Type" = 'ap' and fsrAway."SeasonWeekId" = c."SeasonWeekId"
left  join public."FranchiseSeasonRankingDetail" fsrdAway on fsrdAway."FranchiseSeasonRankingId" = fsrAway."Id"

INNER JOIN public."FranchiseSeason" fsHome on fsHome."Id" = c."HomeTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fHome on fHome."Id" = fsHome."FranchiseId"

INNER JOIN public."GroupSeason" gsHome on gsHome."Id" = fsHome."GroupSeasonId"
left  join public."FranchiseSeasonRanking" fsrHome on fsrHome."FranchiseSeasonId" = fsHome."Id" and fsrHome."Type" = 'ap' and fsrHome."SeasonWeekId" = c."SeasonWeekId"
left  join public."FranchiseSeasonRankingDetail" fsrdHome on fsrdHome."FranchiseSeasonRankingId" = fsrHome."Id"

WHERE c."StartDateUtc" <= NOW() and ((fsAway."Id" = @FranchiseSeasonId) Or (fsHome."Id" = @FranchiseSeasonId))
ORDER BY c."StartDateUtc", fHome."Slug";

-- select * from public."FranchiseSeason" where "Slug" = 'lsu-tigers'