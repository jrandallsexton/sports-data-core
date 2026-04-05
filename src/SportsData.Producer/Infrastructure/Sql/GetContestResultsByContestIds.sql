SELECT
  c."StartDateUtc",
  c."Id" AS "ContestId",
  fAway."Abbreviation" AS "AwayShort",
  fsAway."Id" AS "AwayFranchiseSeasonId",
  fAway."Slug" AS "AwaySlug",
  fsrdAway."Current" AS "AwayRank",
  fHome."Abbreviation" AS "HomeShort",
  fsHome."Id" AS "HomeFranchiseSeasonId",
  fHome."Slug" AS "HomeSlug",
  fsrdHome."Current" AS "HomeRank",
  co."Details" AS "Spread",
  (co."Spread" * -1) AS "AwaySpread",
  co."Spread" AS "HomeSpread",
  co."OverUnder" AS "OverUnder",
  c."FinalizedUtc",
  c."AwayScore",
  c."HomeScore",
  c."WinnerFranchiseId" AS "WinnerFranchiseSeasonId",
  c."SpreadWinnerFranchiseId" AS "SpreadWinnerFranchiseSeasonId",
  c."OverUnder" AS "OverUnderResult",
  c."EndDateUtc" AS "CompletedUtc"
FROM public."Contest" c
LEFT JOIN public."Venue" v ON v."Id" = c."VenueId"
INNER JOIN public."Competition" comp ON comp."ContestId" = c."Id"
LEFT JOIN LATERAL (
  SELECT * FROM public."CompetitionOdds"
  WHERE "CompetitionId" = comp."Id" AND "ProviderId" IN ('{PreferredOddsProviderId}', '{FallbackOddsProviderId}')
  ORDER BY CASE WHEN "ProviderId" = '{PreferredOddsProviderId}' THEN 1 ELSE 2 END
  LIMIT 1
) co ON TRUE
INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = c."AwayTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fAway ON fAway."Id" = fsAway."FranchiseId"
INNER JOIN public."GroupSeason" gsAway ON gsAway."Id" = fsAway."GroupSeasonId"
LEFT JOIN public."FranchiseSeasonRanking" fsrAway ON fsrAway."FranchiseSeasonId" = fsAway."Id"
  AND fsrAway."DefaultRanking" = true AND fsrAway."Type" IN ('ap', 'cfp')
  AND fsrAway."SeasonWeekId" = c."SeasonWeekId"
LEFT JOIN public."FranchiseSeasonRankingDetail" fsrdAway ON fsrdAway."FranchiseSeasonRankingId" = fsrAway."Id"
INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = c."HomeTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fHome ON fHome."Id" = fsHome."FranchiseId"
INNER JOIN public."GroupSeason" gsHome ON gsHome."Id" = fsHome."GroupSeasonId"
LEFT JOIN public."FranchiseSeasonRanking" fsrHome ON fsrHome."FranchiseSeasonId" = fsHome."Id"
  AND fsrHome."DefaultRanking" = true AND fsrHome."Type" IN ('ap', 'cfp')
  AND fsrHome."SeasonWeekId" = c."SeasonWeekId"
LEFT JOIN public."FranchiseSeasonRankingDetail" fsrdHome ON fsrdHome."FranchiseSeasonRankingId" = fsrHome."Id"
WHERE c."Id" = ANY(@ContestIds)
ORDER BY c."StartDateUtc", fHome."Slug"
