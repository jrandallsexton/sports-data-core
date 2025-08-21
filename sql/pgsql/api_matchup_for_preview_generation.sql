SELECT
  sp."Year" as "SeasonYear",
  sw."Number" as "WeekNumber",
  c."Id" AS "ContestId",
  c."StartDateUtc" as "StartDateUtc",

  v."Name" as "Venue",
  v."City" as "VenueCity",
  v."State" as "VenueState",

  fAway."DisplayName" as "Away",
  fAway."Slug" as "AwaySlug",
  fsrdAway."Current" as "AwayRank",
  gsAway."Slug" as "AwayConferenceSlug",
  0 as "AwayWins",
  0 as "AwayLosses",
  0 as "AwayConferenceWins",
  0 as "AwayConferenceLosses",

  fHome."DisplayName" as "Home",
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
FROM public."Contest" c
inner join public."SeasonPhase" sp on sp."Id" = c."SeasonPhaseId"
inner join public."SeasonWeek" sw on sw."Id" = c."SeasonWeekId"
inner join public."Venue" v on v."Id" = c."VenueId"
inner join public."Competition" comp on comp."ContestId" = c."Id"
left  join public."CompetitionOdds" co on co."CompetitionId" = comp."Id"
inner join public."FranchiseSeason" fsAway on fsAway."Id" = c."AwayTeamFranchiseSeasonId"
inner join public."Franchise" fAway on fAway."Id" = fsAway."FranchiseId"
inner join public."GroupSeason" gsAway on gsAway."Id" = fsAway."GroupSeasonId"
inner join public."FranchiseSeason" fsHome on fsHome."Id" = c."HomeTeamFranchiseSeasonId"
inner join public."Franchise" fHome on fHome."Id" = fsHome."FranchiseId"
inner join public."GroupSeason" gsHome on gsHome."Id" = fsHome."GroupSeasonId"
left  join public."FranchiseSeasonRanking" fsrAway on fsrAway."FranchiseSeasonId" = fsAway."Id" and fsrAway."Type" = 'ap'
left  join public."FranchiseSeasonRankingDetail" fsrdAway on fsrdAway."FranchiseSeasonRankingId" = fsrAway."Id"
left  join public."FranchiseSeasonRanking" fsrHome on fsrHome."FranchiseSeasonId" = fsHome."Id" and fsrHome."Type" = 'ap'
left  join public."FranchiseSeasonRankingDetail" fsrdHome on fsrdHome."FranchiseSeasonRankingId" = fsrHome."Id"
where c."Id" = 'c0e6ba13-2f58-b803-7330-44bbfc61eb8f'
ORDER BY "StartDateUtc", fHome."Slug"


