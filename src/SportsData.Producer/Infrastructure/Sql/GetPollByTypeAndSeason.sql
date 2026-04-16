-- Returns poll entries for the most recent week of a given poll type and season.
-- Single query: finds the most recent week, then returns all entries with logos.
WITH most_recent AS (
  SELECT fsr."SeasonWeekId", fsr."Date", fsr."ShortHeadline"
  FROM public."FranchiseSeasonRanking" fsr
  INNER JOIN public."SeasonWeek" sw ON sw."Id" = fsr."SeasonWeekId"
  INNER JOIN public."Season" s ON s."Id" = sw."SeasonId"
  WHERE s."Year" = @SeasonYear AND fsr."Type" = @PollId AND fsr."Date" IS NOT NULL
  ORDER BY fsr."Date" DESC
  LIMIT 1
),
week_info AS (
  SELECT sw."Number" AS "WeekNumber", mr."SeasonWeekId", mr."Date" AS "PollDate", mr."ShortHeadline"
  FROM most_recent mr
  INNER JOIN public."SeasonWeek" sw ON sw."Id" = mr."SeasonWeekId"
)
SELECT
    wi."WeekNumber",
    wi."PollDate" AS "PollDateUtc",
    wi."ShortHeadline" AS "PollName",
    fs."Id" AS "FranchiseSeasonId",
    f."Id" AS "FranchiseId",
    f."Slug" AS "FranchiseSlug",
    f."DisplayNameShort" AS "FranchiseName",
    fs."Wins",
    fs."Losses",
    fsrd."Current" AS "Rank",
    fsrd."Previous" AS "PreviousRank",
    fsrd."Points",
    fsrd."FirstPlaceVotes",
    fsrd."Trend"
FROM public."FranchiseSeasonRankingDetail" fsrd
INNER JOIN public."FranchiseSeasonRanking" fsr ON fsr."Id" = fsrd."FranchiseSeasonRankingId"
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = fsr."FranchiseSeasonId"
INNER JOIN public."Franchise" f ON f."Id" = fsr."FranchiseId"
INNER JOIN week_info wi ON fsr."SeasonWeekId" = wi."SeasonWeekId"
WHERE fsr."Type" = @PollId
ORDER BY fsrd."Current" ASC
