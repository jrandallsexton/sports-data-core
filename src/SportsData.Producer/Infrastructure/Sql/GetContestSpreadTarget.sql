-- The target contest's live line + franchise identities, feeding the
-- spread-context facts (PreviewSpreadContextDto). Same preferred-provider
-- lateral as the matchup payload so both surfaces quote the same number.
-- HomeSpread is home-relative (negative = home favored). NULL spread rows
-- mean no line from the preferred/fallback providers -> no spread context.
SELECT
    c."StartDateUtc"      AS "StartDateUtc",
    c."SeasonYear"        AS "SeasonYear",
    fsAway."FranchiseId"  AS "AwayFranchiseId",
    fsHome."FranchiseId"  AS "HomeFranchiseId",
    fAway."DisplayName"   AS "AwayTeam",
    fHome."DisplayName"   AS "HomeTeam",
    co."Spread"           AS "HomeSpread",
    co."Details"          AS "SpreadDetails"
FROM public."Contest" c
INNER JOIN public."Competition" comp ON comp."ContestId" = c."Id"
LEFT JOIN LATERAL (
    SELECT *
    FROM public."CompetitionOdds"
    WHERE "CompetitionId" = comp."Id"
      AND "ProviderId" IN ('{PreferredOddsProviderId}', '{FallbackOddsProviderId}')
      AND "Spread" IS NOT NULL
    ORDER BY CASE WHEN "ProviderId" = '{PreferredOddsProviderId}' THEN 1 ELSE 2 END
    LIMIT 1
) co ON TRUE
INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = c."AwayTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fAway ON fAway."Id" = fsAway."FranchiseId"
INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = c."HomeTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fHome ON fHome."Id" = fsHome."FranchiseId"
WHERE c."Id" = @ContestId
LIMIT 1
