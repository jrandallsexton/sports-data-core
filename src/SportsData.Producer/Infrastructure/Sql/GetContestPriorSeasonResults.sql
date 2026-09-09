-- Recency bridge: each target-contest team's last @Count finalized games,
-- ROLLING across the current and prior seasons ("how are they playing
-- lately?") — in-season this yields last week's game first, backfilled
-- from the prior season; before week 1 it degrades to exactly the old
-- "prior season finish" shape. Resolved by Franchise so the season
-- boundary is crossed correctly. "Side" says which target team the row
-- belongs to (Away/Home relative to the TARGET contest, not the
-- historical game). Same preview-safe semantics as head-to-head:
-- finalized + non-cancelled only, preseason excluded (TypeCode 1; NULL
-- phase kept), and as-of the target contest (explicit StartDateUtc guard,
-- since current-season rows are now eligible). NOTE: the wire fields are
-- still named Away/HomePriorSeasonGames for contract compatibility —
-- semantics changed 2026-09-09, name did not.
WITH target AS (
    SELECT
        c."Id",
        c."SeasonYear",
        c."StartDateUtc",
        fsAway."FranchiseId" AS "AwayFranchiseId",
        fsHome."FranchiseId" AS "HomeFranchiseId"
    FROM public."Contest" c
    INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = c."AwayTeamFranchiseSeasonId"
    INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = c."HomeTeamFranchiseSeasonId"
    WHERE c."Id" = @ContestId
),
sides AS (
    SELECT t."AwayFranchiseId" AS "FranchiseId", CAST('Away' AS text) AS "Side", t."SeasonYear", t."Id" AS "TargetContestId", t."StartDateUtc" AS "TargetStartDateUtc" FROM target t
    UNION ALL
    SELECT t."HomeFranchiseId", 'Home', t."SeasonYear", t."Id", t."StartDateUtc" FROM target t
)
SELECT s."Side", g.*
FROM sides s
CROSS JOIN LATERAL (
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
        -- Market context — same shape as head-to-head rows (one uniform
        -- GameResult vocabulary for the model). NULL pre-odds-era.
        co."Details" AS "Spread",
        co."Spread" AS "HomeSpread",
        cto."SpreadPointsOpen" AS "HomeSpreadOpen",
        co."OverUnder" AS "OverUnder",
        co."TotalPointsOpen" AS "OverUnderOpen",
        co."OverOdds" AS "OverOdds",
        co."UnderOdds" AS "UnderOdds",
        CASE c."OverUnder" WHEN 1 THEN 'Over' WHEN 2 THEN 'Under' ELSE NULL END AS "OverUnderResult"
    FROM public."Contest" c
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
    WHERE (fsAway."FranchiseId" = s."FranchiseId" OR fsHome."FranchiseId" = s."FranchiseId")
      -- Rolling window: current season (as-of-capped below) + prior season.
      -- Two seasons is always enough to fill @Count; going further back
      -- would let ancient games pad a thin slate.
      AND c."SeasonYear" IN (s."SeasonYear", s."SeasonYear" - 1)
      -- As-of guard: only games that started BEFORE the target contest,
      -- never the target itself. FinalizedUtc alone is not enough once
      -- current-season rows are eligible (same-day earlier finals are fine;
      -- the target must never narrate itself).
      AND c."Id" <> s."TargetContestId"
      AND c."StartDateUtc" < s."TargetStartDateUtc"
      AND c."FinalizedUtc" IS NOT NULL
      AND c."CancelledUtc" IS NULL
      AND (sp."TypeCode" IS NULL OR sp."TypeCode" <> 1)
    ORDER BY c."StartDateUtc" DESC
    LIMIT @Count
) g
ORDER BY s."Side", g."GameDate" DESC
