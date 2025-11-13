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
  left  join public."FranchiseSeasonRanking" fsrAway on fsrAway."FranchiseSeasonId" = fsAway."Id" and
        fsrAway."DefaultRanking" = true and fsrAway."Type" in ('ap', 'cfp') and
        fsrAway."SeasonWeekId" = c."SeasonWeekId"
  left  join public."FranchiseSeasonRankingDetail" fsrdAway on fsrdAway."FranchiseSeasonRankingId" = fsrAway."Id"
  left  join public."FranchiseSeasonRanking" fsrHome on fsrHome."FranchiseSeasonId" = fsHome."Id" and
        fsrHome."DefaultRanking" = true and fsrHome."Type" in ('ap', 'cfp') and
        fsrHome."SeasonWeekId" = c."SeasonWeekId"
  left  join public."FranchiseSeasonRankingDetail" fsrdHome on fsrdHome."FranchiseSeasonRankingId" = fsrHome."Id"
--where c."Id" = @ContestId
where c."Id" = '569daaa1-851c-4ca0-439f-71ba227539f7' --) t;
-- https://api-dev.sportdeets.com/ui/matchup/e890f3f7-a063-0132-04af-913076ae2a99/preview
-- select * from public."Contest" where "Id" = 'e890f3f7-a063-0132-04af-913076ae2a99'
-- SELECT * from "Competition" where "ContestId" = 'e890f3f7-a063-0132-04af-913076ae2a99'
-- SELECT * from "Competition" where "Id" = '6102a620-1ebb-2d9c-964c-a573f629254e'
--select * from public."CompetitionOdds" where "CompetitionId" = '6102a620-1ebb-2d9c-964c-a573f629254e'
--select * from public."CompetitionTeamOdds" WHERE "CompetitionOddsId" = '584d97ce-5b9a-a397-5f83-0986253fb2e9'

-- select *
-- from public."CompetitionOdds" CO
-- inner join public."CompetitionTeamOdds" ctoHome on ctoHome."CompetitionOddsId" = CO."Id" and ctoHome."Side" = 'Home'
-- inner join public."CompetitionTeamOdds" ctoAway on ctoAway."CompetitionOddsId" = CO."Id" and ctoAway."Side" = 'Away'
-- where CO."CompetitionId" = 'a8515029-a951-4648-9029-86190beb97d0'

-- SELECT
--   d.datname AS database_name,
--   pg_size_pretty(pg_database_size(d.datname)) AS size
-- FROM
--   pg_database d
-- WHERE
--   d.datistemplate = false
-- ORDER BY
--   pg_database_size(d.datname) DESC;

-- delete from public."CompetitionTeamOddsSnapshot"
-- delete from public."CompetitionTeamOdds"
-- delete from public."CompetitionTotalsSnapshot"
-- delete from public."CompetitionOdds"

-- SELECT c.*
-- FROM public."Contest" c
-- LEFT JOIN public."Competition" comp ON comp."ContestId" = c."Id"
-- WHERE comp."Id" IS NULL ORDER BY c."StartDateUtc"


--select * from public."FranchiseSeason" where "Id" = '0abfe224-2ff2-951d-25e1-a9d59d57bfe7'