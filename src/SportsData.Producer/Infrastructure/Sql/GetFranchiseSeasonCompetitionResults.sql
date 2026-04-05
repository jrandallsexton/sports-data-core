SELECT
  c."StartDateUtc",
  c."Id" AS "ContestId",
  fAway."Abbreviation" as "AwayShort",
  fsAway."Id" as "AwayFranchiseSeasonId",
  fAway."Slug" as "AwaySlug",
  fsrdAway."Current" as "AwayRank",
  fHome."Abbreviation" as "HomeShort",
  fsHome."Id" as "HomeFranchiseSeasonId",
  fHome."Slug" as "HomeSlug",
  fsrdHome."Current" as "HomeRank",
  co."Details" as "Spread",
  (co."Spread" * -1) as "AwaySpread",
  co."Spread" as "HomeSpread",
  co."OverUnder" as "OverUnder",
  c."FinalizedUtc",
  c."AwayScore",
  c."HomeScore",
  c."WinnerFranchiseId" as "WinnerFranchiseSeasonId",
  c."SpreadWinnerFranchiseId" as "SpreadWinnerFranchiseSeasonId",
  c."OverUnder" as "OverUnderResult",
  c."EndDateUtc" as "CompletedUtc"
FROM public."Contest" c
INNER JOIN public."Competition" comp on comp."ContestId" = c."Id"
LEFT JOIN LATERAL (
  SELECT *
  FROM public."CompetitionOdds"
  WHERE "CompetitionId" = comp."Id"
    AND "ProviderId" IN ('{PreferredOddsProviderId}', '{FallbackOddsProviderId}')
  ORDER BY CASE WHEN "ProviderId" = '{PreferredOddsProviderId}' THEN 1 ELSE 2 END
  LIMIT 1
) co ON TRUE
INNER JOIN public."FranchiseSeason" fsAway on fsAway."Id" = c."AwayTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fAway on fAway."Id" = fsAway."FranchiseId"
LEFT JOIN public."FranchiseSeasonRanking" fsrAway on fsrAway."FranchiseSeasonId" = fsAway."Id" and
        fsrAway."DefaultRanking" = true and fsrAway."Type" in ('ap', 'cfp') and
        fsrAway."SeasonWeekId" = c."SeasonWeekId"
LEFT JOIN public."FranchiseSeasonRankingDetail" fsrdAway on fsrdAway."FranchiseSeasonRankingId" = fsrAway."Id"
INNER JOIN public."FranchiseSeason" fsHome on fsHome."Id" = c."HomeTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fHome on fHome."Id" = fsHome."FranchiseId"
LEFT JOIN public."FranchiseSeasonRanking" fsrHome on fsrHome."FranchiseSeasonId" = fsHome."Id" and
        fsrHome."DefaultRanking" = true and fsrHome."Type" in ('ap', 'cfp') and
        fsrHome."SeasonWeekId" = c."SeasonWeekId"
LEFT JOIN public."FranchiseSeasonRankingDetail" fsrdHome on fsrdHome."FranchiseSeasonRankingId" = fsrHome."Id"
WHERE c."StartDateUtc" <= @NowUtc AND ((fsAway."Id" = @FranchiseSeasonId) OR (fsHome."Id" = @FranchiseSeasonId))
ORDER BY c."StartDateUtc", fHome."Slug"
