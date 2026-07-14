SELECT
  c."Id" AS "ContestId",
  c."AwayTeamFranchiseSeasonId" AS "AwayFranchiseSeasonId",
  c."HomeTeamFranchiseSeasonId" AS "HomeFranchiseSeasonId",
  c."SeasonWeekId",
  coo."Spread" AS "Spread",
  c."AwayScore",
  c."HomeScore",
  c."WinnerFranchiseSeasonId",
  c."SpreadWinnerFranchiseSeasonId",
  c."FinalizedUtc",
  afs."Abbreviation" AS "AwayAbbreviation",
  hfs."Abbreviation" AS "HomeAbbreviation"
FROM public."Contest" c
INNER JOIN public."Competition" co ON co."ContestId" = c."Id"
-- Team abbreviations for the notification copy. Picked-side is resolved in code
-- against Away/HomeFranchiseSeasonId above.
LEFT JOIN public."FranchiseSeason" afs ON afs."Id" = c."AwayTeamFranchiseSeasonId"
LEFT JOIN public."FranchiseSeason" hfs ON hfs."Id" = c."HomeTeamFranchiseSeasonId"
LEFT JOIN LATERAL (
  SELECT *
  FROM public."CompetitionOdds"
  WHERE "CompetitionId" = co."Id"
    AND "ProviderId" IN ('{PreferredOddsProviderId}', '{FallbackOddsProviderId}')
  ORDER BY CASE WHEN "ProviderId" = '{PreferredOddsProviderId}' THEN 1 ELSE 2 END
  LIMIT 1
) coo ON TRUE
WHERE c."Id" = @ContestId
  -- Scoring callers cannot tolerate pre-enrichment rows. WinnerFranchiseSeasonId,
  -- SpreadWinnerFranchiseSeasonId, and the final HomeScore/AwayScore are all
  -- populated atomically by ContestEnrichmentProcessor alongside
  -- FinalizedUtc. Returning a row before that point produced silent
  -- Guid.Empty/0-0 scoring (PickScoringProcessor / PickScoringService).
  AND c."FinalizedUtc" IS NOT NULL
