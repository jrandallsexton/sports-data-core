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
  -- Display week derived from the poll's PUBLISH DATE, not the
  -- SeasonPollWeek.SeasonWeekId link — those links are unreliable
  -- (off-by-one late season, NULL for preseason/final). A poll's week is
  -- the latest week starting before its date + 5 days: the entering Sunday
  -- AP poll and the midweek Tuesday CFP poll both resolve to the week they
  -- serve. 0 when no week qualifies (preseason).
  SELECT COALESCE((
           SELECT sw."Number"
           FROM public."SeasonWeek" sw
           INNER JOIN public."Season" s ON s."Id" = sw."SeasonId"
           INNER JOIN public."SeasonPhase" sph ON sph."Id" = sw."SeasonPhaseId"
           WHERE s."Year" = @SeasonYear
             AND sph."Name" = 'Regular Season'
             AND sw."StartDate" < mr."DateUtc" + INTERVAL '5 days'
           ORDER BY sw."StartDate" DESC
           LIMIT 1
         ), 0) AS "WeekNumber",
         mr."SeasonPollWeekId", mr."DateUtc" AS "PollDate", mr."ShortHeadline"
  FROM most_recent mr
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
