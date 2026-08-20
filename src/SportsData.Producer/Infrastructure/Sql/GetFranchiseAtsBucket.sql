-- ATS record for one franchise conditioned on spread size: as a FAVORITE of
-- @Threshold+ (@AsFavorite = TRUE) or an UNDERDOG of @Threshold+ (FALSE).
-- Market tier: requires historical spread VALUES (preferred/fallback
-- providers, present ~2022+), so counts are honest only within that window —
-- the handler stamps the data-floor season on the DTO. Games counts DECIDED
-- ATS results only (SpreadWinner null = push or unsourced -> excluded).
-- Preview-safe: finalized/non-cancelled, strictly before @AsOf, preseason
-- excluded, NULL phase kept.
SELECT
    COUNT(*) AS "Games",
    COUNT(*) FILTER (
        WHERE (fsHome."FranchiseId" = @FranchiseId AND c."SpreadWinnerFranchiseSeasonId" = c."HomeTeamFranchiseSeasonId")
           OR (fsAway."FranchiseId" = @FranchiseId AND c."SpreadWinnerFranchiseSeasonId" = c."AwayTeamFranchiseSeasonId")
    ) AS "Covers"
FROM public."Contest" c
INNER JOIN public."Competition" comp ON comp."ContestId" = c."Id"
INNER JOIN LATERAL (
    SELECT *
    FROM public."CompetitionOdds"
    WHERE "CompetitionId" = comp."Id"
      AND "ProviderId" IN ('{PreferredOddsProviderId}', '{FallbackOddsProviderId}')
      AND "Spread" IS NOT NULL
    ORDER BY CASE WHEN "ProviderId" = '{PreferredOddsProviderId}' THEN 1 ELSE 2 END
    LIMIT 1
) co ON TRUE
INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = c."AwayTeamFranchiseSeasonId"
INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = c."HomeTeamFranchiseSeasonId"
LEFT JOIN public."SeasonPhase" sp ON sp."Id" = c."SeasonPhaseId"
WHERE c."FinalizedUtc" IS NOT NULL
  AND c."CancelledUtc" IS NULL
  AND c."StartDateUtc" < @AsOf
  AND (sp."TypeCode" IS NULL OR sp."TypeCode" <> 1)
  AND c."SpreadWinnerFranchiseSeasonId" IS NOT NULL
  AND (
        (fsHome."FranchiseId" = @FranchiseId AND ((@AsFavorite AND co."Spread" <= -@Threshold) OR (NOT @AsFavorite AND co."Spread" >= @Threshold)))
     OR (fsAway."FranchiseId" = @FranchiseId AND ((@AsFavorite AND co."Spread" >= @Threshold) OR (NOT @AsFavorite AND co."Spread" <= -@Threshold)))
  )
