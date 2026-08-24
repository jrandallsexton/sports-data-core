select * from public."SeasonPoll" order by "CreatedUtc" desc;

select * from public."SeasonPollWeek" where "SeasonPollId" = 'c07a929b-491c-4f11-b7bc-84c12316e5ad';

-- Raw Data --
select * from public."SeasonPollWeekEntry"
where "SeasonPollWeekId" = 'df0aba6b-dbe9-58c0-f930-5e70296fb2ad' AND
"IsOtherReceivingVotes" = false and "IsDroppedOut" = false
order by "Current";

-- Actual Poll Data --
select f."Slug", f."Name", spwe.*
from public."SeasonPollWeekEntry" spwe
inner join public."FranchiseSeason" fs on fs."Id" = spwe."FranchiseSeasonId"
inner join public."Franchise" f on f."Id" = fs."FranchiseId"
where
  spwe."SeasonPollWeekId" = 'df0aba6b-dbe9-58c0-f930-5e70296fb2ad' AND
  spwe."IsOtherReceivingVotes" = false and
  spwe."IsDroppedOut" = false
order by "Current";

select * from public."SeasonWeek" order by "StartDate" desc;

select * from public."FranchiseSeason" where "Slug" = 'lsu-tigers'

select * from public."FranchiseSeasonRanking" where "SeasonYear" = 2026 and "Type" = 'ap';

select * from public."FranchiseSeasonRanking"
where "FranchiseSeasonId" = 'fe246458-7d82-0137-12ec-00346206577a' and "Type" = 'ap'
order by "Date" desc

select * from public."FranchiseSeasonRankingDetail" order by "CreatedUtc" desc;

select * from public."FranchiseSeasonRankingDetail"
where "FranchiseSeasonRankingId" = '07426492-2a08-ee25-15cc-b1bf03e96590'

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

-- poll-type coverage (UI loads 'cfp','ap','usa')
  SELECT "Type", count(*) FROM public."FranchiseSeasonRanking"
  WHERE "SeasonYear" = 2026 GROUP BY "Type";

  -- Producer's exact most-recent-week CTE for 'ap'
  WITH most_recent AS (
    SELECT fsr."SeasonWeekId"
    FROM public."FranchiseSeasonRanking" fsr
    JOIN public."SeasonWeek" sw ON sw."Id" = fsr."SeasonWeekId"
    JOIN public."Season" s ON s."Id" = sw."SeasonId"
    WHERE s."Year" = 2026 AND fsr."Type" = 'ap' AND fsr."Date" IS NOT NULL
    ORDER BY fsr."Date" DESC LIMIT 1
  )
  SELECT f."DisplayNameShort", fsrd."Current", fsrd."Points", fsrd."FirstPlaceVotes"
  FROM public."FranchiseSeasonRankingDetail" fsrd
  JOIN public."FranchiseSeasonRanking" fsr ON fsr."Id" = fsrd."FranchiseSeasonRankingId"
  JOIN public."Franchise" f ON f."Id" = fsr."FranchiseId"
  JOIN most_recent mr ON fsr."SeasonWeekId" = mr."SeasonWeekId"
  WHERE fsr."Type" = 'ap'
  ORDER BY fsrd."Current";