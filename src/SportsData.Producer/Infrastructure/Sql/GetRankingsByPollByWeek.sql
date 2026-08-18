-- Poll entries for a specific season/week/poll, read from the SeasonPoll*
-- store (see GetPollByTypeAndSeason.sql for why not FranchiseSeasonRanking).
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
    spw."DateUtc" as "PollDateUtc"
FROM public."SeasonPollWeekEntry" spwe
INNER JOIN public."SeasonPollWeek" spw on spw."Id" = spwe."SeasonPollWeekId"
INNER JOIN public."SeasonPoll" sp on sp."Id" = spw."SeasonPollId"
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
INNER JOIN public."SeasonWeek" sw on sw."Id" = spw."SeasonWeekId"
WHERE sp."Slug" = @PollType
  and sp."SeasonYear" = @SeasonYear
  and sw."Number" = @WeekNumber
  and NOT spwe."IsOtherReceivingVotes"
  and NOT spwe."IsDroppedOut"
ORDER BY spwe."Current" asc
