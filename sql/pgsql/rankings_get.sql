select * from public."SeasonPoll"
select * from public."SeasonPollWeek"

select * from public."SeasonPollWeekEntry"
where "SeasonPollWeekId" = '75030c65-aa46-4324-4c03-6f0a1c61b50a' AND
"IsOtherReceivingVotes" = false and "IsDroppedOut" = false
order by "Current"

select * from public."SeasonPollWeekEntry"
Where "FranchiseSeasonId" = 'c13b7c74-6892-3efa-2492-36ebf5220464'
order by "RowDateUtc" desc

-- select *from public."SeasonPollWeekEntryStat"
-- where "SeasonPollWeekEntryId" = '054ed369-98b9-4aee-8295-d9659b54c820'

-- delete from public."SeasonPollWeekEntryStat"
-- delete from public."SeasonPollWeekEntry"
-- delete from public."SeasonPollExternalId"
-- delete from public."SeasonPoll"

select * from public."SeasonWeek" order by "Number"

select * from public."FranchiseSeason" where "Slug" = 'lsu-tigers'

select * from public."FranchiseSeasonRanking"
where "FranchiseSeasonId" = 'c13b7c74-6892-3efa-2492-36ebf5220464' and "Type" = 'ap'
order by "Date" desc

select * from public."FranchiseSeasonRankingDetail"
where "FranchiseSeasonRankingId" = '557e5b90-76f7-d9ea-75a4-dc7db52bd526'

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
  c."Id" AS "ContestId",
  c."StartDateUtc" AS "StartDateUtc",
  fAway."Slug" as "AwaySlug",
  fsrdAway."Current" as "AwayRank",
  gsAway."Slug" as "AwayConferenceSlug",
  fHome."Slug" as "HomeSlug",
  fsrdHome."Current" as "HomeRank",
  gsHome."Slug" as "HomeConferenceSlug",
  co."Details" as "Spread",
  (co."Spread" * -1) as "AwaySpread",
  co."Spread" as "HomeSpread",
  co."OverUnder" as "OverUnder",
  co."OverOdds" as "OverOdds",
  co."UnderOdds" as "UnderOdds"
FROM next_week nw
inner join public."Contest" c ON c."SeasonWeekId" = nw."SeasonWeekId"
inner join public."Competition" comp on comp."ContestId" = c."Id"
left  join public."CompetitionOdds" co on co."CompetitionId" = comp."Id"
inner join public."Venue" v on v."Id" = c."VenueId"
inner join public."FranchiseSeason" fsAway on fsAway."Id" = c."AwayTeamFranchiseSeasonId"
inner join public."Franchise" fAway on fAway."Id" = fsAway."FranchiseId"
inner join public."GroupSeason" gsAway on gsAway."Id" = fsAway."GroupSeasonId"
inner join public."FranchiseSeason" fsHome on fsHome."Id" = c."HomeTeamFranchiseSeasonId"
inner join public."Franchise" fHome on fHome."Id" = fsHome."FranchiseId"
inner join public."GroupSeason" gsHome on gsHome."Id" = fsHome."GroupSeasonId"
left  join public."FranchiseSeasonRanking" fsrAway on fsrAway."FranchiseSeasonId" = fsAway."Id" and fsrAway."Type" = 'ap' and fsrAway."SeasonWeekId" = nw."SeasonWeekId"
left  join public."FranchiseSeasonRankingDetail" fsrdAway on fsrdAway."FranchiseSeasonRankingId" = fsrAway."Id"
left  join public."FranchiseSeasonRanking" fsrHome on fsrHome."FranchiseSeasonId" = fsHome."Id" and fsrHome."Type" = 'ap' and fsrHome."SeasonWeekId" = nw."SeasonWeekId"
left  join public."FranchiseSeasonRankingDetail" fsrdHome on fsrdHome."FranchiseSeasonRankingId" = fsrHome."Id"
WHERE c."StartDateUtc" > CURRENT_DATE
ORDER BY "StartDateUtc", fHome."Slug"
