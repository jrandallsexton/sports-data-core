-- Poll entries for a specific season/week/poll, read from the SeasonPoll*
-- store (see GetPollByTypeAndSeason.sql for why not FranchiseSeasonRanking).
--
-- The week's DESIGNATED poll is resolved by PUBLISH DATE, not by
-- SeasonPollWeek.SeasonWeekId — those links are unreliable (off-by-one late
-- season, NULL for preseason/final). A poll belongs to week N when it is the
-- latest of its type published before week N's start + 5 days: that window
-- admits the entering Sunday AP poll and the midweek Tuesday CFP poll while
-- excluding the NEXT Sunday's AP poll. Week numbers resolve within the
-- Regular Season phase (phases reuse numbers).
WITH target_week AS (
  SELECT sw."StartDate"
  FROM public."SeasonWeek" sw
  INNER JOIN public."Season" s ON s."Id" = sw."SeasonId"
  INNER JOIN public."SeasonPhase" sph ON sph."Id" = sw."SeasonPhaseId"
  WHERE s."Year" = @SeasonYear AND sw."Number" = @WeekNumber
    AND sph."Name" = 'Regular Season'
  ORDER BY sw."StartDate"
  LIMIT 1
),
poll_week AS (
  SELECT spw."Id", spw."DateUtc"
  FROM public."SeasonPollWeek" spw
  INNER JOIN public."SeasonPoll" sp ON sp."Id" = spw."SeasonPollId"
  WHERE sp."SeasonYear" = @SeasonYear AND sp."Slug" = @PollType
    AND spw."DateUtc" IS NOT NULL
    AND spw."DateUtc" < (SELECT "StartDate" + INTERVAL '5 days' FROM target_week)
  ORDER BY spw."DateUtc" DESC
  LIMIT 1
)
SELECT
    fs."Id" as "FranchiseSeasonId",
    fsl."Uri" as "FranchiseLogoUrl",
    fs."Slug" as "FranchiseSlug",
    fs."DisplayNameShort" as "FranchiseName",
    COALESCE(spwe."Wins", fs."Wins") as "Wins",
    COALESCE(spwe."Losses", fs."Losses") as "Losses",
    spwe."Current" as "Rank",
    NULLIF(spwe."Previous", 0) as "PreviousRank",
    spwe."Points",
    spwe."FirstPlaceVotes",
    NULLIF(spwe."Trend", '') as "Trend",
    pw."DateUtc" as "PollDateUtc"
FROM public."SeasonPollWeekEntry" spwe
INNER JOIN poll_week pw on pw."Id" = spwe."SeasonPollWeekId"
INNER JOIN public."FranchiseSeason" fs on fs."Id" = spwe."FranchiseSeasonId"
-- Logo selection: prefer sportdeets-mark + requested direction, then any
-- sportdeets-mark, then anything else. Matches the pattern in
-- GetMatchupsByContestIds.sql and LogoSelectionService.
LEFT JOIN LATERAL (
  SELECT fsl."Uri"
  FROM public."FranchiseSeasonLogo" fsl
  WHERE fsl."FranchiseSeasonId" = fs."Id"
  ORDER BY
    CASE
      WHEN fsl."Rel" @> ARRAY['sportdeets-mark', @Direction]::text[] THEN 0
      WHEN 'sportdeets-mark' = ANY(fsl."Rel")                        THEN 1
      ELSE                                                                2
    END,
    fsl."Uri"
  LIMIT 1
) as fsl on true
WHERE NOT spwe."IsOtherReceivingVotes"
  and NOT spwe."IsDroppedOut"
ORDER BY spwe."Current" asc
