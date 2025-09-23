-- SELECT json_agg(row_to_json(t))
-- FROM (
  SELECT
    sp."Year" as "SeasonYear",
    sw."Number" as "WeekNumber",
    c."Id" AS "ContestId",
    c."StartDateUtc" as "StartDateUtc",

    v."Name" as "Venue",
    v."City" as "VenueCity",
    v."State" as "VenueState",

	  fsAway."Id" as "AwayFranchiseSeasonId",
    fAway."DisplayName" as "Away",
    fAway."Slug" as "AwaySlug",
    fsrdAway."Current" as "AwayRank",
    gsAway."Id" as "AwayConferenceId",
    gsAway."Slug" as "AwayConferenceSlug",
    gsAwayParent."Slug" as "AwayParentConferenceSlug",
    fsAway."Wins" as "AwayWins",
    fsAway."Losses" as "AwayLosses",
    fsAway."ConferenceWins" as "AwayConferenceWins",
    fsAway."ConferenceLosses" as "AwayConferenceLosses",

	  fsHome."Id" as "HomeFranchiseSeasonId",
    fHome."DisplayName" as "Home",
    fHome."Slug" as "HomeSlug",
    fsrdHome."Current" as "HomeRank",
    gsHome."Id" as "HomeConferenceId",
    gsHome."Slug" as "HomeConferenceSlug",
    gsHomeParent."Slug" as "HomeParentConferenceSlug",
    fsHome."Wins" as "HomeWins",
    fsHome."Losses" as "HomeLosses",
    fsHome."ConferenceWins" as "HomeConferenceWins",
    fsHome."ConferenceLosses" as "HomeConferenceLosses",

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
  left  join public."CompetitionOdds" co on co."CompetitionId" = comp."Id" AND co."ProviderId" != '59'

  inner join public."FranchiseSeason" fsAway on fsAway."Id" = c."AwayTeamFranchiseSeasonId"
  inner join public."Franchise" fAway on fAway."Id" = fsAway."FranchiseId"
  inner join public."GroupSeason" gsAway on gsAway."Id" = fsAway."GroupSeasonId"
  inner join public."GroupSeason" gsAwayParent on gsAway."ParentId" = gsAwayParent."Id"

  inner join public."FranchiseSeason" fsHome on fsHome."Id" = c."HomeTeamFranchiseSeasonId"
  inner join public."Franchise" fHome on fHome."Id" = fsHome."FranchiseId"
  inner join public."GroupSeason" gsHome on gsHome."Id" = fsHome."GroupSeasonId"
  inner join public."GroupSeason" gsHomeParent on gsHome."ParentId" = gsHomeParent."Id"

  left  join public."FranchiseSeasonRanking" fsrAway on fsrAway."FranchiseSeasonId" = fsAway."Id" and fsrAway."Type" = 'ap' and fsrAway."SeasonWeekId" = c."SeasonWeekId"
  left  join public."FranchiseSeasonRankingDetail" fsrdAway on fsrdAway."FranchiseSeasonRankingId" = fsrAway."Id"
  left  join public."FranchiseSeasonRanking" fsrHome on fsrHome."FranchiseSeasonId" = fsHome."Id" and fsrHome."Type" = 'ap' and fsrHome."SeasonWeekId" = c."SeasonWeekId"
  left  join public."FranchiseSeasonRankingDetail" fsrdHome on fsrdHome."FranchiseSeasonRankingId" = fsrHome."Id"
--where c."Id" = @ContestId
where c."Id" = 'b6cde160-f48d-9d51-784b-56bf4adb990a' --) t;

-- select * from public."Contest" where "Id" = 'b6cde160-f48d-9d51-784b-56bf4adb990a'
-- SELECT * from "Competition" where "ContestId" = 'b6cde160-f48d-9d51-784b-56bf4adb990a'

-- SELECT c.*
-- FROM public."Contest" c
-- LEFT JOIN public."Competition" comp ON comp."ContestId" = c."Id"
-- WHERE comp."Id" IS NULL ORDER BY c."StartDateUtc"


--select * from public."FranchiseSeason" where "Id" = '0abfe224-2ff2-951d-25e1-a9d59d57bfe7'