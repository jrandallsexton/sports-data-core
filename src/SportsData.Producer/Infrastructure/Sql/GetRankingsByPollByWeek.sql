SELECT
    fs."Id" as "FranchiseSeasonId",
    fsl."Uri" as "FranchiseLogoUrl",
    fs."Slug" as "FranchiseSlug",
    fs."DisplayNameShort" as "FranchiseName",
    fs."Wins",
    fs."Losses",
    fsrd."Current" as "Rank",
    fsrd."Previous" as "PreviousRank",
    fsrd."Points",
    fsrd."FirstPlaceVotes",
    fsrd."Trend",
    fsrd."Date" as "PollDateUtc"
FROM public."FranchiseSeasonRankingDetail" fsrd
INNER JOIN public."FranchiseSeasonRanking" fsr on fsr."Id" = fsrd."FranchiseSeasonRankingId"
INNER JOIN public."FranchiseSeason" fs on fs."Id" = fsr."FranchiseSeasonId"
LEFT JOIN LATERAL (
  SELECT fsl."Uri"
  FROM public."FranchiseSeasonLogo" fsl
  WHERE fsl."FranchiseSeasonId" = fs."Id"
  ORDER BY fsl."Uri"
  LIMIT 1
) as fsl on true
INNER JOIN public."SeasonWeek" sw on sw."Id" = fsr."SeasonWeekId"
INNER JOIN public."Season" s on s."Id" = sw."SeasonId"
WHERE fsr."Type" = @PollType and sw."Number" = @WeekNumber and s."Year" = @SeasonYear
ORDER BY fsrd."Current" asc
