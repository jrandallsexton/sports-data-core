-- Returns poll entries for the most recent published week of a poll type
-- and season, read from the SeasonPoll* store — the store the weekly
-- rankings sourcing job (SeasonRanking → SeasonTypeWeekRankings docs)
-- feeds directly. The prior version read FranchiseSeasonRanking, which
-- only fills when TeamSeason docs are re-sourced with a TeamSeasonRank
-- inclusion filter — so the weekly job never refreshed the UI.
-- Single query: find the most recent week, then return its ranked
-- entries (others-receiving-votes and dropped-out rows excluded).
WITH most_recent AS (
  SELECT spw."Id" AS "SeasonPollWeekId", spw."DateUtc", spw."ShortHeadline", spw."SeasonWeekId"
  FROM public."SeasonPollWeek" spw
  INNER JOIN public."SeasonPoll" sp ON sp."Id" = spw."SeasonPollId"
  WHERE sp."SeasonYear" = @SeasonYear AND sp."Slug" = @PollId AND spw."DateUtc" IS NOT NULL
  ORDER BY spw."DateUtc" DESC
  LIMIT 1
),
week_info AS (
  -- SeasonWeekId is nullable on SeasonPollWeek; a missing link renders as
  -- week 0 rather than dropping the poll.
  SELECT COALESCE(sw."Number", 0) AS "WeekNumber", mr."SeasonPollWeekId", mr."DateUtc" AS "PollDate", mr."ShortHeadline"
  FROM most_recent mr
  LEFT JOIN public."SeasonWeek" sw ON sw."Id" = mr."SeasonWeekId"
)
SELECT
    wi."WeekNumber",
    wi."PollDate" AS "PollDateUtc",
    wi."ShortHeadline" AS "PollName",
    fs."Id" AS "FranchiseSeasonId",
    fs."FranchiseId",
    fs."Slug" AS "FranchiseSlug",
    fs."DisplayNameShort" AS "FranchiseName",
    -- Entry-level record when ESPN published one (mid-season), otherwise
    -- the FranchiseSeason record (preseason entries carry NULLs).
    COALESCE(spwe."Wins", fs."Wins") AS "Wins",
    COALESCE(spwe."Losses", fs."Losses") AS "Losses",
    spwe."Current" AS "Rank",
    NULLIF(spwe."Previous", 0) AS "PreviousRank",
    spwe."Points",
    spwe."FirstPlaceVotes",
    NULLIF(spwe."Trend", '') AS "Trend"
FROM public."SeasonPollWeekEntry" spwe
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = spwe."FranchiseSeasonId"
INNER JOIN week_info wi ON spwe."SeasonPollWeekId" = wi."SeasonPollWeekId"
WHERE NOT spwe."IsOtherReceivingVotes" AND NOT spwe."IsDroppedOut"
ORDER BY spwe."Current" ASC
