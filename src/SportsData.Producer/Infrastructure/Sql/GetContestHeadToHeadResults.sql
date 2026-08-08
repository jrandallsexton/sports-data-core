-- Last @Count head-to-head meetings between the target contest's two
-- FRANCHISES (cross-season identity via Franchise, since FranchiseSeasonIds
-- change yearly). Preview-safe by construction:
--   * finalized, non-cancelled games only
--   * as-of: strictly before the target's start (the target contest can
--     never leak into its own history — see the answer-leak finding in
--     docs/metrics-modeling/matchup-preview-data-inputs.md)
--   * preseason excluded (SeasonPhase.TypeCode 1) — system-testing data
--     only, per policy 2026-08-08. NULL phase (old rows) is kept.
WITH target AS (
    SELECT
        c."Id",
        c."StartDateUtc",
        fsAway."FranchiseId" AS "AwayFranchiseId",
        fsHome."FranchiseId" AS "HomeFranchiseId"
    FROM public."Contest" c
    INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = c."AwayTeamFranchiseSeasonId"
    INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = c."HomeTeamFranchiseSeasonId"
    WHERE c."Id" = @ContestId
)
SELECT
    c."StartDateUtc" AS "GameDate",
    c."SeasonYear",
    sp."Name" AS "Phase",
    c."EventNote" AS "Note",
    fHome."DisplayName" AS "HomeTeam",
    fAway."DisplayName" AS "AwayTeam",
    c."HomeScore",
    c."AwayScore",
    CASE
        WHEN c."WinnerFranchiseSeasonId" = c."HomeTeamFranchiseSeasonId" THEN fHome."DisplayName"
        WHEN c."WinnerFranchiseSeasonId" = c."AwayTeamFranchiseSeasonId" THEN fAway."DisplayName"
        ELSE NULL
    END AS "Winner",
    CASE
        WHEN c."SpreadWinnerFranchiseSeasonId" = c."HomeTeamFranchiseSeasonId" THEN fHome."DisplayName"
        WHEN c."SpreadWinnerFranchiseSeasonId" = c."AwayTeamFranchiseSeasonId" THEN fAway."DisplayName"
        ELSE NULL
    END AS "SpreadWinner",
    -- Market context (preferred-provider lateral, same vocabulary as the
    -- live matchup payload: Spread = details text e.g. "ARI -6.5",
    -- HomeSpread home-relative). Open values expose line movement.
    -- NULL for pre-odds-era games (~pre-2022) — result-only context there.
    co."Details" AS "Spread",
    co."Spread" AS "HomeSpread",
    cto."SpreadPointsOpen" AS "HomeSpreadOpen",
    co."OverUnder" AS "OverUnder",
    co."TotalPointsOpen" AS "OverUnderOpen",
    co."OverOdds" AS "OverOdds",
    co."UnderOdds" AS "UnderOdds",
    CASE c."OverUnder" WHEN 1 THEN 'Over' WHEN 2 THEN 'Under' ELSE NULL END AS "OverUnderResult"
FROM target t
INNER JOIN public."Contest" c ON c."Id" <> t."Id"
INNER JOIN public."Competition" comp ON comp."ContestId" = c."Id"
LEFT JOIN LATERAL (
    SELECT *
    FROM public."CompetitionOdds"
    WHERE "CompetitionId" = comp."Id"
      AND "ProviderId" IN ('{PreferredOddsProviderId}', '{FallbackOddsProviderId}')
    ORDER BY CASE WHEN "ProviderId" = '{PreferredOddsProviderId}' THEN 1 ELSE 2 END
    LIMIT 1
) co ON TRUE
LEFT JOIN public."CompetitionTeamOdds" cto ON cto."CompetitionOddsId" = co."Id" AND cto."Side" = 'Home'
INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = c."AwayTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fAway ON fAway."Id" = fsAway."FranchiseId"
INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = c."HomeTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fHome ON fHome."Id" = fsHome."FranchiseId"
LEFT JOIN public."SeasonPhase" sp ON sp."Id" = c."SeasonPhaseId"
WHERE ((fHome."Id" = t."HomeFranchiseId" AND fAway."Id" = t."AwayFranchiseId")
    OR (fHome."Id" = t."AwayFranchiseId" AND fAway."Id" = t."HomeFranchiseId"))
  AND c."FinalizedUtc" IS NOT NULL
  AND c."CancelledUtc" IS NULL
  AND c."StartDateUtc" < t."StartDateUtc"
  AND (sp."TypeCode" IS NULL OR sp."TypeCode" <> 1)
ORDER BY c."StartDateUtc" DESC
LIMIT @Count
